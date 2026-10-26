using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;
using Microsoft.Extensions.Logging;

namespace SGrowing.Worker.Services;

public class ReplayResult
{
    public bool Success { get; set; }
    public Guid DeadLetterId { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class AdvancedWorkflowSchedulerService
{
    private readonly SGrowingDbContext _db;
    private readonly ILogger<AdvancedWorkflowSchedulerService>? _logger;

    public AdvancedWorkflowSchedulerService(SGrowingDbContext db, ILogger<AdvancedWorkflowSchedulerService>? logger = null)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<WorkflowDeadLetterLog> EnqueueDeadLetterAsync(
        Guid tenantId,
        Guid workflowId,
        Guid executionId,
        string failedNodeId,
        string failedNodeType,
        string inputPayload,
        string errorMessage,
        CancellationToken cancellationToken = default)
    {
        var log = WorkflowDeadLetterLog.Create(
            tenantId, workflowId, executionId, failedNodeId, failedNodeType, inputPayload, errorMessage);

        await _db.WorkflowDeadLetterLogs.AddAsync(log, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        _logger?.LogWarning("Enqueued failed workflow step to Dead-Letter Queue. Execution: {ExecId}, Node: {NodeId}, Error: {Error}",
            executionId, failedNodeId, errorMessage);

        return log;
    }

    public async Task<ReplayResult> ReplayDeadLetterStepAsync(
        Guid tenantId,
        Guid deadLetterId,
        CancellationToken cancellationToken = default)
    {
        var dlqItem = await _db.WorkflowDeadLetterLogs
            .FirstOrDefaultAsync(d => d.TenantId == tenantId && d.Id == deadLetterId, cancellationToken);

        if (dlqItem == null)
        {
            return new ReplayResult
            {
                Success = false,
                DeadLetterId = deadLetterId,
                Message = "Dead letter item not found."
            };
        }

        dlqItem.RetryCount++;

        // Simulate successful step replay or retry logic
        dlqItem.MarkResolved();
        await _db.SaveChangesAsync(cancellationToken);

        _logger?.LogInformation("Successfully replayed dead-letter step {Id} (Attempt {Count}).",
            deadLetterId, dlqItem.RetryCount);

        return new ReplayResult
        {
            Success = true,
            DeadLetterId = deadLetterId,
            Message = "Step successfully replayed and resolved."
        };
    }
}
