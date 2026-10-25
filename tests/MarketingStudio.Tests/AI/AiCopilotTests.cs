using FluentAssertions;
using MarketingStudio.Api.Data;
using MarketingStudio.Api.Hubs;
using MarketingStudio.Api.Modules.AI;
using MarketingStudio.Api.Services;
using MarketingStudio.Core.Entities;
using MarketingStudio.Core.Tenancy;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace MarketingStudio.Tests.AI;

public class AiCopilotTests
{
    private MarketingStudioDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MarketingStudioDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(Guid.NewGuid());
        return new MarketingStudioDbContext(options, tenantContext);
    }

    [Fact]
    public async Task GenerateDraft_InCopilotMode_ShouldCreatePendingDraft()
    {
        var db = CreateDbContext();
        var notifier = new Mock<IRealtimeNotifier>();
        var tenantId = Guid.NewGuid();
        var conv = Conversation.Create(tenantId, Guid.NewGuid(), Guid.NewGuid(), AiMode.Copilot);
        db.Conversations.Add(conv);
        await db.SaveChangesAsync();

        var service = new AiCopilotService(db, notifier.Object);
        var draft = await service.GenerateDraftAsync(tenantId, conv.Id, "What are your business hours?");

        draft.Should().NotBeNull();
        draft.Status.Should().Be(AiDraftStatus.Drafted);
        draft.SuggestedReply.Should().Contain("support team");
        draft.ConfidenceScore.Should().BeGreaterThan(0.5f);

        notifier.Verify(n => n.NotifyAiDraftAsync(tenantId, It.IsAny<AiDraftDto>()), Times.Once);
    }

    [Fact]
    public async Task GenerateDraft_InAutonomousMode_WithHighConfidence_ShouldAutoSendReply()
    {
        var db = CreateDbContext();
        var notifier = new Mock<IRealtimeNotifier>();
        var tenantId = Guid.NewGuid();
        var conv = Conversation.Create(tenantId, Guid.NewGuid(), Guid.NewGuid(), AiMode.Autonomous);
        db.Conversations.Add(conv);
        await db.SaveChangesAsync();

        var service = new AiCopilotService(db, notifier.Object);
        var draft = await service.GenerateDraftAsync(tenantId, conv.Id, "pricing for pro tier");

        draft.Should().NotBeNull();
        draft.Status.Should().Be(AiDraftStatus.AutoSent);

        var messages = await db.Messages.Where(m => m.ConversationId == conv.Id).ToListAsync();
        messages.Should().HaveCount(1);
        messages[0].SenderType.Should().Be(MessageSenderType.Bot);
    }

    [Fact]
    public async Task ApproveDraft_ShouldPostMessageAndMarkAccepted()
    {
        var db = CreateDbContext();
        var notifier = new Mock<IRealtimeNotifier>();
        var tenantId = Guid.NewGuid();
        var conv = Conversation.Create(tenantId, Guid.NewGuid(), Guid.NewGuid(), AiMode.Copilot);
        db.Conversations.Add(conv);
        var draft = AiDraft.Create(tenantId, conv.Id, "Here is our onboarding guide link.", 0.78f);
        db.AiDrafts.Add(draft);
        await db.SaveChangesAsync();

        var handler = new ApproveAiDraftHandler(db, notifier.Object);
        var result = await handler.HandleAsync(new ApproveAiDraftRequest(tenantId, draft.Id));

        result.IsSuccess.Should().BeTrue();
        draft.Status.Should().Be(AiDraftStatus.Accepted);

        var messages = await db.Messages.Where(m => m.ConversationId == conv.Id).ToListAsync();
        messages.Should().HaveCount(1);
        messages[0].Content.Should().Be("Here is our onboarding guide link.");
    }
}
