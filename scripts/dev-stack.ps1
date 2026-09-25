# Starts the local ReturnRight stack (no Docker): API on 5080, extractor on 8000, web on 3000.
# Requires PostgreSQL on 5432 and Mailpit on 8025 to already be running.
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

function Test-Port([int]$port) {
    try {
        $client = [System.Net.Sockets.TcpClient]::new()
        $client.ConnectAsync("127.0.0.1", $port).Wait(1500)
        $client.Close()
        return $true
    } catch {
        return $false
    }
}

# Load .env values we care about.
$envFile = Join-Path $root ".env"
$envValues = @{}
if (Test-Path $envFile) {
    Get-Content $envFile | ForEach-Object {
        if ($_ -match '^\s*([^#=\s]+)\s*=\s*(.*)$') {
            $envValues[$Matches[1]] = $Matches[2].Trim('"').Trim("'")
        }
    }
}

$missing = @()
if (-not (Test-Port 5432)) { $missing += "PostgreSQL on localhost:5432 (docker compose up -d postgres, or your local PG service)" }
if (-not (Test-Port 8025)) { $missing += "Mailpit on http://localhost:8025 (docker compose up -d mailpit, or run Mailpit.exe)" }
if ($missing.Count -gt 0) {
    Write-Host "Missing dependencies:`n - $($missing -join "`n - ")" -ForegroundColor Red
    exit 1
}

$demoPassword = $envValues["Seed__DemoPassword"]
$tesseract = $envValues["TESSERACT_CMD"]
$extractorSecret = $envValues["EXTRACTOR_SHARED_SECRET"]

# --- API ---
$apiCmd = "Set-Location '$root'"
if ($demoPassword) { $apiCmd += "; `$env:Seed__DemoPassword='$demoPassword'" }
if ($extractorSecret) { $apiCmd += "; `$env:Extractor__SharedSecret='$extractorSecret'" }
$apiCmd += "; dotnet run --project apps/api/src/ReturnRight.Api"
$api = Start-Process -PassThru -FilePath "pwsh" -ArgumentList "-NoExit", "-Command", $apiCmd
Write-Host "API starting on http://localhost:5080 (pwsh pid $($api.Id))"

# --- Extractor ---
$uvicorn = Join-Path $root "services/extractor/.venv/Scripts/uvicorn.exe"
if (Test-Path $uvicorn) {
    $extCmd = "Set-Location '$root/services/extractor'"
    if ($tesseract) { $extCmd += "; `$env:TESSERACT_CMD='$tesseract'" }
    if ($extractorSecret) { $extCmd += "; `$env:EXTRACTOR_SHARED_SECRET='$extractorSecret'" }
    $extCmd += "; & '.venv/Scripts/uvicorn.exe' app.main:app --port 8000"
    $ext = Start-Process -PassThru -FilePath "pwsh" -ArgumentList "-NoExit", "-Command", $extCmd
    Write-Host "Extractor starting on http://localhost:8000 (pwsh pid $($ext.Id))"
} else {
    Write-Host "Extractor .venv not found at $uvicorn — skipping (intake extraction will be unavailable)" -ForegroundColor Yellow
}

# --- Web ---
$web = Start-Process -PassThru -FilePath "pwsh" -ArgumentList "-NoExit", "-Command", "Set-Location '$root/apps/web'; npm run dev"
Write-Host "Web starting on http://localhost:3000 (pwsh pid $($web.Id))"

Write-Host "`nDone. Ports: API 5080, extractor 8000, web 3000. Mailpit UI: http://localhost:8025"
Write-Host "Stop everything with scripts/dev-stack-stop.ps1"
