using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;
using SGrowing.Worker.Services;
using Xunit;

namespace SGrowing.Tests.Worker;

public class WorkerDaemonTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly SGrowingDbContext _db;

    public WorkerDaemonTests()
    {
        var services = new ServiceCollection();
        var dbName = Guid.NewGuid().ToString();
        services.AddDbContext<SGrowingDbContext>(options => options.UseInMemoryDatabase(dbName));

        _serviceProvider = services.BuildServiceProvider();
        _db = _serviceProvider.GetRequiredService<SGrowingDbContext>();
    }

    [Fact]
    public async Task OutboxProcessorDaemon_ProcessesPendingMessagesAndMarksHandled()
    {
        var tenantId = Guid.NewGuid();
        var msg1 = OutboxMessage.Create(tenantId, "WhatsAppMessageDelivered", "{\"messageId\":\"msg_1\"}");
        var msg2 = OutboxMessage.Create(tenantId, "DealCreatedEvent", "{\"dealId\":\"deal_1\"}");

        await _db.OutboxMessages.AddRangeAsync(msg1, msg2);
        await _db.SaveChangesAsync();

        var daemon = new OutboxProcessorDaemon(
            _serviceProvider,
            NullLogger<OutboxProcessorDaemon>.Instance,
            TimeSpan.FromSeconds(1));

        var processedCount = await daemon.ProcessBatchAsync();

        Assert.Equal(2, processedCount);

        using var verifyScope = _serviceProvider.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<SGrowingDbContext>();

        var refreshed1 = await verifyDb.OutboxMessages.AsNoTracking().FirstOrDefaultAsync(m => m.Id == msg1.Id);
        var refreshed2 = await verifyDb.OutboxMessages.AsNoTracking().FirstOrDefaultAsync(m => m.Id == msg2.Id);

        Assert.NotNull(refreshed1?.ProcessedOnUtc);
        Assert.NotNull(refreshed2?.ProcessedOnUtc);
        Assert.Null(refreshed1?.Error);
    }

    [Fact]
    public async Task SlaWatchdogDaemon_DetectsBreachesAndEscalatesConversation()
    {
        var tenantId = Guid.NewGuid();
        var inboxId = Guid.NewGuid();
        var contactId = Guid.NewGuid();

        // Policy: 15 minutes target first response
        var policy = SlaPolicy.Create(tenantId, "Standard SLA", ConversationPriority.Normal, targetFirstResponseMinutes: 15, targetResolutionMinutes: 120);
        await _db.SlaPolicies.AddAsync(policy);

        // Conversation created 30 mins ago without response
        var expiredConv = Conversation.Create(tenantId, inboxId, contactId);
        // Using reflection or simulated age
        typeof(Conversation).GetProperty("CreatedAtUtc")!.SetValue(expiredConv, DateTime.UtcNow.AddMinutes(-30));

        // Conversation created 5 mins ago (healthy)
        var healthyConv = Conversation.Create(tenantId, inboxId, contactId);
        typeof(Conversation).GetProperty("CreatedAtUtc")!.SetValue(healthyConv, DateTime.UtcNow.AddMinutes(-5));

        await _db.Conversations.AddRangeAsync(expiredConv, healthyConv);
        await _db.SaveChangesAsync();

        var watchdog = new SlaWatchdogDaemon(
            _serviceProvider,
            NullLogger<SlaWatchdogDaemon>.Instance,
            TimeSpan.FromSeconds(1));

        var breachedCount = await watchdog.CheckSlaBreachesAsync();

        Assert.Equal(1, breachedCount);

        using var verifyScope = _serviceProvider.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<SGrowingDbContext>();

        var refreshedExpired = await verifyDb.Conversations.AsNoTracking().FirstOrDefaultAsync(c => c.Id == expiredConv.Id);
        var refreshedHealthy = await verifyDb.Conversations.AsNoTracking().FirstOrDefaultAsync(c => c.Id == healthyConv.Id);

        Assert.True(refreshedExpired!.IsSlaBreached);
        Assert.Equal(ConversationPriority.Urgent, refreshedExpired.Priority);

        Assert.False(refreshedHealthy!.IsSlaBreached);
        Assert.Equal(ConversationPriority.Normal, refreshedHealthy.Priority);

        // Verify outbox alert generated
        var outboxAlert = await verifyDb.OutboxMessages.AsNoTracking().FirstOrDefaultAsync(o => o.EventType == "SlaBreachedEvent");
        Assert.NotNull(outboxAlert);
        Assert.Contains(expiredConv.Id.ToString(), outboxAlert.Payload);
    }
}
