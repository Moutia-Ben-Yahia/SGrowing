#!/usr/bin/env bash
set -euo pipefail

API_URL="${API_URL:-http://localhost:5000}"
FRONTEND_URL="${FRONTEND_URL:-http://localhost:4200}"
CLICKHOUSE_URL="${CLICKHOUSE_URL:-http://localhost:8123}"
MINIO_URL="${MINIO_URL:-http://localhost:9000}"
MCPSERVER_URL="${MCPSERVER_URL:-http://localhost:5001}"

echo "=========================================================="
echo "  SGrowing Growth OS - Staging Smoke Test Suite (Bash)    "
echo "=========================================================="

test_url() {
    local name="$1"
    local url="$2"
    echo -n "Checking ${name} (${url})... "
    status=$(curl -s -o /dev/null -w "%{http_code}" --max-time 10 "${url}" || echo "000")
    if [[ "$status" =~ ^(200|301|302)$ ]]; then
        echo "PASS (${status})"
        return 0
    else
        echo "FAIL (${status})"
        return 1
    fi
}

passed=0
failed=0

test_url "API Liveness" "${API_URL}/health/live" && ((passed++)) || ((failed++))
test_url "API Readiness" "${API_URL}/health/ready" && ((passed++)) || ((failed++))
test_url "Scalar API Reference" "${API_URL}/scalar/v1" && ((passed++)) || ((failed++))
test_url "ClickHouse Ping" "${CLICKHOUSE_URL}/ping" && ((passed++)) || ((failed++))
test_url "ClickHouse ROAS API" "${API_URL}/api/v1/analytics/olap/realtime-roas" && ((passed++)) || ((failed++))
test_url "MinIO Health" "${MINIO_URL}/minio/health/live" && ((passed++)) || ((failed++))
test_url "MCP Server" "${MCPSERVER_URL}/health/live" && ((passed++)) || ((failed++))
test_url "Angular Frontend" "${FRONTEND_URL}" && ((passed++)) || ((failed++))

echo "=========================================================="
echo "Results: ${passed} passed, ${failed} failed."
if [ "$failed" -eq 0 ]; then
    echo "Staging environment verification complete: ALL GREEN."
    exit 0
else
    echo "Staging checks failed."
    exit 1
fi
