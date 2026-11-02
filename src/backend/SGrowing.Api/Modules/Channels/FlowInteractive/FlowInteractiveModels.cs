namespace SGrowing.Api.Modules.Channels.FlowInteractive;

public record WhatsAppFlowComponent(
    string ComponentType,
    string Name,
    string Label,
    bool IsRequired,
    IReadOnlyList<string>? Options = null
);

public record WhatsAppFlowScreen(
    string ScreenId,
    string Title,
    string LayoutType,
    IReadOnlyList<WhatsAppFlowComponent> Components,
    string? TerminalAction = null
);

public record WhatsAppFlowDefinition(
    string FlowId,
    string FlowName,
    string Category,
    string InitialScreenId,
    IReadOnlyList<WhatsAppFlowScreen> Screens,
    string Status,
    string Version,
    DateTime CreatedAtUtc
);

public record FlowDataExchangeRequest(
    string FlowId,
    string ScreenId,
    string Action,
    IReadOnlyDictionary<string, string> DataPayload
);

public record FlowDataExchangeResponse(
    string ScreenId,
    IReadOnlyDictionary<string, string> DataPayload,
    string? NextScreenId = null,
    string? ErrorMessage = null
);

public record FlowSessionTracking(
    string SessionId,
    string FlowId,
    string UserPhone,
    string CurrentScreenId,
    bool IsCompleted,
    DateTime StartedAtUtc,
    DateTime? CompletedAtUtc = null
);

public record FlowAnalyticsSummary(
    int TotalPublishedFlows,
    int TotalSessionsStarted,
    int TotalCompletedSessions,
    double FunnelCompletionRatePercent,
    string HighestDropOffScreen
);
