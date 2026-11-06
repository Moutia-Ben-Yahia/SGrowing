<#
.SYNOPSIS
    SGrowing SRE Production Launch Readiness Audit Tool
.DESCRIPTION
    Performs comprehensive pre-flight diagnostics across PostgreSQL, Redis, MinIO S3,
    OpenTelemetry Collector, SGrowing API, and Web Nginx reverse proxy.
    Calculates weighted score and outputs a graded readiness report (A, B, C, D).
#>

[CmdletBinding()]
param (
    [string]$ApiUrl = "http://localhost:5000",
    [string]$WebUrl = "http://localhost",
    [string]$DbHost = "localhost",
    [int]$DbPort = 5432,
    [string]$RedisHost = "localhost",
    [int]$RedisPort = 6379,
    [string]$MinioUrl = "http://localhost:9000",
    [string]$OtelHealthUrl = "http://localhost:13133",
    [string]$OtelMetricsUrl = "http://localhost:8889/metrics",
    [string]$OutputPath = "production-readiness-report.json"
)

$ErrorActionPreference = "Continue"

Write-Host "==================================================================" -ForegroundColor Cyan
Write-Host "     SGrowing Enterprise SRE Pre-Launch Readiness Audit Tool     " -ForegroundColor Cyan
Write-Host "==================================================================" -ForegroundColor Cyan
Write-Host "Timestamp: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss UTC')`n" -ForegroundColor DarkGray

$checks = [System.Collections.Generic.List[PSCustomObject]]::new()

function Test-TcpPort {
    param([string]$HostName, [int]$Port, [string]$ServiceName, [int]$Weight = 20)
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $status = "FAIL"
    $latency = -1
    $message = ""

    try {
        $client = New-Object System.Net.Sockets.TcpClient
        $iar = $client.BeginConnect($HostName, $Port, $null, $null)
        $waited = $iar.AsyncWaitHandle.WaitOne(3000, $false)
        if ($waited) {
            $client.EndConnect($iar)
            $sw.Stop()
            $latency = [math]::Round($sw.Elapsed.TotalMilliseconds, 2)
            $client.Close()
            $status = "PASS"
            $message = "Connected to ${HostName}:${Port} in ${latency}ms"
        } else {
            $client.Close()
            $message = "Timeout connecting to ${HostName}:${Port} after 3000ms"
        }
    } catch {
        $message = "Connection error: $($_.Exception.Message)"
    }

    $item = [PSCustomObject]@{
        Component   = $ServiceName
        CheckType   = "TCP Ping & Latency"
        Target      = "${HostName}:${Port}"
        Status      = $status
        LatencyMs   = $latency
        Details     = $message
        Weight      = $Weight
        Score       = if ($status -eq "PASS") { $Weight } else { 0 }
    }
    $checks.Add($item)
    return $item
}

function Test-HttpEndpoint {
    param([string]$Url, [string]$ServiceName, [string]$CheckType, [int]$ExpectedStatus = 200, [int]$Weight = 20)
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $status = "FAIL"
    $latency = -1
    $message = ""

    try {
        $req = [System.Net.HttpWebRequest]::Create($Url)
        $req.Timeout = 5000
        $req.Method = "GET"
        $req.UserAgent = "SGrowing-SRE-Preflight/1.0"

        $resp = $req.GetResponse()
        $httpResp = [System.Net.HttpWebResponse]$resp
        $sw.Stop()
        $latency = [math]::Round($sw.Elapsed.TotalMilliseconds, 2)
        $code = [int]$httpResp.StatusCode

        if ($code -eq $ExpectedStatus) {
            $status = "PASS"
            $message = "HTTP $code OK received in ${latency}ms"
        } else {
            $message = "Unexpected status: HTTP $code (expected $ExpectedStatus)"
        }
        $resp.Close()
    } catch [System.Net.WebException] {
        $sw.Stop()
        if ($_.Response) {
            $httpResp = [System.Net.HttpWebResponse]$_.Response
            $code = [int]$httpResp.StatusCode
            $message = "HTTP error ${code}: $($_.Message)"
        } else {
            $message = "Network error: $($_.Message)"
        }
    } catch {
        $message = "Error: $($_.Exception.Message)"
    }

    $item = [PSCustomObject]@{
        Component   = $ServiceName
        CheckType   = $CheckType
        Target      = $Url
        Status      = $status
        LatencyMs   = $latency
        Details     = $message
        Weight      = $Weight
        Score       = if ($status -eq "PASS") { $Weight } else { 0 }
    }
    $checks.Add($item)
    return $item
}

# --- 1. PostgreSQL Check ---
Write-Host "[1/6] Auditing PostgreSQL 17 Database Engine..." -ForegroundColor Yellow
$pg = Test-TcpPort -HostName $DbHost -Port $DbPort -ServiceName "PostgreSQL 17" -Weight 25
if ($pg.Status -eq "PASS") {
    Write-Host "  -> PASS: PostgreSQL is reachable ($($pg.LatencyMs)ms)" -ForegroundColor Green
} else {
    Write-Host "  -> FAIL: PostgreSQL unreachable - $($pg.Details)" -ForegroundColor Red
}

# --- 2. Redis Check ---
Write-Host "[2/6] Auditing Redis 7.4 Distributed Cache & Outbox..." -ForegroundColor Yellow
$redis = Test-TcpPort -HostName $RedisHost -Port $RedisPort -ServiceName "Redis 7.4" -Weight 20
if ($redis.Status -eq "PASS") {
    Write-Host "  -> PASS: Redis is reachable ($($redis.LatencyMs)ms)" -ForegroundColor Green
} else {
    Write-Host "  -> FAIL: Redis unreachable - $($redis.Details)" -ForegroundColor Red
}

# --- 3. MinIO S3 Check ---
Write-Host "[3/6] Auditing MinIO S3 Object Storage..." -ForegroundColor Yellow
$minio = Test-HttpEndpoint -Url "$MinioUrl/minio/health/live" -ServiceName "MinIO S3" -CheckType "Health Live Probe" -Weight 15
if ($minio.Status -eq "PASS") {
    Write-Host "  -> PASS: MinIO health/live returned 200 OK ($($minio.LatencyMs)ms)" -ForegroundColor Green
} else {
    Write-Host "  -> FAIL: MinIO health check failed - $($minio.Details)" -ForegroundColor Red
}

# --- 4. OpenTelemetry Collector Check ---
Write-Host "[4/6] Auditing OpenTelemetry Collector & Prometheus Exporter..." -ForegroundColor Yellow
$otelHealth = Test-HttpEndpoint -Url $OtelHealthUrl -ServiceName "OpenTelemetry Collector" -CheckType "Health Extension 13133" -Weight 10
$otelMetrics = Test-HttpEndpoint -Url $OtelMetricsUrl -ServiceName "Prometheus Metrics Exporter" -CheckType "Scrape Endpoint 8889" -Weight 10
if ($otelHealth.Status -eq "PASS" -and $otelMetrics.Status -eq "PASS") {
    Write-Host "  -> PASS: OpenTelemetry collector & metrics active" -ForegroundColor Green
} else {
    Write-Host "  -> WARN: OpenTelemetry collector or metrics unavailable" -ForegroundColor Yellow
}

# --- 5. SGrowing Core API Health Check ---
Write-Host "[5/6] Auditing SGrowing Core .NET 10 API /health Endpoint..." -ForegroundColor Yellow
$apiHealth = Test-HttpEndpoint -Url "$ApiUrl/health" -ServiceName "SGrowing API" -CheckType "Service Defaults /health" -Weight 20
if ($apiHealth.Status -eq "PASS") {
    Write-Host "  -> PASS: SGrowing API /health is healthy ($($apiHealth.LatencyMs)ms)" -ForegroundColor Green
} else {
    Write-Host "  -> FAIL: SGrowing API /health check failed - $($apiHealth.Details)" -ForegroundColor Red
}

# --- 6. SGrowing Web (Nginx) & Security Headers Check ---
Write-Host "[6/6] Auditing SGrowing Web Nginx Reverse Proxy & Security Headers..." -ForegroundColor Yellow
$webHealth = Test-HttpEndpoint -Url "$WebUrl/healthz" -ServiceName "Nginx Reverse Proxy" -CheckType "/healthz probe" -Weight 10

# Security Headers Inspection
$headersOk = $false
try {
    $req = [System.Net.HttpWebRequest]::Create($WebUrl)
    $req.Timeout = 5000
    $resp = $req.GetResponse()
    $csp = $resp.Headers["Content-Security-Policy"]
    $xframe = $resp.Headers["X-Frame-Options"]
    $hsts = $resp.Headers["Strict-Transport-Security"]
    $xcontent = $resp.Headers["X-Content-Type-Options"]
    $resp.Close()

    if ($csp -and $xframe -and $xcontent) {
        $headersOk = $true
        Write-Host "  -> PASS: Security headers verified (CSP, X-Frame-Options, X-Content-Type-Options)" -ForegroundColor Green
    } else {
        Write-Host "  -> WARN: One or more recommended security headers missing" -ForegroundColor Yellow
    }
} catch {
    Write-Host "  -> WARN: Could not inspect web headers ($($_.Exception.Message))" -ForegroundColor Yellow
}

# --- Report & Grade Calculation ---
$totalWeight = ($checks | Measure-Object -Property Weight -Sum).Sum
$earnedScore = ($checks | Measure-Object -Property Score -Sum).Sum
$percentage = [math]::Round(($earnedScore / $totalWeight) * 100, 1)

$grade = "D"
$verdictColor = [System.ConsoleColor]::Red
$verdict = "NOT READY - CRITICAL FAILURES DETECTED"

if ($percentage -ge 90) {
    $grade = "A"
    $verdictColor = [System.ConsoleColor]::Green
    $verdict = "PRODUCTION READY (OPTIMAL PASS)"
} elseif ($percentage -ge 75) {
    $grade = "B"
    $verdictColor = [System.ConsoleColor]::Yellow
    $verdict = "QUALIFIED WITH ADVISORIES"
} elseif ($percentage -ge 50) {
    $grade = "C"
    $verdictColor = [System.ConsoleColor]::Magenta
    $verdict = "DEGRADED - DO NOT DEPLOY TO PRODUCTION"
}

Write-Host "`n==================================================================" -ForegroundColor Cyan
Write-Host "                PRE-LAUNCH READINESS SCORECARD                    " -ForegroundColor Cyan
Write-Host "==================================================================" -ForegroundColor Cyan
$checks | Format-Table Component, CheckType, Status, LatencyMs, Details -AutoSize

Write-Host "Overall Audit Score : $earnedScore / $totalWeight ($percentage%)" -ForegroundColor White
Write-Host "Readiness Grade     : GRADE $grade" -ForegroundColor $verdictColor
Write-Host "Deployment Status   : $verdict" -ForegroundColor $verdictColor
Write-Host "==================================================================`n" -ForegroundColor Cyan

# Save JSON artifact
$reportObject = [PSCustomObject]@{
    Timestamp   = (Get-Date).ToString("o")
    Grade       = $grade
    Score       = $earnedScore
    TotalWeight = $totalWeight
    Percentage  = $percentage
    Status      = $verdict
    Checks      = $checks
}
$reportObject | ConvertTo-Json -Depth 4 | Out-File -FilePath $OutputPath -Encoding utf8
Write-Host "Detailed JSON audit report saved to: $OutputPath" -ForegroundColor DarkGray

if ($grade -eq "A" -or $grade -eq "B") {
    exit 0
} else {
    exit 1
}
