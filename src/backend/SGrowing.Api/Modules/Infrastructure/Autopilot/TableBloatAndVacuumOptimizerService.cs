namespace SGrowing.Api.Modules.Infrastructure.Autopilot;

/// <summary>
/// Service interface for PostgreSQL MVCC table bloat analysis and automated vacuum scheduling.
/// </summary>
public interface ITableBloatAndVacuumOptimizerService
{
    /// <summary>
    /// Audits dead-tuple accumulation for a table and computes bloat urgency.
    /// In CriticalEmergency conditions (>40% dead tuples), autonomously schedules an immediate vacuum task.
    /// </summary>
    TableBloatMetric AuditTableBloat(string tableName, long liveTuples, long deadTuples, long totalBytes);

    /// <summary>
    /// Schedules a vacuum or concurrent reindex operation.
    /// </summary>
    VacuumScheduleTask ScheduleVacuumTask(ScheduleVacuumRequest request);

    /// <summary>
    /// Retrieves all pending vacuum tasks.
    /// </summary>
    IReadOnlyList<VacuumScheduleTask> GetPendingVacuumTasks();

    /// <summary>
    /// Marks a vacuum task as completed.
    /// </summary>
    bool CompleteVacuumTask(string taskId);

    /// <summary>
    /// Retrieves all recorded vacuum tasks across all statuses.
    /// </summary>
    IReadOnlyList<VacuumScheduleTask> GetAllTasks();

    /// <summary>
    /// Clears all scheduled vacuum tasks (useful for testing and reset cycles).
    /// </summary>
    void ClearTasks();
}

/// <summary>
/// Production-grade implementation of <see cref="ITableBloatAndVacuumOptimizerService"/>.
/// Thread-safe bloat evaluator and vacuum scheduler utilizing <see cref="System.Threading.Lock"/>.
/// </summary>
public sealed class TableBloatAndVacuumOptimizerService : ITableBloatAndVacuumOptimizerService
{
    private readonly System.Threading.Lock _lock = new();
    private readonly Dictionary<string, VacuumScheduleTask> _tasks = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public TableBloatMetric AuditTableBloat(string tableName, long liveTuples, long deadTuples, long totalBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);

        if (liveTuples < 0) throw new ArgumentOutOfRangeException(nameof(liveTuples), "Live tuples cannot be negative.");
        if (deadTuples < 0) throw new ArgumentOutOfRangeException(nameof(deadTuples), "Dead tuples cannot be negative.");
        if (totalBytes < 0) throw new ArgumentOutOfRangeException(nameof(totalBytes), "Total bytes cannot be negative.");

        var totalTuples = liveTuples + deadTuples;
        var deadTupleRatio = totalTuples > 0 ? (double)deadTuples / totalTuples : 0.0;
        var deadTupleRatioPct = Math.Round(deadTupleRatio * 100.0, 2);
        var estimatedBloatBytes = (long)(totalBytes * deadTupleRatio);

        // Urgency classification:
        // > 40%: CriticalEmergency (Generates immediate vacuum schedule)
        // > 25%: High
        // > 15%: Medium
        // <= 15%: Low
        var urgency = deadTupleRatioPct switch
        {
            > 40.0 => VacuumUrgency.CriticalEmergency,
            > 25.0 => VacuumUrgency.High,
            > 15.0 => VacuumUrgency.Medium,
            _ => VacuumUrgency.Low
        };

        var action = urgency switch
        {
            VacuumUrgency.CriticalEmergency =>
                "CRITICAL: Dead tuple ratio exceeds 40%. Immediate VACUUM (FULL, ANALYZE) and REINDEX TABLE CONCURRENTLY scheduled to prevent write stall and transaction ID wraparound.",
            VacuumUrgency.High =>
                "HIGH: Dead tuple ratio exceeds 25%. Run VACUUM (ANALYZE) and schedule REINDEX TABLE CONCURRENTLY to prevent query degradation.",
            VacuumUrgency.Medium =>
                "MEDIUM: Dead tuple ratio exceeds 15%. Schedule routine VACUUM (ANALYZE) during upcoming maintenance window.",
            _ =>
                "OPTIMAL: Dead tuple ratio is healthy (<= 15%). Table operating within standard PostgreSQL MVCC thresholds."
        };

        // Autonomously schedule vacuum task if CriticalEmergency
        if (urgency == VacuumUrgency.CriticalEmergency)
        {
            ScheduleVacuumTaskInternal(new ScheduleVacuumRequest(
                TableName: tableName,
                FullVacuum: true,
                ReindexConcurrently: true));
        }

        return new TableBloatMetric(
            TableName: tableName,
            TotalBytes: totalBytes,
            DeadTuplesCount: deadTuples,
            LiveTuplesCount: liveTuples,
            DeadTupleRatioPct: deadTupleRatioPct,
            EstimatedBloatBytes: estimatedBloatBytes,
            Urgency: urgency,
            RecommendedAction: action);
    }

    /// <inheritdoc />
    public VacuumScheduleTask ScheduleVacuumTask(ScheduleVacuumRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TableName);

        return ScheduleVacuumTaskInternal(request);
    }

    /// <inheritdoc />
    public IReadOnlyList<VacuumScheduleTask> GetPendingVacuumTasks()
    {
        lock (_lock)
        {
            return [.. _tasks.Values.Where(t => t.Status == VacuumScheduleTask.StatusPending)];
        }
    }

    /// <inheritdoc />
    public bool CompleteVacuumTask(string taskId)
    {
        if (string.IsNullOrWhiteSpace(taskId))
        {
            return false;
        }

        lock (_lock)
        {
            if (_tasks.TryGetValue(taskId, out var task))
            {
                _tasks[taskId] = task with { Status = VacuumScheduleTask.StatusCompleted };
                return true;
            }

            return false;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<VacuumScheduleTask> GetAllTasks()
    {
        lock (_lock)
        {
            return [.. _tasks.Values];
        }
    }

    /// <inheritdoc />
    public void ClearTasks()
    {
        lock (_lock)
        {
            _tasks.Clear();
        }
    }

    private VacuumScheduleTask ScheduleVacuumTaskInternal(ScheduleVacuumRequest request)
    {
        var taskId = $"vac-{Guid.NewGuid():N}";
        var task = new VacuumScheduleTask(
            TaskId: taskId,
            TableName: request.TableName,
            FullVacuum: request.FullVacuum,
            ReindexConcurrently: request.ReindexConcurrently,
            ScheduledAtUtc: DateTimeOffset.UtcNow,
            Status: VacuumScheduleTask.StatusPending);

        lock (_lock)
        {
            _tasks[taskId] = task;
        }

        return task;
    }
}
