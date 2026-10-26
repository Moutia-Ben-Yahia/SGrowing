namespace SGrowing.Core.Entities;

public class Company
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string? Industry { get; set; }
    public int EmployeeCount { get; set; }
    public decimal AnnualRevenue { get; set; }
    public Guid? ParentCompanyId { get; set; }
    public string CustomFieldsJson { get; set; } = "{}";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public Company() { }

    public static Company Create(
        Guid tenantId,
        string name,
        string domain,
        string? industry = null,
        decimal annualRevenue = 0,
        Guid? parentCompanyId = null)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId cannot be empty.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name cannot be empty.", nameof(name));

        return new Company
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name.Trim(),
            Domain = (domain ?? string.Empty).Trim().ToLowerInvariant(),
            Industry = industry?.Trim(),
            AnnualRevenue = annualRevenue,
            ParentCompanyId = parentCompanyId,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}

public class WorkflowGraph
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string TriggerType { get; set; } = "Event";
    public string NodesJson { get; set; } = "[]";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public WorkflowGraph() { }

    public static WorkflowGraph Create(
        Guid tenantId,
        string name,
        string triggerType,
        string nodesJson)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId cannot be empty.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name cannot be empty.", nameof(name));

        return new WorkflowGraph
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name.Trim(),
            TriggerType = string.IsNullOrWhiteSpace(triggerType) ? "Event" : triggerType.Trim(),
            NodesJson = string.IsNullOrWhiteSpace(nodesJson) ? "[]" : nodesJson.Trim(),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}

public class WorkflowExecution
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid WorkflowGraphId { get; set; }
    public string Status { get; set; } = "Running";
    public int ExecutedNodesCount { get; set; }
    public string ExecutionLog { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public WorkflowExecution() { }

    public static WorkflowExecution Create(
        Guid tenantId,
        Guid workflowGraphId,
        string status = "Running",
        int executedNodesCount = 0,
        string executionLog = "")
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId cannot be empty.", nameof(tenantId));
        if (workflowGraphId == Guid.Empty) throw new ArgumentException("WorkflowGraphId cannot be empty.", nameof(workflowGraphId));

        return new WorkflowExecution
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WorkflowGraphId = workflowGraphId,
            Status = status,
            ExecutedNodesCount = executedNodesCount,
            ExecutionLog = executionLog,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    public void AppendLog(string entry)
    {
        if (string.IsNullOrEmpty(ExecutionLog))
        {
            ExecutionLog = entry;
        }
        else
        {
            ExecutionLog += "\n" + entry;
        }
    }

    public void MarkCompleted(int executedCount, string? additionalLog = null)
    {
        Status = "Completed";
        ExecutedNodesCount = executedCount;
        if (!string.IsNullOrEmpty(additionalLog))
        {
            AppendLog(additionalLog);
        }
    }

    public void MarkFailed(int executedCount, string errorLog)
    {
        Status = "Failed";
        ExecutedNodesCount = executedCount;
        if (!string.IsNullOrEmpty(errorLog))
        {
            AppendLog(errorLog);
        }
    }
}
