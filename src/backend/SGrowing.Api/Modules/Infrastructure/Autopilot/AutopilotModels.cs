namespace SGrowing.Api.Modules.Infrastructure.Autopilot;

/// <summary>
/// Supported relational index types in PostgreSQL.
/// </summary>
public enum IndexType
{
    BTree,
    Brin,
    Gin,
    Hash
}

/// <summary>
/// Scan strategy detected in query execution plans.
/// </summary>
public enum QueryScanType
{
    SequentialScan,
    IndexScan,
    BitmapHeapScan
}

/// <summary>
/// Urgency classification for MVCC dead tuple vacuuming and reindexing.
/// </summary>
public enum VacuumUrgency
{
    Low,
    Medium,
    High,
    CriticalEmergency
}

/// <summary>
/// Health status of the .NET Garbage Collector and managed heap.
/// </summary>
public enum GcHealthStatus
{
    Optimal,
    ElevatedPressure,
    CriticalThrottling
}

/// <summary>
/// Query performance telemetry captured from database execution statistics.
/// </summary>
public record SlowQueryMetric(
    string QueryId,
    string SqlText,
    long TotalExecutions,
    double MeanExecutionTimeMs,
    double MaxExecutionTimeMs,
    long RowsExaminedPerExecution,
    QueryScanType ScanType,
    string TargetTable,
    IReadOnlyList<string> PredicateColumns);

/// <summary>
/// Autonomous database index recommendation synthesized from query execution metrics.
/// </summary>
public record IndexRecommendation(
    string RecommendationId,
    string TargetTable,
    IReadOnlyList<string> Columns,
    IndexType IndexType,
    bool IsConcurrent,
    string CreateDdl,
    double EstimatedCostReductionPct,
    double BloatImpactScore,
    string Justification);

/// <summary>
/// PostgreSQL table bloat metric assessing dead tuple accumulation.
/// </summary>
public record TableBloatMetric(
    string TableName,
    long TotalBytes,
    long DeadTuplesCount,
    long LiveTuplesCount,
    double DeadTupleRatioPct,
    long EstimatedBloatBytes,
    VacuumUrgency Urgency,
    string RecommendedAction);

/// <summary>
/// Scheduled non-blocking vacuum or reindex task.
/// </summary>
public record VacuumScheduleTask(
    string TaskId,
    string TableName,
    bool FullVacuum,
    bool ReindexConcurrently,
    DateTimeOffset ScheduledAtUtc,
    string Status)
{
    public const string StatusPending = "Pending";
    public const string StatusCompleted = "Completed";
}

/// <summary>
/// Real-time .NET Garbage Collector health report.
/// </summary>
public record GcMemoryHealthReport(
    int Gen0Collections,
    int Gen1Collections,
    int Gen2Collections,
    long TotalAllocatedBytes,
    long HeapSizeBytes,
    double MemoryFragmentationPct,
    double Gen2PressurePct,
    double CacheBudgetThrottlePct,
    GcHealthStatus HealthStatus);

/// <summary>
/// Audit trail entry of autonomous cache budget tuning actions.
/// </summary>
public record MemoryTuningAction(
    string ActionId,
    string TargetCache,
    double PreviousBudgetMb,
    double NewBudgetMb,
    double ReductionPct,
    string Reason,
    DateTimeOffset TimestampUtc);

// API DTOs

/// <summary>
/// Request to analyze a batch of slow query execution metrics.
/// </summary>
public record AnalyzeQueriesRequest(IReadOnlyList<SlowQueryMetric> Queries);

/// <summary>
/// Request to synthesize an index for a specific slow query.
/// </summary>
public record SynthesizeIndexRequest(SlowQueryMetric Query);

/// <summary>
/// Request to audit table bloat and evaluate vacuum urgency.
/// </summary>
public record AuditBloatRequest(string TableName, long LiveTuples, long DeadTuples, long TotalBytes);

/// <summary>
/// Request to schedule a vacuum or concurrent reindex operation.
/// </summary>
public record ScheduleVacuumRequest(string TableName, bool FullVacuum, bool ReindexConcurrently);

/// <summary>
/// Request to manually tune or throttle a target cache budget.
/// </summary>
public record TuneMemoryRequest(string TargetCache, int TargetReductionPct);
