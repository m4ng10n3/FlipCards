[CmdletBinding()]
param([ValidateSet(8080,8081)][int]$Port = 8080)
$ErrorActionPreference='Stop'
$listener=Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $listener) { Write-Host "Nessun server sulla porta $Port"; exit 0 }
$owner=Get-CimInstance Win32_Process -Filter "ProcessId=$($listener.OwningProcess)"
$alias=if ($Port -eq 8081) {'qwen3.5-2b'} else {'qwen3.5-9b'}
if ($owner.Name -ne 'llama-server.exe' -or $owner.CommandLine -notlike "*--alias $alias*") { throw 'Il processo non corrisponde al modello atteso.' }
if (Get-NetTCPConnection -LocalPort $Port -State Established -ErrorAction SilentlyContinue) { throw 'Modello in uso: attendi prima di fermarlo.' }
Stop-Process -Id $owner.ProcessId
Write-Host "Fermato soltanto $alias sulla porta $Port."
