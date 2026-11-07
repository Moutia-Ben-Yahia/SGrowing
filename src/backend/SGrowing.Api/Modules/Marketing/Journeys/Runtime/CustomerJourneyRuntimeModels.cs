using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Marketing.Journeys.Runtime;

/// <summary>
/// Types of nodes supported in an event-driven customer journey DAG.
/// </summary>
public enum JourneyNodeType
{
    TriggerNode,
    ActionNode,
    DelayNode,
    ConditionNode,
    SplitTestNode
}

/// <summary>
/// Lifecycle execution states of a contact progressing through a journey.
/// </summary>
public enum JourneyExecutionStatus
{
    Active,
    PausedWaitingDelay,
    WaitingEvent,
    Completed,
    Failed
}

/// <summary>
/// Audit execution step entry recorded whenever a contact transitions through a node.
/// </summary>
public record JourneyStepExecutionLog(
    Guid StepId,
    string NodeId,
    JourneyNodeType NodeType,
    DateTime ExecutedAtUtc,
    string Outcome,
    string Details
);

/// <summary>
/// Durable runtime progress state of a contact actively enrolled or finished in a journey.
/// </summary>
public record CustomerJourneyProgress(
    Guid ProgressId,
    Guid TenantId,
    string JourneyId,
    Guid ContactId,
    string ContactEmail,
    string CurrentNodeId,
    JourneyExecutionStatus Status,
    List<JourneyStepExecutionLog> ExecutionHistory,
    Dictionary<string, string> ContextData,
    DateTime? ResumeAtUtc = null,
    DateTime? CompletedAtUtc = null
)
{
    public string CurrentNodeId { get; set; } = CurrentNodeId;
    public JourneyExecutionStatus Status { get; set; } = Status;
    public List<JourneyStepExecutionLog> ExecutionHistory { get; set; } = ExecutionHistory;
    public Dictionary<string, string> ContextData { get; set; } = ContextData;
    public DateTime? ResumeAtUtc { get; set; } = ResumeAtUtc;
    public DateTime? CompletedAtUtc { get; set; } = CompletedAtUtc;
}

/// <summary>
/// Inbound trigger request payload to enroll or correlate a customer event into an automated journey.
/// </summary>
public record TriggerJourneyEventRequest(
    Guid TenantId,
    string EventName,
    Guid ContactId,
    string ContactEmail,
    Dictionary<string, string>? PayloadData = null
);

/// <summary>
/// Request to advance or transition a contact's journey state machine to the next node or override node.
/// </summary>
public record AdvanceJourneyRequest(
    Guid TenantId,
    Guid ProgressId,
    string? OverrideNodeId = null
);

/// <summary>
/// High-level summary of an active journey including enrolled counts, delay counts, and conversion metrics.
/// </summary>
public record ActiveJourneySummary(
    string JourneyId,
    string Title,
    string Description,
    string TriggerEvent,
    int NodeCount,
    int EnrolledContacts,
    int InDelayNodes,
    int ConvertedContacts,
    double ConversionRatePercentage
);

/// <summary>
/// Node configuration within a template or dynamic journey definition.
/// </summary>
public record JourneyNodeDefinition(
    string NodeId,
    string Title,
    JourneyNodeType NodeType,
    string? NextNodeId = null,
    string? TrueNodeId = null,
    string? FalseNodeId = null,
    TimeSpan? DelayDuration = null,
    string? Description = null
);

/// <summary>
/// Journey definition specification with node graph structure.
/// </summary>
public record JourneyDefinition(
    string JourneyId,
    string Title,
    string Description,
    string TriggerEvent,
    string InitialNodeId,
    Dictionary<string, JourneyNodeDefinition> Nodes
);
