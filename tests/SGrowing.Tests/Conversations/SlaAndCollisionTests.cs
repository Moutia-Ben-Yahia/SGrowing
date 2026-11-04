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

    [Fact]
    public async Task UpdatePresenceAsync_And_GetActiveViewersAsync_ShouldTrackTypingAndActiveViewers()
    {
        var db = CreateDbContext();
        var service = new CollisionDetectionAndSlaService(db);
        var tenantId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();

        // Agent 1 is viewing and typing
        var viewers = await service.UpdatePresenceAsync(tenantId, conversationId, "op_01", "Agent Sarah", isTyping: true);

        viewers.Should().ContainSingle();
        viewers[0].OperatorId.Should().Be("op_01");
        viewers[0].OperatorName.Should().Be("Agent Sarah");
        viewers[0].IsTyping.Should().BeTrue();

        // Agent 2 joins conversation (not typing)
        await service.UpdatePresenceAsync(tenantId, conversationId, "op_02", "Agent Alex", isTyping: false);

        var allViewers = await service.GetActiveViewersAsync(tenantId, conversationId);
        allViewers.Should().HaveCount(2);
        allViewers.Should().Contain(v => v.OperatorId == "op_01" && v.IsTyping);
        allViewers.Should().Contain(v => v.OperatorId == "op_02" && !v.IsTyping);
    }

    [Fact]
    public async Task RecordCsatFeedbackAsync_WithValidRating_ShouldStoreFeedback()
    {
        var db = CreateDbContext();
        var service = new CollisionDetectionAndSlaService(db);
        var tenantId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();

        var feedback = await service.RecordCsatFeedbackAsync(tenantId, conversationId, 5, "Outstanding and fast response!");

        feedback.Should().NotBeNull();
        feedback.ConversationId.Should().Be(conversationId);
        feedback.RatingScore.Should().Be(5);
        feedback.FeedbackComment.Should().Be("Outstanding and fast response!");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    public async Task RecordCsatFeedbackAsync_WithOutOfRangeRating_ThrowsArgumentOutOfRangeException(int invalidScore)
    {
        var db = CreateDbContext();
        var service = new CollisionDetectionAndSlaService(db);
        var tenantId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();

        var act = () => service.RecordCsatFeedbackAsync(tenantId, conversationId, invalidScore, "Comment");
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>()
            .WithMessage("*CSAT rating must be between 1 and 5*");
    }

    [Fact]
    public async Task EvaluateSlaBreaches_WithMultiplePriorities_ShouldApplyMatchingPriorityPolicy()
    {
        var db = CreateDbContext();
        var notifier = new Mock<IRealtimeNotifier>();
        var service = new SlaMonitoringService(db, notifier.Object);
        var tenantId = Guid.NewGuid();

        // Policy 1: Normal Priority -> 60 min FRT
        var normalPolicy = SlaPolicy.Create(tenantId, "Normal SLA", ConversationPriority.Normal, targetFirstResponseMinutes: 60, targetResolutionMinutes: 240);
        // Policy 2: Urgent Priority -> 10 min FRT
        var urgentPolicy = SlaPolicy.Create(tenantId, "Urgent SLA", ConversationPriority.Urgent, targetFirstResponseMinutes: 10, targetResolutionMinutes: 60);
        db.SlaPolicies.AddRange(normalPolicy, urgentPolicy);

        var contact = Contact.Create(tenantId, "Diana", "Prince", "diana@amazon.com");
        db.Contacts.Add(contact);

        // Conv 1: Normal priority created 30 mins ago -> NOT breached (since target is 60m)
        var convNormal = Conversation.Create(tenantId, Guid.NewGuid(), contact.Id);
        convNormal.Priority = ConversationPriority.Normal;
        typeof(Conversation).GetProperty("CreatedAtUtc")!.SetValue(convNormal, DateTime.UtcNow.AddMinutes(-30));

        db.Conversations.Add(convNormal);
        await db.SaveChangesAsync();

        var breached = await service.EvaluateSlaBreachesAsync(tenantId);
        breached.Should().Be(0);
        convNormal.IsSlaBreached.Should().BeFalse();
    }

    [Fact]
    public async Task CollisionService_EvaluateSlaBreachesAsync_CalculatesRemainingAndBreachedAccurately()
    {
        var db = CreateDbContext();
        var service = new CollisionDetectionAndSlaService(db);
        var tenantId = Guid.NewGuid();

        var policy = SlaPolicy.Create(tenantId, "Tier 1 SLA", ConversationPriority.Normal, targetFirstResponseMinutes: 15, targetResolutionMinutes: 60);
        db.SlaPolicies.Add(policy);

        var contact = Contact.Create(tenantId, "Arthur", "Curry", "arthur@atlantis.gov");
        db.Contacts.Add(contact);

        // Created 25 minutes ago (> 15 min target) -> should be breached
        var conv = Conversation.Create(tenantId, Guid.NewGuid(), contact.Id);
        typeof(Conversation).GetProperty("CreatedAtUtc")!.SetValue(conv, DateTime.UtcNow.AddMinutes(-25));
        db.Conversations.Add(conv);
        await db.SaveChangesAsync();

        var alerts = await service.EvaluateSlaBreachesAsync(tenantId);

        alerts.Should().ContainSingle();
        alerts[0].IsBreached.Should().BeTrue();
        alerts[0].MinutesRemaining.Should().Be(0);
        alerts[0].PolicyName.Should().Be("Tier 1 SLA");
    }

    [Fact]
    public async Task GetActiveViewersAsync_TenantIsolation_DoesNotLeakAcrossTenants()
    {
        var db = CreateDbContext();
        var service = new CollisionDetectionAndSlaService(db);
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var conversationId = Guid.NewGuid();

        await service.UpdatePresenceAsync(tenantA, conversationId, "op_a", "Operator A", isTyping: true);

        var viewersA = await service.GetActiveViewersAsync(tenantA, conversationId);
        viewersA.Should().ContainSingle();

        var viewersB = await service.GetActiveViewersAsync(tenantB, conversationId);
        viewersB.Should().BeEmpty();
    }
}
