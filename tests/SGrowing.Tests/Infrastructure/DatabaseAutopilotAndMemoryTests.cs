using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using SGrowing.Api.Modules.Infrastructure.Autopilot;

namespace SGrowing.Tests.Infrastructure;

/// <summary>
/// Unit and integration tests for Phase 40:
/// Autonomous Self-Healing Infrastructure, Automated Database Index Tuning &amp; Memory Autopilot.
/// </summary>
public class DatabaseAutopilotAndMemoryTests
{
    // =========================================================================
    // 1. Slow Query Analysis & Scan Filtering Tests
    // =========================================================================

    [Fact]
    public void SynthesizeIndex_WithNonSequentialScan_ReturnsNull()
    {
        var engine = new AutonomousIndexSynthesizerEngine();
        var query = new SlowQueryMetric(
            QueryId: "q-1",
            SqlText: "SELECT * FROM orders WHERE tenant_id = 'T1'",
            TotalExecutions: 10_000,
            MeanExecutionTimeMs: 120.0,
            MaxExecutionTimeMs: 350.0,
            RowsExaminedPerExecution: 50_000,
            ScanType: QueryScanType.IndexScan,
            TargetTable: "orders",
            PredicateColumns: ["tenant_id"]);

        var result = engine.SynthesizeIndex(query);

        result.Should().BeNull();
    }

    [Fact]
    public void SynthesizeIndex_WithBitmapHeapScan_ReturnsNull()
    {
        var engine = new AutonomousIndexSynthesizerEngine();
        var query = new SlowQueryMetric(
            QueryId: "q-2",
            SqlText: "SELECT * FROM invoices WHERE status = 'Paid'",
            TotalExecutions: 20_000,
            MeanExecutionTimeMs: 85.0,
            MaxExecutionTimeMs: 200.0,
            RowsExaminedPerExecution: 15_000,
            ScanType: QueryScanType.BitmapHeapScan,
            TargetTable: "invoices",
            PredicateColumns: ["status"]);

        var result = engine.SynthesizeIndex(query);

        result.Should().BeNull();
    }

    [Fact]
    public void SynthesizeIndex_WithRowsUnderThreshold_ReturnsNull()
    {
        var engine = new AutonomousIndexSynthesizerEngine();
        var query = new SlowQueryMetric(
            QueryId: "q-3",
            SqlText: "SELECT * FROM small_lookup WHERE code = 'XYZ'",
            TotalExecutions: 50_000,
            MeanExecutionTimeMs: 150.0,
            MaxExecutionTimeMs: 400.0,
            RowsExaminedPerExecution: 4_999, // <= 5000 threshold
            ScanType: QueryScanType.SequentialScan,
            TargetTable: "small_lookup",
            PredicateColumns: ["code"]);

        var result = engine.SynthesizeIndex(query);

        result.Should().BeNull();
    }

    [Fact]
    public void SynthesizeIndex_WithMeanTimeUnderThreshold_ReturnsNull()
    {
        var engine = new AutonomousIndexSynthesizerEngine();
        var query = new SlowQueryMetric(
            QueryId: "q-4",
            SqlText: "SELECT * FROM quick_table WHERE id = 'A1'",
            TotalExecutions: 100_000,
            MeanExecutionTimeMs: 49.9, // <= 50ms threshold
            MaxExecutionTimeMs: 65.0,
            RowsExaminedPerExecution: 25_000,
            ScanType: QueryScanType.SequentialScan,
            TargetTable: "quick_table",
            PredicateColumns: ["id"]);

        var result = engine.SynthesizeIndex(query);

        result.Should().BeNull();
    }

    [Fact]
    public void SynthesizeIndex_WithEmptyPredicateColumns_ReturnsNull()
    {
        var engine = new AutonomousIndexSynthesizerEngine();
        var query = new SlowQueryMetric(
            QueryId: "q-5",
            SqlText: "SELECT * FROM full_table_scan",
            TotalExecutions: 100,
            MeanExecutionTimeMs: 250.0,
            MaxExecutionTimeMs: 500.0,
            RowsExaminedPerExecution: 50_000,
            ScanType: QueryScanType.SequentialScan,
            TargetTable: "full_table_scan",
            PredicateColumns: []);

        var result = engine.SynthesizeIndex(query);

        result.Should().BeNull();
    }

    [Fact]
    public void SynthesizeIndex_WithEmptyTargetTable_ReturnsNull()
    {
        var engine = new AutonomousIndexSynthesizerEngine();
        var query = new SlowQueryMetric(
            QueryId: "q-6",
            SqlText: "SELECT 1",
            TotalExecutions: 100,
            MeanExecutionTimeMs: 100.0,
            MaxExecutionTimeMs: 200.0,
            RowsExaminedPerExecution: 10_000,
            ScanType: QueryScanType.SequentialScan,
            TargetTable: "",
            PredicateColumns: ["id"]);

        var result = engine.SynthesizeIndex(query);

        result.Should().BeNull();
    }

    // =========================================================================
    // 2. Index Type Selection & DDL Generation Tests
    // =========================================================================

    [Fact]
    public void SynthesizeIndex_CompositeRelationalColumns_SelectsBTreeIndex()
    {
        var engine = new AutonomousIndexSynthesizerEngine();
        var query = new SlowQueryMetric(
            QueryId: "q-btree-composite",
            SqlText: "SELECT * FROM orders WHERE tenant_id = 't-1' AND status = 'Pending'",
            TotalExecutions: 5_000,
            MeanExecutionTimeMs: 180.0,
            MaxExecutionTimeMs: 450.0,
            RowsExaminedPerExecution: 80_000,
            ScanType: QueryScanType.SequentialScan,
            TargetTable: "orders",
            PredicateColumns: ["tenant_id", "status"]);

        var rec = engine.SynthesizeIndex(query);

        rec.Should().NotBeNull();
        rec!.IndexType.Should().Be(IndexType.BTree);
        rec.TargetTable.Should().Be("orders");
        rec.Columns.Should().BeEquivalentTo(["tenant_id", "status"]);
        rec.CreateDdl.Should().Contain("CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_orders_tenant_id_status ON orders USING btree (tenant_id, status);");
        rec.IsConcurrent.Should().BeTrue();
    }

    [Fact]
    public void SynthesizeIndex_CompositeColumnsWithSorting_SelectsBTreeAndPreservesExpressions()
    {
        var engine = new AutonomousIndexSynthesizerEngine();
        var query = new SlowQueryMetric(
            QueryId: "q-btree-sort",
            SqlText: "SELECT * FROM shipments WHERE tenant_id = 't-1' ORDER BY created_at DESC",
            TotalExecutions: 8_000,
            MeanExecutionTimeMs: 120.0,
            MaxExecutionTimeMs: 300.0,
            RowsExaminedPerExecution: 60_000,
            ScanType: QueryScanType.SequentialScan,
            TargetTable: "shipments",
            PredicateColumns: ["tenant_id", "created_at DESC"]);

        var rec = engine.SynthesizeIndex(query);

        rec.Should().NotBeNull();
        rec!.IndexType.Should().Be(IndexType.BTree);
        rec.CreateDdl.Should().Contain("CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_shipments_tenant_id_created_at ON shipments USING btree (tenant_id, created_at DESC);");
    }

    [Fact]
    public void SynthesizeIndex_SingleMonotonicTimestamp_SelectsBrinIndex()
    {
        var engine = new AutonomousIndexSynthesizerEngine();
        var query = new SlowQueryMetric(
            QueryId: "q-brin-single",
            SqlText: "SELECT * FROM time_series_events WHERE created_at_utc >= '2026-01-01'",
            TotalExecutions: 3_000,
            MeanExecutionTimeMs: 250.0,
            MaxExecutionTimeMs: 800.0,
            RowsExaminedPerExecution: 120_000,
            ScanType: QueryScanType.SequentialScan,
            TargetTable: "time_series_events",
            PredicateColumns: ["created_at_utc"]);

        var rec = engine.SynthesizeIndex(query);

        rec.Should().NotBeNull();
        rec!.IndexType.Should().Be(IndexType.Brin);
        rec.CreateDdl.Should().Contain("CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_time_series_events_created_at_utc ON time_series_events USING brin (created_at_utc);");
    }

    [Fact]
    public void SynthesizeIndex_LargeLogTableTimestamp_SelectsBrinIndex()
    {
        var engine = new AutonomousIndexSynthesizerEngine();
        var query = new SlowQueryMetric(
            QueryId: "q-brin-log",
            SqlText: "SELECT * FROM audit_logs WHERE timestamp >= '2026-10-01'",
            TotalExecutions: 15_000,
            MeanExecutionTimeMs: 320.0,
            MaxExecutionTimeMs: 950.0,
            RowsExaminedPerExecution: 250_000,
            ScanType: QueryScanType.SequentialScan,
            TargetTable: "audit_logs",
            PredicateColumns: ["timestamp"]);

        var rec = engine.SynthesizeIndex(query);

        rec.Should().NotBeNull();
        rec!.IndexType.Should().Be(IndexType.Brin);
        rec.CreateDdl.Should().Contain("USING brin (timestamp);");
    }

    [Fact]
    public void SynthesizeIndex_JsonbMetadataColumn_SelectsGinIndex()
    {
        var engine = new AutonomousIndexSynthesizerEngine();
        var query = new SlowQueryMetric(
            QueryId: "q-gin-meta",
            SqlText: "SELECT * FROM documents WHERE metadata @> '{\"archived\": false}'",
            TotalExecutions: 2_500,
            MeanExecutionTimeMs: 140.0,
            MaxExecutionTimeMs: 400.0,
            RowsExaminedPerExecution: 30_000,
            ScanType: QueryScanType.SequentialScan,
            TargetTable: "documents",
            PredicateColumns: ["metadata"]);

        var rec = engine.SynthesizeIndex(query);

        rec.Should().NotBeNull();
        rec!.IndexType.Should().Be(IndexType.Gin);
        rec.CreateDdl.Should().Contain("CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_documents_metadata ON documents USING gin (metadata);");
    }

    [Fact]
    public void SynthesizeIndex_JsonbTagsColumn_SelectsGinIndex()
    {
        var engine = new AutonomousIndexSynthesizerEngine();
        var query = new SlowQueryMetric(
            QueryId: "q-gin-tags",
            SqlText: "SELECT * FROM products WHERE tags @> ARRAY['electronics']",
            TotalExecutions: 4_000,
            MeanExecutionTimeMs: 110.0,
            MaxExecutionTimeMs: 290.0,
            RowsExaminedPerExecution: 25_000,
            ScanType: QueryScanType.SequentialScan,
            TargetTable: "products",
            PredicateColumns: ["tags"]);

        var rec = engine.SynthesizeIndex(query);

        rec.Should().NotBeNull();
        rec!.IndexType.Should().Be(IndexType.Gin);
        rec.CreateDdl.Should().Contain("CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_products_tags ON products USING gin (tags);");
    }

    [Fact]
    public void SynthesizeIndex_JsonbPayloadColumn_SelectsGinIndex()
    {
        var engine = new AutonomousIndexSynthesizerEngine();
        var query = new SlowQueryMetric(
            QueryId: "q-gin-payload",
            SqlText: "SELECT * FROM webhook_events WHERE payload ->> 'event_type' = 'payment.success'",
            TotalExecutions: 12_000,
            MeanExecutionTimeMs: 160.0,
            MaxExecutionTimeMs: 500.0,
            RowsExaminedPerExecution: 45_000,
            ScanType: QueryScanType.SequentialScan,
            TargetTable: "webhook_events",
            PredicateColumns: ["payload"]);

        var rec = engine.SynthesizeIndex(query);

        rec.Should().NotBeNull();
        rec!.IndexType.Should().Be(IndexType.Gin);
        rec.CreateDdl.Should().Contain("USING gin (payload);");
    }

    [Fact]
    public void SynthesizeIndex_JsonbOperatorExpression_SelectsGinIndex()
    {
        var engine = new AutonomousIndexSynthesizerEngine();
        var query = new SlowQueryMetric(
            QueryId: "q-gin-op",
            SqlText: "SELECT * FROM accounts WHERE attributes ->> 'region' = 'us-east'",
            TotalExecutions: 6_000,
            MeanExecutionTimeMs: 95.0,
            MaxExecutionTimeMs: 220.0,
            RowsExaminedPerExecution: 18_000,
            ScanType: QueryScanType.SequentialScan,
            TargetTable: "accounts",
            PredicateColumns: ["attributes ->> 'region'"]);

        var rec = engine.SynthesizeIndex(query);

        rec.Should().NotBeNull();
        rec!.IndexType.Should().Be(IndexType.Gin);
        rec.CreateDdl.Should().Contain("USING gin");
    }

    // =========================================================================
    // 3. Cost Reduction & Bloat Metrics Tests
    // =========================================================================

    [Fact]
    public void SynthesizeIndex_CalculatesEstimatedCostReduction_WithinExpectedBounds()
    {
        var engine = new AutonomousIndexSynthesizerEngine();
        var query = new SlowQueryMetric(
            QueryId: "q-cost-bounds",
            SqlText: "SELECT * FROM inventory WHERE sku = 'ABC'",
            TotalExecutions: 1_000,
            MeanExecutionTimeMs: 55.0,
            MaxExecutionTimeMs: 90.0,
            RowsExaminedPerExecution: 5_500,
            ScanType: QueryScanType.SequentialScan,
            TargetTable: "inventory",
            PredicateColumns: ["sku"]);

        var rec = engine.SynthesizeIndex(query);

        rec.Should().NotBeNull();
        rec!.EstimatedCostReductionPct.Should().BeInRange(40.0, 95.0);
    }

    [Fact]
    public void SynthesizeIndex_VerySlowHeavyQuery_YieldsHighCostReduction()
    {
        var engine = new AutonomousIndexSynthesizerEngine();
        var query = new SlowQueryMetric(
            QueryId: "q-heavy",
            SqlText: "SELECT * FROM analytics_events WHERE tenant_id = 't-large' AND event_time > '2026-01-01'",
            TotalExecutions: 50_000,
            MeanExecutionTimeMs: 1200.0, // > 500ms
            MaxExecutionTimeMs: 3500.0,
            RowsExaminedPerExecution: 500_000, // > 100k rows
            ScanType: QueryScanType.SequentialScan,
            TargetTable: "analytics_events",
            PredicateColumns: ["tenant_id", "event_time"]);

        var rec = engine.SynthesizeIndex(query);

        rec.Should().NotBeNull();
        rec!.EstimatedCostReductionPct.Should().BeGreaterThanOrEqualTo(80.0);
    }

    [Fact]
    public void SynthesizeIndex_BrinIndex_YieldsLowBloatImpactScore()
    {
        var engine = new AutonomousIndexSynthesizerEngine();
        var query = new SlowQueryMetric(
            QueryId: "q-brin-bloat",
            SqlText: "SELECT * FROM event_store WHERE inserted_at >= '2026-01-01'",
            TotalExecutions: 2_000,
            MeanExecutionTimeMs: 100.0,
            MaxExecutionTimeMs: 250.0,
            RowsExaminedPerExecution: 80_000,
            ScanType: QueryScanType.SequentialScan,
            TargetTable: "event_store",
            PredicateColumns: ["inserted_at"]);

        var rec = engine.SynthesizeIndex(query);

        rec.Should().NotBeNull();
        rec!.BloatImpactScore.Should().Be(0.05);
    }

    [Fact]
    public void SynthesizeIndex_GinIndex_YieldsAppropriateBloatImpactScore()
    {
        var engine = new AutonomousIndexSynthesizerEngine();
        var query = new SlowQueryMetric(
            QueryId: "q-gin-bloat",
            SqlText: "SELECT * FROM user_profiles WHERE metadata @> '{\"verified\": true}'",
            TotalExecutions: 3_000,
            MeanExecutionTimeMs: 90.0,
            MaxExecutionTimeMs: 180.0,
            RowsExaminedPerExecution: 30_000,
            ScanType: QueryScanType.SequentialScan,
            TargetTable: "user_profiles",
            PredicateColumns: ["metadata"]);

        var rec = engine.SynthesizeIndex(query);

        rec.Should().NotBeNull();
        rec!.BloatImpactScore.Should().Be(0.55);
    }

    // =========================================================================
    // 4. Recommendation Management & Batch Processing Tests
    // =========================================================================

    [Fact]
    public void AnalyzeSlowQueries_ProcessesBatchAndStoresRecommendations()
    {
        var engine = new AutonomousIndexSynthesizerEngine();
        SlowQueryMetric[] queries =
        [
            new("q-b1", "SELECT 1", 10, 20.0, 30.0, 100, QueryScanType.SequentialScan, "small_table", ["id"]), // skipped
            new("q-b2", "SELECT * FROM orders WHERE tenant_id = '1'", 100, 150.0, 300.0, 20_000, QueryScanType.SequentialScan, "orders", ["tenant_id"]),
            new("q-b3", "SELECT * FROM logs WHERE timestamp > '2026-01-01'", 100, 200.0, 400.0, 50_000, QueryScanType.SequentialScan, "logs", ["timestamp"])
        ];

        var results = engine.AnalyzeSlowQueries(queries);

        results.Should().HaveCount(2);
        engine.GetActiveRecommendations().Should().HaveCount(2);
    }

    [Fact]
    public void GetActiveRecommendations_ReturnsAllSynthesizedRecommendations()
    {
        var engine = new AutonomousIndexSynthesizerEngine();
        var query = new SlowQueryMetric(
            QueryId: "q-active",
            SqlText: "SELECT * FROM subscriptions WHERE customer_id = 'c1'",
            TotalExecutions: 500,
            MeanExecutionTimeMs: 95.0,
            MaxExecutionTimeMs: 180.0,
            RowsExaminedPerExecution: 12_000,
            ScanType: QueryScanType.SequentialScan,
            TargetTable: "subscriptions",
            PredicateColumns: ["customer_id"]);

        engine.SynthesizeIndex(query);

        var active = engine.GetActiveRecommendations();
        active.Should().ContainSingle(r => r.TargetTable == "subscriptions");
    }

    [Fact]
    public void DismissRecommendation_RemovesTargetRecommendation()
    {
        var engine = new AutonomousIndexSynthesizerEngine();
        var query = new SlowQueryMetric(
            QueryId: "q-dismiss",
            SqlText: "SELECT * FROM customers WHERE email = 'test@example.com'",
            TotalExecutions: 2_000,
            MeanExecutionTimeMs: 80.0,
            MaxExecutionTimeMs: 150.0,
            RowsExaminedPerExecution: 15_000,
            ScanType: QueryScanType.SequentialScan,
            TargetTable: "customers",
            PredicateColumns: ["email"]);

        var rec = engine.SynthesizeIndex(query);
        rec.Should().NotBeNull();

        var dismissed = engine.DismissRecommendation(rec!.RecommendationId);
        dismissed.Should().BeTrue();

        engine.GetActiveRecommendations().Should().BeEmpty();
    }

    [Fact]
    public void DismissRecommendation_NonExistentId_ReturnsFalse()
    {
        var engine = new AutonomousIndexSynthesizerEngine();
        var dismissed = engine.DismissRecommendation("non-existent-rec-id");
        dismissed.Should().BeFalse();
    }

    // =========================================================================
    // 5. Table Bloat & Vacuum Urgency Tests
    // =========================================================================

    [Fact]
    public void AuditTableBloat_ZeroTuples_ReturnsZeroRatioAndOptimalUrgency()
    {
        var service = new TableBloatAndVacuumOptimizerService();

        var metric = service.AuditTableBloat("empty_table", liveTuples: 0, deadTuples: 0, totalBytes: 0);

        metric.DeadTupleRatioPct.Should().Be(0.0);
        metric.Urgency.Should().Be(VacuumUrgency.Low);
        metric.EstimatedBloatBytes.Should().Be(0);
        metric.RecommendedAction.Should().Contain("OPTIMAL");
    }

    [Fact]
    public void AuditTableBloat_LowRatio_ClassifiesAsLowUrgency()
    {
        var service = new TableBloatAndVacuumOptimizerService();

        // 10 dead tuples out of 100 total tuples = 10% (<= 15%)
        var metric = service.AuditTableBloat("healthy_table", liveTuples: 90, deadTuples: 10, totalBytes: 100_000);

        metric.DeadTupleRatioPct.Should().Be(10.0);
        metric.Urgency.Should().Be(VacuumUrgency.Low);
        metric.EstimatedBloatBytes.Should().Be(10_000);
        metric.RecommendedAction.Should().Contain("OPTIMAL");
    }

    [Fact]
    public void AuditTableBloat_MediumRatio_ClassifiesAsMediumUrgency()
    {
        var service = new TableBloatAndVacuumOptimizerService();

        // 20 dead tuples out of 100 total tuples = 20% (> 15% and <= 25%)
        var metric = service.AuditTableBloat("moderate_table", liveTuples: 80, deadTuples: 20, totalBytes: 500_000);

        metric.DeadTupleRatioPct.Should().Be(20.0);
        metric.Urgency.Should().Be(VacuumUrgency.Medium);
        metric.EstimatedBloatBytes.Should().Be(100_000);
        metric.RecommendedAction.Should().Contain("MEDIUM");
    }

    [Fact]
    public void AuditTableBloat_HighRatio_ClassifiesAsHighUrgency()
    {
        var service = new TableBloatAndVacuumOptimizerService();

        // 35 dead tuples out of 100 total tuples = 35% (> 25% and <= 40%)
        var metric = service.AuditTableBloat("degraded_table", liveTuples: 65, deadTuples: 35, totalBytes: 1_000_000);

        metric.DeadTupleRatioPct.Should().Be(35.0);
        metric.Urgency.Should().Be(VacuumUrgency.High);
        metric.EstimatedBloatBytes.Should().Be(350_000);
        metric.RecommendedAction.Should().Contain("HIGH");
    }

    [Fact]
    public void AuditTableBloat_CriticalEmergencyRatio_ClassifiesAsCriticalEmergency()
    {
        var service = new TableBloatAndVacuumOptimizerService();

        // 50 dead tuples out of 100 total tuples = 50% (> 40%)
        var metric = service.AuditTableBloat("bloated_table", liveTuples: 50, deadTuples: 50, totalBytes: 2_000_000);

        metric.DeadTupleRatioPct.Should().Be(50.0);
        metric.Urgency.Should().Be(VacuumUrgency.CriticalEmergency);
        metric.EstimatedBloatBytes.Should().Be(1_000_000);
        metric.RecommendedAction.Should().Contain("CRITICAL");
    }

    [Fact]
    public void AuditTableBloat_CriticalEmergency_AutonomouslySchedulesImmediateVacuumTask()
    {
        var service = new TableBloatAndVacuumOptimizerService();

        var metric = service.AuditTableBloat("emergency_table", liveTuples: 40_000, deadTuples: 60_000, totalBytes: 50_000_000);

        metric.Urgency.Should().Be(VacuumUrgency.CriticalEmergency);

        var pendingTasks = service.GetPendingVacuumTasks();
        pendingTasks.Should().ContainSingle(t => t.TableName == "emergency_table" && t.FullVacuum && t.ReindexConcurrently);
    }

    [Fact]
    public void ScheduleVacuumTask_ValidRequest_CreatesPendingTask()
    {
        var service = new TableBloatAndVacuumOptimizerService();
        var req = new ScheduleVacuumRequest(TableName: "custom_table", FullVacuum: false, ReindexConcurrently: true);

        var task = service.ScheduleVacuumTask(req);

        task.Should().NotBeNull();
        task.TableName.Should().Be("custom_table");
        task.Status.Should().Be(VacuumScheduleTask.StatusPending);
        task.FullVacuum.Should().BeFalse();
        task.ReindexConcurrently.Should().BeTrue();
    }

    [Fact]
    public void CompleteVacuumTask_TransitionsPendingTaskToCompleted()
    {
        var service = new TableBloatAndVacuumOptimizerService();
        var task = service.ScheduleVacuumTask(new ScheduleVacuumRequest("users_table", FullVacuum: true, ReindexConcurrently: true));

        var completed = service.CompleteVacuumTask(task.TaskId);

        completed.Should().BeTrue();
        service.GetPendingVacuumTasks().Should().NotContain(t => t.TaskId == task.TaskId);

        var allTasks = service.GetAllTasks();
        allTasks.Should().ContainSingle(t => t.TaskId == task.TaskId && t.Status == VacuumScheduleTask.StatusCompleted);
    }

    [Fact]
    public void CompleteVacuumTask_InvalidId_ReturnsFalse()
    {
        var service = new TableBloatAndVacuumOptimizerService();
        var completed = service.CompleteVacuumTask("non-existent-task");
        completed.Should().BeFalse();
    }

    [Fact]
    public void GetPendingVacuumTasks_ExcludesCompletedTasks()
    {
        var service = new TableBloatAndVacuumOptimizerService();
        var task1 = service.ScheduleVacuumTask(new ScheduleVacuumRequest("table_a", false, false));
        var task2 = service.ScheduleVacuumTask(new ScheduleVacuumRequest("table_b", false, false));

        service.CompleteVacuumTask(task1.TaskId);

        var pending = service.GetPendingVacuumTasks();
        pending.Should().HaveCount(1);
        pending[0].TaskId.Should().Be(task2.TaskId);
    }

    [Fact]
    public void AuditTableBloat_NegativeInputs_ThrowsArgumentOutOfRangeException()
    {
        var service = new TableBloatAndVacuumOptimizerService();

        var act1 = () => service.AuditTableBloat("table", liveTuples: -1, deadTuples: 10, totalBytes: 100);
        var act2 = () => service.AuditTableBloat("table", liveTuples: 10, deadTuples: -1, totalBytes: 100);
        var act3 = () => service.AuditTableBloat("table", liveTuples: 10, deadTuples: 10, totalBytes: -1);

        act1.Should().Throw<ArgumentOutOfRangeException>();
        act2.Should().Throw<ArgumentOutOfRangeException>();
        act3.Should().Throw<ArgumentOutOfRangeException>();
    }

    // =========================================================================
    // 6. Garbage Collection Health & Dynamic Memory Tuning Tests
    // =========================================================================

    [Fact]
    public void CaptureMemorySnapshot_CapturesLiveTelemetryWithNonNegativeValues()
    {
        var memoryService = new MemoryAllocationAutopilotService();

        var report = memoryService.CaptureMemorySnapshot();

        report.Should().NotBeNull();
        report.Gen0Collections.Should().BeGreaterThanOrEqualTo(0);
        report.Gen1Collections.Should().BeGreaterThanOrEqualTo(0);
        report.Gen2Collections.Should().BeGreaterThanOrEqualTo(0);
        report.TotalAllocatedBytes.Should().BeGreaterThan(0);
        report.HeapSizeBytes.Should().BeGreaterThan(0);
    }

    [Fact]
    public void EvaluateMemoryHealth_Gen2PressureUnder8Percent_ReturnsOptimalAndNoThrottle()
    {
        var memoryService = new MemoryAllocationAutopilotService();

        // gen0: 100, gen2: 5 -> pressure = 5 / 101 = 4.95% (<= 8%)
        var report = memoryService.EvaluateMemoryHealth(
            gen0: 100,
            gen1: 20,
            gen2: 5,
            totalAllocatedBytes: 50_000_000,
            heapSizeBytes: 100_000_000,
            memoryFragmentationPct: 5.0);

        report.HealthStatus.Should().Be(GcHealthStatus.Optimal);
        report.CacheBudgetThrottlePct.Should().Be(0.0);
        report.Gen2PressurePct.Should().BeLessThan(8.0);
    }

    [Fact]
    public void EvaluateMemoryHealth_Gen2PressureBetween8And15Percent_ReturnsElevatedPressureAnd25PercentThrottle()
    {
        var memoryService = new MemoryAllocationAutopilotService();

        // gen0: 100, gen2: 10 -> pressure = 10 / 101 = 9.9% (> 8% and <= 15%)
        var report = memoryService.EvaluateMemoryHealth(
            gen0: 100,
            gen1: 30,
            gen2: 10,
            totalAllocatedBytes: 150_000_000,
            heapSizeBytes: 300_000_000,
            memoryFragmentationPct: 10.0);

        report.HealthStatus.Should().Be(GcHealthStatus.ElevatedPressure);
        report.CacheBudgetThrottlePct.Should().Be(25.0);

        var redisBudget = memoryService.GetCurrentCacheBudgetMb("L2-Redis");
        redisBudget.Should().Be(768.0); // 1024 - 25% = 768.0 MB
    }

    [Fact]
    public void EvaluateMemoryHealth_Gen2PressureOver15Percent_ReturnsCriticalThrottlingAnd50PercentThrottle()
    {
        var memoryService = new MemoryAllocationAutopilotService();

        // gen0: 100, gen2: 20 -> pressure = 20 / 101 = 19.8% (> 15%)
        var report = memoryService.EvaluateMemoryHealth(
            gen0: 100,
            gen1: 50,
            gen2: 20,
            totalAllocatedBytes: 500_000_000,
            heapSizeBytes: 800_000_000,
            memoryFragmentationPct: 15.0);

        report.HealthStatus.Should().Be(GcHealthStatus.CriticalThrottling);
        report.CacheBudgetThrottlePct.Should().Be(50.0);

        var redisBudget = memoryService.GetCurrentCacheBudgetMb("L2-Redis");
        redisBudget.Should().Be(512.0); // 1024 - 50% = 512.0 MB

        var tuningHistory = memoryService.GetTuningHistory();
        tuningHistory.Should().NotBeEmpty();
        tuningHistory.Should().Contain(a => a.TargetCache == "L2-Redis" && a.ReductionPct == 50.0);
    }

    [Fact]
    public void EvaluateMemoryHealth_HighFragmentationOver25Percent_ReturnsCriticalThrottlingAnd50PercentThrottle()
    {
        var memoryService = new MemoryAllocationAutopilotService();

        // Low Gen 2 pressure (1%), but Fragmentation = 32% (> 25%)
        var report = memoryService.EvaluateMemoryHealth(
            gen0: 100,
            gen1: 10,
            gen2: 1,
            totalAllocatedBytes: 200_000_000,
            heapSizeBytes: 600_000_000,
            memoryFragmentationPct: 32.0);

        report.HealthStatus.Should().Be(GcHealthStatus.CriticalThrottling);
        report.CacheBudgetThrottlePct.Should().Be(50.0);

        var hybridBudget = memoryService.GetCurrentCacheBudgetMb("HybridCache-Memory");
        hybridBudget.Should().Be(256.0); // 512 - 50% = 256.0 MB
    }

    [Fact]
    public void EvaluateMemoryHealth_TransitionFromCriticalToOptimal_RestoresFull100PercentBudget()
    {
        var memoryService = new MemoryAllocationAutopilotService();

        // Step 1: Engage Critical Throttling (50% cut)
        memoryService.EvaluateMemoryHealth(
            gen0: 100, gen1: 50, gen2: 25,
            totalAllocatedBytes: 800_000_000, heapSizeBytes: 1_000_000_000, memoryFragmentationPct: 20.0);

        memoryService.GetCurrentCacheBudgetMb("L2-Redis").Should().Be(512.0);

        // Step 2: GC cleans up and pressure normalizes (Gen 2 ratio drops to 2%)
        var optimalReport = memoryService.EvaluateMemoryHealth(
            gen0: 1000, gen1: 100, gen2: 20,
            totalAllocatedBytes: 300_000_000, heapSizeBytes: 500_000_000, memoryFragmentationPct: 8.0);

        optimalReport.HealthStatus.Should().Be(GcHealthStatus.Optimal);
        optimalReport.CacheBudgetThrottlePct.Should().Be(0.0);

        // Verify full 100% budget restored
        memoryService.GetCurrentCacheBudgetMb("L2-Redis").Should().Be(1024.0);
        memoryService.GetCurrentCacheBudgetMb("HybridCache-Memory").Should().Be(512.0);

        var history = memoryService.GetTuningHistory();
        history.Should().Contain(a => a.TargetCache == "L2-Redis" && a.ReductionPct == 0.0);
    }

    [Fact]
    public void ApplyCacheBudgetTuning_ExplicitCall_ReducesTargetCacheBudget()
    {
        var memoryService = new MemoryAllocationAutopilotService();
        memoryService.SetBaseCacheBudgetMb("CustomSearchCache", 800.0);

        var action = memoryService.ApplyCacheBudgetTuning("CustomSearchCache", 30, "Custom load-shedding");

        action.Should().NotBeNull();
        action.TargetCache.Should().Be("CustomSearchCache");
        action.PreviousBudgetMb.Should().Be(800.0);
        action.NewBudgetMb.Should().Be(560.0); // 800 - 30% = 560
        action.ReductionPct.Should().Be(30.0);
        action.Reason.Should().Be("Custom load-shedding");

        memoryService.GetCurrentCacheBudgetMb("CustomSearchCache").Should().Be(560.0);
    }

    // =========================================================================
    // 7. High-Concurrency & Thread-Safety Tests
    // =========================================================================

    [Fact]
    public void ConcurrentAccess_IndexSynthesizer_ThreadSafeUnderHighConcurrency()
    {
        var engine = new AutonomousIndexSynthesizerEngine();
        const int threadCount = 30;
        var tasks = new Task[threadCount];

        for (int i = 0; i < threadCount; i++)
        {
            var idx = i;
            tasks[i] = Task.Run(() =>
            {
                var query = new SlowQueryMetric(
                    QueryId: $"q-concurrent-{idx}",
                    SqlText: $"SELECT * FROM concurrent_tbl_{idx % 5} WHERE tenant_id = 't-{idx}'",
                    TotalExecutions: 5_000 + idx,
                    MeanExecutionTimeMs: 100.0 + idx,
                    MaxExecutionTimeMs: 200.0 + idx,
                    RowsExaminedPerExecution: 15_000 + idx,
                    ScanType: QueryScanType.SequentialScan,
                    TargetTable: $"concurrent_tbl_{idx % 5}",
                    PredicateColumns: ["tenant_id", $"col_{idx % 3}"]);

                engine.SynthesizeIndex(query);
                engine.GetActiveRecommendations();
            });
        }

        var act = () => Task.WaitAll(tasks);
        act.Should().NotThrow();

        engine.GetActiveRecommendations().Should().NotBeEmpty();
    }

    [Fact]
    public void ConcurrentAccess_VacuumOptimizer_ThreadSafeUnderHighConcurrency()
    {
        var service = new TableBloatAndVacuumOptimizerService();
        const int threadCount = 30;
        var tasks = new Task[threadCount];

        for (int i = 0; i < threadCount; i++)
        {
            var idx = i;
            tasks[i] = Task.Run(() =>
            {
                if (idx % 2 == 0)
                {
                    service.AuditTableBloat($"table_{idx}", 50, 50, 1_000_000); // 50% -> auto-schedules
                }
                else
                {
                    var task = service.ScheduleVacuumTask(new ScheduleVacuumRequest($"tbl_manual_{idx}", false, false));
                    service.CompleteVacuumTask(task.TaskId);
                }
                service.GetPendingVacuumTasks();
            });
        }

        var act = () => Task.WaitAll(tasks);
        act.Should().NotThrow();

        service.GetAllTasks().Should().NotBeEmpty();
    }

    [Fact]
    public void ConcurrentAccess_MemoryAutopilot_ThreadSafeUnderHighConcurrency()
    {
        var service = new MemoryAllocationAutopilotService();
        const int threadCount = 30;
        var tasks = new Task[threadCount];

        for (int i = 0; i < threadCount; i++)
        {
            var idx = i;
            tasks[i] = Task.Run(() =>
            {
                if (idx % 3 == 0)
                {
                    service.EvaluateMemoryHealth(100, 20, 20, 100_000, 200_000, 10.0); // Critical
                }
                else if (idx % 3 == 1)
                {
                    service.EvaluateMemoryHealth(100, 20, 10, 100_000, 200_000, 10.0); // Elevated
                }
                else
                {
                    service.EvaluateMemoryHealth(1000, 20, 10, 100_000, 200_000, 5.0); // Optimal
                }

                service.GetTuningHistory();
                service.GetCurrentCacheBudgetMb("L2-Redis");
            });
        }

        var act = () => Task.WaitAll(tasks);
        act.Should().NotThrow();

        service.GetTuningHistory().Should().NotBeEmpty();
    }

    // =========================================================================
    // 8. ServiceCollection Registration Tests (NO BuildServiceProvider)
    // =========================================================================

    [Fact]
    public void ServiceCollectionRegistration_RegistersAllRequiredServicesWithoutCallingBuildServiceProvider()
    {
        // Rule: In tests, NEVER call 'services.BuildServiceProvider()'.
        // Instantiate services directly or assert on ServiceCollection descriptors.
        var services = new ServiceCollection();

        services.AddAutopilotServices();

        services.Should().Contain(d =>
            d.ServiceType == typeof(IAutonomousIndexSynthesizerEngine) &&
            d.ImplementationType == typeof(AutonomousIndexSynthesizerEngine) &&
            d.Lifetime == ServiceLifetime.Singleton);

        services.Should().Contain(d =>
            d.ServiceType == typeof(ITableBloatAndVacuumOptimizerService) &&
            d.ImplementationType == typeof(TableBloatAndVacuumOptimizerService) &&
            d.Lifetime == ServiceLifetime.Singleton);

        services.Should().Contain(d =>
            d.ServiceType == typeof(IMemoryAllocationAutopilotService) &&
            d.ImplementationType == typeof(MemoryAllocationAutopilotService) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }
}
