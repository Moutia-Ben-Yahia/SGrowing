using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Moq.Protected;
using SGrowing.Api.Data;
using SGrowing.Api.Hubs;
using SGrowing.Api.Modules.Attribution;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Attribution;

public class ServerSideCdpAndIdentityStitchingTests
{
    private static SGrowingDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: $"SGrowing_Attribution_Test_{Guid.NewGuid():N}")
            .Options;

        return new SGrowingDbContext(options);
    }

    #region 1. Deterministic Identity Stitching Tests

    [Fact]
    public void GenerateVisitorId_ReturnsValidFirstPartyCookieFormat()
    {
        var service = new DeterministicIdentityStitchingService();

        var visitorId = service.GenerateVisitorId();

        visitorId.Should().StartWith("_sg_vid_");
        visitorId.Length.Should().BeGreaterThan(20);
        service.IsValidVisitorId(visitorId).Should().BeTrue();
        service.IsValidVisitorId("invalid_cookie_id").Should().BeFalse();
        service.IsValidVisitorId(null).Should().BeFalse();
    }

    [Fact]
    public async Task RecordAnonymousTouchpoint_StoresTouchpointWithDeterministicGuid()
    {
        var service = new DeterministicIdentityStitchingService();
        var tenantId = Guid.NewGuid();
        var visitorId = service.GenerateVisitorId();

        var touchpoint = await service.RecordAnonymousTouchpointAsync(new RecordAnonymousTouchpointRequest(
            TenantId: tenantId,
            AnonymousVisitorId: visitorId,
            Channel: "GoogleAds",
            CampaignName: "Spring_Promo_2026",
            Stage: TouchpointStage.FirstDiscovery,
            Cost: 25.50m,
            UtmSource: "google",
            UtmMedium: "cpc",
            UtmCampaign: "spring_sale"
        ));

        touchpoint.Should().NotBeNull();
        touchpoint.TenantId.Should().Be(tenantId);
        touchpoint.Channel.Should().Be("GoogleAds");
        touchpoint.Cost.Should().Be(25.50m);
        touchpoint.ContactId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task StitchIdentity_WithEmailAndPhone_ResolvesCanonicalContactAndUpdatesRetroactiveTouchpoints()
    {
        var db = CreateInMemoryDbContext();
        var service = new DeterministicIdentityStitchingService(db);
        var tenantId = Guid.NewGuid();
        var visitorId = service.GenerateVisitorId();

        // 1. Record 3 anonymous touchpoints
        await service.RecordAnonymousTouchpointAsync(new RecordAnonymousTouchpointRequest(
            TenantId: tenantId,
            AnonymousVisitorId: visitorId,
            Channel: "MetaAds",
            CampaignName: "Retargeting_Catalog",
            Cost: 15.00m
        ));
        await service.RecordAnonymousTouchpointAsync(new RecordAnonymousTouchpointRequest(
            TenantId: tenantId,
            AnonymousVisitorId: visitorId,
            Channel: "GoogleAds",
            CampaignName: "Search_Brand",
            Cost: 10.00m
        ));
        await service.RecordAnonymousTouchpointAsync(new RecordAnonymousTouchpointRequest(
            TenantId: tenantId,
            AnonymousVisitorId: visitorId,
            Channel: "TikTokAds",
            CampaignName: "Viral_Video_1",
            Cost: 5.00m
        ));

        // 2. Perform identification event (FormFill)
        var stitchReq = new StitchIdentityEventRequest(
            TenantId: tenantId,
            AnonymousVisitorId: visitorId,
            Email: "  Sami.BenAli@Company.TN  ",
            Phone: " +216 98 123 456 ",
            FirstName: "Sami",
            LastName: "Ben Ali",
            IdentificationSource: "FormFill",
            DeviceFingerprint: "fp_chrome_win11_a9f821",
            ClientIpAddress: "197.0.22.4",
            ClientUserAgent: "Mozilla/5.0 (Windows NT 10.0; Win64; x64)"
        );

        var result = await service.StitchIdentityAsync(stitchReq);

        result.Success.Should().BeTrue();
        result.CanonicalContactId.Should().NotBeEmpty();
        result.Email.Should().Be("sami.benali@company.tn");
        result.Phone.Should().Be("+21698123456");
        result.PrimaryName.Should().Be("Sami Ben Ali");
        result.RetroactiveTouchpointsUpdated.Should().Be(3);
        result.MergedDeviceFingerprints.Should().Contain("fp_chrome_win11_a9f821");
        result.MergedIpAddresses.Should().Contain("197.0.22.4");

        // 3. Verify all DB touchpoints are now linked to canonical contact
        var dbTouchpoints = await db.AttributionTouchpoints.Where(t => t.TenantId == tenantId).ToListAsync();
        dbTouchpoints.Should().HaveCount(3);
        dbTouchpoints.Should().OnlyContain(t => t.ContactId == result.CanonicalContactId);

        // 4. Verify CustomerIdentityLink was saved
        var links = await db.CustomerIdentityLinks.Where(l => l.TenantId == tenantId && l.UnifiedCustomerId == result.CanonicalContactId).ToListAsync();
        links.Should().Contain(l => l.IdentityType == CustomerChannelIdentityType.WebCookieId && l.IdentityValue == visitorId);
        links.Should().Contain(l => l.IdentityType == CustomerChannelIdentityType.Email && l.IdentityValue == "sami.benali@company.tn");
        links.Should().Contain(l => l.IdentityType == CustomerChannelIdentityType.PhoneE164 && l.IdentityValue == "+21698123456");

        // 5. Verify CustomerTimelineEvent was written
        var timeline = await db.CustomerTimelineEvents.Where(t => t.TenantId == tenantId && t.UnifiedCustomerId == result.CanonicalContactId).ToListAsync();
        timeline.Should().Contain(t => t.EventType == "IdentityStitched");
    }

    [Fact]
    public async Task StitchIdentity_MergesDeviceFingerprintIpAndUserAgent_IntoUnifiedProfile()
    {
        var service = new DeterministicIdentityStitchingService();
        var tenantId = Guid.NewGuid();
        var visitorId = service.GenerateVisitorId();

        // First identification via login on desktop
        await service.StitchIdentityAsync(new StitchIdentityEventRequest(
            TenantId: tenantId,
            AnonymousVisitorId: visitorId,
            Email: "karim@business.com",
            Phone: "+21620111222",
            FirstName: "Karim",
            LastName: "Trabelsi",
            IdentificationSource: "Login",
            DeviceFingerprint: "fp_desktop_win",
            ClientIpAddress: "197.1.1.10",
            ClientUserAgent: "Chrome/124.0"
        ));

        // Second identification via WhatsApp chat on mobile
        var result2 = await service.StitchIdentityAsync(new StitchIdentityEventRequest(
            TenantId: tenantId,
            AnonymousVisitorId: visitorId,
            Email: "karim@business.com",
            Phone: "+21620111222",
            IdentificationSource: "WhatsAppChat",
            DeviceFingerprint: "fp_mobile_android",
            ClientIpAddress: "197.1.1.20",
            ClientUserAgent: "WhatsAppAndroid/2.24"
        ));

        result2.MergedDeviceFingerprints.Should().BeEquivalentTo(["fp_desktop_win", "fp_mobile_android"]);
        result2.MergedIpAddresses.Should().BeEquivalentTo(["197.1.1.10", "197.1.1.20"]);
        result2.MergedUserAgents.Should().BeEquivalentTo(["Chrome/124.0", "WhatsAppAndroid/2.24"]);

        // Retrieve profile
        var profile = await service.GetUnifiedProfileAsync(tenantId, result2.CanonicalContactId);
        profile.Should().NotBeNull();
        profile!.DeviceFingerprints.Should().HaveCount(2);
        profile.IpAddresses.Should().HaveCount(2);
        profile.UserAgents.Should().HaveCount(2);
        profile.AnonymousVisitorIds.Should().Contain(visitorId);
    }

    [Fact]
    public async Task GetProfileByVisitorId_ReturnsCorrectProfile()
    {
        var service = new DeterministicIdentityStitchingService();
        var tenantId = Guid.NewGuid();
        var visitorId = service.GenerateVisitorId();

        var stitched = await service.StitchIdentityAsync(new StitchIdentityEventRequest(
            TenantId: tenantId,
            AnonymousVisitorId: visitorId,
            Email: "nadia@agency.com",
            Phone: "+21650333444",
            FirstName: "Nadia",
            IdentificationSource: "Purchase"
        ));

        var fetched = await service.GetProfileByVisitorIdAsync(tenantId, visitorId);
        fetched.Should().NotBeNull();
        fetched!.ContactId.Should().Be(stitched.CanonicalContactId);
        fetched.PrimaryEmail.Should().Be("nadia@agency.com");
    }

    #endregion

    #region 2. Multi-Model Attribution Engine Tests (6 Models + Shapley)

    [Fact]
    public void ComputeFirstTouch_Attributes100PercentRevenueToFirstChannel()
    {
        var service = new MultiModelRevenueAttributionService();
        var now = DateTime.UtcNow;

        var touchpoints = new List<TouchpointInput>
        {
            new("MetaAds", "Campaign_A", now.AddDays(-10)),
            new("GoogleAds", "Campaign_B", now.AddDays(-5)),
            new("Email", "Newsletter_C", now.AddDays(-1))
        };

        var output = service.ComputeFirstTouch(touchpoints, 10000m);

        output.ModelName.Should().Be("First Touch");
        var meta = output.Channels.First(c => c.Channel == "MetaAds");
        meta.AttributedRevenue.Should().Be(10000m);
        meta.WeightPercentage.Should().Be(100.0);

        var google = output.Channels.First(c => c.Channel == "GoogleAds");
        google.AttributedRevenue.Should().Be(0m);
        google.WeightPercentage.Should().Be(0.0);

        var email = output.Channels.First(c => c.Channel == "Email");
        email.AttributedRevenue.Should().Be(0m);
    }

    [Fact]
    public void ComputeLastTouch_Attributes100PercentRevenueToLastChannel()
    {
        var service = new MultiModelRevenueAttributionService();
        var now = DateTime.UtcNow;

        var touchpoints = new List<TouchpointInput>
        {
            new("MetaAds", "Campaign_A", now.AddDays(-10)),
            new("GoogleAds", "Campaign_B", now.AddDays(-5)),
            new("WhatsApp", "Direct_Closing", now.AddDays(-1))
        };

        var output = service.ComputeLastTouch(touchpoints, 7500m);

        output.ModelName.Should().Be("Last Touch");
        var wa = output.Channels.First(c => c.Channel == "WhatsApp");
        wa.AttributedRevenue.Should().Be(7500m);
        wa.WeightPercentage.Should().Be(100.0);

        var meta = output.Channels.First(c => c.Channel == "MetaAds");
        meta.AttributedRevenue.Should().Be(0m);
    }

    [Fact]
    public void ComputeLinear_DistributesRevenueEquallyAcrossAllTouchpoints()
    {
        var service = new MultiModelRevenueAttributionService();
        var now = DateTime.UtcNow;

        var touchpoints = new List<TouchpointInput>
        {
            new("GoogleAds", "Top_Search", now.AddDays(-10)),
            new("MetaAds", "Remarketing", now.AddDays(-6)),
            new("LinkedIn", "InMail_Lead", now.AddDays(-3)),
            new("GoogleAds", "Brand_Protect", now.AddDays(-1))
        };

        var output = service.ComputeLinear(touchpoints, 10000m);

        output.ModelName.Should().Be("Linear");
        var totalAllocated = output.Channels.Sum(c => c.AttributedRevenue);
        totalAllocated.Should().Be(10000m);

        // GoogleAds had 2 out of 4 touches (50%)
        var google = output.Channels.First(c => c.Channel == "GoogleAds");
        google.AttributedRevenue.Should().Be(5000m);
        google.WeightPercentage.Should().Be(50.0);

        // MetaAds had 1 out of 4 (25%)
        var meta = output.Channels.First(c => c.Channel == "MetaAds");
        meta.AttributedRevenue.Should().Be(2500m);
        meta.WeightPercentage.Should().Be(25.0);

        // LinkedIn had 1 out of 4 (25%)
        var linkedin = output.Channels.First(c => c.Channel == "LinkedIn");
        linkedin.AttributedRevenue.Should().Be(2500m);
        linkedin.WeightPercentage.Should().Be(25.0);
    }

    [Fact]
    public void ComputeTimeDecay_ExponentialDecayWith7DayHalfLife_AttributesCorrectRevenue()
    {
        var service = new MultiModelRevenueAttributionService();
        var convTime = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);

        var touchpoints = new List<TouchpointInput>
        {
            new("ChannelOld", "T1", convTime.AddDays(-14)), // delta = 14 days -> weight = 2^(-14/7) = 0.25
            new("ChannelMid", "T2", convTime.AddDays(-7)),  // delta = 7 days  -> weight = 2^(-7/7)  = 0.50
            new("ChannelNew", "T3", convTime.AddDays(0))    // delta = 0 days  -> weight = 2^(0/7)   = 1.00
        };

        // Total raw weight = 0.25 + 0.50 + 1.00 = 1.75
        // T1 weight = 0.25 / 1.75 = 1/7 (~14.29%)
        // T2 weight = 0.50 / 1.75 = 2/7 (~28.57%)
        // T3 weight = 1.00 / 1.75 = 4/7 (~57.14%)

        var output = service.ComputeTimeDecay(touchpoints, 7000m, convTime, halfLifeDays: 7.0);

        output.ModelName.Should().Be("Time-Decay");
        var totalRev = output.Channels.Sum(c => c.AttributedRevenue);
        totalRev.Should().Be(7000m);

        var chNew = output.Channels.First(c => c.Channel == "ChannelNew");
        chNew.AttributedRevenue.Should().Be(4000m); // 4/7 of 7000

        var chMid = output.Channels.First(c => c.Channel == "ChannelMid");
        chMid.AttributedRevenue.Should().Be(2000m); // 2/7 of 7000

        var chOld = output.Channels.First(c => c.Channel == "ChannelOld");
        chOld.AttributedRevenue.Should().Be(1000m); // 1/7 of 7000
    }

    [Fact]
    public void ComputeUShaped_WithMultipleTouches_Gives40First40Last20Middle()
    {
        var service = new MultiModelRevenueAttributionService();
        var now = DateTime.UtcNow;

        var touchpoints = new List<TouchpointInput>
        {
            new("MetaAds", "First", now.AddDays(-15)),
            new("GoogleAds", "Middle1", now.AddDays(-10)),
            new("Email", "Middle2", now.AddDays(-5)),
            new("Direct", "Last", now.AddDays(-1))
        };

        var output = service.ComputeUShaped(touchpoints, 10000m);

        output.ModelName.Should().Be("Position-Based / U-Shape");
        var totalRev = output.Channels.Sum(c => c.AttributedRevenue);
        totalRev.Should().Be(10000m);

        var meta = output.Channels.First(c => c.Channel == "MetaAds");
        meta.AttributedRevenue.Should().Be(4000m);
        meta.WeightPercentage.Should().Be(40.0);

        var direct = output.Channels.First(c => c.Channel == "Direct");
        direct.AttributedRevenue.Should().Be(4000m);
        direct.WeightPercentage.Should().Be(40.0);

        // Middle 2 channels share 20% evenly (10% each)
        var google = output.Channels.First(c => c.Channel == "GoogleAds");
        google.AttributedRevenue.Should().Be(1000m);
        google.WeightPercentage.Should().Be(10.0);

        var email = output.Channels.First(c => c.Channel == "Email");
        email.AttributedRevenue.Should().Be(1000m);
        email.WeightPercentage.Should().Be(10.0);
    }

    [Fact]
    public void ComputeUShaped_BoundaryConditions_SingleAndTwoTouches()
    {
        var service = new MultiModelRevenueAttributionService();
        var now = DateTime.UtcNow;

        // 1 Touch
        var single = service.ComputeUShaped([new TouchpointInput("Search", "P1", now)], 5000m);
        single.Channels[0].AttributedRevenue.Should().Be(5000m);
        single.Channels[0].WeightPercentage.Should().Be(100.0);

        // 2 Touches
        var pair = service.ComputeUShaped([
            new TouchpointInput("ChA", "P1", now.AddDays(-2)),
            new TouchpointInput("ChB", "P2", now.AddDays(-1))
        ], 6000m);
        pair.Channels.First(c => c.Channel == "ChA").AttributedRevenue.Should().Be(3000m);
        pair.Channels.First(c => c.Channel == "ChB").AttributedRevenue.Should().Be(3000m);
    }

    [Fact]
    public void ComputeMarkovChain_CalculatesRemovalEffectsAcrossChannels()
    {
        var service = new MultiModelRevenueAttributionService();
        var now = DateTime.UtcNow;

        var targetJourney = new List<TouchpointInput>
        {
            new("Search", "C1", now.AddDays(-5)),
            new("Social", "C2", now.AddDays(-3)),
            new("Email", "C3", now.AddDays(-1))
        };

        var cohort = new List<CustomerJourneyInput>
        {
            // Journey 1: Search -> Social -> Converted
            new("J1", [new TouchpointInput("Search", "C1", now.AddDays(-6)), new TouchpointInput("Social", "C2", now.AddDays(-4))], true, 1000m),
            // Journey 2: Social -> Email -> Converted
            new("J2", [new TouchpointInput("Social", "C2", now.AddDays(-5)), new TouchpointInput("Email", "C3", now.AddDays(-2))], true, 2000m),
            // Journey 3: Search -> Email -> Converted
            new("J3", [new TouchpointInput("Search", "C1", now.AddDays(-7)), new TouchpointInput("Email", "C3", now.AddDays(-1))], true, 1500m)
        };

        var output = service.ComputeMarkovChain(targetJourney, 9000m, cohort);

        output.ModelName.Should().Be("Markov-Chain Attribution");
        output.Channels.Should().HaveCount(3);
        var totalRev = output.Channels.Sum(c => c.AttributedRevenue);
        totalRev.Should().Be(9000m);

        // All channels should have positive attribution
        output.Channels.Should().AllSatisfy(c => c.AttributedRevenue.Should().BeGreaterThan(0m));
    }

    [Fact]
    public void ComputeShapley_CalculatesCooperativeMarginalContributions()
    {
        var service = new MultiModelRevenueAttributionService();
        var now = DateTime.UtcNow;

        var touchpoints = new List<TouchpointInput>
        {
            new("Google", "G1", now.AddDays(-4)),
            new("Meta", "M1", now.AddDays(-2)),
            new("Email", "E1", now.AddDays(-1))
        };

        var output = service.ComputeShapley(touchpoints, 12000m);

        output.ModelName.Should().Be("Shapley Value Attribution");
        output.Channels.Should().HaveCount(3);
        var sumRev = output.Channels.Sum(c => c.AttributedRevenue);
        sumRev.Should().Be(12000m);
        output.Channels.Should().AllSatisfy(c => c.WeightPercentage.Should().BeGreaterThan(0.0));
    }

    [Fact]
    public async Task CalculateMultiModelMatrix_GeneratesComparisonMatrixRowsAndRecommendations()
    {
        var service = new MultiModelRevenueAttributionService();
        var now = DateTime.UtcNow;

        var req = new CalculateMultiModelRequest(
            TenantId: Guid.NewGuid(),
            TotalRevenue: 20000m,
            Touchpoints:
            [
                new TouchpointInput("GoogleAds", "Top_Search", now.AddDays(-12)),
                new TouchpointInput("MetaAds", "Retargeting", now.AddDays(-7)),
                new TouchpointInput("WhatsApp", "Consultation", now.AddDays(-1))
            ]
        );

        var matrix = await service.CalculateMultiModelMatrixAsync(req);

        matrix.TotalRevenue.Should().Be(20000m);
        matrix.EvaluatedChannels.Should().BeEquivalentTo(["GoogleAds", "MetaAds", "WhatsApp"]);
        matrix.Models.Should().ContainKey("FirstTouch");
        matrix.Models.Should().ContainKey("LastTouch");
        matrix.Models.Should().ContainKey("Linear");
        matrix.Models.Should().ContainKey("TimeDecay");
        matrix.Models.Should().ContainKey("UShaped");
        matrix.Models.Should().ContainKey("MarkovChain");
        matrix.Models.Should().ContainKey("Shapley");

        matrix.MatrixRows.Should().HaveCount(3);
        var googleRow = matrix.MatrixRows.First(r => r.Channel == "GoogleAds");
        googleRow.FirstTouchRevenue.Should().Be(20000m);
        googleRow.LastTouchRevenue.Should().Be(0m);
        googleRow.Recommendation.Should().Contain("top-of-funnel discovery");

        var waRow = matrix.MatrixRows.First(r => r.Channel == "WhatsApp");
        waRow.FirstTouchRevenue.Should().Be(0m);
        waRow.LastTouchRevenue.Should().Be(20000m);
        waRow.Recommendation.Should().Contain("bottom-of-funnel");
    }

    #endregion

    #region 3. Server-Side CAPI Fanout & Hashing Tests

    [Fact]
    public void NormalizePii_StandardizesAndHashesEmailPhoneNamesAndAddress()
    {
        var service = new ServerSideCapiFanoutService();

        var raw = new RawCustomerPii(
            Email: "  Mohamed.Slimane@Example.COM  ",
            Phone: " +216 98-765-432 ",
            FirstName: "  Mohamed ",
            LastName: " Slimane  ",
            City: "  Tunis ",
            State: " TUN ",
            Zip: " 1002 ",
            Country: " TN "
        );

        var pii = service.NormalizePii(raw);

        // Expected SHA-256 for "mohamed.slimane@example.com"
        var expectedEmailHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("mohamed.slimane@example.com")));
        pii.HashedEmail.Should().Be(expectedEmailHash);

        // Expected Phone: "21698765432"
        var expectedPhoneHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("21698765432")));
        pii.HashedPhone.Should().Be(expectedPhoneHash);

        pii.HashedFirstName.Should().NotBeNullOrEmpty();
        pii.HashedLastName.Should().NotBeNullOrEmpty();
        pii.HashedCity.Should().NotBeNullOrEmpty();
        pii.HashedZip.Should().NotBeNullOrEmpty();
        pii.HashedCountry.Should().NotBeNullOrEmpty();

        pii.RawNormalizedCity.Should().Be("tunis");
        pii.RawNormalizedZip.Should().Be("1002");
        pii.RawNormalizedCountry.Should().Be("tn");
    }

    [Fact]
    public void FormatMetaPayload_ContainsUserDataAndCustomData_WithSha256Hashes()
    {
        var service = new ServerSideCapiFanoutService();
        var tenantId = Guid.NewGuid();
        var pii = service.NormalizePii(new RawCustomerPii(
            Email: "client@store.com",
            Phone: "+15551234567",
            ClientIpAddress: "24.120.30.5",
            ClientUserAgent: "Mozilla/5.0",
            Fbc: "fb.1.1712000000.IwAR123",
            Fbp: "fb.1.1712000000.987654321"
        ));

        var payload = new CapiConversionPayload(
            TenantId: tenantId,
            EventName: "Purchase",
            EventId: "ev_order_1001",
            EventTimeUtc: new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc),
            Value: 149.99m,
            Currency: "USD",
            OrderId: "ORD-998877"
        );

        var json = service.FormatMetaPayload(payload, pii, testEventCode: "TEST999");

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.GetProperty("test_event_code").GetString().Should().Be("TEST999");
        var dataArray = root.GetProperty("data");
        dataArray.GetArrayLength().Should().Be(1);

        var eventItem = dataArray[0];
        eventItem.GetProperty("event_name").GetString().Should().Be("Purchase");
        eventItem.GetProperty("event_id").GetString().Should().Be("ev_order_1001");

        var userData = eventItem.GetProperty("user_data");
        userData.GetProperty("em")[0].GetString().Should().Be(pii.HashedEmail);
        userData.GetProperty("ph")[0].GetString().Should().Be(pii.HashedPhone);
        userData.GetProperty("client_ip_address").GetString().Should().Be("24.120.30.5");
        userData.GetProperty("fbc").GetString().Should().Be("fb.1.1712000000.IwAR123");

        var customData = eventItem.GetProperty("custom_data");
        customData.GetProperty("value").GetDecimal().Should().Be(149.99m);
        customData.GetProperty("currency").GetString().Should().Be("USD");
        customData.GetProperty("order_id").GetString().Should().Be("ORD-998877");
    }

    [Fact]
    public void FormatGoogleEnhancedConversionsPayload_ContainsHashedEmailPhoneAndAddress()
    {
        var service = new ServerSideCapiFanoutService();
        var tenantId = Guid.NewGuid();
        var pii = service.NormalizePii(new RawCustomerPii(
            Email: "lead@acme.org",
            Phone: "+15559876543",
            FirstName: "Alice",
            LastName: "Smith",
            City: "Chicago",
            State: "IL",
            Zip: "60601",
            Country: "US",
            Gclid: "Cj0KCQjwxsm3BhDrARIsALtI..."
        ));

        var payload = new CapiConversionPayload(
            TenantId: tenantId,
            EventName: "Lead",
            EventId: "ev_lead_4455",
            EventTimeUtc: new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc),
            Value: 50.00m,
            Currency: "USD"
        );

        var json = service.FormatGoogleEnhancedConversionsPayload(payload, pii);

        using var doc = JsonDocument.Parse(json);
        var conversions = doc.RootElement.GetProperty("conversions");
        conversions.GetArrayLength().Should().Be(1);

        var conv = conversions[0];
        conv.GetProperty("conversion_value").GetDecimal().Should().Be(50.00m);
        conv.GetProperty("gclid").GetString().Should().Be("Cj0KCQjwxsm3BhDrARIsALtI...");

        var userIds = conv.GetProperty("user_identifiers");
        userIds.GetArrayLength().Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void FormatTikTokEventsPayload_ContainsEventAndUserHashedPii()
    {
        var service = new ServerSideCapiFanoutService();
        var tenantId = Guid.NewGuid();
        var pii = service.NormalizePii(new RawCustomerPii(
            Email: "buyer@tiktok.shop",
            Phone: "+33612345678",
            Ttclid: "ttclid_viral_12345"
        ));

        var payload = new CapiConversionPayload(
            TenantId: tenantId,
            EventName: "CompletePayment",
            EventId: "ev_tt_7788",
            EventTimeUtc: new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc),
            Value: 89.00m,
            Currency: "EUR"
        );

        var json = service.FormatTikTokEventsPayload(payload, pii);

        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("event_source").GetString().Should().Be("web");
        var data = doc.RootElement.GetProperty("data")[0];
        data.GetProperty("event").GetString().Should().Be("CompletePayment");
        data.GetProperty("user").GetProperty("email").GetString().Should().Be(pii.HashedEmail);
        data.GetProperty("user").GetProperty("ttclid").GetString().Should().Be("ttclid_viral_12345");
        data.GetProperty("properties").GetProperty("value").GetDecimal().Should().Be(89.00m);
    }

    [Fact]
    public async Task DispatchAsync_WhenCircuitBreakerIsOpen_FastFailsWithoutCallingHttp()
    {
        var mockHttp = new Mock<HttpMessageHandler>();
        var client = new HttpClient(mockHttp.Object);
        var service = new ServerSideCapiFanoutService(client);

        service.TripCircuitBreakerForTesting(CapiPlatform.TikTok);
        service.GetCircuitBreakerState(CapiPlatform.TikTok).Should().Be(CircuitState.Open);

        var req = new CapiDispatchEventRequest(
            TenantId: Guid.NewGuid(),
            EventName: "Purchase",
            TargetPlatforms: [CapiPlatform.TikTok]
        );

        var result = await service.DispatchAsync(req);

        result.AllSucceeded.Should().BeFalse();
        var outcome = result.PlatformOutcomes.First(o => o.Platform == CapiPlatform.TikTok);
        outcome.Success.Should().BeFalse();
        outcome.CircuitState.Should().Be(CircuitState.Open);
        outcome.StatusCode.Should().Be(503);
        outcome.ErrorMessage.Should().Contain("Circuit breaker");

        // Verify HTTP was never sent for TikTok
        mockHttp.Protected().Verify(
            "SendAsync",
            Times.Never(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>()
        );
    }

    [Fact]
    public async Task DispatchAsync_WithTransient500_RetriesAndSucceeds()
    {
        var mockHttp = new Mock<HttpMessageHandler>();
        int callCount = 0;

        mockHttp.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 1)
                {
                    return new HttpResponseMessage(HttpStatusCode.InternalServerError)
                    {
                        Content = new StringContent("{\"error\":\"Internal Server Error\"}")
                    };
                }
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"events_received\":1,\"messages\":[]}")
                };
            });

        var client = new HttpClient(mockHttp.Object);
        var service = new ServerSideCapiFanoutService(client);

        var req = new CapiDispatchEventRequest(
            TenantId: Guid.NewGuid(),
            EventName: "Purchase",
            TargetPlatforms: [CapiPlatform.Meta]
        );

        var result = await service.DispatchAsync(req);

        result.AllSucceeded.Should().BeTrue();
        var metaOutcome = result.PlatformOutcomes.First(o => o.Platform == CapiPlatform.Meta);
        metaOutcome.Success.Should().BeTrue();
        metaOutcome.AttemptCount.Should().Be(2);
        metaOutcome.StatusCode.Should().Be(200);
        metaOutcome.ResponseJson.Should().Contain("events_received");
    }

    #endregion

    #region 4. Funnel Slippage Monitor & Alert Tests

    [Fact]
    public async Task IngestStepEvent_AccumulatesCountsAcrossFunnelStages()
    {
        var monitor = new FunnelSlippageMonitorService();
        var tenantId = Guid.NewGuid();

        // Ingest 100 ViewItem, 40 AddToCart, 20 InitiateCheckout, 10 SubmitPayment
        for (int i = 0; i < 100; i++)
            await monitor.IngestStepEventAsync(new FunnelStepEvent(tenantId, "ViewItem"));

        for (int i = 0; i < 40; i++)
            await monitor.IngestStepEventAsync(new FunnelStepEvent(tenantId, "AddToCart"));

        for (int i = 0; i < 20; i++)
            await monitor.IngestStepEventAsync(new FunnelStepEvent(tenantId, "InitiateCheckout"));

        for (int i = 0; i < 10; i++)
            await monitor.IngestStepEventAsync(new FunnelStepEvent(tenantId, "SubmitPayment"));

        var report = await monitor.GetSlippageReportAsync(tenantId);

        report.TenantId.Should().Be(tenantId);
        report.StepMetrics.Should().HaveCount(4);
        report.StepMetrics.First(s => s.StepName == "ViewItem").EventCount.Should().Be(100);
        report.StepMetrics.First(s => s.StepName == "AddToCart").EventCount.Should().Be(40);
        report.StepMetrics.First(s => s.StepName == "InitiateCheckout").EventCount.Should().Be(20);
        report.StepMetrics.First(s => s.StepName == "SubmitPayment").EventCount.Should().Be(10);

        // Overall conversion rate: 10 / 100 = 10%
        report.OverallConversionRate.Should().Be(10.0);
    }

    [Fact]
    public async Task IngestStepEvent_WhenConversionDropsBelowTwoStdDev_TriggersSlippageAlert()
    {
        var mockHubContext = new Mock<IHubContext<OmniHub, IOmniClient>>();
        var mockClients = new Mock<IHubClients<IOmniClient>>();
        var mockClientProxy = new Mock<IOmniClient>();

        mockHubContext.Setup(h => h.Clients).Returns(mockClients.Object);
        mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(mockClientProxy.Object);

        var monitor = new FunnelSlippageMonitorService(mockHubContext.Object);
        var tenantId = Guid.NewGuid();

        // 1. Seed historical baseline for AddToCart -> InitiateCheckout
        // Mean = 50%, StdDev = 2.0%
        var historicalDailyRates = new[] { 0.49, 0.51, 0.50, 0.48, 0.52, 0.50, 0.50 };
        monitor.SeedHistoricalDailyRates(tenantId, "AddToCart", "InitiateCheckout", historicalDailyRates);

        // 2. Ingest 100 AddToCart events
        for (int i = 0; i < 100; i++)
        {
            await monitor.IngestStepEventAsync(new FunnelStepEvent(tenantId, "AddToCart"));
        }

        // 3. Ingest only 10 InitiateCheckout events (conversion rate = 10 / 100 = 10%)
        // Baseline mean is 50%, with stddev ~1.3%. Z-score = (0.10 - 0.50) / 0.013 = -30.7! Well below -2.0.
        FunnelSlippageAlertDto? triggeredAlert = null;
        for (int i = 0; i < 10; i++)
        {
            var alert = await monitor.IngestStepEventAsync(new FunnelStepEvent(tenantId, "InitiateCheckout"));
            if (alert != null)
            {
                triggeredAlert = alert;
            }
        }

        triggeredAlert.Should().NotBeNull();
        triggeredAlert!.TenantId.Should().Be(tenantId);
        triggeredAlert.StepFrom.Should().Be("AddToCart");
        triggeredAlert.StepTo.Should().Be("InitiateCheckout");
        triggeredAlert.CurrentRate.Should().Be(0.10);
        triggeredAlert.ZScore.Should().BeLessThan(-2.0);
        triggeredAlert.Severity.Should().Be("Critical");
        triggeredAlert.Message.Should().Contain("Funnel conversion slippage detected");

        // Verify SignalR broadcast to OmniHub tenant group
        mockClientProxy.Verify(
            c => c.OnFunnelSlippageAlert(It.Is<FunnelSlippageAlertDto>(a => a.TenantId == tenantId && a.StepFrom == "AddToCart")),
            Times.AtLeastOnce()
        );

        // Verify report marks slippage active
        var report = await monitor.GetSlippageReportAsync(tenantId);
        report.HasActiveSlippage.Should().BeTrue();
        report.ActiveAlerts.Should().Contain(a => a.StepFrom == "AddToCart" && a.StepTo == "InitiateCheckout");
    }

    [Fact]
    public async Task GetSlippageReport_CalculatesTransitionMetricsCorrectly()
    {
        var monitor = new FunnelSlippageMonitorService();
        var tenantId = Guid.NewGuid();

        // Ingest normal healthy numbers
        for (int i = 0; i < 50; i++)
            await monitor.IngestStepEventAsync(new FunnelStepEvent(tenantId, "ViewItem"));

        for (int i = 0; i < 25; i++)
            await monitor.IngestStepEventAsync(new FunnelStepEvent(tenantId, "AddToCart"));

        var report = await monitor.GetSlippageReportAsync(tenantId);

        var transition = report.Transitions.First(t => t.FromStep == "ViewItem" && t.ToStep == "AddToCart");
        transition.FromCount.Should().Be(50);
        transition.ToCount.Should().Be(25);
        transition.CurrentRate.Should().Be(0.50);
        transition.Status.Should().Be("Healthy");
        transition.IsSlipping.Should().BeFalse();
    }

    #endregion
}
