using FluentAssertions;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.AI;
using SGrowing.Api.Modules.Conversations;
using SGrowing.Api.Modules.KnowledgeBase;
using SGrowing.Api.Services;
using SGrowing.Core.Entities;
using SGrowing.Core.Tenancy;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace SGrowing.Tests.AI;

public class AutonomousAiAgentTests
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
    public async Task HandleIncomingMessage_WhenAutonomousAndOrderQuery_ReturnsOrderDetails()
    {
        var db = CreateDbContext();
        var notifier = new Mock<IRealtimeNotifier>();
        var tenantId = Guid.NewGuid();

        var contact = Contact.Create(tenantId, "Marcus", "Aurelius", "marcus@rome.gov");
        db.Contacts.Add(contact);

        var conv = Conversation.Create(tenantId, Guid.NewGuid(), contact.Id);
        conv.AiMode = AiMode.Autonomous;
        db.Conversations.Add(conv);

        var deal = Deal.Create(tenantId, contact.Id, "Enterprise WhatsApp License", 7500m, DealStage.Qualified);
        db.Deals.Add(deal);
        await db.SaveChangesAsync();

        var ragService = new HelpCenterRagService(db);
        var tools = new List<IAgentTool>
        {
            new KnowledgeBaseTool(ragService),
            new OrderLookupTool(db),
            new EscalateToHumanTool(db)
        };

        var service = new AutonomousAiAgentService(db, tools, notifier.Object);

        var res = await service.HandleIncomingMessageAsync(tenantId, conv.Id, contact.Id, "Could you check my order status?");

        res.IsSuccess.Should().BeTrue();
        res.Value.Processed.Should().BeTrue();
        res.Value.ReplySent.Should().Contain("Enterprise WhatsApp License");
        res.Value.EscalatedToHuman.Should().BeFalse();

        // Verify bot message was added to conversation
        var messages = await db.Messages.Where(m => m.ConversationId == conv.Id).ToListAsync();
        messages.Should().ContainSingle();
        messages.First().SenderType.Should().Be(MessageSenderType.Bot);
    }

    [Fact]
    public async Task HandleIncomingMessage_WhenHumanRequested_EscalatesToHumanAndSwitchesAiMode()
    {
        var db = CreateDbContext();
        var notifier = new Mock<IRealtimeNotifier>();
        var tenantId = Guid.NewGuid();

        var contact = Contact.Create(tenantId, "Cleopatra", "Ptolemy", "cleo@egypt.gov");
        db.Contacts.Add(contact);

        var conv = Conversation.Create(tenantId, Guid.NewGuid(), contact.Id);
        conv.AiMode = AiMode.Autonomous;
        db.Conversations.Add(conv);
        await db.SaveChangesAsync();

        var ragService = new HelpCenterRagService(db);
        var tools = new List<IAgentTool>
        {
            new KnowledgeBaseTool(ragService),
            new OrderLookupTool(db),
            new EscalateToHumanTool(db)
        };

        var service = new AutonomousAiAgentService(db, tools, notifier.Object);

        var res = await service.HandleIncomingMessageAsync(tenantId, conv.Id, contact.Id, "I want to speak with a human agent please");

        res.IsSuccess.Should().BeTrue();
        res.Value.EscalatedToHuman.Should().BeTrue();

        var updatedConv = await db.Conversations.FindAsync(conv.Id);
        updatedConv!.AiMode.Should().Be(AiMode.Copilot);
        updatedConv.Priority.Should().Be(ConversationPriority.High);
    }

    [Fact]
    public async Task HandleIncomingMessage_WhenConversationNotFound_ReturnsFailure()
    {
        var db = CreateDbContext();
        var notifier = new Mock<IRealtimeNotifier>();
        var tenantId = Guid.NewGuid();
        var missingConvId = Guid.NewGuid();
        var contactId = Guid.NewGuid();

        var service = new AutonomousAiAgentService(db, new List<IAgentTool>(), notifier.Object);

        var res = await service.HandleIncomingMessageAsync(tenantId, missingConvId, contactId, "Hello");

        res.IsSuccess.Should().BeFalse();
        res.Error.Should().Contain("Conversation not found");
    }

    [Fact]
    public async Task HandleIncomingMessage_WhenNotAutonomousMode_ShouldNotProcess()
    {
        var db = CreateDbContext();
        var notifier = new Mock<IRealtimeNotifier>();
        var tenantId = Guid.NewGuid();

        var contact = Contact.Create(tenantId, "Seneca", "Younger", "seneca@stoic.org");
        db.Contacts.Add(contact);

        var conv = Conversation.Create(tenantId, Guid.NewGuid(), contact.Id);
        conv.AiMode = AiMode.Copilot; // Not Autonomous!
        db.Conversations.Add(conv);
        await db.SaveChangesAsync();

        var service = new AutonomousAiAgentService(db, new List<IAgentTool>(), notifier.Object);

        var res = await service.HandleIncomingMessageAsync(tenantId, conv.Id, contact.Id, "Help me");

        res.IsSuccess.Should().BeTrue();
        res.Value.Processed.Should().BeFalse();
        res.Value.ReplySent.Should().BeNull();
        res.Value.EscalatedToHuman.Should().BeFalse();
    }

    [Fact]
    public async Task HandleIncomingMessage_WhenDocumentationQuestionAnsweredByKb_ReturnsKbAnswer()
    {
        var db = CreateDbContext();
        var notifier = new Mock<IRealtimeNotifier>();
        var tenantId = Guid.NewGuid();

        var contact = Contact.Create(tenantId, "Zeno", "Citium", "zeno@stoic.org");
        db.Contacts.Add(contact);

        var conv = Conversation.Create(tenantId, Guid.NewGuid(), contact.Id);
        conv.AiMode = AiMode.Autonomous;
        db.Conversations.Add(conv);

        // Seed KB article
        var category = HelpCategory.Create(tenantId, "Security", "security");
        db.HelpCategories.Add(category);
        var article = HelpArticle.Create(
            tenantId,
            category.Id,
            "Two-Factor Authentication Setup",
            "2fa-setup",
            "To enable 2FA, open Account Settings, scan the QR code using Google Authenticator, and enter the 6-digit code."
        );
        db.HelpArticles.Add(article);
        await db.SaveChangesAsync();

        var ragService = new HelpCenterRagService(db);
        var tools = new List<IAgentTool>
        {
            new KnowledgeBaseTool(ragService),
            new OrderLookupTool(db),
            new EscalateToHumanTool(db)
        };

        var service = new AutonomousAiAgentService(db, tools, notifier.Object);

        var res = await service.HandleIncomingMessageAsync(tenantId, conv.Id, contact.Id, "How do I configure two-factor authentication?");

        res.IsSuccess.Should().BeTrue();
        res.Value.Processed.Should().BeTrue();
        res.Value.EscalatedToHuman.Should().BeFalse();
        res.Value.ReplySent.Should().Contain("Google Authenticator");
    }

    [Fact]
    public async Task HandleIncomingMessage_WhenKbReturnsIncompleteAnswer_EscalatesToHuman()
    {
        var db = CreateDbContext();
        var notifier = new Mock<IRealtimeNotifier>();
        var tenantId = Guid.NewGuid();

        var contact = Contact.Create(tenantId, "Plato", "Athens", "plato@academy.gr");
        db.Contacts.Add(contact);

        var conv = Conversation.Create(tenantId, Guid.NewGuid(), contact.Id);
        conv.AiMode = AiMode.Autonomous;
        db.Conversations.Add(conv);
        await db.SaveChangesAsync();

        var ragService = new HelpCenterRagService(db);
        var tools = new List<IAgentTool>
        {
            new KnowledgeBaseTool(ragService),
            new OrderLookupTool(db),
            new EscalateToHumanTool(db)
        };

        var service = new AutonomousAiAgentService(db, tools, notifier.Object);

        // Question with no matching KB articles in database
        var res = await service.HandleIncomingMessageAsync(tenantId, conv.Id, contact.Id, "What is the airspeed velocity of an unladen swallow?");

        res.IsSuccess.Should().BeTrue();
        res.Value.EscalatedToHuman.Should().BeTrue();
        res.Value.ReplySent.Should().Contain("transferred your question to our support team");

        var updatedConv = await db.Conversations.FindAsync(conv.Id);
        updatedConv!.AiMode.Should().Be(AiMode.Copilot);
        updatedConv.Priority.Should().Be(ConversationPriority.High);
    }

    [Fact]
    public async Task HandleIncomingMessage_ShouldRecordBotMessageInDatabaseAndNotifyRealtimeHub()
    {
        var db = CreateDbContext();
        var notifier = new Mock<IRealtimeNotifier>();
        var tenantId = Guid.NewGuid();

        var contact = Contact.Create(tenantId, "Aristotle", "Stagira", "aristotle@lyceum.gr");
        db.Contacts.Add(contact);

        var conv = Conversation.Create(tenantId, Guid.NewGuid(), contact.Id);
        conv.AiMode = AiMode.Autonomous;
        db.Conversations.Add(conv);
        await db.SaveChangesAsync();

        var service = new AutonomousAiAgentService(db, new List<IAgentTool>(), notifier.Object);

        var res = await service.HandleIncomingMessageAsync(tenantId, conv.Id, contact.Id, "Tell me about my order");

        res.IsSuccess.Should().BeTrue();
        notifier.Verify(n => n.NotifyNewMessageAsync(tenantId, It.IsAny<MessageDto>()), Times.Once);

        var dbMessages = await db.Messages.Where(m => m.ConversationId == conv.Id).ToListAsync();
        dbMessages.Should().ContainSingle();
        dbMessages[0].SenderType.Should().Be(MessageSenderType.Bot);
    }
}
