using FluentAssertions;
using SGrowing.Api.Data;
using SGrowing.Api.Hubs;
using SGrowing.Api.Modules.AI;
using SGrowing.Api.Modules.Channels;
using SGrowing.Api.Modules.Conversations;
using SGrowing.Api.Services;
using SGrowing.Core.Entities;
using SGrowing.Core.Tenancy;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace SGrowing.Tests.E2E;

public class EndToEndFlowTests
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
    public async Task CompleteCustomerLifecycle_FromInboundWhatsAppToAiCopilotApproval()
    {
        var db = CreateDbContext();
        var notifier = new Mock<IRealtimeNotifier>();
        var outbox = new OutboxService(db);
        var ingressService = new ChannelIngressService(db, outbox);
        var aiCopilot = new AiCopilotService(db, notifier.Object);
        var approveHandler = new ApproveAiDraftHandler(db, notifier.Object);
        var waAdapter = new WhatsAppCloudAdapter();

        var tenantId = Guid.NewGuid();
        var inboxId = Guid.NewGuid();

        // 1. Inbound WhatsApp Webhook payload arrives
        var metaWebhookPayload = """
        {
          "object": "whatsapp_business_account",
          "entry": [{
            "id": "ACCOUNT_123",
            "changes": [{
              "value": {
                "messaging_product": "whatsapp",
                "contacts": [{ "profile": { "name": "Marcus Aurelius" }, "wa_id": "3906123456" }],
                "messages": [{
                  "from": "3906123456",
                  "id": "wamid.XYZ987654",
                  "timestamp": "1729881000",
                  "text": { "body": "What are your customer support hours?" },
                  "type": "text"
                }]
              },
              "field": "messages"
            }]
          }]
        }
        """;

        var parsedChannelMessage = waAdapter.ParseInboundWebhook(tenantId, metaWebhookPayload);
        parsedChannelMessage.Should().NotBeNull();

        // 2. Ingest message into system
        var ingestResult = await ingressService.IngestAsync(inboxId, parsedChannelMessage!);
        ingestResult.IsSuccess.Should().BeTrue();

        var customerMessage = ingestResult.Value;
        customerMessage.SenderType.Should().Be(MessageSenderType.Contact);
        customerMessage.Content.Should().Be("What are your customer support hours?");

        // 3. Verify Contact & Conversation were created in DB
        var contact = await db.Contacts.FirstOrDefaultAsync(c => c.Phone == "3906123456");
        contact.Should().NotBeNull();
        contact!.FirstName.Should().Be("Marcus");
        contact.LastName.Should().Be("Aurelius");

        var conversation = await db.Conversations.FirstOrDefaultAsync(c => c.ContactId == contact.Id);
        conversation.Should().NotBeNull();
        conversation!.Status.Should().Be(ConversationStatus.Open);

        // 4. Verify Outbox event was recorded for zero-loss durability
        var outboxEvents = await db.OutboxMessages.Where(o => o.TenantId == tenantId).ToListAsync();
        outboxEvents.Should().HaveCount(1);
        outboxEvents[0].EventType.Should().Be("InboundMessageReceived");

        // 5. Trigger AI Copilot response generation
        var draft = await aiCopilot.GenerateDraftAsync(tenantId, conversation.Id, customerMessage.Content);
        draft.Should().NotBeNull();
        draft.Status.Should().Be(AiDraftStatus.Drafted);
        draft.SuggestedReply.Should().Contain("support team is available Monday through Friday");
        draft.ConfidenceScore.Should().BeGreaterThan(0.80f);

        // 6. Human agent reviews and approves AI draft in Shared Inbox
        var approveResult = await approveHandler.HandleAsync(new ApproveAiDraftRequest(tenantId, draft.Id));
        approveResult.IsSuccess.Should().BeTrue();

        // 7. Verify conversation messages contain both customer message and agent approved response
        var conversationMessages = await db.Messages
            .Where(m => m.ConversationId == conversation.Id)
            .OrderBy(m => m.CreatedAtUtc)
            .ToListAsync();

        conversationMessages.Should().HaveCount(2);
        conversationMessages[0].SenderType.Should().Be(MessageSenderType.Contact);
        conversationMessages[1].SenderType.Should().Be(MessageSenderType.Agent);
        conversationMessages[1].Content.Should().Be(draft.SuggestedReply);

        // 8. Agent resolves conversation
        var statusHandler = new ChangeStatusHandler(db);
        var statusResult = await statusHandler.HandleAsync(new ChangeStatusRequest(tenantId, conversation.Id, ConversationStatus.Resolved));
        statusResult.IsSuccess.Should().BeTrue();
        statusResult.Value.Status.Should().Be(ConversationStatus.Resolved);
    }
}
