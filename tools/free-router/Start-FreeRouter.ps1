[CmdletBinding()]
param([int]$Port = 8099, [switch]$Restart, [switch]$Foreground)
$ErrorActionPreference = 'Stop'
$runtimePath = Join-Path $PSScriptRoot 'runtime'
New-Item -ItemType Directory -Force $runtimePath | Out-Null
$health = $null
try { $health = Invoke-RestMethod "http://127.0.0.1:$Port/health" -TimeoutSec 2 } catch {}
if ($health) {
    if (-not $Restart) { Write-Host "Router gia attivo: http://127.0.0.1:$Port/"; exit 0 }
    if ($health.active -gt 0) { throw 'Richiesta in corso: attendi prima di riavviare.' }
    $listener = Get-NetTCPConnection -LocalPort $Port -State Listen | Select-Object -First 1
    $owner = Get-CimInstance Win32_Process -Filter "ProcessId=$($listener.OwningProcess)"
    $scriptPath = Join-Path $PSScriptRoot 'router.py'
    if ($owner.Name -ne 'python.exe' -or $owner.CommandLine -notlike "*$scriptPath*") { throw 'La porta appartiene a un altro programma.' }
    if (Get-NetTCPConnection -LocalPort $Port -State Established -ErrorAction SilentlyContinue) { throw 'Ci sono client collegati: riprova a chat ferma.' }
    Stop-Process -Id $owner.ProcessId
}
$keyFile = Join-Path $env:USERPROFILE '.llama-local/api-key.txt'
if (-not (Test-Path -LiteralPath $keyFile)) {
    New-Item -ItemType Directory -Force (Split-Path $keyFile) | Out-Null
    $bytes = New-Object byte[] 32
    $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
    $rng.GetBytes($bytes); $rng.Dispose()
    [IO.File]::WriteAllText($keyFile, 'sk-local-' + (-join ($bytes | ForEach-Object { $_.ToString('x2') })))
}
$pythonPath = (Get-Command python -ErrorAction Stop).Source
$scriptPath = Join-Path $PSScriptRoot 'router.py'
$env:ROUTER_PORT = [string]$Port
if ($Foreground) { & $pythonPath -B -u $scriptPath; exit $LASTEXITCODE }
$proc = Start-Process -FilePath $pythonPath -ArgumentList @('-B','-u',('"'+$scriptPath+'"')) -WindowStyle Hidden -PassThru `
    -RedirectStandardOutput (Join-Path $runtimePath 'router.stdout.log') -RedirectStandardError (Join-Path $runtimePath 'router.stderr.log')
$proc.Id | Set-Content (Join-Path $runtimePath 'router.pid')
Write-Host "Auto avviato (PID $($proc.Id)): http://127.0.0.1:$Port/"
$deadline = (Get-Date).AddSeconds(40)
while ((Get-Date) -lt $deadline) {
    if ($proc.HasExited) { throw "Router terminato: vedi runtime/router.stderr.log" }
    try {
        $ready = Invoke-RestMethod "http://127.0.0.1:$Port/health" -TimeoutSec 2
        if ($ready.status -eq 'ok') { Write-Host 'Endpoint pronto.'; exit 0 }
    } catch {}
    Start-Sleep -Milliseconds 500
}
throw 'Router non pronto entro 40 secondi.'
