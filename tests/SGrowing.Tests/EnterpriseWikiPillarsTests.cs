using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Commerce.CallCenter;
using SGrowing.Api.Modules.Conversations.PresenceAndCsat;
using SGrowing.Api.Modules.Feedback.Community;
using SGrowing.Api.Modules.Feedback.MicroSurveys;
using SGrowing.Api.Modules.Gateways.Android;
using SGrowing.Api.Modules.Outbound.Enrichment;
using SGrowing.Api.Modules.SEO;
using SGrowing.Api.Modules.Tenancy.SnapshotsAndRebilling;
using SGrowing.Api.Modules.Voice;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests;

public class EnterpriseWikiPillarsTests
{
    private static SGrowingDbContext CreateInMemoryDb(string dbName)
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public async Task CallCenter_AntiCollision_PreventsConcurrentAgentDialing()
    {
        var db = CreateInMemoryDb(Guid.NewGuid().ToString());
        var tenantId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var agent1Id = Guid.NewGuid();
        var agent2Id = Guid.NewGuid();

        var service = new CallCenterAntiCollisionService(db);

        // Agent 1 dials customer and acquires lock
        var lock1 = await service.AcquireLockAsync(
            tenantId,
            orderId,
            "ORD-TUN-9001",
            agent1Id,
            "Agent Yassine",
            TimeSpan.FromMinutes(3));

        Assert.True(lock1.IsAcquired);
        Assert.Equal("Agent Yassine", lock1.LockedByUserName);

        // Agent 2 attempts to dial the same customer simultaneously
        var lock2 = await service.AcquireLockAsync(
            tenantId,
            orderId,
            "ORD-TUN-9001",
            agent2Id,
            "Agent Meriem",
            TimeSpan.FromMinutes(3));

        Assert.False(lock2.IsAcquired);
        Assert.Contains("Collision", lock2.Reason);
        Assert.Equal("Agent Yassine", lock2.LockedByUserName);

        // Agent 1 releases lock
        var released = await service.ReleaseLockAsync(tenantId, orderId, agent1Id);
        Assert.True(released);

        // Agent 2 can now acquire lock
        var lock2AfterRelease = await service.AcquireLockAsync(
            tenantId,
            orderId,
            "ORD-TUN-9001",
            agent2Id,
            "Agent Meriem",
            TimeSpan.FromMinutes(3));

        Assert.True(lock2AfterRelease.IsAcquired);
        Assert.Equal("Agent Meriem", lock2AfterRelease.LockedByUserName);
    }

    [Fact]
    public async Task CodNetMargin_CalculatesTrueMarginWithReturnPenalty()
    {
        var db = CreateInMemoryDb(Guid.NewGuid().ToString());
        var tenantId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var service = new CodNetMarginLedgerService(db);

        var margin = await service.CalculateAndRecordMarginAsync(
            tenantId: tenantId,
            orderId: orderId,
            orderNumber: "ORD-MALLA-441",
            grossRevenue: 150.00m,
            cogs: 45.00m,
            packagingCost: 3.00m,
            adSpendCost: 18.00m,
            courierShippingFee: 9.00m,
            returnRatePercent: 0.30m, // 30% return rate
            returnPenaltyFee: 7.00m  // 7 TND return handling cost
        );

        // Return risk = 0.30 * 7 = 2.10
        Assert.Equal(2.10m, margin.ReturnRiskCost);
        // Net profit = 150 - 45 - 3 - 18 - 9 - 2.10 = 72.90
        Assert.Equal(72.90m, margin.NetProfit);
        // Net margin % = (72.90 / 150) * 100 = 48.60%
        Assert.Equal(48.60m, margin.NetMarginPercent);

        var summary = await service.GetTenantMarginSummaryAsync(tenantId);
        Assert.Equal(1, summary.TotalOrdersEvaluated);
        Assert.Equal(72.90m, summary.TotalNetProfit);
        Assert.Equal(150.00m, summary.AverageOrderValue);
    }

    [Fact]
    public async Task ConversationPresence_DetectsCollisionBetweenTwoActiveAgents()
    {
        var db = CreateInMemoryDb(Guid.NewGuid().ToString());
        var tenantId = Guid.NewGuid();
        var convId = Guid.NewGuid();
        var agent1Id = Guid.NewGuid();
        var agent2Id = Guid.NewGuid();

        var service = new ConversationPresenceService(db);

        // Agent 1 views ticket
        var report1 = await service.HeartbeatPresenceAsync(tenantId, convId, agent1Id, "Agent Sarah", isTyping: false);
        Assert.False(report1.HasCollision);
        Assert.Single(report1.ActiveAgents);

        // Agent 2 opens ticket and starts typing
        var report2 = await service.HeartbeatPresenceAsync(tenantId, convId, agent2Id, "Agent Karim", isTyping: true);
        Assert.True(report2.HasCollision);
        Assert.Equal(2, report2.ActiveAgents.Count);
        Assert.NotNull(report2.CollisionWarningMessage);
        Assert.Contains("Collision Warning", report2.CollisionWarningMessage);
        Assert.Contains("Agent Karim", report2.CollisionWarningMessage);
    }

    [Fact]
    public async Task CsatSurvey_RecordsResponseAndCalculatesMetrics()
    {
        var db = CreateInMemoryDb(Guid.NewGuid().ToString());
        var tenantId = Guid.NewGuid();

        var service = new CsatSurveyService(db);

        await service.RecordCsatResponseAsync(tenantId, Guid.NewGuid(), Guid.NewGuid(), "Customer A", 5, "WhatsApp");
        await service.RecordCsatResponseAsync(tenantId, Guid.NewGuid(), Guid.NewGuid(), "Customer B", 5, "WhatsApp");
        await service.RecordCsatResponseAsync(tenantId, Guid.NewGuid(), Guid.NewGuid(), "Customer C", 4, "Email");
        await service.RecordCsatResponseAsync(tenantId, Guid.NewGuid(), Guid.NewGuid(), "Customer D", 2, "WebChat", "Delivery was delayed");

        var metrics = await service.GetTenantCsatMetricsAsync(tenantId);

        Assert.Equal(4, metrics.TotalResponses);
        Assert.Equal(4.0, metrics.AverageScore);
        Assert.Equal(75.0, metrics.SatisfactionPercent); // 3 out of 4 rated 4 or 5
        Assert.Equal(25.0, metrics.NetPromoterScore); // (2 promoters (score 5) - 1 detractor (score 2)) / 4 = 25%
        Assert.True(metrics.ChannelBreakdown.ContainsKey("WhatsApp"));
        Assert.Equal(2, metrics.ChannelBreakdown["WhatsApp"]);
    }

    [Fact]
    public async Task TenantSnapshot_CreatesBlueprintAndDeploysToSubAccount()
    {
        var db = CreateInMemoryDb(Guid.NewGuid().ToString());
        var agencyTenantId = Guid.NewGuid();
        var subAccountTenantId = Guid.NewGuid();

        var service = new TenantSnapshotService(db);

        // Agency creates snapshot
        var snapshot = await service.CreateSnapshotAsync(
            agencyTenantId,
            "Tunisia High-Conversion COD Ecom Blueprint",
            "E-Commerce",
            "Ready-to-use checkout funnel, WhatsApp welcome workflow, and VIP custom fields");

        Assert.NotNull(snapshot);
        Assert.Contains("Tunisia High-Conversion", snapshot.SnapshotName);

        // Deploy to new client sub-account
        var deployment = await service.DeploySnapshotAsync(
            subAccountTenantId,
            snapshot.Id,
            "Sfax Electronics Boutique");

        Assert.True(deployment.CustomFieldsCreated > 0);
        Assert.True(deployment.WorkflowsCreated > 0);
        Assert.True(deployment.FunnelsCreated > 0);

        // Verify entities were created under sub-account tenant
        var deployedFunnels = await db.SalesFunnels
            .Where(f => f.TenantId == subAccountTenantId)
            .ToListAsync();

        Assert.NotEmpty(deployedFunnels);
        Assert.Contains("Sfax Electronics Boutique", deployedFunnels.First().Name);
    }

    [Fact]
    public async Task TenantRebilling_CalculatesWholesaleToRetailMarkup()
    {
        var db = CreateInMemoryDb(Guid.NewGuid().ToString());
        var agencyTenantId = Guid.NewGuid();
        var subAccountTenantId = Guid.NewGuid();

        var service = new TenantRebillingService(db);

        await service.SetRebillingConfigAsync(
            agencyTenantId,
            subAccountTenantId,
            smsMultiplier: 1.40m,    // 40% margin
            voiceMultiplier: 1.25m,  // 25% margin
            aiMultiplier: 1.50m      // 50% margin
        );

        var smsRebill = await service.CalculateRebilledCostAsync(agencyTenantId, subAccountTenantId, "sms", 10.00m);
        Assert.Equal(14.00m, smsRebill.RetailCharge);
        Assert.Equal(4.00m, smsRebill.NetAgencyProfit);

        var aiRebill = await service.CalculateRebilledCostAsync(agencyTenantId, subAccountTenantId, "ai", 20.00m);
        Assert.Equal(30.00m, aiRebill.RetailCharge);
        Assert.Equal(10.00m, aiRebill.NetAgencyProfit);
    }

    [Fact]
    public async Task WaterfallEnrichment_CascadesThroughProvidersUntilMatch()
    {
        var db = CreateInMemoryDb(Guid.NewGuid().ToString());
        var tenantId = Guid.NewGuid();

        var service = new WaterfallEnrichmentService(db);

        var result = await service.ExecuteWaterfallEnrichmentAsync(
            tenantId,
            "Moncef Marzouki",
            "tunisiacapital.tn");

        Assert.True(result.IsMatched);
        Assert.NotNull(result.WinningProvider);
        Assert.NotNull(result.VerifiedEmail);
        Assert.Contains("@tunisiacapital.tn", result.VerifiedEmail);
        Assert.Equal(1, result.TotalCreditsCharged);
        Assert.NotEmpty(result.StepAuditTrail);

        var logs = await service.GetExecutionLogsAsync(tenantId);
        Assert.NotEmpty(logs);
    }

    [Fact]
    public async Task SeoMultipageGraph_DetectsOrphanPagesAndDuplicateTitles()
    {
        var db = CreateInMemoryDb(Guid.NewGuid().ToString());
        var tenantId = Guid.NewGuid();
        var rootDomain = "https://myshop.tn";

        var service = new SeoMultipageGraphService(db);

        var sitemap = new List<string>
        {
            "https://myshop.tn",
            "https://myshop.tn/about",
            "https://myshop.tn/catalog",
            "https://myshop.tn/hidden-promo-orphan"
        };

        var crawledNodes = new List<CrawledPageLinkNodeDto>
        {
            new("https://myshop.tn", "Premium Shop Tunisia", "Shop online", new List<string> { "https://myshop.tn/about", "https://myshop.tn/catalog" }),
            new("https://myshop.tn/about", "About Us", "Our story", new List<string> { "https://myshop.tn" }),
            new("https://myshop.tn/catalog", "Premium Shop Tunisia", "Collection", new List<string> { "https://myshop.tn" }), // Duplicate Title
            new("https://myshop.tn/hidden-promo-orphan", "Special Secret Promo", "Secret", new List<string>())
        };

        var result = await service.AnalyzeMultipageGraphAsync(tenantId, rootDomain, sitemap, crawledNodes);

        Assert.Equal(1, result.OrphanPageCount);
        Assert.Contains("https://myshop.tn/hidden-promo-orphan", result.OrphanPageUrls);
        Assert.Equal(1, result.DuplicateTitleClusterCount);
        Assert.True(result.DuplicateTitleClusters.ContainsKey("Premium Shop Tunisia"));
    }

    [Fact]
    public async Task InAppMicroSurvey_EvaluatesTriggersAndIngestsPartialDropoffs()
    {
        var db = CreateInMemoryDb(Guid.NewGuid().ToString());
        var tenantId = Guid.NewGuid();

        var service = new InAppMicroSurveyService(db);

        var survey = await service.CreateSurveyAsync(
            tenantId,
            "Post-Checkout Experience Survey",
            "checkout_completed",
            "[{\"q\":\"How easy was placing your order?\"},{\"q\":\"Did you find what you wanted?\"}]",
            recontactDays: 14);

        // Client triggers evaluation
        var eval = await service.EvaluateClientTriggerAsync(tenantId, "user-441", "checkout_completed");
        Assert.True(eval.ShouldDisplay);
        Assert.Equal(survey.Id, eval.SurveyId);

        // User answers Question 1
        var partial1 = await service.IngestPartialAnswerAsync(
            tenantId,
            survey.Id,
            "user-441",
            "{\"q1\":\"Very Easy\"}",
            stepIndex: 1,
            isFinished: false);

        Assert.Equal(1, partial1.LastCompletedStepIndex);
        Assert.False(partial1.IsFinished);
        Assert.Equal("Step_1", partial1.DropoffStep);

        // Recontact frequency guard blocks immediate second prompt
        var evalBlocked = await service.EvaluateClientTriggerAsync(tenantId, "user-441", "checkout_completed");
        Assert.False(evalBlocked.ShouldDisplay);
        Assert.Contains("Recontact", evalBlocked.Reason);
    }

    [Fact]
    public async Task AndroidSmsGateway_DispatchesViaAvailableHandset()
    {
        var db = CreateInMemoryDb(Guid.NewGuid().ToString());
        var tenantId = Guid.NewGuid();

        var service = new AndroidSmsGatewayService(db);

        // Register Android phone
        var device = await service.RegisterDeviceAsync(
            tenantId,
            "Samsung Galaxy A54 - Hub 1",
            "IMEI-884920192831",
            "fcm_token_sample_123",
            sim1: "Ooredoo",
            sim2: "Orange",
            dailyQuota: 500);

        Assert.Equal(100, device.BatteryPercent);
        Assert.True(device.IsOnline);

        // Dispatch SMS
        var dispatch = await service.DispatchSmsAsync(
            tenantId,
            "+21698123456",
            "Your SGrowing order #ORD-771 has been confirmed for dispatch!");

        Assert.True(dispatch.IsSuccess);
        Assert.Equal("Dispatched", dispatch.Status);
        Assert.Equal(device.Id, dispatch.DeviceId);

        var updatedDevice = await db.AndroidSmsGatewayDevices.FindAsync(device.Id);
        Assert.Equal(1, updatedDevice!.DailySentCount);
    }

    [Fact]
    public async Task VoiceCallAmd_ClassifiesAnsweringMachineAndDropsVoicemail()
    {
        var db = CreateInMemoryDb(Guid.NewGuid().ToString());
        var tenantId = Guid.NewGuid();

        var service = new VoiceCallAmdService(db);

        await service.ConfigureVoicemailTemplateAsync(
            tenantId,
            "Confirmation Follow-Up Pitch",
            "https://cdn.sgrowing.io/audio/vm-drop-promo.wav");

        // Long monotonic audio duration indicates voicemail greeting + beep
        var amdResult = await service.ClassifyAudioStreamAmdAsync(
            tenantId,
            "+21650888999",
            audioDurationMs: 2700);

        Assert.Equal("AnsweringMachineBeep", amdResult.Classification);
        Assert.True(amdResult.VoicemailDropped);
        Assert.Equal("https://cdn.sgrowing.io/audio/vm-drop-promo.wav", amdResult.AudioDropFileUrl);
        Assert.Contains("Automated Voicemail Drop injected", amdResult.ActionTaken);
    }

    [Fact]
    public async Task CommunityFeedback_PreventsDuplicateVotingAndUpdatesRoadmap()
    {
        var db = CreateInMemoryDb(Guid.NewGuid().ToString());
        var tenantId = Guid.NewGuid();

        var service = new CommunityFeedbackService(db);

        // User posts suggestion
        var post = await service.SubmitPostAsync(
            tenantId,
            "Native TikTok Shop Two-Way Inventory Sync",
            "Please add live bi-directional stock sync with TikTok Shop for MENA region",
            "merchant@growthstore.tn");

        Assert.Equal(1, post.UpvoteCount);
        Assert.Equal("UnderReview", post.RoadmapStatus);

        // Second user votes
        var voted = await service.VotePostAsync(tenantId, post.Id, "investor@venture.com");
        Assert.True(voted);

        // Same user votes again -> rejected
        var duplicateVote = await service.VotePostAsync(tenantId, post.Id, "investor@venture.com");
        Assert.False(duplicateVote);

        // Product manager accepts idea and moves to 'Planned'
        var updated = await service.UpdateRoadmapStatusAsync(
            tenantId,
            post.Id,
            "Planned",
            "Accepted: Scheduled for Q1 2027 release");

        Assert.Equal("Planned", updated.RoadmapStatus);
        Assert.Equal("Accepted: Scheduled for Q1 2027 release", updated.OfficialResponse);

        var roadmap = await service.GetPublicRoadmapAsync(tenantId);
        Assert.Single(roadmap.Planned);
        Assert.Equal(post.Id, roadmap.Planned.First().Id);
    }
}
