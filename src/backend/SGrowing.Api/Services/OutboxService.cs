using System.Text.Json;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;
using SGrowing.Core.Outbox;

namespace SGrowing.Api.Services;

public class OutboxService : IOutbox
{
    private readonly SGrowingDbContext _dbContext;

    public OutboxService(SGrowingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task EnqueueAsync<TEvent>(Guid tenantId, string eventType, TEvent payload, CancellationToken cancellationToken = default) where TEvent : class
    {
        var json = JsonSerializer.Serialize(payload);
        var message = OutboxMessage.Create(tenantId, eventType, json);
        _dbContext.OutboxMessages.Add(message);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
