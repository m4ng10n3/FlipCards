[CmdletBinding()]
param([switch]$RestartRouter)
$ErrorActionPreference='Stop'
& (Join-Path $PSScriptRoot 'Start-FreeRouter.ps1') -Restart:$RestartRouter
$ready=$false
try { $ready=(Invoke-RestMethod 'http://127.0.0.1:8081/health' -TimeoutSec 2).status -eq 'ok' } catch {}
if (-not $ready) {
    $scriptPath=Join-Path $PSScriptRoot 'Start-LocalWorker.ps1'
    $runtimePath=Join-Path $PSScriptRoot 'runtime'
    $proc=Start-Process powershell.exe -ArgumentList @('-NoProfile','-ExecutionPolicy','Bypass','-File',('"'+$scriptPath+'"')) -WindowStyle Hidden -PassThru `
        -RedirectStandardOutput (Join-Path $runtimePath 'worker-monitor.stdout.log') -RedirectStandardError (Join-Path $runtimePath 'worker-monitor.stderr.log')
    Write-Host "Sorveglianza locale PID $($proc.Id). Due contesti, un solo modello 2B."
    $deadline=(Get-Date).AddSeconds(40)
    while ((Get-Date) -lt $deadline) {
        try { if ((Invoke-RestMethod 'http://127.0.0.1:8081/health' -TimeoutSec 2).status -eq 'ok') { $ready=$true; break } } catch {}
        if ($proc.HasExited) { throw 'Worker terminato: vedi runtime/worker-monitor.stderr.log' }
        Start-Sleep -Milliseconds 500
    }
    if (-not $ready) { throw 'Worker non pronto entro 40 secondi.' }
}
Write-Host 'In Kilo: Reload Config and Skills, poi agente auto. Pannello: FlipCards Auto: apri pannello.'
