namespace SGrowing.Core.Entities;

public enum FlowNodeType
{
    TextMessage,
    QuickReplyButtons,
    ListPicker,
    ProductCatalog,
    ActionTrigger,
    DecisionBranch,
    HumanEscalate
}

public class FlowDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string TriggerKeyword { get; set; } = string.Empty; // e.g. "START", "COMMANDE", "SUIVI", "ECHANGE"
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public string NodesJson { get; set; } = "[]";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public static FlowDefinition Create(
        Guid tenantId,
        string name,
        string triggerKeyword,
        string description = "")
    {
        return new FlowDefinition
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name.Trim(),
            TriggerKeyword = triggerKeyword.Trim().ToUpperInvariant(),
            Description = description.Trim(),
            IsActive = true,
            NodesJson = "[]",
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}

public class FlowExecutionSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid FlowDefinitionId { get; set; }
    public string CustomerPhone { get; set; } = string.Empty;
    public string CurrentNodeKey { get; set; } = "start";
    public string VariablesJson { get; set; } = "{}";
    public string DetectedLanguage { get; set; } = "Darija"; // Darija, Arabic, French, English
    public decimal SentimentScore { get; set; } = 0.5m; // 0.0 (furious/negative) to 1.0 (delighted)
    public bool IsEscalatedToHuman { get; set; } = false;
    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastInteractionAtUtc { get; set; } = DateTime.UtcNow;
}

public record FlowTurnResult(
    string NextNodeKey,
    string ResponseMessage,
    List<string> Buttons,
    bool IsEscalated,
    string DetectedLanguage,
    string? ExecutedAction = null
);
