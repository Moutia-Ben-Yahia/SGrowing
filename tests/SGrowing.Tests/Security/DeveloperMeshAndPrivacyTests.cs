using System;
using System.Threading.Tasks;
using FluentAssertions;
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

        dispatchedCount.Should().Be(2); // sub1 and sub2

        var logs = await _db.WebhookDeliveryLogs.ToListAsync();
        logs.Should().HaveCount(2);
        logs.Should().AllSatisfy(l => l.IsSuccess.Should().BeTrue());

        var sig = OutboundWebhookDispatcherService.ComputeHmacSignature(payload, "secret_zapier_1");
        sig.Should().StartWith("sha256=");
    }

    [Fact]
    public void OutboundWebhooks_ComputeHmacSignature_ProducesDeterministicSha256()
    {
        // Arrange
        var payload = "{\"event\":\"order.created\",\"id\":\"ord_123\"}";
        var secret = "super_secret_signing_key_42";

        // Act
        var sig1 = OutboundWebhookDispatcherService.ComputeHmacSignature(payload, secret);
        var sig2 = OutboundWebhookDispatcherService.ComputeHmacSignature(payload, secret);

        // Assert
        sig1.Should().Be(sig2);
        sig1.Should().StartWith("sha256=");
    }

    [Fact]
    public async Task OutboundWebhooks_RespectsTenantIsolation_DoesNotDispatchToOtherTenantEndpoints()
    {
        // Arrange
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var subA = WebhookSubscription.Create(tenantA, "https://tenant-a.com/webhook", "secret_a", "*");
        var subB = WebhookSubscription.Create(tenantB, "https://tenant-b.com/webhook", "secret_b", "*");
        await _db.WebhookSubscriptions.AddRangeAsync(subA, subB);
        await _db.SaveChangesAsync();

        // Act
        var count = await _dispatcher.DispatchOutboundWebhooksAsync(tenantA, "OrderCreated", "{}");

        // Assert
        count.Should().Be(1);
        var logs = await _db.WebhookDeliveryLogs.ToListAsync();
        logs.Should().ContainSingle();
        logs[0].SubscriptionId.Should().Be(subA.Id);
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

        result.Success.Should().BeTrue();
        result.ErasedContactsCount.Should().Be(1);
        result.RedactedMessagesCount.Should().Be(1);
        result.RemovedRagChunksCount.Should().Be(1);

        // Verify Contact is anonymized
        var refreshedContact = await _db.Contacts.FindAsync(contact.Id);
        refreshedContact!.FirstName.Should().Be("Anonymized");
        refreshedContact.Phone.Should().BeNull();
        refreshedContact.Email.Should().Contain("anonymized_");

        // Verify Message text is redacted
        var refreshedMessage = await _db.Messages.FindAsync(message.Id);
        refreshedMessage!.Content.Should().Contain("Personal Data Anonymized");

        // Verify RAG chunk was completely removed
        var refreshedChunk = await _db.DocumentChunks.FindAsync(chunk.Id);
        refreshedChunk.Should().BeNull();

        // Verify Request record
        var request = await _db.PrivacyErasureRequests.FindAsync(result.RequestId);
        request!.Status.Should().Be("Completed");
        request.ErasedRecordsCount.Should().Be(3);
    }

    [Fact]
    public async Task CustomerPrivacy_RightToBeForgotten_RemovesMultipleRagChunksReferencingEmail()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var targetEmail = "pii.customer@multichunk.com";

        var c1 = DocumentChunk.Create(tenantId, Guid.NewGuid(), "Doc 1", 0, "Contains email pii.customer@multichunk.com in section 1");
        var c2 = DocumentChunk.Create(tenantId, Guid.NewGuid(), "Doc 2", 0, "Also cites pii.customer@multichunk.com in section 2");
        var c3 = DocumentChunk.Create(tenantId, Guid.NewGuid(), "Doc 3", 0, "Unrelated chunk with general information");

        await _db.DocumentChunks.AddRangeAsync(c1, c2, c3);
        await _db.SaveChangesAsync();

        // Act
        var result = await _privacyService.ExecuteRightToBeForgottenAsync(tenantId, targetEmail);

        // Assert
        result.RemovedRagChunksCount.Should().Be(2);
        var remaining = await _db.DocumentChunks.Where(c => c.TenantId == tenantId).ToListAsync();
        remaining.Should().ContainSingle();
        remaining[0].Id.Should().Be(c3.Id);
    }

    [Fact]
    public async Task CustomerPrivacy_RightToBeForgotten_PreservesNonMatchingContactsInSameTenant()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var targetEmail = "remove.me@domain.com";
        var keepEmail = "keep.me@domain.com";

        var cTarget = Contact.Create(tenantId, "Target", "Person", targetEmail, "+111");
        var cKeep = Contact.Create(tenantId, "Keep", "Person", keepEmail, "+222");
        await _db.Contacts.AddRangeAsync(cTarget, cKeep);
        await _db.SaveChangesAsync();

        // Act
        var result = await _privacyService.ExecuteRightToBeForgottenAsync(tenantId, targetEmail);

        // Assert
        result.ErasedContactsCount.Should().Be(1);

        var refreshedKeep = await _db.Contacts.FindAsync(cKeep.Id);
        refreshedKeep!.FirstName.Should().Be("Keep");
        refreshedKeep.Email.Should().Be(keepEmail);
        refreshedKeep.Phone.Should().Be("+222");
    }

    [Fact]
    public async Task OutboundWebhooks_ReturnsZero_WhenNoSubscriptionsExist()
    {
        var tenantId = Guid.NewGuid();
        var dispatched = await _dispatcher.DispatchOutboundWebhooksAsync(tenantId, "OrderCreated", "{}");
        dispatched.Should().Be(0);
    }

    [Fact]
    public async Task CustomerPrivacy_ExecutesCleanlyWhenNoMatchingContactFound()
    {
        var tenantId = Guid.NewGuid();
        var result = await _privacyService.ExecuteRightToBeForgottenAsync(tenantId, "unknown.user.99@test.com");

        result.Success.Should().BeTrue();
        result.ErasedContactsCount.Should().Be(0);
        result.RedactedMessagesCount.Should().Be(0);
        result.RemovedRagChunksCount.Should().Be(0);
    }
}
