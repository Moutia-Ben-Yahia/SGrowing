namespace SGrowing.Core.Entities;

public record EventFanoutOptions(
    bool DispatchWebhooks = true,
    bool DispatchSignalR = true,
    bool DispatchRedis = true,
    bool DispatchMessageQueue = true,
    int MaxWebhookRetries = 3
);

public record BrokerDeliveryStatus(
    string BrokerName,
    bool Success,
    int LatencyMs,
    string? Details = null
);

public record EventFanoutResult(
    Guid EventId,
    string EventType,
    DateTime DispatchedAtUtc,
    List<BrokerDeliveryStatus> BrokerDeliveries,
    bool OverallSuccess
);

public record DispatchEventFanoutRequest(
    string EventType,
    string PayloadJson,
    EventFanoutOptions? Options = null
);
