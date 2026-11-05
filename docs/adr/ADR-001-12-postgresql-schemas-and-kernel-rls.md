# ADR-001: 12 PostgreSQL Schemas and Zero-Cross-Tenant RLS Interceptor

## Status
**Accepted & Implemented** (Commit `297fac4`, `b3ed6a4`)

## Context
SGrowing Growth OS operates as an enterprise multi-tenant platform managing commercial, financial, and AI operational data across global and regional enterprises. A single flat database schema creates noisy-neighbor risks, complex migration interdependencies, and elevated risk of cross-tenant data leakage.

## Decision
1. **12 Domain-Partitioned Schemas**:
   Normalize database tables across 12 distinct schemas:
   - `core`: Identity, organizations, workspaces, and audit logs.
   - `commerce`: Orders, COD lifecycles, WMS inventory, dynamic pricing, and cart reservations.
   - `crm`: Contacts, customer 360, pipelines, and activities.
   - `marketing`: Campaigns, cadences, spintax variations, and social broadcasts.
   - `analytics`: Multi-touch attribution, session replay, and LTV forecasts.
   - `voice`: Softphone sessions, SIP trunk media streams, and AMD detection.
   - `ai`: Agent fleets, swarm missions, tool execution logs, and grounding embeddings.
   - `billing`: Saas plans, meters, and tax invoices.
   - `operations`: Courier deliveries, PWA offline sync, and SLA monitors.
   - `compliance`: Cryptographic document seals, PAdES-B-LTV signatures, and audit trails.
   - `platform`: Transactional outbox messages and idempotency ledgers.
   - `integrations`: Webhook channels, connectors, and third-party credential vaults.

2. **Kernel-Level PostgreSQL Row-Level Security (RLS)**:
   - EF Core interceptor `PostgreSqlRlsDbConnectionInterceptor` sets `SET LOCAL app.current_tenant_id = '<guid>'` on every physical connection checkout.
   - SQL Migration scripts enable `ALTER TABLE ... ENABLE ROW LEVEL SECURITY;` and `FORCE ROW LEVEL SECURITY;` with isolation policies across all 12 schemas.
   - Normalized `TenantId` as authoritative `Guid`.

## Consequences
- **Positive**: Strict mathematical prevention of cross-tenant leakage at the database engine level. Clean schema modularity.
- **Trade-off**: Requires tenant context propagation across all background jobs and streaming consumers.
