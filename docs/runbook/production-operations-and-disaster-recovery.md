# SGrowing Growth OS — Production Operations & Disaster Recovery Runbook

## 1. System Architecture Overview

```
                      +-----------------------------------+
                      |       NGINX / Cloudflare Edge     |
                      +-----------------+-----------------+
                                        |
                 +----------------------+----------------------+
                 | (Port 80/443)                               | (Port 5000/5001)
                 v                                             v
+-----------------------------------+         +-----------------------------------+
|      Angular 22 SSR Frontend      |         |     SGrowing.Api / McpServer      |
+-----------------------------------+         +-----------------+-----------------+
                                                                |
              +-------------------------+-----------------------+-----------------------+
              |                         |                       |                       |
              v                         v                       v                       v
+---------------------------+ +-------------------+ +-----------------------+ +-------------------+
|  PostgreSQL 17 (pgvector) | | Redis 7.4 Cluster | | ClickHouse 24.8 OLAP  | |  MinIO S3 Vault   |
|     (12 RLS Schemas)      | |   (Stream Mesh)   | |  (MergeTree Engine)   | | (Immutable Seals) |
+---------------------------+ +-------------------+ +-----------------------+ +-------------------+
              ^                         ^                       ^
              |                         |                       |
              +-------------------------+-----------------------+
                                        |
                      +-----------------+-----------------+
                      |    SGrowing.Worker Daemon Cluster |
                      +-----------------------------------+
```

---

## 2. Standard Deployment Procedure

### 2.1 Staging Deployment
```bash
# 1. Boot staging cluster with health checks
docker compose -f docker-compose.staging.yml up -d --build

# 2. Run automated smoke tests
powershell -File ./scripts/staging-smoke-test.ps1
# or on Linux:
./scripts/staging-smoke-test.sh
```

### 2.2 Production Canary Deployment
1. Pull new release tag: `git checkout tags/v1.0.0-prod`.
2. Apply EF Core database migrations:
   ```bash
   dotnet ef database update --project src/backend/SGrowing.Api
   ```
3. Deploy canary worker and API instances (10% traffic).
4. Monitor OpenTelemetry error rates via Prometheus/Grafana dashboard (`GrowthOsMetrics.ActiveWorkflows`).
5. Promote to 100% upon 15 minutes of zero error rate.

---

## 3. Disaster Recovery & Backup SOP

### 3.1 PostgreSQL WAL Archiving & Point-In-Time Recovery (PITR)
- **Continuous Archiving**: WAL logs shipped every 5 minutes to cold S3 bucket.
- **Daily Snapshots**: Automated pg_dump executed at 02:00 UTC:
  ```bash
  pg_dump -h postgres -U postgres -d sgrowing -F c -b -v -f /backups/sgrowing_daily_$(date +%Y%m%d).dump
  ```
- **Recovery Procedure**:
  1. Stop API and Worker daemons: `docker compose stop api worker`.
  2. Restore base snapshot: `pg_restore -h postgres -U postgres -d sgrowing --clean /backups/sgrowing_daily_YYYYMMDD.dump`.
  3. Replay WAL archive up to desired transaction timestamp.
  4. Verify tenant schema integrity and restart cluster: `docker compose start`.

### 3.2 MinIO / S3 Document Vault Mirroring
- Sealed documents in `compliance.DocumentSealRecords` are mirrored to MinIO bucket `sgrowing-tenant-{tenantId}` with tier `GLACIER_INSTANT_RETRIEVAL`.
- **Integrity Audit**:
  ```bash
  # Execute integrity scan via SGrowing MCP server
  curl -X POST http://localhost:5001/mcp -H "Content-Type: application/json" \
    -d '{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"document_vault_verify_seal","arguments":{"sealId":"<id>"}}}'
  ```

---

## 4. Operational Troubleshooting

| Symptom | Probable Cause | Action |
|---|---|---|
| **High Queue Latency in Event Mesh** | Consumer offset stall in Worker | Inspect `GetConsumerOffsetAsync` and restart `RedisStreamConsumerDaemon`. |
| **ROAS Dashboard Query Slowdown** | ClickHouse unmerged parts | Trigger `OPTIMIZE TABLE analytics.events FINAL;` on ClickHouse container. |
| **Carrier Remittance Discrepancies** | Courier tariff mismatch | Review `CarrierRemittanceReconciliationService` report and trigger `AutoResolveDiscrepancyAsync`. |
| **Prompt Injection Alert Surge** | Hostile LLM scraping / attack | Inspect `PromptInjectionGuardService` logs and update firewall blocklist. |
