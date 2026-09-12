<#
.SYNOPSIS
    Avvia llama-server (Qwen3.5-9B) per l'agente locale di Kilo, con i controlli di
    sicurezza per la ROG Ally X.

.DESCRIPTION
    Prima di caricare il modello controlla porta, alimentazione, disco e memoria di
    commit, e si rifiuta di partire se il modello non ci sta senza togliere memoria
    a Unity e VS Code. Mentre il server gira sorveglia il commit libero e ferma
    llama-server se resta sotto -MinCommitFreeGB: meglio perdere il server che una
    scena Unity non salvata.

    La API key viene generata al primo avvio in %USERPROFILE%\.llama-local\api-key.txt
    e Kilo la legge dallo stesso file (~/.config/kilo/kilo.jsonc).

    Scelte misurate sulla Ally X (Radeon 780M, Vulkan, 10/09/2026, con Devstral 24B):
    - KV cache sulla GPU: generazione +13% rispetto a --no-kv-offload, e con la KV su
      CPU la generazione crollava oltre i 18k token di contesto.
    - -ub 1024 non migliora il prompt processing rispetto al default 512.
    - --cache-ram 0: il default tiene fino a 8 GB di prompt in RAM.
    - Il file di paging gestito da Windows cresce troppo tardi: caricando un modello da
      12 GB il commit libero e' sceso a 0,5 GB. Per questo il controllo iniziale guarda
      solo il commit libero reale.

.EXAMPLE
    .\Start-LocalLLM.ps1

.EXAMPLE
    .\Start-LocalLLM.ps1 -Context 65536 -Thinking
#>
[CmdletBinding()]
param(
    # Deve essere >= limit.context del modello in kilo.json, altrimenti Kilo sfora.
    [ValidateSet(16384, 32768, 65536)]
    [int]$Context = 32768,

    # Accende il ragionamento del modello: risposte migliori sui compiti difficili, ma
    # centinaia di token in piu' a ogni passo.
    [switch]$Thinking,

    # Rimette la KV cache nella RAM di sistema (--no-kv-offload). Piu' lento a contesto pieno.
    [switch]$KvOnCpu,

    # Abilita la web UI di llama-server su http://127.0.0.1:<Port> (chiede la API key).
    [switch]$WebUI,

    # Parte anche se il controllo della memoria dice che non c'e' spazio.
    [switch]$Force,

    # Se il commit libero resta sotto questa soglia per due controlli di fila, il server viene fermato.
    [double]$MinCommitFreeGB = 0.7,

    [int]$Port = 8080,

    [string]$ModelFile = (Join-Path $env:USERPROFILE '.llama-local\models\Qwen3.5-9B-UD-Q4_K_XL.gguf'),

    # Template jinja alternativo; di default si usa quello incluso nel GGUF.
    [string]$ChatTemplate
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
Set-StrictMode -Version Latest

# --- Modello ------------------------------------------------------------------------
# Qwen3.5-9B, Unsloth UD-Q4_K_XL. Architettura ibrida: 24 layer ad attenzione lineare
# (stato ricorrente di dimensione fissa) e 8 ad attenzione piena, gli unici con KV cache.

$ModelAlias = 'qwen3.5-9b'
$ModelUrl = 'https://huggingface.co/unsloth/Qwen3.5-9B-GGUF/resolve/main/Qwen3.5-9B-UD-Q4_K_XL.gguf'
# KV: 8 layer x 4 teste x 256 dimensioni x (K+V) = 16384 elementi per token; q8_0 occupa 34 byte ogni 32.
$KvBytesPerToken = 16384 * 34 / 32
# Checkpoint dello stato ricorrente per riusare il prompt fra un turno e l'altro:
# 24 layer x 32 teste x 128 x 128 in f32 = ~50 MB l'uno. Il default di llama-server e' 32.
$CtxCheckpoints = 8
$CheckpointGB = 0.05
# Buffer di calcolo Vulkan e runtime.
$BuffersGB = 0.8
# Campionamento raccomandato da Qwen senza ragionamento, ma con presence_penalty a 0:
# il valore suggerito (1.5) penalizza gli identificatori che nel codice si ripetono di proposito.
$Sampling = @('--temp', '0.7', '--top-p', '0.8', '--top-k', '20', '--min-p', '0')
if ($Thinking) { $Sampling = @('--temp', '0.6', '--top-p', '0.95', '--top-k', '20', '--min-p', '0') }

$KeyFile = Join-Path $env:USERPROFILE '.llama-local\api-key.txt'
$RepoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
# Commit che deve restare libero a modello caricato.
$MinMarginGB = 1.0

function Write-Step([string]$Text) { Write-Host "[llm] $Text" -ForegroundColor Cyan }
function Write-Warn([string]$Text) { Write-Host "[llm] ATTENZIONE: $Text" -ForegroundColor Yellow }
function Stop-WithError([string]$Text) { Write-Host "[llm] $Text" -ForegroundColor Red; exit 1 }

function Get-MemoryState {
    $os = Get-CimInstance Win32_OperatingSystem
    [pscustomobject]@{
        CommitFreeGB = $os.FreeVirtualMemory / 1MB
        RamFreeGB    = $os.FreePhysicalMemory / 1MB
        DiskFreeGB   = (Get-PSDrive C).Free / 1GB
    }
}

# --- 1. Eseguibile, modello, porta --------------------------------------------------

$server = Get-Command llama-server -ErrorAction SilentlyContinue
if (-not $server) { Stop-WithError 'llama-server non trovato nel PATH (winget install ggml.llamacpp).' }

if (-not (Test-Path $ModelFile)) {
    Stop-WithError "Modello non trovato in $ModelFile. Scaricalo con (barra di avanzamento, riprende se interrotto): curl.exe -L --fail -C - -# -o `"$ModelFile`" $ModelUrl"
}
$modelGB = (Get-Item $ModelFile).Length / 1GB

$listener = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue | Select-Object -First 1
if ($listener) {
    $owner = Get-Process -Id $listener.OwningProcess -ErrorAction SilentlyContinue
    if ($owner -and $owner.ProcessName -eq 'llama-server') {
        Write-Step "llama-server e' gia' in esecuzione (PID $($owner.Id)) su http://127.0.0.1:$Port."
        exit 0
    }
    Stop-WithError "La porta $Port e' occupata dal processo $($listener.OwningProcess)."
}

# --- 2. Coerenza con kilo.json ------------------------------------------------------

$kiloJson = Join-Path $RepoRoot 'kilo.json'
if (Test-Path $kiloJson) {
    $kiloLimit = $null
    try {
        $kiloLimit = (Get-Content $kiloJson -Raw | ConvertFrom-Json).provider.llamacpp.models.$ModelAlias.limit.context
    } catch {
        Write-Warn "Non riesco a leggere limit.context del modello '$ModelAlias' da kilo.json: $($_.Exception.Message)"
    }
    if ($kiloLimit -and $kiloLimit -gt $Context) {
        Stop-WithError "kilo.json dichiara $($kiloLimit) token di contesto ma il server ne avrebbe $($Context): Kilo sforerebbe. Usa -Context $($kiloLimit)."
    }
}

# --- 3. Alimentazione e disco -------------------------------------------------------

$battery = Get-CimInstance Win32_Battery -ErrorAction SilentlyContinue | Select-Object -First 1
if ($battery -and $battery.BatteryStatus -in 1, 4, 5) {
    Write-Warn "Sei a batteria ($($battery.EstimatedChargeRemaining)%). Il modello tiene GPU e RAM al massimo: collega l'alimentatore per sessioni lunghe."
}

$mem = Get-MemoryState
if ($mem.DiskFreeGB -lt 10) {
    Write-Warn ("Disco C: quasi pieno ({0:N1} GB liberi): il file di paging non ha spazio per crescere." -f $mem.DiskFreeGB)
}

# --- 4. Memoria di commit -----------------------------------------------------------
# Su questa macchina anche la memoria della iGPU (dedicata e condivisa) pesa sul commit,
# quindi pesi e KV cache contano per intero anche se stanno "sulla GPU".

$kvGB = $Context * $KvBytesPerToken / 1GB
$checkpointsGB = $CtxCheckpoints * $CheckpointGB
$requiredGB = $modelGB + $kvGB + $checkpointsGB + $BuffersGB
$marginGB = $mem.CommitFreeGB - $requiredGB

Write-Step ("Memoria richiesta ~{0:N1} GB (modello {1:N1} + KV {2:N1} a {3} token + checkpoint {4:N1} + buffer {5:N1})." -f $requiredGB, $modelGB, $kvGB, $Context, $checkpointsGB, $BuffersGB)
Write-Step ("Commit libero {0:N1} GB: a modello caricato ne resterebbero {1:N1} (minimo {2:N1})." -f $mem.CommitFreeGB, $marginGB, $MinMarginGB)

if ($marginGB -lt $MinMarginGB) {
    Write-Host ''
    Write-Host 'Processi che usano piu'' memoria:' -ForegroundColor Yellow
    Get-Process | Sort-Object PrivateMemorySize64 -Descending | Select-Object -First 6 |
        ForEach-Object { Write-Host ('  {0,-24} {1,5:N1} GB' -f $_.ProcessName, ($_.PrivateMemorySize64 / 1GB)) }
    if ((Get-CimInstance Win32_ComputerSystem).AutomaticManagedPagefile) {
        Write-Warn 'Su questa macchina il file di paging non cresce, nemmeno sotto pressione: il limite di commit e'' fisso. Chiudi qualche applicazione (un kilo run interrotto lascia un backend da ~1 GB).'
    }
    if (-not $Force) {
        Stop-WithError ('Mancano ~{0:N1} GB: chiudi qualche applicazione o riduci -Context (oppure -Force, a tuo rischio).' -f ($MinMarginGB - $marginGB))
    }
    Write-Warn 'Avvio forzato: salva il lavoro in Unity prima di mandare richieste al modello.'
}

# --- 5. API key ---------------------------------------------------------------------

if (-not (Test-Path $KeyFile)) {
    New-Item -ItemType Directory -Force (Split-Path $KeyFile) | Out-Null
    $bytes = New-Object byte[] 32
    [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
    [IO.File]::WriteAllText($KeyFile, 'sk-local-' + (-join ($bytes | ForEach-Object { $_.ToString('x2') })))
    Write-Step "Creata la API key in $KeyFile (Kilo la legge da li')."
}

# --- 6. Avvio e sorveglianza --------------------------------------------------------

$serverArgs = @(
    '--model', $ModelFile,
    '--alias', $ModelAlias,
    '--device', 'Vulkan0',
    '--ctx-size', $Context,
    '--cache-type-k', 'q8_0',
    '--cache-type-v', 'q8_0',
    '--flash-attn', 'on',
    '--parallel', '1',
    '--cache-ram', '0',
    '--ctx-checkpoints', $CtxCheckpoints,
    '--reasoning', $(if ($Thinking) { 'on' } else { 'off' }),
    '--fit', 'on',
    '--fit-target', '1024',
    '--jinja',
    '--host', '127.0.0.1',
    '--port', $Port,
    '--api-key-file', $KeyFile,
    '--cors-origins', 'localhost',
    '--no-cors-credentials'
) + $Sampling
if ($ChatTemplate) {
    if (-not (Test-Path $ChatTemplate)) { Stop-WithError "Template $ChatTemplate non trovato." }
    $serverArgs += '--chat-template-file', $ChatTemplate
}
if ($KvOnCpu) { $serverArgs += '--no-kv-offload' }
if (-not $WebUI) { $serverArgs += '--no-webui' }

$argLine = ($serverArgs | ForEach-Object { $a = [string]$_; if ($a -match '\s') { '"{0}"' -f $a } else { $a } }) -join ' '

Write-Step 'Carico il modello. Ctrl+C per fermare.'
$started = Get-Date
$proc = Start-Process -FilePath $server.Source -ArgumentList $argLine -NoNewWindow -PassThru
# Tiene aperto l'handle: senza, ExitCode resta vuoto a processo finito.
$null = $proc.Handle

$ready = $false
$lowSamples = 0
$lastWarn = [datetime]::MinValue
try {
    while (-not $proc.HasExited) {
        Start-Sleep -Seconds 3

        if (-not $ready) {
            try {
                $null = Invoke-WebRequest -Uri "http://127.0.0.1:$Port/health" -UseBasicParsing -TimeoutSec 2
                $ready = $true
                $m = Get-MemoryState
                Write-Step ("Pronto in {0:N0} s: http://127.0.0.1:{1}/v1, modello '{2}'. Commit libero {3:N1} GB, RAM libera {4:N1} GB." -f ((Get-Date) - $started).TotalSeconds, $Port, $ModelAlias, $m.CommitFreeGB, $m.RamFreeGB)
            } catch {
                # 503 finche' il modello e' in caricamento.
            }
        }

        $m = Get-MemoryState
        if ($m.CommitFreeGB -lt $MinCommitFreeGB) {
            $lowSamples++
            if ($lowSamples -ge 2) {
                Write-Host ('[llm] Commit libero {0:N2} GB: fermo llama-server prima che vadano in crash Unity o VS Code.' -f $m.CommitFreeGB) -ForegroundColor Red
                Stop-Process -Id $proc.Id -Force
                break
            }
        } else {
            $lowSamples = 0
            if ($m.CommitFreeGB -lt $MinCommitFreeGB + 1 -and ((Get-Date) - $lastWarn).TotalSeconds -gt 60) {
                Write-Warn ('Commit libero {0:N1} GB: non aprire altre applicazioni pesanti.' -f $m.CommitFreeGB)
                $lastWarn = Get-Date
            }
        }
    }
} finally {
    if (-not $proc.HasExited) { Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue }
}
Write-Step "llama-server terminato (codice $($proc.ExitCode))."
