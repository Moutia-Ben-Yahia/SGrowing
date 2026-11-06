<#
.SYNOPSIS
    Automated Staging Smoke Test & Health Probe Suite for SGrowing Growth OS.
.DESCRIPTION
    Validates that all microservices and persistent infrastructure containers
    (API, Frontend, PostgreSQL, Redis, ClickHouse, MinIO, MCP Server) are responsive.
#>

[CmdletBinding()]
param(
    [string]$ApiBaseUrl = "http://localhost:5000",
    [string]$FrontendBaseUrl = "http://localhost:4200",
    [string]$ClickHouseUrl = "http://localhost:8123",
    [string]$MinioUrl = "http://localhost:9000",
    [string]$McpServerUrl = "http://localhost:5001",
    [int]$MaxRetries = 3,
    [int]$RetryDelaySeconds = 2
)

$ErrorActionPreference = "Stop"

function Test-Endpoint {
    param(
        [string]$Name,
        [string]$Url,
        [string]$ExpectedStatus = "200"
    )

    Write-Host -NoNewline "Checking $Name ($Url)... "
    for ($attempt = 1; $attempt -le $MaxRetries; $attempt++) {
        try {
            $response = Invoke-WebRequest -Uri $Url -Method Get -TimeoutSec 10 -UseBasicParsing
            if ($response.StatusCode.ToString() -eq $ExpectedStatus) {
                Write-Host -ForegroundColor Green "PASS ($($response.StatusCode))"
                return $true
            } else {
                if ($attempt -lt $MaxRetries) {
                    Start-Sleep -Seconds $RetryDelaySeconds
                    continue
                }
                Write-Host -ForegroundColor Yellow "WARN ($($response.StatusCode))"
                return $false
            }
        } catch {
            if ($attempt -lt $MaxRetries) {
                Start-Sleep -Seconds $RetryDelaySeconds
                continue
            }
            Write-Host -ForegroundColor Red "FAIL ($($_.Exception.Message))"
            return $false
        }
    }
    return $false
}

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  SGrowing Growth OS - Staging Smoke Test Suite           " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

$allPassed = $true

# 1. API Health Probes
$allPassed = (Test-Endpoint "API Liveness Probe" "$ApiBaseUrl/health/live") -and $allPassed
$allPassed = (Test-Endpoint "API Readiness Probe" "$ApiBaseUrl/health/ready") -and $allPassed
$allPassed = (Test-Endpoint "Scalar API Reference Documentation" "$ApiBaseUrl/scalar/v1") -and $allPassed

# 2. ClickHouse OLAP Engine
$allPassed = (Test-Endpoint "ClickHouse OLAP Ping" "$ClickHouseUrl/ping") -and $allPassed
$allPassed = (Test-Endpoint "ClickHouse Realtime ROAS API" "$ApiBaseUrl/api/v1/analytics/olap/realtime-roas") -and $allPassed

# 3. MinIO S3 Object Storage
$allPassed = (Test-Endpoint "MinIO S3 Health Check" "$MinioUrl/minio/health/live") -and $allPassed

# 4. MCP Server
$allPassed = (Test-Endpoint "MCP AI Server Status" "$McpServerUrl/health/live") -and $allPassed

# 5. Inbound Webhook Mesh DLQ Endpoint
$allPassed = (Test-Endpoint "Inbound Webhook DLQ API" "$ApiBaseUrl/api/v1/webhooks/inbound/dlq") -and $allPassed

# 6. Frontend Application
$allPassed = (Test-Endpoint "Angular 22 SSR Frontend" "$FrontendBaseUrl") -and $allPassed

Write-Host "==========================================================" -ForegroundColor Cyan
if ($allPassed) {
    Write-Host "  ALL SMOKE TESTS PASSED! Staging environment is green.  " -ForegroundColor Green
    exit 0
} else {
    Write-Host "  SOME CHECKS FAILED! Please review container logs.      " -ForegroundColor Red
    exit 1
}
