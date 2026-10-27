using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Automation;

public record BotCardButton(
    string Title,
    string ActionType, // "Postback", "Url", "Call"
    string Value
);

public record BotCarouselCard(
    string Title,
    string? Subtitle,
    string? ImageUrl,
    List<BotCardButton> Buttons
);

public record BotTranscriptMessage(
    string Sender, // "Bot" or "User"
    string Content,
    List<BotCarouselCard>? Cards,
    bool IsTypingIndicator,
    int? DelayMilliseconds,
    DateTime TimestampUtc
);

public record BotSimulationSession(
    Guid SessionId,
    Guid WorkflowId,
    string? CurrentNodeId,
    Dictionary<string, object> Variables,
    List<BotTranscriptMessage> Transcript,
    bool IsWaitingForUserInput,
    bool IsHandedOverToHuman,
    bool IsCompleted,
    int TotalStepsExecuted
);

public record BotSimulationStepResult(
    Guid SessionId,
    string? CurrentNodeId,
    List<BotTranscriptMessage> NewMessages,
    Dictionary<string, object> UpdatedVariables,
    bool IsWaitingForUserInput,
    bool IsHandedOverToHuman,
    bool IsCompleted,
    List<string> ExecutionTraceLogs
);

public interface IBotSimulatorService
{
    BotSimulationSession StartSimulation(
        WorkflowGraph workflow,
        Dictionary<string, object>? initialVariables = null);

    BotSimulationStepResult SimulateUserMessage(
        WorkflowGraph workflow,
        BotSimulationSession session,
        string? userMessage = null);
}
