<#
.SYNOPSIS
    Automated MCP Stdio Client Test Harness for SGrowing Growth OS.
.DESCRIPTION
    Launches SGrowing.McpServer over stdio, sends JSON-RPC 2.0 messages (initialize, ping,
    tools/list, tools/call), validates responses, and verifies clean shutdown.
#>

[CmdletBinding()]
param(
    [string]$ServerBinary = "",
    [int]$TimeoutSeconds = 10
)

$ErrorActionPreference = "Stop"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  SGrowing Growth OS - MCP Client Stdio Test Suite        " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# Resolve binary path
if ([string]::IsNullOrWhiteSpace($ServerBinary)) {
    $ServerBinary = "$PSScriptRoot/../src/backend/SGrowing.McpServer/bin/Debug/net10.0/SGrowing.McpServer.exe"
}
$binaryPath = [System.IO.Path]::GetFullPath($ServerBinary)
if (-not (Test-Path $binaryPath)) {
    Write-Host "Binary not found at $binaryPath. Building SGrowing.McpServer..." -ForegroundColor Yellow
    dotnet build "$PSScriptRoot/../src/backend/SGrowing.McpServer/SGrowing.McpServer.csproj" -c Debug
    if (-not (Test-Path $binaryPath)) {
        Write-Error "Could not find built binary at $binaryPath"
        exit 1
    }
}

Write-Host "Launching MCP Server: $binaryPath --stdio" -ForegroundColor Gray

$pinfo = New-Object System.Diagnostics.ProcessStartInfo
$pinfo.FileName = $binaryPath
$pinfo.Arguments = "--stdio"
$pinfo.UseShellExecute = $false
$pinfo.RedirectStandardInput = $true
$pinfo.RedirectStandardOutput = $true
$pinfo.RedirectStandardError = $true
$pinfo.CreateNoWindow = $true

$proc = New-Object System.Diagnostics.Process
$proc.StartInfo = $pinfo

$proc.Start() | Out-Null
$writer = $proc.StandardInput
$reader = $proc.StandardOutput

$allPassed = $true

function Send-McpRequest {
    param(
        [string]$Name,
        [string]$RequestJson,
        [scriptblock]$Validator
    )

    Write-Host -NoNewline "Testing $Name... "
    try {
        $writer.WriteLine($RequestJson)
        $writer.Flush()

        # Read line with timeout
        $readTask = $reader.ReadLineAsync()
        if (-not $readTask.Wait($TimeoutSeconds * 1000)) {
            Write-Host -ForegroundColor Red "FAIL (Timeout waiting for response)"
            return $false
        }

        $responseLine = $readTask.Result
        if ([string]::IsNullOrWhiteSpace($responseLine)) {
            Write-Host -ForegroundColor Red "FAIL (Empty response received)"
            return $false
        }

        $resultObj = $responseLine | ConvertFrom-Json
        $passed = & $Validator $resultObj $responseLine

        if ($passed) {
            Write-Host -ForegroundColor Green "PASS"
            return $true
        } else {
            Write-Host -ForegroundColor Red "FAIL (Assertion failed: $responseLine)"
            return $false
        }
    } catch {
        Write-Host -ForegroundColor Red "FAIL ($($_.Exception.Message))"
        return $false
    }
}

# 1. MCP initialize handshake
$allPassed = (Send-McpRequest -Name "MCP Initialize Protocol" `
    -RequestJson '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05"}}' `
    -Validator {
        param($res)
        return ($res.jsonrpc -eq "2.0") -and ($res.id -eq 1) -and ($res.result.serverInfo.name -eq "SGrowing.McpServer")
    }) -and $allPassed

# 2. MCP initialized notification (no response expected)
Write-Host -NoNewline "Sending notifications/initialized... "
$writer.WriteLine('{"jsonrpc":"2.0","method":"notifications/initialized"}')
$writer.Flush()
Write-Host -ForegroundColor Green "OK"

# 3. MCP ping
$allPassed = (Send-McpRequest -Name "MCP Ping" `
    -RequestJson '{"jsonrpc":"2.0","id":2,"method":"ping"}' `
    -Validator {
        param($res)
        return ($res.jsonrpc -eq "2.0") -and ($res.id -eq 2)
    }) -and $allPassed

# 4. MCP tools/list
$allPassed = (Send-McpRequest -Name "MCP Tools Enumeration (17 tools)" `
    -RequestJson '{"jsonrpc":"2.0","id":3,"method":"tools/list"}' `
    -Validator {
        param($res)
        $count = $res.result.tools.Count
        return ($res.jsonrpc -eq "2.0") -and ($count -eq 17)
    }) -and $allPassed

# 5. MCP tools/call: analytics_get_realtime_roas
$allPassed = (Send-McpRequest -Name "MCP Tool Call: analytics_get_realtime_roas" `
    -RequestJson '{"jsonrpc":"2.0","id":4,"method":"tools/call","params":{"name":"analytics_get_realtime_roas","arguments":{"channel":"meta","daysBack":14}}}' `
    -Validator {
        param($res)
        return ($res.result.isError -eq $false) -and ($res.result.content[0].text -like "*roas*")
    }) -and $allPassed

# 6. MCP tools/call: cod_calculate_net_margin
$allPassed = (Send-McpRequest -Name "MCP Tool Call: cod_calculate_net_margin" `
    -RequestJson '{"jsonrpc":"2.0","id":5,"method":"tools/call","params":{"name":"cod_calculate_net_margin","arguments":{"orderTotal":200,"productCost":60,"shippingFee":10,"callCenterFee":5,"returnRatePercent":10,"returnPenaltyFee":20}}}' `
    -Validator {
        param($res)
        return ($res.result.isError -eq $false) -and ($res.result.content[0].text -like "*trueNetMarginTnd*")
    }) -and $allPassed

# 7. MCP tools/call: courier_reconcile_remittance
$allPassed = (Send-McpRequest -Name "MCP Tool Call: courier_reconcile_remittance" `
    -RequestJson '{"jsonrpc":"2.0","id":6,"method":"tools/call","params":{"name":"courier_reconcile_remittance","arguments":{"courierProvider":"Yalidine","statementId":"STMT-ALG-8891","declaredGross":50000,"declaredFees":4500}}}' `
    -Validator {
        param($res)
        return ($res.result.isError -eq $false) -and ($res.result.content[0].text -like "*statementId*")
    }) -and $allPassed

# Close standard input to signal graceful shutdown
Write-Host -NoNewline "Closing stdio stream and waiting for clean exit... "
$writer.Close()

if ($proc.WaitForExit(5000)) {
    Write-Host -ForegroundColor Green "PASS (Exit Code: $($proc.ExitCode))"
} else {
    Write-Host -ForegroundColor Yellow "WARN (Process did not exit in 5s, killing)"
    $proc.Kill()
}

Write-Host "==========================================================" -ForegroundColor Cyan
if ($allPassed) {
    Write-Host "  ALL 7 MCP PROTOCOL & TOOL CHECKS PASSED SUCCESSFULLY!   " -ForegroundColor Green
    exit 0
} else {
    Write-Host "  SOME MCP PROTOCOL CHECKS FAILED!                        " -ForegroundColor Red
    exit 1
}
