[CmdletBinding()]
param()
$ErrorActionPreference='Continue'
foreach ($port in 8099,8081,8080) {
    try { $health=Invoke-RestMethod "http://127.0.0.1:$port/health" -TimeoutSec 2; Write-Host "Porta $port : $($health.status)" }
    catch { Write-Host "Porta $port : non attiva" }
}
$state=$null
try { $state=Invoke-RestMethod 'http://127.0.0.1:8099/api/state' -TimeoutSec 3 } catch {}
if ($state) {
    Write-Host "Budget osservato: $($state.used_last_hour)/$($state.hourly_limit)"
    Write-Host "Routing locale: $($state.local_capabilities.routing_accuracy)"
}
$kiloPath=Get-ChildItem (Join-Path $env:USERPROFILE '.vscode/extensions/kilocode.kilo-code-*/bin/kilo.exe') | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if ($kiloPath) { & $kiloPath.FullName mcp list }
