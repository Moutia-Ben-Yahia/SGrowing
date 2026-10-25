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
        tenantContext.SetTenant(Guid.NewGuid());
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
}
