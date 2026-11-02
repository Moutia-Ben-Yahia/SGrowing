using System.Text.Json.Serialization;

namespace SGrowing.Api.Modules.Voice.IvrTree;

public sealed record IvrNode(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("promptText")] string PromptText,
    [property: JsonPropertyName("allowedDtmfKeys")] IReadOnlyList<string> AllowedDtmfKeys,
    [property: JsonPropertyName("routes")] IReadOnlyDictionary<string, string> Routes,
    [property: JsonPropertyName("fallbackNodeId")] string? FallbackNodeId
);

public sealed record IvrTree(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("entryNodeId")] string EntryNodeId,
    [property: JsonPropertyName("nodes")] IReadOnlyList<IvrNode> Nodes,
    [property: JsonPropertyName("createdAtUtc")] DateTime CreatedAtUtc
);

public sealed record SimulateIvrCallRequest(
    [property: JsonPropertyName("treeId")] string TreeId,
    [property: JsonPropertyName("callerNumber")] string CallerNumber,
    [property: JsonPropertyName("inputs")] IReadOnlyList<string> Inputs
);

public sealed record SimulateIvrCallResponse(
    [property: JsonPropertyName("sessionId")] string SessionId,
    [property: JsonPropertyName("callerNumber")] string CallerNumber,
    [property: JsonPropertyName("finalNodeId")] string FinalNodeId,
    [property: JsonPropertyName("isCompleted")] bool IsCompleted,
    [property: JsonPropertyName("executionLog")] IReadOnlyList<string> ExecutionLog
);

public sealed record VoiceBroadcastCampaign(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("callerId")] string CallerId,
    [property: JsonPropertyName("targetCount")] int TargetCount,
    [property: JsonPropertyName("maxConcurrentChannels")] int MaxConcurrentChannels,
    [property: JsonPropertyName("enableAmd")] bool EnableAmd,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("deliveredCalls")] int DeliveredCalls,
    [property: JsonPropertyName("amdVoicemailsDetected")] int AmdVoicemailsDetected
);
