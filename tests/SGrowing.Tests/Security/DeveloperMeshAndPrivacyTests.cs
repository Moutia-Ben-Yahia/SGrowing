using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Security;
using SGrowing.Core.Entities;
using SGrowing.Worker.Services;
using Xunit;

namespace SGrowing.Tests.Security;

public class DeveloperMeshAndPrivacyTests
{
    private readonly SGrowingDbContext _db;
    private readonly OutboundWebhookDispatcherService _dispatcher;
    private readonly CustomerPrivacyService _privacyService;

    public DeveloperMeshAndPrivacyTests()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new SGrowingDbContext(options);
        _dispatcher = new OutboundWebhookDispatcherService(_db);
        _privacyService = new CustomerPrivacyService(_db);
    }

    [Fact]
    public async Task OutboundWebhooks_MatchesSubscribersAndLogsDeliveriesWithHmac()
    {
        var tenantId = Guid.NewGuid();

        // 1. All events subscriber
        var sub1 = WebhookSubscription.Create(tenantId, "https://hooks.zapier.com/sgrowing/all", "secret_zapier_1", "*");
        // 2. Specific event subscriber
        var sub2 = WebhookSubscription.Create(tenantId, "https://api.n8n.cloud/webhook/deals", "secret_n8n_2", "DealWonEvent,OrderPaidEvent");
        // 3. Different event subscriber (should not trigger)
        var sub3 = WebhookSubscription.Create(tenantId, "https://hooks.slack.com/services/voice", "secret_slack_3", "VoiceCallCompleted");

        await _db.WebhookSubscriptions.AddRangeAsync(sub1, sub2, sub3);
        await _db.SaveChangesAsync();

        var payload = "{\"dealId\":\"12345\",\"amount\":50000}";
        var dispatchedCount = await _dispatcher.DispatchOutboundWebhooksAsync(tenantId, "DealWonEvent", payload);

        Assert.Equal(2, dispatchedCount); // sub1 and sub2

        var logs = await _db.WebhookDeliveryLogs.ToListAsync();
        Assert.Equal(2, logs.Count);
        Assert.All(logs, l => Assert.True(l.IsSuccess));

        var sig = OutboundWebhookDispatcherService.ComputeHmacSignature(payload, "secret_zapier_1");
        Assert.StartsWith("sha256=", sig);
    }

    [Fact]
    public async Task CustomerPrivacy_ExecutesGdprRightToBeForgotten()
    {
        var tenantId = Guid.NewGuid();
        var inboxId = Guid.NewGuid();

        // 1. Contact with PII
        var targetEmail = "gdpr.subject@bigcorp.com";
        var contact = Contact.Create(tenantId, "Johnathan", "Doe", targetEmail, "+15559998888");
        await _db.Contacts.AddAsync(contact);

        // 2. Conversation & Messages
        var conversation = Conversation.Create(tenantId, inboxId, contact.Id);
        await _db.Conversations.AddAsync(conversation);

        var message = Message.Create(tenantId, conversation.Id, MessageSenderType.Contact, "My confidential tax ID is 998-22-1111 and email is gdpr.subject@bigcorp.com");
        await _db.Messages.AddAsync(message);

        // 3. Vector RAG chunk containing PII
        var chunk = DocumentChunk.Create(tenantId, Guid.NewGuid(), "Customer Note", 0, "Lead details: gdpr.subject@bigcorp.com requested NDA");
        await _db.DocumentChunks.AddAsync(chunk);

        await _db.SaveChangesAsync();

        // Execute Right to be Forgotten
        var result = await _privacyService.ExecuteRightToBeForgottenAsync(tenantId, targetEmail);

        Assert.True(result.Success);
        Assert.Equal(1, result.ErasedContactsCount);
        Assert.Equal(1, result.RedactedMessagesCount);
        Assert.Equal(1, result.RemovedRagChunksCount);

        // Verify Contact is anonymized
        var refreshedContact = await _db.Contacts.FindAsync(contact.Id);
        Assert.Equal("Anonymized", refreshedContact!.FirstName);
        Assert.Null(refreshedContact.Phone);
        Assert.Contains("anonymized_", refreshedContact.Email);

        // Verify Message text is redacted
        var refreshedMessage = await _db.Messages.FindAsync(message.Id);
        Assert.Contains("Personal Data Anonymized", refreshedMessage!.Content);

        // Verify RAG chunk was completely removed
        var refreshedChunk = await _db.DocumentChunks.FindAsync(chunk.Id);
        Assert.Null(refreshedChunk);

        // Verify Request record
        var request = await _db.PrivacyErasureRequests.FindAsync(result.RequestId);
        Assert.Equal("Completed", request!.Status);
        Assert.Equal(3, request.ErasedRecordsCount);
    }
}
