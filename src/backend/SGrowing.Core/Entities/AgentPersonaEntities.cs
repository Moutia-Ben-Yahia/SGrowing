namespace SGrowing.Core.Entities;

public class AgentPersonaRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string RoleKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Layer { get; set; } = "Business"; // Business, Engineering
    public string PrimaryObjective { get; set; } = string.Empty;
    public string AutonomousTrigger { get; set; } = "CronHeartbeat"; // CronHeartbeat, OnDemand, EventDriven
    public string ModelPreference { get; set; } = "gemini-2.0-flash";
    public string Status { get; set; } = "Active"; // Active, Idle, Running
    public DateTime? LastHeartbeatUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class AgentHeartbeatRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string RoleKey { get; set; } = string.Empty;
    public string AgentName { get; set; } = string.Empty;
    public string Status { get; set; } = "Healthy"; // Healthy, Warning, ActionRequired
    public string FindingsSummary { get; set; } = string.Empty;
    public string RecommendedAction { get; set; } = string.Empty;
    public int ActionsExecutedCount { get; set; } = 1;
    public DateTime ExecutedAtUtc { get; set; } = DateTime.UtcNow;
}

public record RunPersonaTaskRequest(
    string TaskPrompt);

public record AgentPersonaExecutionResult(
    string RoleKey,
    string AgentName,
    string Output,
    int TokensUsed,
    int LatencyMs,
    DateTime CompletedAtUtc);

public record AgentHeartbeatCycleResult(
    Guid TenantId,
    int AgentsEvaluatedCount,
    int AnomaliesDetectedCount,
    List<AgentHeartbeatRecord> Reports,
    DateTime CycleCompletedAtUtc);
