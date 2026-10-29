using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Conversations;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Conversations;

public class OmnichannelUnifiedDeskTests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public async Task PresenceTracking_ShouldTrackActiveViewersAndTypers()
    {
        // Arrange
        var db = CreateDbContext();
        var service = new OmnichannelUnifiedDeskService(db);
        var tenantId = Guid.NewGuid();
        var convId = Guid.NewGuid();

        // Act - Agent 1 viewing
        var report1 = await service.UpdatePresenceAsync(tenantId, convId, new UpdateAgentPresenceRequest("agent-1", "Agent Sarah", false));

        // Assert
        report1.HasCollision.Should().BeFalse();
        report1.ActiveAgents.Should().HaveCount(1);
        report1.ActiveAgents.First().AgentName.Should().Be("Agent Sarah");
        report1.ActiveAgents.First().IsTyping.Should().BeFalse();
    }

    [Fact]
    public async Task CollisionWarning_ShouldTriggerWhenMultipleAgentsViewOrType()
    {
        // Arrange
        var db = CreateDbContext();
        var service = new OmnichannelUnifiedDeskService(db);
        var tenantId = Guid.NewGuid();
        var convId = Guid.NewGuid();

        // Act - Agent 1 viewing, then Agent 2 viewing and typing
        await service.UpdatePresenceAsync(tenantId, convId, new UpdateAgentPresenceRequest("agent-1", "Agent Sarah", false));
        var report = await service.UpdatePresenceAsync(tenantId, convId, new UpdateAgentPresenceRequest("agent-2", "Agent Alex", true));

        // Assert
        report.HasCollision.Should().BeTrue();
        report.WarningMessage.Should().NotBeNullOrEmpty();
        report.WarningMessage.Should().Contain("Agent Collision Alert");
        report.WarningMessage.Should().Contain("Agent Sarah");
        report.WarningMessage.Should().Contain("Agent Alex");
        report.ActiveAgents.Should().HaveCount(2);
    }

    [Fact]
    public async Task SoftLockWarning_ShouldWarnWhenAnotherAgentSentMessageWithin60Seconds()
    {
        // Arrange
        var db = CreateDbContext();
        var service = new OmnichannelUnifiedDeskService(db);
        var tenantId = Guid.NewGuid();

        // Seed initial conversation
        var convs = await service.GetConversationsAsync(tenantId);
        var convId = convs.First().ConversationId;

        // Act 1: Agent Sarah sends a message
        var agentSarahId = Guid.NewGuid();
        var resp1 = await service.SendMessageAsync(tenantId, convId, new SendUnifiedDeskMessageRequest(
            Content: "Hi there! I am looking into your order.",
            SenderType: MessageSenderType.Agent,
            SenderId: agentSarahId,
            SenderName: "Agent Sarah"
        ));
        resp1.Success.Should().BeTrue();
        resp1.SoftLockTriggered.Should().BeFalse();

        // Act 2: Agent Alex attempts to send a message immediately (within 60 seconds)
        var agentAlexId = Guid.NewGuid();
        var resp2 = await service.SendMessageAsync(tenantId, convId, new SendUnifiedDeskMessageRequest(
            Content: "I also have an update on this order.",
            SenderType: MessageSenderType.Agent,
            SenderId: agentAlexId,
            SenderName: "Agent Alex",
            BypassSoftLock: false
        ));

        // Assert
        resp2.Success.Should().BeFalse();
        resp2.SoftLockTriggered.Should().BeTrue();
        resp2.SoftLockWarning.Should().Contain("Agent Sarah sent a message");
        resp2.SoftLockWarning.Should().Contain("Please confirm before sending");
    }

    [Fact]
    public async Task SoftLockWarning_ShouldAllowBypassWhenExplicitlyAuthorized()
    {
        // Arrange
        var db = CreateDbContext();
        var service = new OmnichannelUnifiedDeskService(db);
        var tenantId = Guid.NewGuid();

        var convs = await service.GetConversationsAsync(tenantId);
        var convId = convs.First().ConversationId;

        // Agent Sarah sends message
        var agentSarahId = Guid.NewGuid();
        await service.SendMessageAsync(tenantId, convId, new SendUnifiedDeskMessageRequest(
            Content: "Agent Sarah initial reply",
            SenderType: MessageSenderType.Agent,
            SenderId: agentSarahId,
            SenderName: "Agent Sarah"
        ));

        // Agent Alex sends message with BypassSoftLock = true
        var agentAlexId = Guid.NewGuid();
        var resp = await service.SendMessageAsync(tenantId, convId, new SendUnifiedDeskMessageRequest(
            Content: "Agent Alex confirmed secondary reply",
            SenderType: MessageSenderType.Agent,
            SenderId: agentAlexId,
            SenderName: "Agent Alex",
            BypassSoftLock: true
        ));

        // Assert
        resp.Success.Should().BeTrue();
        resp.SoftLockTriggered.Should().BeFalse();
        resp.Message.Should().NotBeNull();
        resp.Message!.Content.Should().Be("Agent Alex confirmed secondary reply");
    }

    [Fact]
    public async Task PrivateInternalNotes_ShouldBeFlaggedAndNotTriggerCustomerSoftLock()
    {
        // Arrange
        var db = CreateDbContext();
        var service = new OmnichannelUnifiedDeskService(db);
        var tenantId = Guid.NewGuid();

        var convs = await service.GetConversationsAsync(tenantId);
        var convId = convs.First().ConversationId;

        // Act: Save internal note
        var resp = await service.SendMessageAsync(tenantId, convId, new SendUnifiedDeskMessageRequest(
            Content: "Internal note: Customer verified over phone with VIP status.",
            IsPrivateNote: true,
            SenderName: "Lead Supervisor"
        ));

        // Assert
        resp.Success.Should().BeTrue();
        resp.Message.Should().NotBeNull();
        resp.Message!.IsPrivateNote.Should().BeTrue();
        resp.Message.SenderType.Should().Be(MessageSenderType.InternalNote);

        // Fetch thread detail and verify note presence
        var detail = await service.GetConversationByIdAsync(tenantId, convId);
        detail.Should().NotBeNull();
        detail!.Messages.Should().Contain(m => m.IsPrivateNote && m.Content.Contains("Internal note"));
    }

    [Fact]
    public async Task AiCopilot_ShouldGenerateThreeContextualSuggestedReplies()
    {
        // Arrange
        var db = CreateDbContext();
        var service = new OmnichannelUnifiedDeskService(db);
        var tenantId = Guid.NewGuid();

        var convs = await service.GetConversationsAsync(tenantId);
        var convId = convs.First().ConversationId;

        // Act
        var suggestions = await service.GenerateCopilotSuggestionsAsync(tenantId, convId, "Where is my parcel #ORD-7721?");

        // Assert
        suggestions.Should().HaveCount(3);
        suggestions.Should().Contain(s => s.Category == "Fulfillment" && s.SuggestedText.Contains("ORD-7721"));
        suggestions.Should().Contain(s => s.Category == "Returns & Exchanges" && s.SuggestedText.Contains("14-day warranty"));
        suggestions.Should().Contain(s => s.Category == "Shipping" && s.SuggestedText.Contains("delivery address"));

        suggestions.All(s => s.ConfidenceScore >= 0.85).Should().BeTrue();
        suggestions.All(s => !string.IsNullOrEmpty(s.GroundingSource)).Should().BeTrue();
    }

    [Theory]
    [InlineData("Where is my shipment?", "OrderStatusTracking", "https://track.sgrowing.io/TN-99218")]
    [InlineData("What is my order tracking number?", "OrderTrackingNumber", "TN-99218")]
    [InlineData("Update delivery address before courier pickup", "UpdateDeliveryAddress", "Address modification lock")]
    [InlineData("What is your return policy?", "ReturnPolicy", "14 days of delivery")]
    public async Task Autopilot_ShouldAutoResolveTransactionalIntents(string userText, string expectedIntent, string expectedReplySubstring)
    {
        // Arrange
        var db = CreateDbContext();
        var service = new OmnichannelUnifiedDeskService(db);
        var tenantId = Guid.NewGuid();

        var convs = await service.GetConversationsAsync(tenantId);
        var convId = convs.First().ConversationId;

        // Act
        var result = await service.ResolveAutopilotAsync(tenantId, convId, userText, applyResolution: true);

        // Assert
        result.Intent.Should().Be(expectedIntent);
        result.ConfidenceScore.Should().BeGreaterThan(0.90);
        result.AutomatedReply.Should().Contain(expectedReplySubstring);
        result.CanAutoResolve.Should().BeTrue();
        result.SuggestedStatus.Should().Be(ConversationStatus.WaitingOnCustomer);
        result.DispatchedMessage.Should().NotBeNull();
        result.DispatchedMessage!.SenderType.Should().Be(MessageSenderType.Bot);

        // Verify that conversation status transitioned to WaitingOnCustomer (which triggers Pause-on-Hold)
        var updatedConv = await db.Conversations.FindAsync(convId);
        updatedConv!.Status.Should().Be(ConversationStatus.WaitingOnCustomer);
        updatedConv.PausedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task SlaTimer_ShouldPauseOnHold_WhenWaitingOnCustomer()
    {
        // Arrange
        var db = CreateDbContext();
        var service = new OmnichannelUnifiedDeskService(db);
        var tenantId = Guid.NewGuid();

        var convs = await service.GetConversationsAsync(tenantId);
        var convId = convs.First(c => c.Status == ConversationStatus.Open).ConversationId;

        // Act: Update status to WaitingOnCustomer
        var updated = await service.UpdateStatusAsync(tenantId, convId, ConversationStatus.WaitingOnCustomer);

        // Assert
        updated.Status.Should().Be(ConversationStatus.WaitingOnCustomer);
        updated.ResolutionSla.IsPaused.Should().BeTrue();
        updated.ResolutionSla.StatusBadge.Should().Contain("PAUSED");
        updated.FirstResponseSla.IsPaused.Should().BeTrue();

        var inDb = await db.Conversations.FindAsync(convId);
        inDb!.PausedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task SlaTimer_ShouldResume_WhenCustomerReplies()
    {
        // Arrange
        var db = CreateDbContext();
        var service = new OmnichannelUnifiedDeskService(db);
        var tenantId = Guid.NewGuid();

        var convs = await service.GetConversationsAsync(tenantId);
        var convId = convs.First().ConversationId;

        // Put on hold
        await service.UpdateStatusAsync(tenantId, convId, ConversationStatus.WaitingOnCustomer);

        // Customer replies
        var replyResp = await service.SendMessageAsync(tenantId, convId, new SendUnifiedDeskMessageRequest(
            Content: "Here is my updated address: 12 Rue de la Liberte, Tunis",
            SenderType: MessageSenderType.Contact
        ));

        // Assert: Customer reply automatically unpaused and reopened ticket
        replyResp.Success.Should().BeTrue();
        var inDb = await db.Conversations.FindAsync(convId);
        inDb!.Status.Should().Be(ConversationStatus.Open);
        inDb.PausedAtUtc.Should().BeNull();

        var detail = await service.GetConversationByIdAsync(tenantId, convId);
        detail!.Summary.ResolutionSla.IsPaused.Should().BeFalse();
    }

    [Fact]
    public async Task SlaOverview_ShouldAggregateBreachedAndPausedCount()
    {
        // Arrange
        var db = CreateDbContext();
        var service = new OmnichannelUnifiedDeskService(db);
        var tenantId = Guid.NewGuid();

        // Ensure seed conversations
        await service.GetConversationsAsync(tenantId);

        // Act
        var overview = await service.GetSlaOverviewAsync(tenantId);

        // Assert
        overview.TotalActiveConversations.Should().BeGreaterThanOrEqualTo(4);
        overview.PausedOnHoldCount.Should().BeGreaterThanOrEqualTo(1); // One conversation in seed data is WaitingOnCustomer
    }

    [Fact]
    public async Task GetConversations_WithChannelFilter_ShouldFilterCorrectly()
    {
        // Arrange
        var db = CreateDbContext();
        var service = new OmnichannelUnifiedDeskService(db);
        var tenantId = Guid.NewGuid();

        // Act - filter by WhatsApp
        var waList = await service.GetConversationsAsync(tenantId, channelFilter: "WhatsApp");

        // Assert
        waList.Should().NotBeEmpty();
        waList.All(c => c.Channel == "WhatsApp").Should().BeTrue();
    }
}
