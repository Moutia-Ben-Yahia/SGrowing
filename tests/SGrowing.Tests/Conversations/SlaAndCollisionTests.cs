using FluentAssertions;
using SGrowing.Api.Data;
using SGrowing.Api.Hubs;
using SGrowing.Api.Modules.Conversations;
using SGrowing.Api.Services;
using SGrowing.Core.Entities;
using SGrowing.Core.Tenancy;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace SGrowing.Tests.Conversations;

public class SlaAndCollisionTests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(Guid.NewGuid());
        return new SGrowingDbContext(options, tenantContext);
    }

    [Fact]
    public async Task EvaluateSlaBreaches_FlagsBreachedConversationsAndElevatesToUrgent()
    {
        var db = CreateDbContext();
        var notifier = new Mock<IRealtimeNotifier>();
        var service = new SlaMonitoringService(db, notifier.Object);
        var tenantId = Guid.NewGuid();

        var policy = SlaPolicy.Create(tenantId, "Standard Support SLA", ConversationPriority.Normal, targetFirstResponseMinutes: 15, targetResolutionMinutes: 120);
        db.SlaPolicies.Add(policy);

        var contact = Contact.Create(tenantId, "Barry", "Allen", "barry@star-labs.com");
        db.Contacts.Add(contact);

        // Conversation created 20 minutes ago without a first response
        var conv = Conversation.Create(tenantId, Guid.NewGuid(), contact.Id);
        typeof(Conversation).GetProperty("CreatedAtUtc")!.SetValue(conv, DateTime.UtcNow.AddMinutes(-20));
        db.Conversations.Add(conv);
        await db.SaveChangesAsync();

        var breachedCount = await service.EvaluateSlaBreachesAsync(tenantId);

        breachedCount.Should().Be(1);

        var updatedConv = await db.Conversations.FindAsync(conv.Id);
        updatedConv!.IsSlaBreached.Should().BeTrue();
        updatedConv.Priority.Should().Be(ConversationPriority.Urgent);

        notifier.Verify(n => n.NotifyConversationUpdatedAsync(tenantId, It.IsAny<ConversationDto>()), Times.Once);
    }

    [Fact]
    public async Task EvaluateSlaBreaches_WhenWithinTimeWindow_DoesNotBreach()
    {
        var db = CreateDbContext();
        var notifier = new Mock<IRealtimeNotifier>();
        var service = new SlaMonitoringService(db, notifier.Object);
        var tenantId = Guid.NewGuid();

        var policy = SlaPolicy.Create(tenantId, "Standard Support SLA", ConversationPriority.Normal, targetFirstResponseMinutes: 30, targetResolutionMinutes: 120);
        db.SlaPolicies.Add(policy);

        var contact = Contact.Create(tenantId, "Hal", "Jordan", "hal@ferris.com");
        db.Contacts.Add(contact);

        // Created 5 minutes ago
        var conv = Conversation.Create(tenantId, Guid.NewGuid(), contact.Id);
        typeof(Conversation).GetProperty("CreatedAtUtc")!.SetValue(conv, DateTime.UtcNow.AddMinutes(-5));
        db.Conversations.Add(conv);
        await db.SaveChangesAsync();

        var breachedCount = await service.EvaluateSlaBreachesAsync(tenantId);

        breachedCount.Should().Be(0);

        var updatedConv = await db.Conversations.FindAsync(conv.Id);
        updatedConv!.IsSlaBreached.Should().BeFalse();
        updatedConv.Priority.Should().Be(ConversationPriority.Normal);
    }
}
