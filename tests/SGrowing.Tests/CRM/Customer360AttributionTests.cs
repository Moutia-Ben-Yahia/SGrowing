using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.CRM;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.CRM;

public class Customer360AttributionTests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: $"Customer360TestDb_{Guid.NewGuid()}")
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public async Task IdentityStitching_Should_Unify_Anonymous_Cookie_With_Phone_And_Email()
    {
        var db = CreateDbContext();
        var service = new Customer360AttributionService(db);
        var tenantId = Guid.NewGuid();

        // 1. Anonymous visitor browsing website with cookie _sg_vid_web_987
        var cookieId = "_sg_vid_web_987";
        var stitchAnon = await service.StitchIdentityAsync(new StitchIdentityRequest(
            TenantId: tenantId,
            CookieId: cookieId,
            Name: "Anonymous Visitor",
            PerformedBy: "WebTracker"
        ));

        Assert.True(stitchAnon.Success);
        Assert.False(stitchAnon.WasMerged);
        var anonId = stitchAnon.UnifiedCustomerId;

        // Record browsing events under this anonymous session
        await service.RecordEventAsync(new RecordCustomerEventRequest(
            TenantId: tenantId,
            UnifiedCustomerId: anonId,
            EventType: "PageView",
            Summary: "Viewed Product: Ergonomic Desk Chair"
        ));

        await service.RecordTouchpointAsync(new RecordTouchpointRequest(
            TenantId: tenantId,
            ContactId: anonId,
            Channel: "GoogleAds",
            CampaignName: "Search_Brand_DeskChairs",
            Stage: TouchpointStage.FirstDiscovery,
            Cost: 25.50m
        ));

        // 2. Later, customer enters Phone (+21698123456) and Email (samir@example.com) and Name
        var stitchIdentified = await service.StitchIdentityAsync(new StitchIdentityRequest(
            TenantId: tenantId,
            CookieId: cookieId,
            Phone: "98123456", // Tunisian 8 digits normalized to +21698123456
            Email: "samir@example.com",
            Name: "Samir Ben Ali",
            PerformedBy: "CheckoutForm"
        ));

        Assert.True(stitchIdentified.Success);
        Assert.Equal(anonId, stitchIdentified.UnifiedCustomerId); // Promoted in-place!
        Assert.Equal("+21698123456", stitchIdentified.PrimaryPhone);
        Assert.Equal("samir@example.com", stitchIdentified.PrimaryEmail);
        Assert.Equal("Samir Ben Ali", stitchIdentified.PrimaryName);

        // Verify linked channels
        var overview = await service.GetCustomer360OverviewAsync(tenantId, anonId);
        Assert.Contains(overview.LinkedChannels, c => c.IdentityType == CustomerChannelIdentityType.PhoneE164 && c.IdentityValue == "+21698123456");
        Assert.Contains(overview.LinkedChannels, c => c.IdentityType == CustomerChannelIdentityType.Email && c.IdentityValue == "samir@example.com");
        Assert.Contains(overview.LinkedChannels, c => c.IdentityType == CustomerChannelIdentityType.WebCookieId && c.IdentityValue == cookieId);

        // Verify timeline contains both PageView, Touchpoint, and IdentityStitched
        var timeline = await service.GetCustomerTimelineAsync(tenantId, anonId);
        Assert.Contains(timeline, t => t.EventType == "PageView");
        Assert.Contains(timeline, t => t.EventType == "Touchpoint_FirstDiscovery");
        Assert.Contains(timeline, t => t.EventType == "IdentityStitched");
    }

    [Fact]
    public async Task IdentityStitching_Should_Merge_Anonymous_Session_Into_Existing_Customer_Profile()
    {
        var db = CreateDbContext();
        var service = new Customer360AttributionService(db);
        var tenantId = Guid.NewGuid();

        // 1. Existing customer registered via WhatsApp with past orders
        var existingCustomer = new UnifiedCustomerProfile
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PrimaryName = "Amira Trabelsi",
            PrimaryPhone = "+21620123456",
            PrimaryEmail = "amira@tech.tn",
            TotalOrdersCount = 3,
            TotalSpent = 450.00m,
            DeliverySuccessRate = 100.0,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-30),
            LastSeenAtUtc = DateTime.UtcNow.AddDays(-10)
        };
        db.UnifiedCustomerProfiles.Add(existingCustomer);
        db.CustomerIdentityLinks.Add(new CustomerIdentityLink
        {
            TenantId = tenantId,
            UnifiedCustomerId = existingCustomer.Id,
            IdentityType = CustomerChannelIdentityType.PhoneE164,
            IdentityValue = "+21620123456"
        });
        db.CustomerIdentityLinks.Add(new CustomerIdentityLink
        {
            TenantId = tenantId,
            UnifiedCustomerId = existingCustomer.Id,
            IdentityType = CustomerChannelIdentityType.WhatsAppId,
            IdentityValue = "21620123456@s.whatsapp.net"
        });
        await db.SaveChangesAsync();

        // 2. Anonymous session created on web
        var cookieId = "_sg_vid_amira_safari_991";
        var anonStitch = await service.StitchIdentityAsync(new StitchIdentityRequest(
            TenantId: tenantId,
            CookieId: cookieId,
            Name: "Anonymous Visitor"
        ));
        var anonId = anonStitch.UnifiedCustomerId;

        // Add anonymous timeline event and touchpoint
        await service.RecordEventAsync(new RecordCustomerEventRequest(
            TenantId: tenantId,
            UnifiedCustomerId: anonId,
            EventType: "ProposalViewed",
            Summary: "Viewed Annual SaaS Enterprise Proposal"
        ));

        await service.RecordTouchpointAsync(new RecordTouchpointRequest(
            TenantId: tenantId,
            ContactId: anonId,
            Channel: "MetaAds",
            CampaignName: "Retargeting_Enterprise_Video",
            Stage: TouchpointStage.LeadCreated,
            Cost: 40m
        ));

        // 3. User now reveals their identity (phone) in the web session
        var mergedResult = await service.StitchIdentityAsync(new StitchIdentityRequest(
            TenantId: tenantId,
            CookieId: cookieId,
            Phone: "+21620123456", // Matches existingCustomer!
            WhatsAppJid: "21620123456@s.whatsapp.net",
            SocialHandle: "@amira_official",
            SocialType: CustomerChannelIdentityType.InstagramHandle
        ));

        Assert.True(mergedResult.Success);
        Assert.True(mergedResult.WasMerged);
        Assert.Equal(existingCustomer.Id, mergedResult.UnifiedCustomerId);
        Assert.Equal(anonId, mergedResult.MergedFromCustomerId);

        // 4. Verify anonymous profile is removed
        var anonStillExists = await db.UnifiedCustomerProfiles.AnyAsync(c => c.Id == anonId);
        Assert.False(anonStillExists);

        // 5. Verify events & touchpoints transferred to master profile
        var masterOverview = await service.GetCustomer360OverviewAsync(tenantId, existingCustomer.Id);
        Assert.Equal(3, masterOverview.TotalOrdersCount);
        Assert.Equal(450.00m, masterOverview.TotalSpent);

        var timeline = await service.GetCustomerTimelineAsync(tenantId, existingCustomer.Id);
        Assert.Contains(timeline, t => t.EventType == "ProposalViewed");
        Assert.Contains(timeline, t => t.EventType == "ProfileMerged");

        var touchpoints = await db.AttributionTouchpoints.Where(t => t.ContactId == existingCustomer.Id).ToListAsync();
        Assert.Contains(touchpoints, t => t.CampaignName == "Retargeting_Enterprise_Video");

        // Verify linked channels include Phone, WhatsApp, Cookie, Instagram
        Assert.Contains(masterOverview.LinkedChannels, c => c.IdentityType == CustomerChannelIdentityType.PhoneE164);
        Assert.Contains(masterOverview.LinkedChannels, c => c.IdentityType == CustomerChannelIdentityType.WhatsAppId);
        Assert.Contains(masterOverview.LinkedChannels, c => c.IdentityType == CustomerChannelIdentityType.WebCookieId && c.IdentityValue == cookieId);
        Assert.Contains(masterOverview.LinkedChannels, c => c.IdentityType == CustomerChannelIdentityType.InstagramHandle && c.IdentityValue == "@amira_official");
    }

    [Fact]
    public async Task MasterTimeline_Should_Sort_Chronologically_And_Categorize_Event_Types()
    {
        var db = CreateDbContext();
        var service = new Customer360AttributionService(db);
        var tenantId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        db.UnifiedCustomerProfiles.Add(new UnifiedCustomerProfile
        {
            Id = customerId,
            TenantId = tenantId,
            PrimaryName = "Karim Jaziri",
            PrimaryPhone = "+21655112233"
        });
        await db.SaveChangesAsync();

        var baseTime = new DateTime(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc);

        // Add events out of order
        await service.RecordEventAsync(new RecordCustomerEventRequest(
            TenantId: tenantId, UnifiedCustomerId: customerId,
            EventType: "EmailOpened", Summary: "Opened Welcome Email", OccurredAtUtc: baseTime.AddDays(2)));

        await service.RecordEventAsync(new RecordCustomerEventRequest(
            TenantId: tenantId, UnifiedCustomerId: customerId,
            EventType: "OrderPlaced", Summary: "Placed COD order #TN-8821 for $120.00", OccurredAtUtc: baseTime.AddDays(5)));

        await service.RecordEventAsync(new RecordCustomerEventRequest(
            TenantId: tenantId, UnifiedCustomerId: customerId,
            EventType: "VoiceCallConfirmed", Summary: "Call center confirmed shipping address", OccurredAtUtc: baseTime.AddDays(6)));

        await service.RecordEventAsync(new RecordCustomerEventRequest(
            TenantId: tenantId, UnifiedCustomerId: customerId,
            EventType: "WhatsAppMessage", Summary: "Customer asked for tracking link on WhatsApp", OccurredAtUtc: baseTime.AddDays(7)));

        await service.RecordEventAsync(new RecordCustomerEventRequest(
            TenantId: tenantId, UnifiedCustomerId: customerId,
            EventType: "SupportTicket", Summary: "Support ticket opened for delivery rescheduling", OccurredAtUtc: baseTime.AddDays(8)));

        await service.RecordEventAsync(new RecordCustomerEventRequest(
            TenantId: tenantId, UnifiedCustomerId: customerId,
            EventType: "FirstDiscovery", Summary: "Initial click on Meta Ad", OccurredAtUtc: baseTime));

        // 1. Fetch chronological (oldest to newest)
        var chrono = await service.GetCustomerTimelineAsync(tenantId, customerId, chronological: true);
        Assert.Equal(6, chrono.Count);
        Assert.Equal("FirstDiscovery", chrono.First().EventType);
        Assert.Equal("SupportTicket", chrono.Last().EventType);

        // Verify channel and icon categorization
        var orderEvent = chrono.First(e => e.EventType == "OrderPlaced");
        Assert.Equal("Order", orderEvent.Channel);
        Assert.Equal("shopping-cart", orderEvent.Icon);
        Assert.Equal("success", orderEvent.Severity);

        var voiceEvent = chrono.First(e => e.EventType == "VoiceCallConfirmed");
        Assert.Equal("Voice", voiceEvent.Channel);
        Assert.Equal("phone", voiceEvent.Icon);

        var waEvent = chrono.First(e => e.EventType == "WhatsAppMessage");
        Assert.Equal("WhatsApp", waEvent.Channel);
        Assert.Equal("message-circle", waEvent.Icon);

        var supportEvent = chrono.First(e => e.EventType == "SupportTicket");
        Assert.Equal("Support", supportEvent.Channel);
        Assert.Equal("headphones", supportEvent.Icon);

        // 2. Fetch reverse-chronological (newest first)
        var reverse = await service.GetCustomerTimelineAsync(tenantId, customerId, chronological: false);
        Assert.Equal("SupportTicket", reverse.First().EventType);
        Assert.Equal("FirstDiscovery", reverse.Last().EventType);

        // 3. Filter by channel
        var voiceOnly = await service.GetCustomerTimelineAsync(tenantId, customerId, channel: "Voice");
        Assert.Single(voiceOnly);
        Assert.Equal("VoiceCallConfirmed", voiceOnly[0].EventType);
    }

    [Fact]
    public void AttributionModels_Should_Calculate_Accurately_Across_All_5_Models()
    {
        var tenantId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var baseDate = new DateTime(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);
        decimal dealRevenue = 1000m;

        // Journey of 4 touchpoints over 21 days
        var touchpoints = new List<AttributionTouchpoint>
        {
            new AttributionTouchpoint
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ContactId = contactId,
                Channel = "TikTokAds",
                CampaignName = "Viral_Hook_Fall",
                Stage = TouchpointStage.FirstDiscovery,
                Cost = 50m,
                OccurredAtUtc = baseDate // Day 0
            },
            new AttributionTouchpoint
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ContactId = contactId,
                Channel = "GoogleSearch",
                CampaignName = "HighIntent_ExactMatch",
                Stage = TouchpointStage.LeadCreated,
                Cost = 30m,
                OccurredAtUtc = baseDate.AddDays(7) // Day 7
            },
            new AttributionTouchpoint
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ContactId = contactId,
                Channel = "EmailNewsletter",
                CampaignName = "Weekly_Digest_Demo",
                Stage = TouchpointStage.OpportunityCreated,
                Cost = 5m,
                OccurredAtUtc = baseDate.AddDays(14) // Day 14
            },
            new AttributionTouchpoint
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ContactId = contactId,
                Channel = "WhatsAppDirect",
                CampaignName = "Sales_Closing_Offer",
                Stage = TouchpointStage.DealWon,
                Cost = 15m,
                OccurredAtUtc = baseDate.AddDays(21) // Day 21 (Conversion)
            }
        };

        // 1. First-Touch (100% to TikTokAds)
        var firstResult = Customer360AttributionService.ComputeFirstTouch(touchpoints, dealRevenue);
        Assert.Equal(1000m, firstResult.Shares[0].AttributedRevenue);
        Assert.Equal(100m, firstResult.Shares[0].WeightPercent);
        Assert.Equal(0m, firstResult.Shares[1].AttributedRevenue);
        Assert.Equal(0m, firstResult.Shares[2].AttributedRevenue);
        Assert.Equal(0m, firstResult.Shares[3].AttributedRevenue);
        Assert.Equal(1000m, firstResult.ChannelRevenueBreakdown["TikTokAds"]);

        // 2. Last-Touch (100% to WhatsAppDirect)
        var lastResult = Customer360AttributionService.ComputeLastTouch(touchpoints, dealRevenue);
        Assert.Equal(0m, lastResult.Shares[0].AttributedRevenue);
        Assert.Equal(0m, lastResult.Shares[1].AttributedRevenue);
        Assert.Equal(0m, lastResult.Shares[2].AttributedRevenue);
        Assert.Equal(1000m, lastResult.Shares[3].AttributedRevenue);
        Assert.Equal(100m, lastResult.Shares[3].WeightPercent);
        Assert.Equal(1000m, lastResult.ChannelRevenueBreakdown["WhatsAppDirect"]);

        // 3. Linear (25% / $250 to each of 4 touchpoints)
        var linearResult = Customer360AttributionService.ComputeLinear(touchpoints, dealRevenue);
        Assert.Equal(4, linearResult.Shares.Count);
        foreach (var share in linearResult.Shares)
        {
            Assert.Equal(250m, share.AttributedRevenue);
            Assert.Equal(25m, share.WeightPercent);
        }
        Assert.Equal(dealRevenue, linearResult.Shares.Sum(s => s.AttributedRevenue));

        // 4. U-Shaped (40% First, 40% Last, 20% split among intermediate = 10% each)
        var uShapedResult = Customer360AttributionService.ComputeUShaped(touchpoints, dealRevenue);
        Assert.Equal(400m, uShapedResult.Shares[0].AttributedRevenue); // First
        Assert.Equal(40m, uShapedResult.Shares[0].WeightPercent);
        Assert.Equal(100m, uShapedResult.Shares[1].AttributedRevenue); // Middle 1 (10%)
        Assert.Equal(100m, uShapedResult.Shares[2].AttributedRevenue); // Middle 2 (10%)
        Assert.Equal(400m, uShapedResult.Shares[3].AttributedRevenue); // Last
        Assert.Equal(40m, uShapedResult.Shares[3].WeightPercent);
        Assert.Equal(dealRevenue, uShapedResult.Shares.Sum(s => s.AttributedRevenue));

        // 5. Time-Decay (half-life τ = 7 days)
        // Days before conversion: tp3: 0 days (weight 2^0 = 1.0)
        // tp2: 7 days (weight 2^-1 = 0.5)
        // tp1: 14 days (weight 2^-2 = 0.25)
        // tp0: 21 days (weight 2^-3 = 0.125)
        // Sum weights = 1.0 + 0.5 + 0.25 + 0.125 = 1.875
        var timeDecayResult = Customer360AttributionService.ComputeTimeDecay(touchpoints, dealRevenue);
        Assert.Equal(4, timeDecayResult.Shares.Count);
        // Verify monotonically increasing weights as we get closer to conversion
        Assert.True(timeDecayResult.Shares[0].AttributedRevenue < timeDecayResult.Shares[1].AttributedRevenue);
        Assert.True(timeDecayResult.Shares[1].AttributedRevenue < timeDecayResult.Shares[2].AttributedRevenue);
        Assert.True(timeDecayResult.Shares[2].AttributedRevenue < timeDecayResult.Shares[3].AttributedRevenue);
        // Exact sum check
        Assert.Equal(dealRevenue, timeDecayResult.Shares.Sum(s => s.AttributedRevenue));
    }

    [Fact]
    public async Task CampaignRoasAndCac_Should_Calculate_Accurately()
    {
        var db = CreateDbContext();
        var service = new Customer360AttributionService(db);
        var tenantId = Guid.NewGuid();

        // Create 2 customers with orders
        var cust1 = new UnifiedCustomerProfile
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PrimaryName = "Customer One",
            PrimaryPhone = "+21699000001",
            TotalOrdersCount = 2,
            TotalSpent = 1200m
        };
        var cust2 = new UnifiedCustomerProfile
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PrimaryName = "Customer Two",
            PrimaryPhone = "+21699000002",
            TotalOrdersCount = 1,
            TotalSpent = 800m
        };
        db.UnifiedCustomerProfiles.AddRange(cust1, cust2);

        // Record touchpoints for cust1: Meta Campaign A ($100 cost), Google Campaign B ($50 cost)
        await service.RecordTouchpointAsync(new RecordTouchpointRequest(
            TenantId: tenantId, ContactId: cust1.Id, Channel: "MetaAds", CampaignName: "Campaign_A", Cost: 100m, OccurredAtUtc: DateTime.UtcNow.AddDays(-5)));
        await service.RecordTouchpointAsync(new RecordTouchpointRequest(
            TenantId: tenantId, ContactId: cust1.Id, Channel: "GoogleAds", CampaignName: "Campaign_B", Cost: 50m, OccurredAtUtc: DateTime.UtcNow));

        // Record touchpoints for cust2: Meta Campaign A ($100 cost)
        await service.RecordTouchpointAsync(new RecordTouchpointRequest(
            TenantId: tenantId, ContactId: cust2.Id, Channel: "MetaAds", CampaignName: "Campaign_A", Cost: 100m, OccurredAtUtc: DateTime.UtcNow));

        var roasReport = await service.GetCampaignRoasBreakdownAsync(tenantId, AttributionModelType.Linear);

        Assert.Equal(2, roasReport.Count);

        var campA = roasReport.First(c => c.CampaignName == "Campaign_A");
        Assert.Equal(200m, campA.TotalCost); // $100 + $100
        Assert.Equal(2, campA.CustomerCount); // cust1 and cust2
        Assert.Equal(100m, campA.Cac); // $200 / 2 customers = $100 CAC

        // Revenue attributed to Campaign A:
        // cust1 ($1200 linear split across Camp A and Camp B = $600 each)
        // cust2 ($800 linear 100% to Camp A = $800)
        // Total Attributed Revenue = $600 + $800 = $1400
        Assert.Equal(1400m, campA.AttributedRevenue);
        // True ROAS = $1400 / $200 = 7.0x
        Assert.Equal(7.0m, campA.TrueRoas);

        var campB = roasReport.First(c => c.CampaignName == "Campaign_B");
        Assert.Equal(50m, campB.TotalCost);
        Assert.Equal(1, campB.CustomerCount);
        Assert.Equal(50m, campB.Cac);
        Assert.Equal(600m, campB.AttributedRevenue);
        // True ROAS = $600 / $50 = 12.0x
        Assert.Equal(12.0m, campB.TrueRoas);
    }
}
