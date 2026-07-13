# ==============================================================================
# Judge0 Enterprise Monitoring Script
# Checks the health, availability, response times, and language runtimes.
# ==============================================================================

$baseUrl = "http://localhost:2358"
$expectedLanguages = @(50, 54, 62, 63, 71, 51) # C, C++, Java, JS, Python, C#

Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "   JUDGE0 ENTERPRISE MONITORING SYSTEM       " -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan

# 1. Check if Docker Daemon is running
Write-Host "[1/4] Checking Docker Daemon..." -NoNewline
& docker info > $null 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host " [FAILED]" -ForegroundColor Red
    Write-Host "ERROR: Docker is not running. Please start Docker Desktop and try again." -ForegroundColor Yellow
    exit 1
}
Write-Host " [OK]" -ForegroundColor Green

# 2. Check Judge0 Containers status
Write-Host "[2/4] Checking Judge0 Containers..."
$containers = @("server", "worker", "db", "redis")
$allRunning = $true

foreach ($c in $containers) {
    $status = & docker compose -f docker-compose.judge0.yml ps --format json | ConvertFrom-Json | Where-Object { $_.Service -eq $c }
    if ($status -and $status.State -eq "running") {
        Write-Host "  - Service '$c': Running (ID: $($status.ID))" -ForegroundColor Green
    } else {
        Write-Host "  - Service '$c': NOT RUNNING!" -ForegroundColor Red
        $allRunning = $false
    }
}

if (-not $allRunning) {
    Write-Host "WARNING: Some services are not running. Run: 'docker compose -f docker-compose.judge0.yml up -d' to start them." -ForegroundColor Yellow
}

# 3. Check Judge0 System Info API (health and latency test)
Write-Host "[3/4] Testing API Health & Latency..." -NoNewline
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
try {
    $sysInfo = Invoke-RestMethod -Uri "$baseUrl/system_info" -Method Get -TimeoutSec 5
    $stopwatch.Stop()
    Write-Host " [OK]" -ForegroundColor Green
    Write-Host "  - Latency: $($stopwatch.ElapsedMilliseconds) ms" -ForegroundColor Gray
    Write-Host "  - OS: $($sysInfo.os)" -ForegroundColor Gray
    Write-Host "  - CPU: $($sysInfo.cpu.model) ($($sysInfo.cpu.cores) cores)" -ForegroundColor Gray
} catch {
    $stopwatch.Stop()
    Write-Host " [FAILED]" -ForegroundColor Red
    Write-Host "ERROR: Could not connect to Judge0 API at $baseUrl. Check if the server container is listening." -ForegroundColor Yellow
    exit 1
}

# 4. Check Supported Languages
Write-Host "[4/4] Verifying Language Runtimes..." -NoNewline
try {
    $languagesList = Invoke-RestMethod -Uri "$baseUrl/languages" -Method Get
    Write-Host " [OK]" -ForegroundColor Green
    
    $supportedIds = $languagesList | ForEach-Object { $_.id }
    $missingLanguages = @()
    
    foreach ($langId in $expectedLanguages) {
        $found = $languagesList | Where-Object { $_.id -eq $langId }
        if ($found) {
            Write-Host "  - Found ID $($langId): $($found.name)" -ForegroundColor Gray
        } else {
            Write-Host "  - Missing ID $($langId)!" -ForegroundColor Red
            $missingLanguages += $langId
        }
    }
    
    if ($missingLanguages.Count -eq 0) {
        Write-Host "SUCCESS: All 6 required languages (C, C++, Java, JS, Python, C#) are supported!" -ForegroundColor Green
    } else {
        Write-Host "WARNING: Some required languages are missing: $($missingLanguages -join ', ')" -ForegroundColor Yellow
    }
} catch {
    Write-Host " [FAILED]" -ForegroundColor Red
    Write-Host "ERROR: Could not query languages API." -ForegroundColor Yellow
    exit 1
}

Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "Health Check Completed!" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan
