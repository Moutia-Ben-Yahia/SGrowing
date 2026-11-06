<#
.SYNOPSIS
    SGrowing Growth OS - Local Staging Lifecycle & Health Orchestration Script.
.DESCRIPTION
    Boots the complete staging container environment (PostgreSQL 17, Redis 7.4, ClickHouse 24.8,
    MinIO, API, Worker, McpServer, and Angular 22 Frontend) using Docker Compose (native or WSL2),
    waits for healthiness, and executes automated smoke tests.
#>

[CmdletBinding()]
param(
    [switch]$Down,
    [switch]$Build,
    [switch]$SkipSmokeTest,
    [int]$WaitTimeoutSeconds = 60
)

$ErrorActionPreference = "Stop"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  SGrowing Growth OS - Local Staging Orchestrator         " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Detect Docker Compose engine (Native Windows vs WSL Ubuntu)
$useWsl = $false
$dockerComposeCmd = "docker compose"

if (Get-Command docker -ErrorAction SilentlyContinue) {
    Write-Host "Detected native Docker engine on host." -ForegroundColor Gray
} elseif (Get-Command wsl -ErrorAction SilentlyContinue) {
    try {
        $wslCheck = wsl -d Ubuntu -e docker compose version 2>&1
        if ($LASTEXITCODE -eq 0) {
            $useWsl = $true
            Write-Host "Detected Docker Compose via WSL Ubuntu: $wslCheck" -ForegroundColor Gray
        }
    } catch {}
}

if (-not (Get-Command docker -ErrorAction SilentlyContinue) -and -not $useWsl) {
    Write-Error "Neither native Docker nor WSL Ubuntu Docker was detected on this machine."
    exit 1
}

# Helper to run docker compose
function Invoke-DockerCompose {
    param([string]$Args)
    if ($useWsl) {
        $wslPath = "/mnt/c/Business/Software/SGrowing"
        $cmd = "cd $wslPath && docker compose --env-file .env.staging -f docker-compose.staging.yml $Args"
        wsl -d Ubuntu -e bash -c $cmd
    } else {
        Invoke-Expression "docker compose --env-file .env.staging -f docker-compose.staging.yml $Args"
    }
}

# Handle teardown request
if ($Down) {
    Write-Host "Stopping and tearing down staging containers..." -ForegroundColor Yellow
    Invoke-DockerCompose "down -v --remove-orphans"
    Write-Host "Staging containers removed successfully." -ForegroundColor Green
    exit 0
}

# 2. Start services
$upArgs = "up -d"
if ($Build) {
    $upArgs += " --build"
}

Write-Host "Starting staging stack with $upArgs..." -ForegroundColor Gray
Invoke-DockerCompose $upArgs
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to start staging containers."
    exit 1
}

# 3. Wait for database and core services to report healthy
Write-Host "Waiting for infrastructure healthchecks (up to $WaitTimeoutSeconds s)..." -ForegroundColor Gray
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$ready = $false

while ($sw.Elapsed.TotalSeconds -lt $WaitTimeoutSeconds) {
    try {
        # Check API liveness probe
        $resp = Invoke-WebRequest -Uri "http://localhost:5000/health/live" -TimeoutSec 2 -UseBasicParsing -ErrorAction SilentlyContinue
        if ($resp.StatusCode -eq 200) {
            $ready = $true
            break
        }
    } catch {}
    Start-Sleep -Seconds 2
}

if ($ready) {
    Write-Host "Staging core services are healthy after $($sw.Elapsed.Seconds)s." -ForegroundColor Green
} else {
    Write-Host "Staging services are still initializing. Checking status..." -ForegroundColor Yellow
}

# 4. Execute Smoke Tests
if (-not $SkipSmokeTest) {
    Write-Host ""
    Write-Host "Executing Staging Smoke Test Suite..." -ForegroundColor Cyan
    powershell -ExecutionPolicy Bypass -File "$PSScriptRoot/staging-smoke-test.ps1"
    exit $LASTEXITCODE
} else {
    Write-Host "Skipping smoke test execution as requested (-SkipSmokeTest)." -ForegroundColor Gray
    exit 0
}
