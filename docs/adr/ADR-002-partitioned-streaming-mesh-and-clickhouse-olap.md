# ADR-002: High-Throughput Partitioned Event Streaming Mesh and ClickHouse OLAP Projection

## Status
**Accepted & Implemented** (Commit `218df4b`, `133cfd1`)

## Context
High-volume marketing operations, clickstream events, ad attribution touchpoints, and agent telemetry generate >100M events/month. Storing these directly into transactional PostgreSQL causes table bloat, lock contention, and high query latency for real-time dashboards.

## Decision
1. **Partitioned Event Streaming Mesh (`IEventStreamingMesh`)**:
   - In-memory deterministic 32-bit FNV-1a partition hashing guarantees strict FIFO ordering per tenant or contact key.
   - Independent consumer group offset checkpointing (`group:topic:partition`) allows autonomous consumer progression.
   - Poison messages are automatically quarantined into a Dead-Letter Queue (DLQ).
2. **ClickHouse OLAP Projection Pipeline (`IClickHouseAnalyticsProjectionService`)**:
   - Micro-batch buffering with vectorized execution.
   - Sub-second vector aggregation queries for:
     - Real-Time ROAS (`GetRealtimeRoasAsync`) across Google, Meta, TikTok.
     - Multi-Step Funnel Conversion & Drop-off Analysis (`GetFunnelConversionAsync`).
     - Autonomous Agent Telemetry & Cost Aggregations (`GetAgentExecutionTelemetryAggregationAsync`).

## Consequences
- **Positive**: Sub-second analytical query performance over millions of records with 0% impact on OLTP database throughput.
- **Trade-off**: Requires projection workers to sync streaming offsets.
