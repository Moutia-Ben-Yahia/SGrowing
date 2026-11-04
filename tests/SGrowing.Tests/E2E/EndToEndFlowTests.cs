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
        // tenantContext.SetTenant(Guid.NewGuid());
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

    [Fact]
    public async Task CompleteCustomerLifecycle_WhenCustomerFollowsUpAfterResolution_CreatesNewOpenConversation()
    {
        var db = CreateDbContext();
        var outbox = new OutboxService(db);
        var ingressService = new ChannelIngressService(db, outbox);
        var waAdapter = new WhatsAppCloudAdapter();

        var tenantId = Guid.NewGuid();
        var inboxId = Guid.NewGuid();

        // 1. Initial message arrives and conversation is created & resolved
        var payload1 = """
        {
          "object": "whatsapp_business_account",
          "entry": [{
            "id": "ACCOUNT_123",
            "changes": [{
              "value": {
                "messaging_product": "whatsapp",
                "contacts": [{ "profile": { "name": "Julius Caesar" }, "wa_id": "3906999888" }],
                "messages": [{
                  "from": "3906999888",
                  "id": "wamid.STEP1",
                  "timestamp": "1729881000",
                  "text": { "body": "Hello, do you support international shipping?" },
                  "type": "text"
                }]
              },
              "field": "messages"
            }]
          }]
        }
        """;

        var parsed1 = waAdapter.ParseInboundWebhook(tenantId, payload1);
        var ingest1 = await ingressService.IngestAsync(inboxId, parsed1!);
        ingest1.IsSuccess.Should().BeTrue();

        var conv1 = await db.Conversations.FirstOrDefaultAsync(c => c.TenantId == tenantId);
        conv1.Should().NotBeNull();

        // Agent resolves conv1
        var statusHandler = new ChangeStatusHandler(db);
        var resolveRes = await statusHandler.HandleAsync(new ChangeStatusRequest(tenantId, conv1!.Id, ConversationStatus.Resolved));
        resolveRes.IsSuccess.Should().BeTrue();

        // 2. Customer follows up with a new message after resolution
        var payload2 = """
        {
          "object": "whatsapp_business_account",
          "entry": [{
            "id": "ACCOUNT_123",
            "changes": [{
              "value": {
                "messaging_product": "whatsapp",
                "contacts": [{ "profile": { "name": "Julius Caesar" }, "wa_id": "3906999888" }],
                "messages": [{
                  "from": "3906999888",
                  "id": "wamid.STEP2",
                  "timestamp": "1729882000",
                  "text": { "body": "Also, can I pay via cash on delivery in Rome?" },
                  "type": "text"
                }]
              },
              "field": "messages"
            }]
          }]
        }
        """;

        var parsed2 = waAdapter.ParseInboundWebhook(tenantId, payload2);
        var ingest2 = await ingressService.IngestAsync(inboxId, parsed2!);
        ingest2.IsSuccess.Should().BeTrue();

        // 3. Verify that a new Open conversation was created
        var allConversations = await db.Conversations
            .Where(c => c.TenantId == tenantId)
            .OrderBy(c => c.CreatedAtUtc)
            .ToListAsync();

        allConversations.Should().HaveCount(2);
        allConversations[0].Id.Should().Be(conv1.Id);
        allConversations[0].Status.Should().Be(ConversationStatus.Resolved);

        allConversations[1].Id.Should().NotBe(conv1.Id);
        allConversations[1].Status.Should().Be(ConversationStatus.Open);
        allConversations[1].ContactId.Should().Be(allConversations[0].ContactId);
    }

    [Fact]
    public async Task CompleteCustomerLifecycle_AiDraftRejection_And_StatusValidationFailures()
    {
        var db = CreateDbContext();
        var notifier = new Mock<IRealtimeNotifier>();
        var aiCopilot = new AiCopilotService(db, notifier.Object);
        var approveHandler = new ApproveAiDraftHandler(db, notifier.Object);
        var statusHandler = new ChangeStatusHandler(db);

        var tenantId = Guid.NewGuid();
        var contact = Contact.Create(tenantId, "Titus", "Livius", "titus@history.rome");
        db.Contacts.Add(contact);

        var conv = Conversation.Create(tenantId, Guid.NewGuid(), contact.Id);
        db.Conversations.Add(conv);
        await db.SaveChangesAsync();

        // 1. Approving non-existent draft fails cleanly
        var missingDraftRes = await approveHandler.HandleAsync(new ApproveAiDraftRequest(tenantId, Guid.NewGuid()));
        missingDraftRes.IsSuccess.Should().BeFalse();
        missingDraftRes.Error.Should().Be("AI Draft not found.");

        // 2. Changing status of non-existent conversation fails cleanly
        var missingConvRes = await statusHandler.HandleAsync(new ChangeStatusRequest(tenantId, Guid.NewGuid(), ConversationStatus.Snoozed));
        missingConvRes.IsSuccess.Should().BeFalse();
        missingConvRes.Error.Should().Be("Conversation not found.");

        // 3. Draft generation and idempotent single-approval check
        var draft = await aiCopilot.GenerateDraftAsync(tenantId, conv.Id, "Can you provide documentation on your enterprise security model?");
        draft.Should().NotBeNull();

        var firstApprove = await approveHandler.HandleAsync(new ApproveAiDraftRequest(tenantId, draft.Id));
        firstApprove.IsSuccess.Should().BeTrue();

        var secondApprove = await approveHandler.HandleAsync(new ApproveAiDraftRequest(tenantId, draft.Id));
        secondApprove.IsSuccess.Should().BeFalse();
        secondApprove.Error.Should().Be("Draft has already been processed.");
    }

    [Fact]
    public async Task CompleteCustomerLifecycle_EmailChannelIngress_And_MultiTenantIsolation()
    {
        var db = CreateDbContext();
        var outbox = new OutboxService(db);
        var ingressService = new ChannelIngressService(db, outbox);
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var inboxA = Guid.NewGuid();

        var emailMsgA = new SGrowing.Core.Channels.InboundChannelMessage(
            TenantId: tenantA,
            ChannelType: "email",
            ExternalSenderId: "alexander@macedon.org",
            SenderName: "Alexander TheGreat",
            ExternalMessageId: "msg-email-001",
            ContentType: "text/plain",
            Content: "We would like to integrate SGrowing into our logistics fleet."
        );

        var resultA = await ingressService.IngestAsync(inboxA, emailMsgA);
        resultA.IsSuccess.Should().BeTrue();
        resultA.Value.SenderType.Should().Be(MessageSenderType.Contact);

        var contactA = await db.Contacts.FirstOrDefaultAsync(c => c.TenantId == tenantA && c.Email == "alexander@macedon.org");
        contactA.Should().NotBeNull();
        contactA!.FirstName.Should().Be("Alexander");
        contactA.LastName.Should().Be("TheGreat");

        // Verify Tenant B cannot see Tenant A's contact or outbox messages
        var contactB = await db.Contacts.FirstOrDefaultAsync(c => c.TenantId == tenantB && c.Email == "alexander@macedon.org");
        contactB.Should().BeNull();

        var outboxA = await db.OutboxMessages.Where(o => o.TenantId == tenantA).ToListAsync();
        outboxA.Should().HaveCount(1);

        var outboxB = await db.OutboxMessages.Where(o => o.TenantId == tenantB).ToListAsync();
        outboxB.Should().BeEmpty();
    }

    [Fact]
    public async Task CompleteCustomerLifecycle_SmsChannelIngress_And_ConversationReopeningLifecycle()
    {
        var db = CreateDbContext();
        var outbox = new OutboxService(db);
        var ingressService = new ChannelIngressService(db, outbox);
        var statusHandler = new ChangeStatusHandler(db);
        var tenantId = Guid.NewGuid();
        var inboxId = Guid.NewGuid();

        // 1. Ingest WhatsApp/SMS inbound message
        var msg1 = new SGrowing.Core.Channels.InboundChannelMessage(
            TenantId: tenantId,
            ChannelType: "whatsapp",
            ExternalSenderId: "+213555123456",
            SenderName: "Khadija Benali",
            ExternalMessageId: "wa-inbound-101",
            ContentType: "text/plain",
            Content: "Hello, I want to track my pending order #DZ-8899."
        );

        var ingest1 = await ingressService.IngestAsync(inboxId, msg1);
        ingest1.IsSuccess.Should().BeTrue();
        ingest1.Value.SenderType.Should().Be(MessageSenderType.Contact);

        var contact = await db.Contacts.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Phone == "+213555123456");
        contact.Should().NotBeNull();
        contact!.FirstName.Should().Be("Khadija");
        contact.LastName.Should().Be("Benali");

        var conv1 = await db.Conversations.FirstOrDefaultAsync(c => c.ContactId == contact.Id);
        conv1.Should().NotBeNull();
        conv1!.Status.Should().Be(ConversationStatus.Open);

        // 2. Resolve initial conversation
        var resolveRes = await statusHandler.HandleAsync(new ChangeStatusRequest(tenantId, conv1.Id, ConversationStatus.Resolved));
        resolveRes.IsSuccess.Should().BeTrue();
        var conv1Fresh = await db.Conversations.FindAsync(conv1.Id);
        conv1Fresh!.Status.Should().Be(ConversationStatus.Resolved);

        // 3. Second message from same contact creates a new conversation linking to the same Contact entity
        var msg2 = new SGrowing.Core.Channels.InboundChannelMessage(
            TenantId: tenantId,
            ChannelType: "whatsapp",
            ExternalSenderId: "+213555123456",
            SenderName: "Khadija Benali",
            ExternalMessageId: "wa-inbound-102",
            ContentType: "text/plain",
            Content: "My package arrived safely. Thank you so much!"
        );

        var ingest2 = await ingressService.IngestAsync(inboxId, msg2);
        ingest2.IsSuccess.Should().BeTrue();

        var allConversations = await db.Conversations
            .Where(c => c.ContactId == contact.Id)
            .OrderBy(c => c.CreatedAtUtc)
            .ToListAsync();

        allConversations.Should().HaveCount(2);
        allConversations[0].Status.Should().Be(ConversationStatus.Resolved);
        allConversations[1].Status.Should().Be(ConversationStatus.Open);

        var outboxEvents = await db.OutboxMessages.Where(o => o.TenantId == tenantId).ToListAsync();
        outboxEvents.Should().HaveCount(2);
    }
}
