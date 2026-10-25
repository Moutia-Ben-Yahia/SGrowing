using System.Text.Json;
using MarketingStudio.Api.Data;
using MarketingStudio.Core.Entities;
using MarketingStudio.Core.Outbox;

namespace MarketingStudio.Api.Services;

public class OutboxService : IOutbox
{
    private readonly MarketingStudioDbContext _dbContext;

    public OutboxService(MarketingStudioDbContext dbContext)
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
