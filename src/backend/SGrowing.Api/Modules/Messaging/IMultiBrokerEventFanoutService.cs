using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Messaging;

public interface IMultiBrokerEventFanoutService
{
    Task<EventFanoutResult> FanoutEventAsync(
        Guid tenantId,
        string eventType,
        string payloadJson,
        EventFanoutOptions? options = null,
        string? customEventId = null,
        CancellationToken ct = default);
}
