using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using SGrowing.Api.Hubs;

namespace SGrowing.Api.Modules.Automation;

public class WorkflowApprovalTask
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid ExecutionId { get; set; }
    public string NodeId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal? FinancialRiskAmount { get; set; }
    public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected
    public string? ApproverEmail { get; set; }
    public string? DecisionNotes { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? DecidedAtUtc { get; set; }
}

public record WorkflowApprovalResult(
    bool Success,
    WorkflowApprovalTask Task,
    WorkflowExecutionInstance? Execution,
    string Message
);

public interface IWorkflowApprovalTaskStore
{
    Task SaveTaskAsync(WorkflowApprovalTask task, CancellationToken ct = default);
    Task<WorkflowApprovalTask?> GetTaskAsync(Guid taskId, CancellationToken ct = default);
    Task<IReadOnlyList<WorkflowApprovalTask>> GetTasksByTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<WorkflowApprovalTask>> GetTasksByExecutionIdAsync(Guid executionId, CancellationToken ct = default);
}

public class InMemoryWorkflowApprovalTaskStore : IWorkflowApprovalTaskStore
{
    private readonly ConcurrentDictionary<Guid, WorkflowApprovalTask> _tasks = new();

    public Task SaveTaskAsync(WorkflowApprovalTask task, CancellationToken ct = default)
    {
        _tasks[task.Id] = task;
        return Task.CompletedTask;
    }

    public Task<WorkflowApprovalTask?> GetTaskAsync(Guid taskId, CancellationToken ct = default)
    {
        _tasks.TryGetValue(taskId, out var task);
        return Task.FromResult(task);
    }

    public Task<IReadOnlyList<WorkflowApprovalTask>> GetTasksByTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        var list = _tasks.Values
            .Where(t => tenantId == Guid.Empty || t.TenantId == tenantId)
            .OrderByDescending(t => t.CreatedAtUtc)
            .ToList();

        return Task.FromResult<IReadOnlyList<WorkflowApprovalTask>>(list);
    }

    public Task<IReadOnlyList<WorkflowApprovalTask>> GetTasksByExecutionIdAsync(Guid executionId, CancellationToken ct = default)
    {
        var list = _tasks.Values
            .Where(t => t.ExecutionId == executionId)
            .OrderBy(t => t.CreatedAtUtc)
            .ToList();

        return Task.FromResult<IReadOnlyList<WorkflowApprovalTask>>(list);
    }
}

public interface IHumanInTheLoopApprovalService
{
    Task<WorkflowApprovalTask> CreateApprovalTaskAsync(
        Guid tenantId,
        Guid executionId,
        string nodeId,
        string description,
        decimal? financialRiskAmount = null,
        CancellationToken ct = default);

    Task<WorkflowApprovalResult> ApproveTaskAsync(
        Guid tenantId,
        Guid taskId,
        string approverEmail,
        string? notes = null,
        CancellationToken ct = default);

    Task<WorkflowApprovalResult> RejectTaskAsync(
        Guid tenantId,
        Guid taskId,
        string approverEmail,
        string reason,
        CancellationToken ct = default);

    Task<WorkflowApprovalTask?> GetTaskByIdAsync(
        Guid tenantId,
        Guid taskId,
        CancellationToken ct = default);

    Task<IReadOnlyList<WorkflowApprovalTask>> GetPendingTasksAsync(
        Guid tenantId,
        CancellationToken ct = default);

    Task<IReadOnlyList<WorkflowApprovalTask>> GetTasksByExecutionIdAsync(
        Guid tenantId,
        Guid executionId,
        CancellationToken ct = default);
}

public class HumanInTheLoopApprovalService : IHumanInTheLoopApprovalService
{
    private readonly IDurableWorkflowExecutionEngine _engine;
    private readonly IWorkflowApprovalTaskStore _taskStore;
    private readonly IHubContext<OmniHub, IOmniClient>? _hubContext;
    private readonly ILogger<HumanInTheLoopApprovalService>? _logger;

    public HumanInTheLoopApprovalService(
        IDurableWorkflowExecutionEngine engine,
        IWorkflowApprovalTaskStore taskStore,
        IHubContext<OmniHub, IOmniClient>? hubContext = null,
        ILogger<HumanInTheLoopApprovalService>? logger = null)
    {
        _engine = engine;
        _taskStore = taskStore;
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task<WorkflowApprovalTask> CreateApprovalTaskAsync(
        Guid tenantId,
        Guid executionId,
        string nodeId,
        string description,
        decimal? financialRiskAmount = null,
        CancellationToken ct = default)
    {
        var task = new WorkflowApprovalTask
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ExecutionId = executionId,
            NodeId = nodeId,
            Description = description,
            FinancialRiskAmount = financialRiskAmount,
            Status = "Pending",
            CreatedAtUtc = DateTime.UtcNow
        };

        await _taskStore.SaveTaskAsync(task, ct);

        // Broadcast to SignalR OmniHub tenant group
        if (_hubContext != null)
        {
            try
            {
                var dto = new WorkflowApprovalTaskDto(
                    task.Id,
                    task.TenantId,
                    task.ExecutionId,
                    task.NodeId,
                    task.Description,
                    task.FinancialRiskAmount,
                    task.Status,
                    task.CreatedAtUtc
                );

                await _hubContext.Clients.Group(OmniHub.GetTenantGroup(tenantId)).OnApprovalTaskCreated(dto);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to broadcast OnApprovalTaskCreated event to OmniHub for task {TaskId}", task.Id);
            }
        }

        return task;
    }

    public async Task<WorkflowApprovalResult> ApproveTaskAsync(
        Guid tenantId,
        Guid taskId,
        string approverEmail,
        string? notes = null,
        CancellationToken ct = default)
    {
        var task = await _taskStore.GetTaskAsync(taskId, ct)
            ?? throw new KeyNotFoundException($"Approval task '{taskId}' not found.");

        if (task.TenantId != tenantId && tenantId != Guid.Empty)
        {
            throw new UnauthorizedAccessException($"Tenant mismatch for approval task '{taskId}'.");
        }

        if (task.Status != "Pending")
        {
            throw new InvalidOperationException($"Approval task '{taskId}' is already '{task.Status}'.");
        }

        task.Status = "Approved";
        task.ApproverEmail = approverEmail;
        task.DecisionNotes = notes;
        task.DecidedAtUtc = DateTime.UtcNow;

        await _taskStore.SaveTaskAsync(task, ct);

        // Releases workflow suspension, and resumes execution
        var execution = await _engine.ResumeExecutionAsync(tenantId, task.ExecutionId, isApproved: true, approverEmail, notes, ct);

        return new WorkflowApprovalResult(
            Success: true,
            Task: task,
            Execution: execution,
            Message: $"Task approved by {approverEmail}. Workflow execution resumed."
        );
    }

    public async Task<WorkflowApprovalResult> RejectTaskAsync(
        Guid tenantId,
        Guid taskId,
        string approverEmail,
        string reason,
        CancellationToken ct = default)
    {
        var task = await _taskStore.GetTaskAsync(taskId, ct)
            ?? throw new KeyNotFoundException($"Approval task '{taskId}' not found.");

        if (task.TenantId != tenantId && tenantId != Guid.Empty)
        {
            throw new UnauthorizedAccessException($"Tenant mismatch for approval task '{taskId}'.");
        }

        if (task.Status != "Pending")
        {
            throw new InvalidOperationException($"Approval task '{taskId}' is already '{task.Status}'.");
        }

        task.Status = "Rejected";
        task.ApproverEmail = approverEmail;
        task.DecisionNotes = reason;
        task.DecidedAtUtc = DateTime.UtcNow;

        await _taskStore.SaveTaskAsync(task, ct);

        // Transitions workflow to Terminated
        var execution = await _engine.TerminateExecutionAsync(tenantId, task.ExecutionId, reason, ct);

        return new WorkflowApprovalResult(
            Success: true,
            Task: task,
            Execution: execution,
            Message: $"Task rejected by {approverEmail}. Workflow execution terminated."
        );
    }

    public async Task<WorkflowApprovalTask?> GetTaskByIdAsync(
        Guid tenantId,
        Guid taskId,
        CancellationToken ct = default)
    {
        var task = await _taskStore.GetTaskAsync(taskId, ct);
        if (task != null && task.TenantId != tenantId && tenantId != Guid.Empty)
        {
            return null;
        }
        return task;
    }

    public async Task<IReadOnlyList<WorkflowApprovalTask>> GetPendingTasksAsync(
        Guid tenantId,
        CancellationToken ct = default)
    {
        var all = await _taskStore.GetTasksByTenantAsync(tenantId, ct);
        return all.Where(t => t.Status == "Pending").ToList();
    }

    public async Task<IReadOnlyList<WorkflowApprovalTask>> GetTasksByExecutionIdAsync(
        Guid tenantId,
        Guid executionId,
        CancellationToken ct = default)
    {
        var all = await _taskStore.GetTasksByExecutionIdAsync(executionId, ct);
        return all.Where(t => tenantId == Guid.Empty || t.TenantId == tenantId).ToList();
    }
}
