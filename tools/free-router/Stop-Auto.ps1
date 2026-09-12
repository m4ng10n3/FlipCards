[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$health=$null
try { $health=Invoke-RestMethod 'http://127.0.0.1:8099/health' -TimeoutSec 2 } catch {}
if ($health -and $health.active -gt 0) { throw 'Auto sta lavorando: attendi il completamento o annulla dalla chat.' }
if (Get-NetTCPConnection -LocalPort 8099 -State Established -ErrorAction SilentlyContinue) { throw 'Un client e collegato al router: riprova a chat ferma.' }
& (Join-Path $PSScriptRoot 'Stop-LocalServer.ps1') -Port 8081
$listener=Get-NetTCPConnection -LocalPort 8099 -State Listen -ErrorAction SilentlyContinue | Select-Object -First 1
if ($listener) {
    $owner=Get-CimInstance Win32_Process -Filter "ProcessId=$($listener.OwningProcess)"
    $scriptPath=Join-Path $PSScriptRoot 'router.py'
    if ($owner.Name -ne 'python.exe' -or $owner.CommandLine -notlike "*$scriptPath*") { throw 'La porta router appartiene a un altro programma.' }
    Stop-Process -Id $owner.ProcessId
}
Write-Host 'Auto e worker 2B fermati. Kilo, Unity e il server 9B restano disponibili.'
