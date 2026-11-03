using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;
using SGrowing.Worker.Services;
using Xunit;

namespace SGrowing.Tests.Worker;

public class SlaWatchdogDaemonTests
{
    private (IServiceProvider serviceProvider, SGrowingDbContext db) SetupDependencies()
    {
        var services = new ServiceCollection();
        var dbName = Guid.NewGuid().ToString();
        services.AddDbContext<SGrowingDbContext>(options => options.UseInMemoryDatabase(dbName));

        var sp = services.BuildServiceProvider();
        var db = sp.GetRequiredService<SGrowingDbContext>();
        return (sp, db);
    }

    [Fact]
    public async Task CheckSlaBreachesAsync_WhenNoActivePolicies_ReturnsZero()
    {
        var (sp, db) = SetupDependencies();
        using var scope = db;

        var daemon = new SlaWatchdogDaemon(sp, NullLogger<SlaWatchdogDaemon>.Instance);
        var breachCount = await daemon.CheckSlaBreachesAsync();

        breachCount.Should().Be(0);
    }

    [Fact]
    public async Task CheckSlaBreachesAsync_WhenNoOpenConversations_ReturnsZero()
    {
        var (sp, db) = SetupDependencies();
        using var scope = db;
        var tenantId = Guid.NewGuid();

        var policy = SlaPolicy.Create(tenantId, "Gold Support Policy", ConversationPriority.Normal, targetFirstResponseMinutes: 15, targetResolutionMinutes: 120);
        db.SlaPolicies.Add(policy);
        await db.SaveChangesAsync();

        var daemon = new SlaWatchdogDaemon(sp, NullLogger<SlaWatchdogDaemon>.Instance);
        var breachCount = await daemon.CheckSlaBreachesAsync();

        breachCount.Should().Be(0);
    }

    [Fact]
    public async Task CheckSlaBreachesAsync_WhenFirstResponseSlaBreached_MarksBreachedEscalatesPriorityAndDispatchesOutbox()
    {
        var (sp, db) = SetupDependencies();
        using var scope = db;
        var tenantId = Guid.NewGuid();
        var inboxId = Guid.NewGuid();
        var contactId = Guid.NewGuid();

        var policy = SlaPolicy.Create(tenantId, "Urgent Care SLA", ConversationPriority.Normal, targetFirstResponseMinutes: 10, targetResolutionMinutes: 180);
        db.SlaPolicies.Add(policy);

        var conv = Conversation.Create(tenantId, inboxId, contactId);
        conv.Priority = ConversationPriority.Normal;
        conv.Status = ConversationStatus.Open;
        conv.FirstResponseAtUtc = null;
        db.Conversations.Add(conv);
        db.Entry(conv).Property(c => c.CreatedAtUtc).CurrentValue = DateTime.UtcNow.AddMinutes(-30); // 30 mins elapsed > 10 min target
        await db.SaveChangesAsync();

        var daemon = new SlaWatchdogDaemon(sp, NullLogger<SlaWatchdogDaemon>.Instance);
        var breachCount = await daemon.CheckSlaBreachesAsync();

        breachCount.Should().Be(1);

        using var verifyScope = sp.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<SGrowingDbContext>();

        var updatedConv = await verifyDb.Conversations.FirstAsync(c => c.Id == conv.Id);
        updatedConv.IsSlaBreached.Should().BeTrue();
        updatedConv.Priority.Should().Be(ConversationPriority.Urgent);

        var outboxMessage = await verifyDb.OutboxMessages.FirstOrDefaultAsync(m => m.EventType == "SlaBreachedEvent");
        outboxMessage.Should().NotBeNull();
        outboxMessage!.Payload.Should().Contain(conv.Id.ToString());
    }

    [Fact]
    public async Task CheckSlaBreachesAsync_WhenResolutionSlaBreached_MarksBreachedEvenWithFirstResponse()
    {
        var (sp, db) = SetupDependencies();
        using var scope = db;
        var tenantId = Guid.NewGuid();
        var inboxId = Guid.NewGuid();
        var contactId = Guid.NewGuid();

        var policy = SlaPolicy.Create(tenantId, "Standard Support SLA", ConversationPriority.Normal, targetFirstResponseMinutes: 60, targetResolutionMinutes: 120);
        db.SlaPolicies.Add(policy);

        var conv = Conversation.Create(tenantId, inboxId, contactId);
        conv.Priority = ConversationPriority.Normal;
        conv.Status = ConversationStatus.Open;
        conv.FirstResponseAtUtc = DateTime.UtcNow.AddMinutes(-200); // First response sent on time
        db.Conversations.Add(conv);
        db.Entry(conv).Property(c => c.CreatedAtUtc).CurrentValue = DateTime.UtcNow.AddMinutes(-210); // 210 mins elapsed > 120 min resolution target
        await db.SaveChangesAsync();

        var daemon = new SlaWatchdogDaemon(sp, NullLogger<SlaWatchdogDaemon>.Instance);
        var breachCount = await daemon.CheckSlaBreachesAsync();

        breachCount.Should().Be(1);

        using var verifyScope = sp.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<SGrowingDbContext>();

        var updatedConv = await verifyDb.Conversations.FirstAsync(c => c.Id == conv.Id);
        updatedConv.IsSlaBreached.Should().BeTrue();
        updatedConv.Priority.Should().Be(ConversationPriority.Urgent);
    }

    [Fact]
    public async Task CheckSlaBreachesAsync_WhenWithinTargets_DoesNotBreach()
    {
        var (sp, db) = SetupDependencies();
        using var scope = db;
        var tenantId = Guid.NewGuid();
        var inboxId = Guid.NewGuid();
        var contactId = Guid.NewGuid();

        var policy = SlaPolicy.Create(tenantId, "Standard SLA", ConversationPriority.Normal, targetFirstResponseMinutes: 30, targetResolutionMinutes: 240);
        db.SlaPolicies.Add(policy);

        var conv = Conversation.Create(tenantId, inboxId, contactId);
        conv.Priority = ConversationPriority.Normal;
        conv.Status = ConversationStatus.Open;
        db.Conversations.Add(conv);
        db.Entry(conv).Property(c => c.CreatedAtUtc).CurrentValue = DateTime.UtcNow.AddMinutes(-5); // 5 mins elapsed < 30 min target
        await db.SaveChangesAsync();

        var daemon = new SlaWatchdogDaemon(sp, NullLogger<SlaWatchdogDaemon>.Instance);
        var breachCount = await daemon.CheckSlaBreachesAsync();

        breachCount.Should().Be(0);

        using var verifyScope = sp.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<SGrowingDbContext>();

        var updatedConv = await verifyDb.Conversations.FirstAsync(c => c.Id == conv.Id);
        updatedConv.IsSlaBreached.Should().BeFalse();
    }
}
