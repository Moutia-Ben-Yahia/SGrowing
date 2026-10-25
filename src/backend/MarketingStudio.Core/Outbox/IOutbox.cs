namespace MarketingStudio.Core.Outbox;

public interface IOutbox
{
    Task EnqueueAsync<TEvent>(Guid tenantId, string eventType, TEvent payload, CancellationToken cancellationToken = default) where TEvent : class;
}
