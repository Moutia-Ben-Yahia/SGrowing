using FluentAssertions;
using SGrowing.Api.Data;
using SGrowing.Api.Hubs;
using SGrowing.Api.Modules.AI;
using SGrowing.Api.Services;
using SGrowing.Core.Entities;
using SGrowing.Core.Tenancy;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace SGrowing.Tests.AI;

public class AiCopilotTests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var tenantContext = new TenantContext();
        // tenantContext.SetTenant(Guid.NewGuid());
        return new SGrowingDbContext(options, tenantContext);
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

    [Fact]
    public async Task AcceptDraft_WithEditedReply_ShouldPostEditedContentAndMarkAccepted()
    {
        var db = CreateDbContext();
        var notifier = new Mock<IRealtimeNotifier>();
        var tenantId = Guid.NewGuid();
        var conv = Conversation.Create(tenantId, Guid.NewGuid(), Guid.NewGuid(), AiMode.Copilot);
        db.Conversations.Add(conv);
        var draft = AiDraft.Create(tenantId, conv.Id, "Original AI suggested reply", 0.82f);
        db.AiDrafts.Add(draft);
        await db.SaveChangesAsync();

        var service = new AiCopilotService(db, notifier.Object);
        var messageDto = await service.AcceptDraftAsync(tenantId, draft.Id, "Human edited customized response.");

        messageDto.Should().NotBeNull();
        messageDto!.Content.Should().Be("Human edited customized response.");
        draft.Status.Should().Be(AiDraftStatus.Accepted);

        var savedMsg = await db.Messages.FirstOrDefaultAsync(m => m.Id == messageDto.Id);
        savedMsg.Should().NotBeNull();
        savedMsg!.Content.Should().Be("Human edited customized response.");
        savedMsg.SenderType.Should().Be(MessageSenderType.Agent);
    }

    [Fact]
    public async Task RejectDraft_ShouldMarkStatusAsRejected()
    {
        var db = CreateDbContext();
        var notifier = new Mock<IRealtimeNotifier>();
        var tenantId = Guid.NewGuid();
        var conv = Conversation.Create(tenantId, Guid.NewGuid(), Guid.NewGuid(), AiMode.Copilot);
        db.Conversations.Add(conv);
        var draft = AiDraft.Create(tenantId, conv.Id, "Unsuitable suggestion", 0.65f);
        db.AiDrafts.Add(draft);
        await db.SaveChangesAsync();

        var service = new AiCopilotService(db, notifier.Object);
        var rejected = await service.RejectDraftAsync(tenantId, draft.Id, "Not relevant to lead");

        rejected.Should().BeTrue();
        draft.Status.Should().Be(AiDraftStatus.Rejected);

        // Rejecting non-existent draft returns false
        var notFound = await service.RejectDraftAsync(tenantId, Guid.NewGuid());
        notFound.Should().BeFalse();
    }

    [Fact]
    public async Task GetDrafts_ShouldReturnPagedDraftsForConversation()
    {
        var db = CreateDbContext();
        var notifier = new Mock<IRealtimeNotifier>();
        var tenantId = Guid.NewGuid();
        var conv = Conversation.Create(tenantId, Guid.NewGuid(), Guid.NewGuid(), AiMode.Copilot);
        db.Conversations.Add(conv);

        var d1 = AiDraft.Create(tenantId, conv.Id, "Draft 1", 0.70f);
        var d2 = AiDraft.Create(tenantId, conv.Id, "Draft 2", 0.85f);
        db.AiDrafts.AddRange(d1, d2);
        await db.SaveChangesAsync();

        var service = new AiCopilotService(db, notifier.Object);
        var drafts = (await service.GetDraftsAsync(tenantId, conv.Id)).ToList();

        drafts.Should().HaveCount(2);
        drafts.Should().Contain(d => d.SuggestedReply == "Draft 1");
        drafts.Should().Contain(d => d.SuggestedReply == "Draft 2");
    }

    [Fact]
    public async Task GenerateDraft_NonExistentConversation_ThrowsInvalidOperationException()
    {
        var db = CreateDbContext();
        var notifier = new Mock<IRealtimeNotifier>();
        var tenantId = Guid.NewGuid();
        var nonExistentConvId = Guid.NewGuid();

        var service = new AiCopilotService(db, notifier.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GenerateDraftAsync(tenantId, nonExistentConvId, "Hello"));
    }
}
