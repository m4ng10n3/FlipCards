<#
.SYNOPSIS
    Avvia l'instradatore locale dei modelli gratuiti di Kilo.

.DESCRIPTION
    Espone a Kilo un provider OpenAI-compatibile su http://127.0.0.1:<Port>/v1, cosi' le
    sue voci (auto, vista, cervello, codice) compaiono nel selettore dei modelli di
    VS Code. L'instradatore sceglie il modello gratuito a monte in base alla richiesta e
    ricade su un altro se il fornitore risponde 429.

    Il cruscotto su http://127.0.0.1:<Port>/ mostra regole, richieste usate nell'ultima
    ora (tetto 200 per IP) e modello che ha servito ogni richiesta. Per vederlo in un tab
    di VS Code: Ctrl+Shift+P, "Simple Browser: Show", e incolla l'indirizzo.

    Non serve alcuna credenziale: i modelli :free del gateway Kilo rispondono senza
    autenticazione. L'instradatore pretende invece la API key locale
    (%USERPROFILE%\.llama-local\api-key.txt) da chi lo chiama, cioe' da Kilo.

.EXAMPLE
    .\Start-FreeRouter.ps1

.EXAMPLE
    .\Start-FreeRouter.ps1 -Port 8099 -OpenDashboard
#>
[CmdletBinding()]
param(
    [int]$Port = 8099,

    # Apre il cruscotto nel browser di sistema (in VS Code usa invece "Simple Browser: Show").
    [switch]$OpenDashboard
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

function Write-Step([string]$Text) { Write-Host "[router] $Text" -ForegroundColor Cyan }
function Stop-WithError([string]$Text) { Write-Host "[router] $Text" -ForegroundColor Red; exit 1 }

$python = Get-Command python -ErrorAction SilentlyContinue
if (-not $python) { $python = Get-Command py -ErrorAction SilentlyContinue }
if (-not $python) { Stop-WithError 'Python non trovato nel PATH: serve Python 3.' }

$script = Join-Path $PSScriptRoot 'router.py'
if (-not (Test-Path $script)) { Stop-WithError "router.py non trovato in $PSScriptRoot." }

$listener = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue | Select-Object -First 1
if ($listener) {
    $owner = Get-Process -Id $listener.OwningProcess -ErrorAction SilentlyContinue
    if ($owner -and $owner.ProcessName -match 'python') {
        Write-Step "l'instradatore e' gia' in esecuzione (PID $($owner.Id)): http://127.0.0.1:$Port/"
        exit 0
    }
    Stop-WithError "La porta $Port e' occupata dal processo $($listener.OwningProcess)."
}

$keyFile = Join-Path $env:USERPROFILE '.llama-local\api-key.txt'
if (-not (Test-Path $keyFile)) {
    Write-Step "Nessuna API key locale in $keyFile : l'instradatore accettera' richieste senza autenticazione."
}

Write-Step "Cruscotto: http://127.0.0.1:$Port/  (in VS Code: Ctrl+Shift+P, Simple Browser: Show)"
Write-Step "Endpoint per Kilo: http://127.0.0.1:$Port/v1  -  Ctrl+C per fermare."
if ($OpenDashboard) { Start-Process "http://127.0.0.1:$Port/" }

$env:ROUTER_PORT = $Port
& $python.Source -u $script
Write-Step "instradatore terminato (codice $LASTEXITCODE)."
