<#
.SYNOPSIS
    Finestra di sorveglianza: mostra in diretta cosa sta girando in locale.

.DESCRIPTION
    Aggiorna ogni due secondi e mostra:
    - le ultime decisioni dell'instradatore gratuito (gruppo, modello, esito, durata);
    - i processi in corso (instradatore, server locale, backend Kilo) con memoria e CPU;
    - memoria di commit libera e consumo dell'ora sul tetto di 200 richieste.

    Serve a non avere processi lunghi che girano senza che si veda niente. Non avvia e
    non ferma nulla: si puo' chiudere quando si vuole.

.EXAMPLE
    .\Watch-Local.ps1
#>
[CmdletBinding()]
param(
    [int]$Port = 8099,
    [int]$IntervalSeconds = 2
)

$ErrorActionPreference = 'Continue'
$ProgressPreference = 'SilentlyContinue'
$Host.UI.RawUI.WindowTitle = 'FlipCards: sorveglianza locale'

while ($true) {
    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add("FlipCards - sorveglianza locale - $(Get-Date -Format 'HH:mm:ss')  (Ctrl+C per chiudere)")
    $lines.Add('')

    $os = Get-CimInstance Win32_OperatingSystem
    $lines.Add(("memoria: commit libero {0:N2} GB / limite {1:N2} GB | RAM libera {2:N2} GB" -f `
        ($os.FreeVirtualMemory / 1MB), ($os.TotalVirtualMemorySize / 1MB), ($os.FreePhysicalMemory / 1MB)))

    $state = $null
    try { $state = Invoke-RestMethod "http://127.0.0.1:$Port/api/state" -TimeoutSec 3 } catch {}
    if ($state) {
        $lines.Add(("instradatore: {0}/{1} richieste nell'ultima ora | catalogo: {2} modelli gratuiti, {3} con la vista" -f `
            $state.used_last_hour, $state.hourly_limit, $state.catalogo.modelli_gratuiti, $state.catalogo.con_vista))
        $cooling = $state.cooling.PSObject.Properties
        if ($cooling.Count -gt 0) {
            $lines.Add('in pausa dopo un 429: ' + (($cooling | ForEach-Object { "$($_.Name.Replace(':free','')) ($($_.Value)s)" }) -join ', '))
        }
    } else {
        $lines.Add("instradatore: non in ascolto su 127.0.0.1:$Port")
    }

    $lines.Add('')
    $lines.Add('processi:')
    $procs = Get-Process kilo, python, llama-server -ErrorAction SilentlyContinue
    if ($procs) {
        foreach ($p in $procs) {
            $lines.Add(("  {0,-13} PID {1,-6} {2,5:N2} GB  CPU {3,5:N0} s  da {4:HH:mm:ss}" -f `
                $p.ProcessName, $p.Id, ($p.PrivateMemorySize64 / 1GB), $p.CPU, $p.StartTime))
        }
    } else {
        $lines.Add('  nessuno')
    }

    if ($state -and $state.last) {
        $lines.Add('')
        $lines.Add('ultime richieste:')
        foreach ($r in ($state.last | Select-Object -First 8)) {
            $img = if ($r.immagini) { ' +img' } else { '' }
            $lines.Add(("  {0}  {1,-9} {2,-42} {3}  {4,6:N1} s{5}" -f `
                $r.quando, $r.gruppo, $r.modello.Replace(':free', ''), $r.stato, ($r.ms / 1000), $img))
        }
    }

    if ($state -and $state.errori -and $state.errori.Count -gt 0) {
        $lines.Add('')
        $lines.Add('ultimi errori:')
        foreach ($e in ($state.errori | Select-Object -Last 3)) {
            $lines.Add('  ' + $e.Substring(0, [Math]::Min(150, $e.Length)))
        }
    }

    Clear-Host
    $lines | ForEach-Object { Write-Host $_ }
    Start-Sleep -Seconds $IntervalSeconds
}
