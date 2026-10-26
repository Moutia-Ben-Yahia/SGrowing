namespace SGrowing.Core.Entities;

public class QuietHoursPolicy
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public bool IsEnabled { get; set; } = true;
    public int StartHourLocal { get; set; } = 21; // 9:00 PM
    public int EndHourLocal { get; set; } = 8;    // 8:00 AM
    public bool AllowBypassForUrgent { get; set; } = true;

    public static QuietHoursPolicy CreateDefault(Guid tenantId)
    {
        return new QuietHoursPolicy
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            IsEnabled = true,
            StartHourLocal = 21,
            EndHourLocal = 8,
            AllowBypassForUrgent = true
        };
    }

    public bool IsInQuietHours(DateTime localTime)
    {
        if (!IsEnabled) return false;
        int hour = localTime.Hour;

        if (StartHourLocal > EndHourLocal)
        {
            // Overnight quiet hours, e.g. 21 to 8: true if hour >= 21 OR hour < 8
            return hour >= StartHourLocal || hour < EndHourLocal;
        }
        else
        {
            return hour >= StartHourLocal && hour < EndHourLocal;
        }
    }
}

public class WorkflowDeadLetterLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid WorkflowId { get; set; }
    public Guid ExecutionId { get; set; }
    public string FailedNodeId { get; set; } = string.Empty;
    public string FailedNodeType { get; set; } = string.Empty;
    public string InputPayload { get; set; } = "{}";
    public string ErrorMessage { get; set; } = string.Empty;
    public int RetryCount { get; set; } = 0;
    public bool IsResolved { get; set; } = false;
    public DateTime FailedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAtUtc { get; set; }

    public static WorkflowDeadLetterLog Create(
        Guid tenantId,
        Guid workflowId,
        Guid executionId,
        string failedNodeId,
        string failedNodeType,
        string inputPayload,
        string errorMessage)
    {
        return new WorkflowDeadLetterLog
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WorkflowId = workflowId,
            ExecutionId = executionId,
            FailedNodeId = failedNodeId,
            FailedNodeType = failedNodeType,
            InputPayload = inputPayload,
            ErrorMessage = errorMessage,
            RetryCount = 0,
            IsResolved = false,
            FailedAtUtc = DateTime.UtcNow
        };
    }

    public void MarkResolved()
    {
        IsResolved = true;
        ResolvedAtUtc = DateTime.UtcNow;
    }
}
