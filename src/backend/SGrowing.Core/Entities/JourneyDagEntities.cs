using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace SGrowing.Core.Entities;

public enum JourneyNodeType
{
    Trigger = 1,
    Delay = 2,
    WaitUntilEvent = 3,
    ConditionBranch = 4,
    ChannelAction = 5,
    Exit = 6
}

public enum JourneyInstanceStatus
{
    Active = 1,
    WaitingForEvent = 2,
    Completed = 3,
    Unenrolled = 4
}

public class JourneyNode
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public JourneyNodeType Type { get; set; }

    // Graph Edge Target Identifiers
    public string? NextNodeId { get; set; }
    public string? SuccessNextNodeId { get; set; }
    public string? TimeoutNextNodeId { get; set; }
    public string? TrueNextNodeId { get; set; }
    public string? FalseNextNodeId { get; set; }

    // Node Configurations
    public string? EventName { get; set; }
    public int DelaySeconds { get; set; }
    public string? Timezone { get; set; }
    public int TtlSeconds { get; set; }
    public string? ConditionExpression { get; set; }
    public string? ChannelType { get; set; } // "Email", "WhatsApp", "SMS", "Webhook"
    public string? ActionPayload { get; set; }

    // Visual Canvas Placement Coordinates
    public double PositionX { get; set; }
    public double PositionY { get; set; }

    public JourneyNode() { }

    public JourneyNode(string id, string name, JourneyNodeType type)
    {
        Id = id;
        Name = name;
        Type = type;
    }
}

public class TriggerNode : JourneyNode
{
    public TriggerNode()
    {
        Type = JourneyNodeType.Trigger;
    }

    public TriggerNode(string id, string name, string eventName, string? nextNodeId = null, double x = 0, double y = 0)
        : this()
    {
        Id = id;
        Name = name;
        EventName = eventName;
        NextNodeId = nextNodeId;
        PositionX = x;
        PositionY = y;
    }
}

public class DelayNode : JourneyNode
{
    public DelayNode()
    {
        Type = JourneyNodeType.Delay;
    }

    public DelayNode(string id, string name, int delaySeconds, string? timezone = null, string? nextNodeId = null, double x = 0, double y = 0)
        : this()
    {
        Id = id;
        Name = name;
        DelaySeconds = delaySeconds;
        Timezone = timezone;
        NextNodeId = nextNodeId;
        PositionX = x;
        PositionY = y;
    }
}

public class WaitUntilEventNode : JourneyNode
{
    public WaitUntilEventNode()
    {
        Type = JourneyNodeType.WaitUntilEvent;
    }

    public WaitUntilEventNode(string id, string name, string eventName, int ttlSeconds, string successNextNodeId, string timeoutNextNodeId, double x = 0, double y = 0)
        : this()
    {
        Id = id;
        Name = name;
        EventName = eventName;
        TtlSeconds = ttlSeconds;
        SuccessNextNodeId = successNextNodeId;
        TimeoutNextNodeId = timeoutNextNodeId;
        PositionX = x;
        PositionY = y;
    }
}

public class ConditionBranchNode : JourneyNode
{
    public ConditionBranchNode()
    {
        Type = JourneyNodeType.ConditionBranch;
    }

    public ConditionBranchNode(string id, string name, string conditionExpression, string trueNextNodeId, string falseNextNodeId, double x = 0, double y = 0)
        : this()
    {
        Id = id;
        Name = name;
        ConditionExpression = conditionExpression;
        TrueNextNodeId = trueNextNodeId;
        FalseNextNodeId = falseNextNodeId;
        PositionX = x;
        PositionY = y;
    }
}

public class ChannelActionNode : JourneyNode
{
    public ChannelActionNode()
    {
        Type = JourneyNodeType.ChannelAction;
    }

    public ChannelActionNode(string id, string name, string channelType, string actionPayload, string? nextNodeId = null, double x = 0, double y = 0)
        : this()
    {
        Id = id;
        Name = name;
        ChannelType = channelType;
        ActionPayload = actionPayload;
        NextNodeId = nextNodeId;
        PositionX = x;
        PositionY = y;
    }
}

public class ExitNode : JourneyNode
{
    public ExitNode()
    {
        Type = JourneyNodeType.Exit;
    }

    public ExitNode(string id, string name, double x = 0, double y = 0)
        : this()
    {
        Id = id;
        Name = name;
        PositionX = x;
        PositionY = y;
    }
}

public class JourneyStepExecutionRecord
{
    public string NodeId { get; set; } = string.Empty;
    public string NodeName { get; set; } = string.Empty;
    public JourneyNodeType NodeType { get; set; }
    public string Status { get; set; } = string.Empty; // "Executed", "Waiting", "MatchedEvent", "TimedOut", "BranchTrue", "BranchFalse", "Unenrolled", "Completed"
    public string Details { get; set; } = string.Empty;
    public DateTime ExecutedAtUtc { get; set; } = DateTime.UtcNow;
}

public class JourneyDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string TriggerEvent { get; set; } = string.Empty;
    public string ConversionEventsJson { get; set; } = "[]";
    public string NodesJson { get; set; } = "[]";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    [NotMapped]
    public List<string> ConversionEvents
    {
        get => string.IsNullOrWhiteSpace(ConversionEventsJson)
            ? new List<string>()
            : JsonSerializer.Deserialize<List<string>>(ConversionEventsJson, JsonOpts) ?? new List<string>();
        set => ConversionEventsJson = JsonSerializer.Serialize(value ?? new List<string>(), JsonOpts);
    }

    [NotMapped]
    public List<JourneyNode> Nodes
    {
        get => string.IsNullOrWhiteSpace(NodesJson)
            ? new List<JourneyNode>()
            : JsonSerializer.Deserialize<List<JourneyNode>>(NodesJson, JsonOpts) ?? new List<JourneyNode>();
        set => NodesJson = JsonSerializer.Serialize(value ?? new List<JourneyNode>(), JsonOpts);
    }
}

public class JourneyInstance
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid JourneyDefinitionId { get; set; }
    public Guid ContactId { get; set; }
    public string CurrentNodeId { get; set; } = string.Empty;
    public JourneyInstanceStatus Status { get; set; } = JourneyInstanceStatus.Active;
    public string? WaitUntilEventName { get; set; }
    public DateTime? WaitUntilExpiresAtUtc { get; set; }
    public DateTime? NextExecutionAtUtc { get; set; }
    public string AttributesJson { get; set; } = "{}";
    public string ExecutionHistoryJson { get; set; } = "[]";
    public DateTime EnrolledAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    [NotMapped]
    public Dictionary<string, object> CustomerAttributes
    {
        get => string.IsNullOrWhiteSpace(AttributesJson)
            ? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            : JsonSerializer.Deserialize<Dictionary<string, object>>(AttributesJson, JsonOpts) ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        set => AttributesJson = JsonSerializer.Serialize(value ?? new Dictionary<string, object>(), JsonOpts);
    }

    [NotMapped]
    public List<JourneyStepExecutionRecord> ExecutionHistory
    {
        get => string.IsNullOrWhiteSpace(ExecutionHistoryJson)
            ? new List<JourneyStepExecutionRecord>()
            : JsonSerializer.Deserialize<List<JourneyStepExecutionRecord>>(ExecutionHistoryJson, JsonOpts) ?? new List<JourneyStepExecutionRecord>();
        set => ExecutionHistoryJson = JsonSerializer.Serialize(value ?? new List<JourneyStepExecutionRecord>(), JsonOpts);
    }
}

public record DagValidationResult(
    bool IsValid,
    string? ErrorMessage = null,
    List<string>? CyclePath = null
);

public record JourneyNodeMetric(
    string NodeId,
    string NodeName,
    JourneyNodeType NodeType,
    int ExecutionsCount,
    int CurrentContactsCount,
    double ConversionPercent,
    int DropOffCount
);

public record JourneyMetricsResponse(
    Guid JourneyId,
    string JourneyName,
    int TotalEnrolled,
    int CurrentlyActive,
    int CurrentlyWaiting,
    int TotalCompleted,
    int TotalUnenrolled,
    double CompletionRate,
    double DropOffRate,
    List<JourneyNodeMetric> NodeMetrics
);

public record JourneyEventIngestResult(
    string IngestedEventName,
    int UnenrolledCount,
    int AdvancedCount,
    int EnrolledCount,
    string Message
);
