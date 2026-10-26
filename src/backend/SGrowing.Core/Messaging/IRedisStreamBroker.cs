namespace SGrowing.Core.Messaging;

public record StreamMessageEnvelope(
    string MessageId,
    string StreamKey,
    string EventType,
    Guid TenantId,
    string PayloadJson,
    DateTime PublishedAtUtc
);

public interface IRedisStreamBroker
{
    Task<string> PublishEventAsync(string streamKey, string eventType, string payloadJson, Guid tenantId);
    Task EnsureConsumerGroupAsync(string streamKey, string groupName);
    Task<List<StreamMessageEnvelope>> ReadGroupMessagesAsync(string streamKey, string groupName, string consumerName, int count = 10);
    Task AcknowledgeAsync(string streamKey, string groupName, string messageId);
    bool IsConnected { get; }
}
