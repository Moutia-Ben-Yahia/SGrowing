using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SGrowing.Api.Data;
using SGrowing.Api.Hubs;
using SGrowing.Api.Modules.Agents.Git;
using SGrowing.Api.Modules.AI;
using SGrowing.Api.Modules.AI.Personas;
using SGrowing.Api.Modules.Auth;
using SGrowing.Api.Modules.Billing.Invoicing;
using SGrowing.Api.Modules.Conversations;
using SGrowing.Api.Services;
using SGrowing.Core.Entities;
using SGrowing.Core.Messaging;
using SGrowing.Core.Spatial;
using SGrowing.Worker.BackgroundJobs;
using Xunit;

namespace SGrowing.Tests;

public class MockRealtimeNotifier : IRealtimeNotifier
{
    public List<MessageDto> DispatchedMessages { get; } = new();
    public List<ConversationDto> UpdatedConversations { get; } = new();
    public List<AiDraftDto> GeneratedDrafts { get; } = new();

    public Task NotifyNewMessageAsync(Guid tenantId, MessageDto message)
    {
        DispatchedMessages.Add(message);
        return Task.CompletedTask;
    }

    public Task NotifyConversationUpdatedAsync(Guid tenantId, ConversationDto conversation)
    {
        UpdatedConversations.Add(conversation);
        return Task.CompletedTask;
    }

    public Task NotifyAiDraftAsync(Guid tenantId, AiDraftDto draft)
    {
        GeneratedDrafts.Add(draft);
        return Task.CompletedTask;
    }
}

public class MockRedisStreamBroker : IRedisStreamBroker
{
    public List<(string streamKey, string eventType, string payloadJson, Guid tenantId)> PublishedEvents { get; } = new();
    public bool ShouldThrow { get; set; } = false;

    public Task<string> PublishEventAsync(string streamKey, string eventType, string payloadJson, Guid tenantId)
    {
        if (ShouldThrow) throw new InvalidOperationException("Simulated Redis stream broker failure");
        PublishedEvents.Add((streamKey, eventType, payloadJson, tenantId));
        return Task.FromResult($"msg-{PublishedEvents.Count}");
    }

    public Task EnsureConsumerGroupAsync(string streamKey, string groupName) => Task.CompletedTask;
    public Task<List<StreamMessageEnvelope>> ReadGroupMessagesAsync(string streamKey, string groupName, string consumerName, int count = 10) =>
        Task.FromResult(new List<StreamMessageEnvelope>());
    public Task AcknowledgeAsync(string streamKey, string groupName, string messageId) => Task.CompletedTask;
    public bool IsConnected => true;
}

public class EnterpriseHorizonTests
{
    private SGrowingDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SGrowingDbContext(options);
    }

    // =========================================================================
    // 1. Inbound WhatsApp Message Router & Thread Mesh
    // =========================================================================

    [Fact]
    public async Task InboundMessageRouter_CreatesContactInboxConversationAndDispatchesRealtime()
    {
        using var db = CreateInMemoryDb();
        var notifier = new MockRealtimeNotifier();
        var copilot = new AiCopilotService(db, notifier);
        var router = new InboundMessageRouterService(db, notifier, copilot);

        var tenantId = Guid.NewGuid();
        var payload = new InboundMessagePayload(
            TenantId: tenantId,
            PhoneNumber: "+216 98 123 456",
            SenderName: "Yassine Mansour",
            Content: "Hello, what is your pricing for enterprise SaaS?"
        );

        var result = await router.RouteInboundMessageAsync(payload);

        Assert.NotNull(result);
        Assert.Equal("Routed", result.Status);
        Assert.True(result.IsNewConversation);
        Assert.NotNull(result.AiSuggestedReply);
        Assert.Contains("Pro tier starts at $97", result.AiSuggestedReply);

        // Verify Contact created
        var contact = await db.Contacts.FirstOrDefaultAsync(c => c.Id == result.ContactId);
        Assert.NotNull(contact);
        Assert.Equal("Yassine", contact.FirstName);
        Assert.Equal("Mansour", contact.LastName);
        Assert.Equal("+21698123456", contact.Phone);

        // Verify Message saved
        var message = await db.Messages.FirstOrDefaultAsync(m => m.Id == result.MessageId);
        Assert.NotNull(message);
        Assert.Equal(MessageSenderType.Contact, message.SenderType);
        Assert.Equal(DeliveryStatus.Delivered, message.DeliveryStatus);

        // Verify Realtime notification
        Assert.Single(notifier.DispatchedMessages);
        Assert.Single(notifier.UpdatedConversations);
    }

    // =========================================================================
    // 2. In-Thread AI Suggested Replies (Accept / Reject)
    // =========================================================================

    [Fact]
    public async Task AiCopilot_GenerateAndAcceptDraft_ConvertsToAgentMessageAndDispatches()
    {
        using var db = CreateInMemoryDb();
        var notifier = new MockRealtimeNotifier();
        var copilot = new AiCopilotService(db, notifier);

        var tenantId = Guid.NewGuid();
        var contact = Contact.Create(tenantId, "Amira", "Ben Ali", phone: "+21620111222");
        db.Contacts.Add(contact);
        var inbox = new Inbox { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Support" };
        db.Inboxes.Add(inbox);
        var conv = Conversation.Create(tenantId, inbox.Id, contact.Id, AiMode.Copilot);
        db.Conversations.Add(conv);
        await db.SaveChangesAsync();

        // 1. Generate Draft
        var draft = await copilot.GenerateDraftAsync(tenantId, conv.Id, "What are your support open hours?");
        Assert.NotNull(draft);
        Assert.Equal(AiDraftStatus.Drafted, draft.Status);
        Assert.Contains("Monday through Friday", draft.SuggestedReply);

        // 2. Accept Draft with slight edit
        var editedReply = "Our support is available Monday through Friday from 9 AM to 6 PM EST. Can I help with anything else?";
        var acceptedMessage = await copilot.AcceptDraftAsync(tenantId, draft.Id, editedReply);

        Assert.NotNull(acceptedMessage);
        Assert.Equal(MessageSenderType.Agent, acceptedMessage.SenderType);
        Assert.Equal(editedReply, acceptedMessage.Content);

        // Verify Draft updated to Accepted
        var updatedDraft = await db.AiDrafts.FirstOrDefaultAsync(d => d.Id == draft.Id);
        Assert.Equal(AiDraftStatus.Accepted, updatedDraft!.Status);

        // Verify Message persisted
        var savedMsg = await db.Messages.FirstOrDefaultAsync(m => m.Id == acceptedMessage.Id);
        Assert.NotNull(savedMsg);
        Assert.Equal(editedReply, savedMsg.Content);
    }

    [Fact]
    public async Task AiCopilot_RejectDraft_MarksDraftAsRejected()
    {
        using var db = CreateInMemoryDb();
        var notifier = new MockRealtimeNotifier();
        var copilot = new AiCopilotService(db, notifier);

        var tenantId = Guid.NewGuid();
        var contact = Contact.Create(tenantId, "Karim", "Zouari");
        db.Contacts.Add(contact);
        var inbox = new Inbox { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Sales" };
        db.Inboxes.Add(inbox);
        var conv = Conversation.Create(tenantId, inbox.Id, contact.Id, AiMode.Copilot);
        db.Conversations.Add(conv);
        await db.SaveChangesAsync();

        var draft = await copilot.GenerateDraftAsync(tenantId, conv.Id, "Tell me about pricing");
        var rejected = await copilot.RejectDraftAsync(tenantId, draft.Id, "Not applicable for this enterprise deal");

        Assert.True(rejected);
        var updatedDraft = await db.AiDrafts.FirstOrDefaultAsync(d => d.Id == draft.Id);
        Assert.Equal(AiDraftStatus.Rejected, updatedDraft!.Status);
    }

    // =========================================================================
    // 3. Multi-Currency Enterprise Invoicing & Tax Ledger
    // =========================================================================

    [Fact]
    public async Task Invoicing_CreateInvoiceFromDeal_ComputesAccurateFxAndVatTax()
    {
        using var db = CreateInMemoryDb();
        var invoiceService = new InvoiceService(db);
        var tenantId = Guid.NewGuid();

        var contact = Contact.Create(tenantId, "Tariq", "Gharbi", email: "tariq@enterprise.tn");
        db.Contacts.Add(contact);

        var deal = Deal.Create(
            tenantId: tenantId,
            contactId: contact.Id,
            title: "Omnichannel License Deal",
            amount: 5000.00m, // $5,000 USD Base
            stage: DealStage.Won
        );
        db.Deals.Add(deal);
        await db.SaveChangesAsync();

        // Generate Invoice in Tunisian Dinars (TND rate: 3.12, VAT: 19%)
        var invoice = await invoiceService.CreateInvoiceFromDealAsync(tenantId, deal.Id, currency: "TND", taxPercentage: 19.0m);

        Assert.NotNull(invoice);
        Assert.StartsWith("INV-", invoice.InvoiceNumber);
        Assert.Equal("TND", invoice.Currency);
        Assert.Equal(3.12m, invoice.ExchangeRate);

        // Subtotal: 5000 * 3.12 = 15,600.00 TND
        Assert.Equal(15600.00m, invoice.SubTotal);
        // Tax: 15600 * 0.19 = 2,964.00 TND
        Assert.Equal(2964.00m, invoice.TaxAmount);
        // Total: 18,564.00 TND
        Assert.Equal(18564.00m, invoice.TotalAmount);
        Assert.Single(invoice.LineItems);
        Assert.Equal(InvoiceStatus.Draft, invoice.Status);

        // Record Payment
        var paymentReq = new RecordPaymentRequest(Amount: 18564.00m, PaymentMethod: "BankTransfer");
        var payment = await invoiceService.RecordPaymentAsync(tenantId, invoice.Id, paymentReq);

        Assert.NotNull(payment);
        Assert.Equal("Success", payment.Status);

        var updatedInvoice = await invoiceService.GetInvoiceByIdAsync(tenantId, invoice.Id);
        Assert.Equal(InvoiceStatus.Paid, updatedInvoice!.Status);
        Assert.NotNull(updatedInvoice.PaidAtUtc);
    }

    [Fact]
    public async Task Invoicing_ConvertCurrency_PerformsAccurateTwoWayConversion()
    {
        using var db = CreateInMemoryDb();
        var invoiceService = new InvoiceService(db);

        // Convert $1,000 USD to Moroccan Dirham (MAD rate: 10.15)
        var result = await invoiceService.ConvertCurrencyAsync(1000m, "USD", "MAD");
        Assert.Equal(10150.00m, result.TargetAmount);
        Assert.Equal("MAD", result.ToCurrency);

        // Convert 3,120 TND back to USD (TND rate: 3.12 -> $1,000 USD)
        var reverse = await invoiceService.ConvertCurrencyAsync(3120m, "TND", "USD");
        Assert.Equal(1000.00m, reverse.TargetAmount);
    }

    // =========================================================================
    // 4. Git-Native Funnel Drift & PR Maintenance
    // =========================================================================

    [Fact]
    public async Task GitHubAutomation_DetectsFunnelDriftAndCreatesPullRequest()
    {
        using var db = CreateInMemoryDb();
        var gitService = new GitHubAutomationService(db);
        var tenantId = Guid.NewGuid();

        var funnelRun = new AutonomousFunnelRunRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ConceptPrompt = "Autonomous B2B Lead Funnel for MENA Logistics",
            GeneratedHeadline = "Scale your freight operations with AI workflows",
            ValueProposition = "Autonomous logistics booking platform",
            TargetSku = "LOGISTICS-PRO",
            PerformanceScore = 95
        };
        db.AutonomousFunnelRuns.Add(funnelRun);
        await db.SaveChangesAsync();

        // 1. Detect Drift
        var drift = await gitService.DetectFunnelDriftAsync(tenantId, funnelRun.Id);
        Assert.NotNull(drift);
        Assert.True(drift.HasDrift);
        Assert.True(drift.DriftScore > 0);
        Assert.Contains(drift.DriftItems, d => d.Field == "ProductPricing");
        Assert.Contains(drift.DriftItems, d => d.Field == "LandingPageHeroHeadline");

        // 2. Create PR
        var prReq = new CreateFunnelPrRequest(
            BaseBranch: "main",
            ReviewerPersonas: new List<string> { "Conversion Specialist", "Security Auditor" }
        );
        var pr = await gitService.CreatePullRequestForFunnelDriftAsync(tenantId, funnelRun.Id, prReq);

        Assert.NotNull(pr);
        Assert.Equal("Created", pr.Status);
        Assert.StartsWith("funnel/drift-patch-", pr.BranchName);
        Assert.Contains("Autonomous Funnel Drift Sync PR", pr.Body);
        Assert.Contains("conversion-specialist", pr.Body);
        Assert.Contains("Strict Left-to-Right LTR layout enforced", pr.Body);
    }

    // =========================================================================
    // 5. Token Revocation Blocklist Service
    // =========================================================================

    [Fact]
    public async Task TokenRevocation_RevokesJtiAndRejectsRevokedTokens()
    {
        var revocationService = new TokenRevocationService(NullLogger<TokenRevocationService>.Instance);
        var jti = Guid.NewGuid().ToString("N");

        Assert.False(await revocationService.IsTokenRevokedAsync(jti));

        await revocationService.RevokeTokenAsync(jti, TimeSpan.FromHours(2), "User logout");

        Assert.True(await revocationService.IsTokenRevokedAsync(jti));
        Assert.True(await revocationService.GetRevokedCountAsync() >= 1);

        // Different JTI is still valid
        Assert.False(await revocationService.IsTokenRevokedAsync(Guid.NewGuid().ToString("N")));
    }

    // =========================================================================
    // 6. Native PostGIS GiST Spatial Expressions & Geodesic Helper
    // =========================================================================

    [Fact]
    public void PostGisSpatialHelper_ComputesBoundingBoxAndAntimeridianCrossing()
    {
        // Tunis location (36.8065, 10.1815) with 50km radius
        var box = PostGisSpatialHelper.CreateBoundingBoxEnvelope(36.8065, 10.1815, 50.0);
        Assert.False(box.CrossesAntimeridian);
        Assert.True(box.MinLatitude < 36.8065 && box.MaxLatitude > 36.8065);
        Assert.True(box.MinLongitude < 10.1815 && box.MaxLongitude > 10.1815);

        // Test in-memory point filtering
        Assert.True(PostGisSpatialHelper.IsPointWithinBox(36.8065, 10.1815, box));
        Assert.False(PostGisSpatialHelper.IsPointWithinBox(48.8566, 2.3522, box)); // Paris is outside

        // Test Geodesic Haversine Distance
        // Tunis to Carthage is ~14.5km
        var dist = PostGisSpatialHelper.CalculateHaversineKm(36.8065, 10.1815, 36.8530, 10.3230);
        Assert.InRange(dist, 13.0, 16.0);

        // Test SQL generation
        var dwithinSql = PostGisSpatialHelper.BuildPostGisDWithinSql("Location", 36.8065, 10.1815, 50000);
        Assert.Contains("ST_DWithin(Location", dwithinSql);
        Assert.Contains("geography", dwithinSql);
    }

    // =========================================================================
    // 7. Transactional Outbox Publisher Job with Exponential Backoff & DLQ
    // =========================================================================

    [Fact]
    public async Task OutboxPublisherJob_ProcessesPendingMessages_HandlesFailuresAndDeadLetters()
    {
        var services = new ServiceCollection();
        var dbName = Guid.NewGuid().ToString();
        services.AddDbContext<SGrowingDbContext>(o => o.UseInMemoryDatabase(dbName));
        var broker = new MockRedisStreamBroker();
        services.AddSingleton<IRedisStreamBroker>(broker);
        var provider = services.BuildServiceProvider();

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SGrowingDbContext>();
            var tenantId = Guid.NewGuid();

            // Message 1: Normal pending
            db.OutboxMessages.Add(OutboxMessage.Create(tenantId, "OrderCreated", "{\"orderId\":\"101\"}"));

            // Message 2: Already exceeded max retries (DLQ candidate)
            var dlqMsg = OutboxMessage.Create(tenantId, "InvoicePaid", "{\"invoiceId\":\"999\"}");
            for (int i = 0; i < OutboxPublisherJob.MaxRetries; i++)
            {
                dlqMsg.MarkFailed($"Error attempt {i + 1}");
            }
            db.OutboxMessages.Add(dlqMsg);

            await db.SaveChangesAsync();
        }

        var job = new OutboxPublisherJob(provider, NullLogger<OutboxPublisherJob>.Instance);
        var processed = await job.ProcessPendingOutboxMessagesAsync();

        Assert.Equal(1, processed);
        Assert.Single(broker.PublishedEvents);
        Assert.Equal("OrderCreated", broker.PublishedEvents[0].eventType);

        // Verify DLQ message was skipped and remains dead-lettered
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SGrowingDbContext>();
            var dlqMessage = await db.OutboxMessages.FirstOrDefaultAsync(m => m.EventType == "InvoicePaid");
            Assert.NotNull(dlqMessage);
            Assert.Null(dlqMessage.ProcessedOnUtc);
            Assert.True(dlqMessage.RetryCount >= OutboxPublisherJob.MaxRetries);
        }
    }

    // =========================================================================
    // 8. Periodic Agent Heartbeat Job
    // =========================================================================

    [Fact]
    public async Task AgentHeartbeatJob_ExecutesFleetCycleForActiveTenant()
    {
        var services = new ServiceCollection();
        var dbName = Guid.NewGuid().ToString();
        services.AddDbContext<SGrowingDbContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddScoped<IAgentPersonaService, AgentPersonaService>();
        var provider = services.BuildServiceProvider();

        var tenantId = Guid.NewGuid();
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SGrowingDbContext>();
            db.Tenants.Add(new Tenant { Id = tenantId, Name = "Test Fleet Corp" });
            await db.SaveChangesAsync();
        }

        var job = new AgentHeartbeatJob(provider, NullLogger<AgentHeartbeatJob>.Instance);
        var cyclesExecuted = await job.RunHeartbeatOnceAsync(tenantId);

        Assert.Equal(1, cyclesExecuted);

        // Verify heartbeat reports were generated in db
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SGrowingDbContext>();
            var reports = await db.AgentHeartbeatRecords.Where(r => r.TenantId == tenantId).ToListAsync();
            Assert.NotEmpty(reports);
            Assert.Contains(reports, r => r.Status == "Healthy" || r.Status == "Warning");
        }
    }
}
