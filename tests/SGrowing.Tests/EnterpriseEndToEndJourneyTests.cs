using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Commerce.CallCenter;
using SGrowing.Api.Modules.Feedback.Community;
using SGrowing.Api.Modules.Feedback.MicroSurveys;
using SGrowing.Api.Modules.Gateways.Android;
using SGrowing.Api.Modules.Outbound.Enrichment;
using SGrowing.Api.Modules.Outbound.Research;
using SGrowing.Api.Modules.Tenancy.SnapshotsAndRebilling;
using SGrowing.Api.Modules.Voice;
using SGrowing.Core.Entities;
using SGrowing.Worker.Services;
using Xunit;

namespace SGrowing.Tests;

public class EnterpriseEndToEndJourneyTests
{
    private static SGrowingDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase($"SGrowing_E2E_{Guid.NewGuid()}")
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public async Task Journey_Step01_OutboundLeadDiscoveryAndDeepWebResearch()
    {
        // 1. Prospect discovery and web intelligence scraping
        using var db = CreateInMemoryDb();
        var tenantId = Guid.NewGuid();
        var service = new DeepWebResearchService(db);

        var result = await service.CrawlAndExtractAsync(tenantId, new WebResearchCrawlRequest(
            Domain: "https://www.biotech-maghreb.tn",
            ExtractBios: true,
            ExtractTechStack: true,
            GenerateSalesHooks: true));

        Assert.Equal("biotech-maghreb.tn", result.Domain);
        Assert.NotEmpty(result.CorporateSummary);
        Assert.NotEmpty(result.SalesHooks);
        Assert.Equal(3, result.SalesHooks.Count);
    }

    [Fact]
    public async Task Journey_Step02_WaterfallEnrichmentCascade_StopsOnFirstMatch()
    {
        // 2. Cascade lookup stops on first provider match to save credits
        using var db = CreateInMemoryDb();
        var tenantId = Guid.NewGuid();
        var service = new WaterfallEnrichmentService(db);

        var result = await service.ExecuteWaterfallEnrichmentAsync(
            tenantId,
            "Dr. Karim Trabelsi",
            "biotech-maghreb.tn");

        Assert.True(result.IsMatched);
        Assert.NotNull(result.VerifiedEmail);
        Assert.Contains(result.WinningProvider, new[] { "Hunter", "Apollo", "Prospeo" });
        Assert.Equal(1, result.TotalCreditsCharged);
    }

    [Fact]
    public async Task Journey_Step03_VoiceCallAmdAndVoicemailDrop()
    {
        // 3. Telephony outbound call with Answering Machine Detection (AMD)
        using var db = CreateInMemoryDb();
        var tenantId = Guid.NewGuid();
        var service = new VoiceCallAmdService(db);

        await service.ConfigureVoicemailTemplateAsync(
            tenantId,
            "Urgent B2B Follow-up",
            "https://cdn.sgrowing.io/audio/urgent-followup.wav");

        var amdResult = await service.ClassifyAudioStreamAmdAsync(
            tenantId,
            destinationPhone: "+21650112233",
            audioDurationMs: 2500);

        Assert.Equal("AnsweringMachineBeep", amdResult.Classification);
        Assert.True(amdResult.VoicemailDropped);
        Assert.Equal("https://cdn.sgrowing.io/audio/urgent-followup.wav", amdResult.AudioDropFileUrl);
    }

    [Fact]
    public async Task Journey_Step04_CallCenterAntiCollisionLocking_ResolvesConcurrentDialing()
    {
        // 4. Two operators dial same COD order -> second is blocked with collision alert
        using var db = CreateInMemoryDb();
        var tenantId = Guid.NewGuid();
        var service = new CallCenterAntiCollisionService(db);

        var orderId = Guid.NewGuid();
        var agent1Id = Guid.NewGuid();
        var agent2Id = Guid.NewGuid();

        // Agent 1 locks order
        var lock1 = await service.AcquireLockAsync(tenantId, orderId, "ORD-2026-901", agent1Id, "Agent Alice");
        Assert.True(lock1.IsAcquired);
        Assert.Equal("Agent Alice", lock1.LockedByUserName);

        // Agent 2 attempts to dial same order -> Collision!
        var lock2 = await service.AcquireLockAsync(tenantId, orderId, "ORD-2026-901", agent2Id, "Agent Bob");
        Assert.False(lock2.IsAcquired);
        Assert.Equal(agent1Id, lock2.LockedByUserId);
        Assert.Contains("Agent Alice", lock2.Reason);

        // Agent 1 confirms order and releases lock
        var released = await service.ReleaseLockAsync(tenantId, orderId, agent1Id);
        Assert.True(released);

        // Now Agent 2 can acquire lock
        var lock3 = await service.AcquireLockAsync(tenantId, orderId, "ORD-2026-901", agent2Id, "Agent Bob");
        Assert.True(lock3.IsAcquired);
        Assert.Equal("Agent Bob", lock3.LockedByUserName);
    }

    [Fact]
    public async Task Journey_Step05_CodTrueNetMarginLedgerCalculation()
    {
        // 5. Compute true net cash profit accounting for returns
        using var db = CreateInMemoryDb();
        var tenantId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var service = new CodNetMarginLedgerService(db);

        // Order Total = 200, COGS = 70, Packaging = 2, AdSpend = 15, Shipping = 12
        // Return Rate = 0.20 (20%), Return Penalty = 15 => ReturnRiskCost = 0.20 * 15 = 3.0
        // Net profit = 200 - 70 - 2 - 15 - 12 - 3 = 98
        var entry = await service.CalculateAndRecordMarginAsync(
            tenantId,
            orderId,
            "ORD-2026-901",
            grossRevenue: 200m,
            cogs: 70m,
            packagingCost: 2m,
            adSpendCost: 15m,
            courierShippingFee: 12m,
            returnRatePercent: 0.20m,
            returnPenaltyFee: 15m);

        Assert.Equal(3.0m, entry.ReturnRiskCost);
        Assert.Equal(98.0m, entry.NetProfit);
        Assert.Equal(49.0m, entry.NetMarginPercent);
    }

    [Fact]
    public async Task Journey_Step06_AndroidSmsGateway_DispatchAndStatusCallback()
    {
        // 6. Dispatch delivery confirmation via physical handset and process status webhook
        using var db = CreateInMemoryDb();
        var tenantId = Guid.NewGuid();
        var service = new AndroidSmsGatewayService(db);

        // Enroll device
        var device = await service.RegisterDeviceAsync(
            tenantId,
            deviceName: "Hub-Phone-North",
            deviceIdentifier: "IMEI-8899001122",
            fcmToken: "fcm_token_test_123");

        Assert.NotNull(device);
        Assert.True(device.IsOnline);

        // Dispatch SMS
        var dispatchResult = await service.DispatchSmsAsync(
            tenantId,
            recipientPhone: "+21698223344",
            messageBody: "Your order ORD-2026-901 is out for delivery!");

        Assert.True(dispatchResult.IsSuccess);
        Assert.NotNull(dispatchResult.DispatchId);

        // Delivery callback received
        var updated = await service.RecordDeliveryStatusAsync(
            tenantId,
            dispatchResult.DispatchId.Value,
            "Delivered");

        Assert.True(updated);
        var record = await db.AndroidSmsDispatchRecords.FindAsync(dispatchResult.DispatchId.Value);
        Assert.NotNull(record);
        Assert.Equal("Delivered", record.DispatchStatus);
        Assert.NotNull(record.DeliveredAtUtc);
    }

    [Fact]
    public async Task Journey_Step07_InAppMicroSurvey_PartialIngestionAndDropOff()
    {
        // 7. Client answers Question 1 then closes tab -> partial drop-off captured
        using var db = CreateInMemoryDb();
        var tenantId = Guid.NewGuid();
        var service = new InAppMicroSurveyService(db);

        var survey = await service.CreateSurveyAsync(
            tenantId,
            "Post-Delivery CSAT",
            "delivery_completed",
            "[{\"q\":\"Rating\"},{\"q\":\"Courier Feedback\"}]",
            recontactDays: 14);

        var step1 = await service.IngestPartialAnswerAsync(
            tenantId,
            survey.Id,
            "respondent-99",
            "{\"q1\": 5}",
            stepIndex: 1,
            isFinished: false);

        Assert.Equal(1, step1.LastCompletedStepIndex);
        Assert.False(step1.IsFinished);
        Assert.Equal("Step_1", step1.DropoffStep);

        // Complete survey
        var step2 = await service.IngestPartialAnswerAsync(
            tenantId,
            survey.Id,
            "respondent-99",
            "{\"q1\": 5, \"q2\": \"Fast courier!\"}",
            stepIndex: 2,
            isFinished: true);

        Assert.True(step2.IsFinished);
    }

    [Fact]
    public async Task Journey_Step08_AgencySnapshotBlueprintDeployment()
    {
        // 8. Deploy Agency Snapshot Blueprint (custom fields + workflows) to tenant
        using var db = CreateInMemoryDb();
        var agencyTenantId = Guid.NewGuid();
        var subAccountTenantId = Guid.NewGuid();
        var service = new TenantSnapshotService(db);

        var snapshot = await service.CreateSnapshotAsync(
            agencyTenantId,
            "Tunisia E-Commerce COD Accelerator",
            "E-Commerce",
            "Pre-configured pipeline and custom fields for COD merchants.");

        var deployResult = await service.DeploySnapshotAsync(
            subAccountTenantId,
            snapshot.Id,
            "Tunis Hub Storefront");

        Assert.True(deployResult.CustomFieldsCreated > 0);
        Assert.True(deployResult.WorkflowsCreated > 0);
        Assert.True(deployResult.FunnelsCreated > 0);
    }

    [Fact]
    public async Task Journey_Step09_CommunityRoadmap_PostCreationAndDuplicateVoteGuard()
    {
        // 9. Client submits feature feedback and votes
        using var db = CreateInMemoryDb();
        var tenantId = Guid.NewGuid();
        var service = new CommunityFeedbackService(db);

        var post = await service.SubmitPostAsync(
            tenantId,
            "Automated WhatsApp Waybill Tracking PDF",
            "Automatically generate waybill PDFs and send via WhatsApp upon confirmation.",
            "merchant@growthstore.tn");

        Assert.Equal(1, post.UpvoteCount);
        Assert.Equal("UnderReview", post.RoadmapStatus);

        // Another user upvotes
        var vote1 = await service.VotePostAsync(tenantId, post.Id, "investor@venture.com");
        Assert.True(vote1);

        // Same user votes again -> rejected
        var duplicateVote = await service.VotePostAsync(tenantId, post.Id, "investor@venture.com");
        Assert.False(duplicateVote);
    }

    [Fact]
    public async Task Journey_Step10_WorkerDaemons_LockSweepingAndSmsQueueProcessing()
    {
        // 10. Background worker daemons: CallCenterLockSweeper and AndroidSmsQueueDispatcher
        using var db = CreateInMemoryDb();
        var tenantId = Guid.NewGuid();

        // Setup an expired lock
        var expiredLock = CallCenterOrderLockRecord.Create(
            tenantId,
            Guid.NewGuid(),
            "ORD-EXP-1",
            Guid.NewGuid(),
            "Agent Sarah",
            TimeSpan.FromSeconds(-10)); // expired 10s ago

        db.CallCenterOrderLocks.Add(expiredLock);
        await db.SaveChangesAsync();

        // Run Sweeper
        var swept = await CallCenterLockSweeperDaemon.SweepExpiredLocksAsync(db);
        Assert.Equal(1, swept);
        Assert.False(expiredLock.IsActive);

        // Setup queued SMS and online handset
        var device = AndroidSmsGatewayDevice.Create(tenantId, "Handset-01", "IMEI-123", "fcm");
        db.AndroidSmsGatewayDevices.Add(device);

        var queuedSms = new AndroidSmsDispatchRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DeviceId = Guid.Empty,
            RecipientPhone = "+21698000111",
            MessageBody = "Delivery Alert",
            DispatchStatus = "Queued",
            SentAtUtc = DateTime.UtcNow
        };
        db.AndroidSmsDispatchRecords.Add(queuedSms);
        await db.SaveChangesAsync();

        // Run Dispatcher Daemon
        var dispatched = await AndroidSmsQueueDispatcherDaemon.DispatchQueuedMessagesAsync(db);
        Assert.Equal(1, dispatched);
        Assert.Equal("Dispatched", queuedSms.DispatchStatus);
        Assert.Equal(device.Id, queuedSms.DeviceId);
        Assert.Equal(1, device.DailySentCount);
    }

    [Fact]
    public async Task Journey_Step11_McpServer_ExecutesNewPillarsToolsSuite()
    {
        // 11. Test MCP Autonomous AI tools execution
        var toolService = new McpToolService();

        // 1. Waterfall enrichment tool
        var (waterfallResult, isErr1) = await toolService.ExecuteToolAsync(
            "waterfall_enrich_lead",
            new JsonObject { ["companyDomain"] = "tunis-commerce.tn" });
        Assert.False(isErr1);
        Assert.NotNull(waterfallResult);

        // 2. Call center order lock tool
        var (lockResult, isErr2) = await toolService.ExecuteToolAsync(
            "callcenter_acquire_order_lock",
            new JsonObject { ["orderId"] = "ORD-2026-901", ["agentName"] = "Agent Sarah" });
        Assert.False(isErr2);

        // 3. COD Net Margin calculator tool
        var (marginResult, isErr3) = await toolService.ExecuteToolAsync(
            "cod_calculate_net_margin",
            new JsonObject
            {
                ["orderTotal"] = 180.0,
                ["productCost"] = 60.0,
                ["shippingFee"] = 12.0,
                ["callCenterFee"] = 3.0,
                ["returnRatePercent"] = 15.0,
                ["returnPenaltyFee"] = 16.0
            });
        Assert.False(isErr3);

        // 4. Android SMS dispatch tool
        var (smsResult, isErr4) = await toolService.ExecuteToolAsync(
            "gateways_dispatch_android_sms",
            new JsonObject { ["recipientPhone"] = "+21698112233", ["messageBody"] = "Dispatched!" });
        Assert.False(isErr4);
    }
}
