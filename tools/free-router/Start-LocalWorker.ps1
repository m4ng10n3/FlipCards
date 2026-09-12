[CmdletBinding()]
param([int]$Port = 8081)
$ErrorActionPreference = 'Stop'
$modelPath = Join-Path $env:USERPROFILE '.llama-local/models/Qwen3.5-2B-Q4_K_M.gguf'
$keyPath = Join-Path $env:USERPROFILE '.llama-local/api-key.txt'
$runtimePath = Join-Path $PSScriptRoot 'runtime'
New-Item -ItemType Directory -Force $runtimePath | Out-Null
try {
    $health = Invoke-RestMethod "http://127.0.0.1:$Port/health" -TimeoutSec 2
    if ($health.status -eq 'ok') { Write-Host "Worker gia attivo su $Port"; exit 0 }
} catch {}
if (-not (Test-Path -LiteralPath $modelPath)) { throw "Manca $modelPath. Vedi README.md per download e SHA256." }
if (-not (Test-Path -LiteralPath $keyPath)) { throw 'Manca la chiave locale; avvia prima Start-FreeRouter.ps1.' }
$memory = Get-CimInstance Win32_OperatingSystem
$totalContext = 65536
# GGUF verified on this machine: 24 blocks, full attention every 4, 2 KV heads,
# K/V dimension 256, q8_0 = 34/32 bytes. Remaining runtime overhead measured ~1.37 GB.
$kvGB = $totalContext * 6 * 2 * (256 + 256) * 34 / 32 / 1GB
$requiredGB = (Get-Item -LiteralPath $modelPath).Length / 1GB + $kvGB + 1.5
$marginGB = 1.5
Write-Host ('Memoria stimata {0:N2} GB + margine {1:N1} GB, commit libero {2:N2} GB.' -f $requiredGB,$marginGB,($memory.FreeVirtualMemory/1MB))
if ($memory.FreeVirtualMemory / 1MB -lt $requiredGB + $marginGB -or $memory.FreePhysicalMemory / 1MB -lt $requiredGB) {
    throw 'Memoria insufficiente per due contesti locali: il worker lascia margine a Unity.'
}
$serverPath = (Get-Command llama-server -ErrorAction Stop).Source
$arguments = @('--model', ('"' + $modelPath + '"'), '--alias', 'qwen3.5-2b',
    '--device', 'Vulkan0', '--ctx-size', '65536', '--parallel', '2',
    '--cache-type-k', 'q8_0', '--cache-type-v', 'q8_0', '--flash-attn', 'on',
    '--cache-ram', '0', '--ctx-checkpoints', '4', '--reasoning', 'off', '--jinja',
    '--threads', '4', '--threads-batch', '4', '--fit', 'on', '--fit-target', '1024',
    '--host', '127.0.0.1', '--port', $Port, '--api-key-file', ('"' + $keyPath + '"'),
    '--no-webui', '--temp', '0.2', '--top-p', '0.8', '--top-k', '20', '--min-p', '0')
$proc = Start-Process -FilePath $serverPath -ArgumentList $arguments -WindowStyle Hidden -PassThru `
    -RedirectStandardOutput (Join-Path $runtimePath 'worker.stdout.log') `
    -RedirectStandardError (Join-Path $runtimePath 'worker.stderr.log')
$proc.Id | Set-Content (Join-Path $runtimePath 'worker.pid')
Write-Host "Qwen 2B PID $($proc.Id): un caricamento pesi, due slot fino a 32768 token. Worker limitato a 16k, coordinatore a 32k."
$ready = $false
try {
    while (-not $proc.HasExited) {
        Start-Sleep -Seconds 3
        $memory = Get-CimInstance Win32_OperatingSystem
        if ($memory.FreeVirtualMemory / 1MB -lt 1) { throw 'Commit sotto 1 GB: fermo soltanto il worker avviato da questo script.' }
        if (-not $ready) {
            try {
                $health = Invoke-RestMethod "http://127.0.0.1:$Port/health" -TimeoutSec 2
                if ($health.status -eq 'ok') { $ready = $true; Write-Host "Worker pronto su http://127.0.0.1:$Port/v1" }
            } catch {}
        }
    }
} finally {
    if (-not $proc.HasExited) { Stop-Process -Id $proc.Id }
}
