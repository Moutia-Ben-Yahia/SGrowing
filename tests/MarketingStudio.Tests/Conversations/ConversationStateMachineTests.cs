using FluentAssertions;
using MarketingStudio.Api.Data;
using MarketingStudio.Api.Modules.Conversations;
using MarketingStudio.Core.Entities;
using MarketingStudio.Core.Tenancy;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MarketingStudio.Tests.Conversations;

public class ConversationStateMachineTests
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
    public async Task CreateConversation_ShouldHaveOpenStatusByDefault()
    {
        var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var inboxId = Guid.NewGuid();

        var conv = Conversation.Create(tenantId, inboxId, contactId);
        db.Conversations.Add(conv);
        await db.SaveChangesAsync();

        conv.Status.Should().Be(ConversationStatus.Open);
        conv.Priority.Should().Be(ConversationPriority.Normal);
        conv.AiMode.Should().Be(AiMode.Copilot);
    }

    [Fact]
    public async Task ChangeStatusSlice_ShouldTransitionStateCorrectly()
    {
        var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var conv = Conversation.Create(tenantId, Guid.NewGuid(), Guid.NewGuid());
        db.Conversations.Add(conv);
        await db.SaveChangesAsync();

        var handler = new ChangeStatusHandler(db);
        var resolveResult = await handler.HandleAsync(new ChangeStatusRequest(tenantId, conv.Id, ConversationStatus.Resolved));

        resolveResult.IsSuccess.Should().BeTrue();
        resolveResult.Value.Status.Should().Be(ConversationStatus.Resolved);

        var inDb = await db.Conversations.FindAsync(conv.Id);
        inDb!.Status.Should().Be(ConversationStatus.Resolved);
    }

    [Fact]
    public async Task SendMessage_ShouldUpdateConversationLastMessageAt()
    {
        var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var conv = Conversation.Create(tenantId, Guid.NewGuid(), Guid.NewGuid());
        db.Conversations.Add(conv);
        await db.SaveChangesAsync();

        var handler = new SendMessageHandler(db);
        var sendResult = await handler.HandleAsync(new SendMessageRequest(
            TenantId: tenantId,
            ConversationId: conv.Id,
            SenderType: MessageSenderType.Agent,
            SenderId: Guid.NewGuid(),
            ContentType: MessageContentType.Text,
            Content: "Hello! How can I assist you today?"
        ));

        sendResult.IsSuccess.Should().BeTrue();
        sendResult.Value.Content.Should().Be("Hello! How can I assist you today?");

        var messages = await db.Messages.Where(m => m.ConversationId == conv.Id).ToListAsync();
        messages.Should().HaveCount(1);
    }
}
