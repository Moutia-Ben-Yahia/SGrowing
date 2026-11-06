#!/usr/bin/env bash
# ==============================================================================
# SGrowing Enterprise Zero-Downtime Database Migration Runner (Linux/macOS)
# ==============================================================================
set -euo pipefail

COLOR_RESET="\033[0m"
COLOR_CYAN="\033[36m"
COLOR_GREEN="\033[32m"
COLOR_YELLOW="\033[33m"
COLOR_RED="\033[31m"
COLOR_MAGENTA="\033[35m"

log_step() { echo -e "\n${COLOR_CYAN}[MIGRATION-STEP] $1${COLOR_RESET}"; }
log_success() { echo -e "${COLOR_GREEN}[SUCCESS] $1${COLOR_RESET}"; }
log_warn() { echo -e "${COLOR_YELLOW}[WARNING] $1${COLOR_RESET}"; }
log_fail() { echo -e "${COLOR_RED}[ERROR] $1${COLOR_RESET}"; }

echo -e "${COLOR_MAGENTA}==================================================================${COLOR_RESET}"
echo -e "${COLOR_MAGENTA}   SGrowing Enterprise Zero-Downtime Database Migration Runner   ${COLOR_RESET}"
echo -e "${COLOR_MAGENTA}==================================================================${COLOR_RESET}"

# 1. Resolve Connection String
CONNECTION_STRING="${ConnectionStrings__DefaultConnection:-}"

if [[ -z "$CONNECTION_STRING" && -f ".env" ]]; then
    log_step "Reading connection parameters from .env file..."
    CONNECTION_STRING=$(grep -E '^ConnectionStrings__DefaultConnection=' .env | cut -d '=' -f2- | tr -d '"' | tr -d "'") || true
fi

if [[ -z "$CONNECTION_STRING" ]]; then
    log_warn "No connection string found. Defaulting to local Docker Postgres setup."
    CONNECTION_STRING="Host=localhost;Port=5432;Database=sgrowing_prod;Username=sgrowing_app;Password=sgrowing_secure_password_2026;"
fi

DB_HOST="localhost"
DB_PORT="5432"

if [[ "$CONNECTION_STRING" =~ Host=([^;]+) ]]; then DB_HOST="${BASH_REMATCH[1]}"; fi
if [[ "$CONNECTION_STRING" =~ Port=([^;]+) ]]; then DB_PORT="${BASH_REMATCH[1]}"; fi

# 2. Pre-flight Check: TCP Socket Reachability
log_step "Checking PostgreSQL reachability at ${DB_HOST}:${DB_PORT}..."
if nc -z -w5 "$DB_HOST" "$DB_PORT" 2>/dev/null || (exec 3<>/dev/tcp/"$DB_HOST"/"$DB_PORT" && exec 3>&-) 2>/dev/null; then
    log_success "PostgreSQL server is reachable on port $DB_PORT."
else
    log_fail "Pre-flight connection to ${DB_HOST}:${DB_PORT} failed or timed out."
    log_warn "Verify container status with: docker compose -f docker-compose.prod.yml ps postgres"
    exit 1
fi

API_PROJECT="src/backend/SGrowing.Api/SGrowing.Api.csproj"
if [[ ! -f "$API_PROJECT" ]]; then
    log_fail "API project not found at $API_PROJECT. Run script from repository root."
    exit 1
fi

# 3. Zero-Downtime Migration Execution
log_step "Executing EF Core database update..."
START_TIME=$(date +%s%N 2>/dev/null || date +%s)

export ConnectionStrings__DefaultConnection="$CONNECTION_STRING"
if dotnet ef database update --project "$API_PROJECT" --context SGrowingDbContext; then
    END_TIME=$(date +%s%N 2>/dev/null || date +%s)
    log_success "EF Core migrations executed successfully."
else
    log_fail "EF Core database update failed!"
    echo -e "${COLOR_YELLOW}"
    echo "ROLLBACK SAFETY ADVISORY:"
    echo "1. SGrowing uses additive expand-and-contract migrations."
    echo "2. Existing production API instances remain backward-compatible."
    echo "3. Query PostgreSQL locks with: SELECT * FROM pg_stat_activity WHERE wait_event_type = 'Lock';"
    echo -e "${COLOR_RESET}"
    exit 2
fi

log_success "Database schema is up-to-date and verified."
echo -e "${COLOR_GREEN}\n[ZERO-DOWNTIME STATUS] System ready for rolling API container restart.${COLOR_RESET}"
exit 0
