using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.AI;
using SGrowing.Api.Modules.Commerce;
using SGrowing.Api.Modules.Conversations;
using SGrowing.Api.Modules.CRM;
using SGrowing.Api.Modules.Deliverability;
using SGrowing.Api.Modules.Documents;
using SGrowing.Api.Modules.Funnels;
using SGrowing.Api.Modules.Marketing;
using SGrowing.Api.Modules.Outbound;
using SGrowing.Api.Modules.Telemetry;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests;

public class InspirationFeaturesTests
{
    private SGrowingDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SGrowingDbContext(options);
    }

    // 1. ChatbotX Tests
    [Fact]
    public async Task ChatbotX_FlowExecutionAndSocialComment_Succeeds()
    {
        using var db = CreateInMemoryDb();
        var service = new ChatbotFlowEngineService(db);
        var tenantId = Guid.NewGuid();

        // Register flow
        var nodes = new List<ChatbotFlowNodeDto>
        {
            new("n1", ChatbotNodeType.Message, "Intro", new Dictionary<string, object> { ["text"] = "Hello {{name}}!" }, new List<string> { "n2" }),
            new("n2", ChatbotNodeType.UserInput, "GetEmail", new Dictionary<string, object> { ["prompt"] = "Enter email:" }, new List<string>())
        };
        var flow = await service.RegisterFlowAsync(tenantId, "Lead Funnel", "WhatsApp", nodes, "n1");
        Assert.NotNull(flow);

        // Step 1: Execute step -> triggers message and stops at UserInput
        var step1 = await service.ExecuteFlowStepAsync(tenantId, flow.FlowId, null, new Dictionary<string, object> { ["name"] = "Alice" });
        Assert.False(step1.IsCompleted);
        Assert.True(step1.IsWaitingForUserInput);
        Assert.Single(step1.DispatchedMessages);
        Assert.Equal("Hello Alice!", step1.DispatchedMessages[0]);

        // Social Comment Trigger test (PRICE keyword)
        var commentEv = new SocialCommentEventDto("Instagram", "post-100", "c-1", "user-44", "JohnDoe", "Can you send me the PRICE and info?");
        var reaction = await service.ProcessSocialCommentTriggerAsync(tenantId, commentEv);
        Assert.True(reaction.InitiatedPrivateDm);
        Assert.Contains("private direct message", reaction.PublicReplyComment);
        Assert.NotNull(reaction.TriggeredFlowId);
    }

    // 2. Chaskiq Tests
    [Fact]
    public async Task Chaskiq_InAppToursAndBanners_Succeeds()
    {
        var service = new InAppEngagementService();
        var tenantId = Guid.NewGuid();

        // Create Tour
        var steps = new List<TourStepDto>
        {
            new(1, "#nav-inbox", "Unified Inbox", "All your chats here", TooltipPlacement.Right),
            new(2, "#nav-crm", "Sales CRM", "Manage deals", TooltipPlacement.Bottom)
        };
        var tour = await service.CreateTourAsync(tenantId, "New User Tour", "/app", steps);
        Assert.NotNull(tour);

        var activeTours = await service.GetActiveToursAsync(tenantId, "/app/dashboard");
        Assert.NotEmpty(activeTours);

        var completed = await service.RecordTourCompletionAsync(tenantId, tour.TourId, "user-123");
        Assert.True(completed);

        // Banners
        var banner = await service.CreateBannerAsync(tenantId, "Spring Offer", "20% off all plans", "Modal", "Upgrade Now", "/pricing", "ENTERPRISE");
        Assert.NotNull(banner);

        var activeBanners = await service.EvaluateActiveBannersAsync(tenantId, "/app", "ENTERPRISE");
        Assert.NotEmpty(activeBanners);
    }

    // 3. Chatwoot Tests
    [Fact]
    public async Task Chatwoot_CollisionDetectionAndSlaBreaches_Succeeds()
    {
        using var db = CreateInMemoryDb();
        var tenantId = Guid.NewGuid();

        // Seed an SLA policy and an open conversation
        var policy = SlaPolicy.Create(tenantId, "Critical SLA", ConversationPriority.Urgent, 15, 60);
        db.SlaPolicies.Add(policy);

        var conv = Conversation.Create(tenantId, Guid.NewGuid(), Guid.NewGuid());
        conv.Priority = ConversationPriority.Urgent;
        db.Conversations.Add(conv);
        await db.SaveChangesAsync();

        var service = new CollisionDetectionAndSlaService(db);

        // Presence test
        var viewers = await service.UpdatePresenceAsync(tenantId, conv.Id, "agent-1", "Sarah Connor", true);
        Assert.Single(viewers);
        Assert.True(viewers[0].IsTyping);

        // SLA evaluation
        var breaches = await service.EvaluateSlaBreachesAsync(tenantId);
        Assert.NotEmpty(breaches);

        // CSAT feedback
        var feedback = await service.RecordCsatFeedbackAsync(tenantId, conv.Id, 5, "Outstanding support!");
        Assert.Equal(5, feedback.RatingScore);
    }

    // 4. Warmbly Tests
    [Fact]
    public async Task Warmbly_P2PWarmupAndDnsDiagnostics_Succeeds()
    {
        using var db = CreateInMemoryDb();
        var tenantId = Guid.NewGuid();

        var sender = SenderAccount.Create(tenantId, "outreach@sgrowing.io", "Outreach Bot", MailboxProvider.GoogleWorkspace, 45, 5);
        db.SenderAccounts.Add(sender);
        await db.SaveChangesAsync();

        var service = new WarmupPeerNetworkService(db);

        // P2P Pairing
        var pairs = await service.PairMailboxesForWarmupAsync(tenantId);
        Assert.NotEmpty(pairs);
        Assert.Equal("outreach@sgrowing.io", pairs[0].SenderEmail);

        // Spam Rescue Sweep
        var rescue = await service.ExecuteSpamRescueSweepAsync(tenantId);
        Assert.True(rescue.RescuedFromSpam > 0);
        Assert.True(rescue.MovedToInbox > 0);

        // DNS diagnostics
        var dns = await service.RunDnsDiagnosticsAsync("sgrowing.io");
        Assert.True(dns.DeliverabilityHealthScore >= 80);
        Assert.True(dns.HasSpfRecord);
        Assert.True(dns.HasDkimRecord);
        Assert.True(dns.HasDmarcRecord);
    }

    // 5. AgentSDR Tests
    [Fact]
    public async Task AgentSDR_SlotAllocationCallingAndReplyClassifier_Succeeds()
    {
        using var db = CreateInMemoryDb();
        var tenantId = Guid.NewGuid();

        var acc = SenderAccount.Create(tenantId, "rep1@growthscale.com", "Sales Rep", MailboxProvider.GoogleWorkspace, 40);
        db.SenderAccounts.Add(acc);
        await db.SaveChangesAsync();

        var service = new AutonomousSdrFleetService(db);

        // Slot allocation
        var slots = await service.AllocateInboxSlotsAsync(tenantId, 100);
        Assert.NotEmpty(slots);
        Assert.True(slots[0].PacingSecondsDelay >= 180);

        // WebRTC Softphone Call Session
        var call = await service.StartWebRtcCallSessionAsync(tenantId, "+1234567890", "+1987654321");
        Assert.NotNull(call.WebRtcRoomToken);

        var dispSuccess = await service.SubmitCallDispositionAsync(tenantId, new CallDispositionDto(call.CallSessionId, "Interested", 125, null, "Requested enterprise quote"));
        Assert.True(dispSuccess);

        // Intent Classifier
        var reply = await service.ClassifyInboundReplyAsync(tenantId, "Re: Demo inquiry", "I am definitely interested in this. Let's talk tomorrow.");
        Assert.Equal(ReplyIntentCategory.Interested, reply.Category);
        Assert.True(reply.AutoCalendarLinkInjected);
    }

    // 6. Postiz Tests
    [Fact]
    public async Task Postiz_DurablePublishingAndAiCopilot_Succeeds()
    {
        using var db = CreateInMemoryDb();
        var tenantId = Guid.NewGuid();

        var post = new SocialPost
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Content = "Scaling B2B outbound in 2026",
            Status = SocialPostStatus.Draft
        };
        db.SocialPosts.Add(post);
        await db.SaveChangesAsync();

        var service = new SocialPublishingEngineService(db);

        // Publish durably with partial failure guarantees
        var results = await service.PublishPostDurablyAsync(tenantId, post.Id, new List<SocialPlatform> { SocialPlatform.X, SocialPlatform.LinkedIn });
        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.Platform == SocialPlatform.X && r.Status == DurablePublishStatus.Published);
        Assert.Contains(results, r => r.Platform == SocialPlatform.LinkedIn && r.Status == DurablePublishStatus.PublishedWithError);

        // Mastra AI Viral Hooks
        var hooks = await service.GenerateViralHooksAsync(tenantId, "AI Cold Outreach");
        Assert.NotEmpty(hooks);

        // Content Rewriter
        var rewritten = await service.RewriteForPlatformsAsync(tenantId, "Master post on autonomous AI SDRs", new List<SocialPlatform> { SocialPlatform.X, SocialPlatform.LinkedIn });
        Assert.Equal(2, rewritten.Count);
    }

    // 7. TryCRM Tests
    [Fact]
    public async Task TryCRM_McpServerAndContactFactExtraction_Succeeds()
    {
        using var db = CreateInMemoryDb();
        var tenantId = Guid.NewGuid();

        var contact = Contact.Create(tenantId, "Elon", "Musk", "elon@x.com");
        db.Contacts.Add(contact);
        await db.SaveChangesAsync();

        var service = new McpServerRegistryService(db);

        // MCP tool introspection
        var tools = await service.GetRegisteredMcpToolsAsync();
        Assert.Contains(tools, t => t.Name == "search_contacts");

        // Tool invocation
        var exec = await service.ExecuteMcpToolAsync(tenantId, "search_contacts", new Dictionary<string, object> { ["query"] = "Elon" });
        Assert.True(exec.IsSuccess);

        // Fact extraction
        var transcript = "The client mentioned their budget is around $40k and they want to make a decision by next month.";
        var facts = await service.ExtractContactFactsAsync(tenantId, contact.Id, transcript);
        Assert.Contains(facts, f => f.FactType == "Budget");
        Assert.Contains(facts, f => f.FactType == "Timeline");

        // Rolling brief
        var brief = await service.GetContactBriefAsync(tenantId, contact.Id);
        Assert.NotEmpty(brief.KeyBulletPoints);
    }

    // 8. Frappe CRM Tests
    [Fact]
    public async Task FrappeCRM_RottenDealsAndTerritories_Succeeds()
    {
        using var db = CreateInMemoryDb();
        var tenantId = Guid.NewGuid();

        var deal = Deal.Create(tenantId, Guid.NewGuid(), "Strategic Partnership", 80000m, DealStage.Prospecting);
        db.Deals.Add(deal);
        await db.SaveChangesAsync();

        var service = new FrappeCrmEnhancementsService(db);

        // Rotten deals check
        var rotten = await service.EvaluateRottenDealsAsync(tenantId, 5);
        Assert.NotEmpty(rotten);

        // DocType schema validation
        var schema = await service.ValidateDocTypeSchemaAsync(tenantId, "CRM Lead", new Dictionary<string, object> { ["lead_name"] = "Acme Corp", ["annual_revenue"] = 500000 });
        Assert.True(schema.IsValid);

        // Territory assignment
        var territory = await service.AssignLeadTerritoryAsync(tenantId, deal.Id, "123 Ocean Blvd", 36.8, 10.2);
        Assert.Equal("North Coast Territory", territory.AssignedTerritoryName);
    }

    // 9. Frappe Helpdesk Tests
    [Fact]
    public async Task FrappeHelpdesk_ErpCommercialContextAndPauseSla_Succeeds()
    {
        using var db = CreateInMemoryDb();
        var tenantId = Guid.NewGuid();

        var conv = Conversation.Create(tenantId, Guid.NewGuid(), Guid.NewGuid());
        db.Conversations.Add(conv);

        var order = CodOrder.Create(tenantId, "ORD-9901", "Alice Corp", "+123456", "Main Street", "Tunis", "Carthage", 450.0m);
        db.CodOrders.Add(order);
        await db.SaveChangesAsync();

        var service = new HelpdeskCommercialContextService(db);

        // Commercial context
        var context = await service.GetTicketCommercialContextAsync(tenantId, conv.Id);
        Assert.NotEmpty(context.RecentOrders);
        Assert.NotEmpty(context.RecentInvoices);
        Assert.True(context.HasActiveSubscription);

        // Pause SLA
        var pause = await service.SetTicketSlaPauseStateAsync(tenantId, conv.Id, true, "Waiting for customer part specification");
        Assert.True(pause.IsSlaPaused);
    }

    // 10. Laravel-CRM Tests
    [Fact]
    public async Task LaravelCrm_CpqQuotesAndPolymorphicEav_Succeeds()
    {
        var service = new CpqQuoteAndEavService();
        var tenantId = Guid.NewGuid();

        var items = new List<CpqLineItemDto>
        {
            new("SKU-ENT-1", "Enterprise License", 2, 1000m, 19m, 10m, 0),
            new("SKU-SUPP-1", "Premium Support", 1, 500m, 19m, 0m, 0)
        };

        var quote = await service.GenerateCpqQuoteAsync(tenantId, "Global Corp", "procurement@global.com", "USD", items, 5m);
        Assert.NotNull(quote);
        Assert.True(quote.GrandTotal > 0);
        Assert.True(quote.TotalDiscount > 0);

        var pdfBytes = await service.GenerateQuotePdfBytesAsync(tenantId, quote.QuoteId);
        Assert.NotEmpty(pdfBytes);

        // EAV attributes
        var entityId = Guid.NewGuid();
        var eav = await service.SetEavAttributeAsync(tenantId, "Lead", entityId, "IndustryVertical", "Telecommunications");
        Assert.Equal("IndustryVertical", eav.AttributeKey);

        var retrieved = await service.GetEavAttributesAsync(tenantId, "Lead", entityId);
        Assert.Single(retrieved);
    }

    // 11. Documenso Tests
    [Fact]
    public async Task Documenso_CryptographicSealingAndAuditCertificate_Succeeds()
    {
        using var db = CreateInMemoryDb();
        var tenantId = Guid.NewGuid();
        var docId = Guid.NewGuid();

        var contract = DigitalContract.Create(tenantId, "Commercial MSA", "MSA", "John Smith", "john@client.com", 25000m);
        db.DigitalContracts.Add(contract);
        await db.SaveChangesAsync();

        var service = new CryptographicPdfSealingService(db);

        // PKCS#7 / CMS Seal
        var rawPdf = System.Text.Encoding.UTF8.GetBytes("%PDF-1.4 Mock Document Content %%EOF");
        var sealedDoc = await service.SealDocumentWithPkcs7Async(tenantId, contract.Id, rawPdf, contract.SignerEmail);
        Assert.NotNull(sealedDoc.Sha256Hash);
        Assert.Equal("PKCS#7/CMS", sealedDoc.SignatureFormat);
        Assert.True(sealedDoc.HasRfc3161Timestamp);

        // Certificate of completion
        var cert = await service.GenerateCertificateOfCompletionAsync(tenantId, contract.Id);
        Assert.Equal(contract.SignerEmail, cert.SignerEmail);
        Assert.NotNull(cert.TsaAuthorityName);
    }

    // 12. Dittofeed Tests
    [Fact]
    public async Task Dittofeed_SegmentTelemetryAndDurableJourneys_Succeeds()
    {
        using var db = CreateInMemoryDb();
        var tenantId = Guid.NewGuid();
        var service = new SegmentTelemetryAndJourneysService(db);

        // Identify
        var identify = new SegmentIdentifyDto("usr-1", null, new Dictionary<string, object> { ["plan"] = "pro", ["company"] = "Acme" }, DateTime.UtcNow);
        var idRes = await service.RecordIdentifyEventAsync(tenantId, identify);
        Assert.True(idRes);

        // Track
        var track = new SegmentTrackDto("usr-1", null, "project_created", new Dictionary<string, object> { ["projectName"] = "Mobile App" }, DateTime.UtcNow);
        var trRes = await service.RecordTrackEventAsync(tenantId, track);
        Assert.True(trRes);

        // Journey Step Evaluation
        var journeyStep = await service.EvaluateDurableJourneyStepAsync(tenantId, Guid.NewGuid(), "usr-1", "project_created");
        Assert.True(journeyStep.IsConditionMet);
        Assert.Equal("DispatchEmail", journeyStep.NextAction);
    }
}
