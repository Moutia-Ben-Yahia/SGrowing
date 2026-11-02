using FluentAssertions;
using SGrowing.Api.Modules.Advertising.MetaGoogleAdsSync;
using Xunit;

namespace SGrowing.Tests.Advertising;

public class MetaGoogleAdsSyncTests
{
    private readonly AdsSyncEngine _engine = new();

    [Fact]
    public void UploadConversions_WithValidEvents_AcceptsAndComputesPayloadHashes()
    {
        var request = new CapiUploadRequest(
            TenantId: "tenant_growth_ads",
            AdAccountId: "act_10928374",
            Platform: "Meta",
            Events: new List<ConversionEvent>
            {
                new(
                    EventId: "evt_purchase_001",
                    EventName: "Purchase",
                    EventTimeUtc: DateTime.UtcNow,
                    Platform: "Meta",
                    Email: "sarah.connor@cyberdyne.io",
                    PhoneNumber: "+1-555-019-2834",
                    ClientIpAddress: "203.0.113.195",
                    UserAgent: "Mozilla/5.0",
                    Fbp: "fb.1.1620000000.12345678",
                    ConversionValue: 249.99m,
                    Currency: "USD",
                    OrderId: "ORD-9821"
                ),
                new(
                    EventId: "evt_lead_002",
                    EventName: "Lead",
                    EventTimeUtc: DateTime.UtcNow,
                    Platform: "Meta",
                    Email: "john.doe@startup.com",
                    ConversionValue: 0m
                )
            }
        );

        var result = _engine.UploadConversions(request);

        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.AcceptedEventsCount.Should().Be(2);
        result.DeduplicatedEventsCount.Should().Be(0);
        result.GeneratedPayloadHashes.Should().HaveCount(2);
        result.GeneratedPayloadHashes[0].Should().HaveLength(64); // SHA256 hex
    }

    [Fact]
    public void UploadConversions_WithDuplicateEventId_CorrectlyDeduplicates()
    {
        var request = new CapiUploadRequest(
            TenantId: "tenant_growth_ads_dedup",
            AdAccountId: "act_google_776",
            Platform: "Google",
            Events: new List<ConversionEvent>
            {
                new(
                    EventId: "evt_dup_123",
                    EventName: "Purchase",
                    EventTimeUtc: DateTime.UtcNow,
                    Platform: "Google",
                    Gclid: "gclid_sample_token_xyz",
                    ConversionValue: 120.00m
                )
            }
        );

        var firstResult = _engine.UploadConversions(request);
        firstResult.AcceptedEventsCount.Should().Be(1);
        firstResult.DeduplicatedEventsCount.Should().Be(0);

        // Upload same event again
        var secondResult = _engine.UploadConversions(request);
        secondResult.AcceptedEventsCount.Should().Be(0);
        secondResult.DeduplicatedEventsCount.Should().Be(1);
    }

    [Fact]
    public void SyncAudienceHashes_WithRawEmailsAndPhones_NormalizesAndProducesUniqueSha256()
    {
        var request = new AudienceHashSyncRequest(
            TenantId: "tenant_growth_aud",
            Platform: "Meta",
            AudienceId: "aud_lookalike_top_buyers",
            RawEmails: new List<string>
            {
                "Alice@Example.com",
                "  alice@example.com ", // duplicate after trim & lowercase
                "bob@example.com"
            },
            RawPhoneNumbers: new List<string>
            {
                "+1 (555) 000-1111",
                "+15550001111" // duplicate after normalization
            }
        );

        var result = _engine.SyncAudienceHashes(request);

        result.Should().NotBeNull();
        result.TotalRecords.Should().Be(5);
        result.ValidHashesGenerated.Should().Be(3); // 2 unique emails + 1 unique phone
        result.Sha256Hashes.Should().HaveCount(3);
    }

    [Fact]
    public void OptimizeRoas_WithHighRoasCampaign_RecommendsScaleWithDiminishingReturns()
    {
        var request = new RoasOptimizationRequest(
            TenantId: "tenant_roas_scale",
            TotalBudget: 1000m,
            TargetRoas: 3.0m,
            Campaigns: new List<CampaignPerformanceRecord>
            {
                new("camp-high", "High Intent Retargeting", "Meta", Spend: 300m, Revenue: 1500m, Conversions: 30, Impressions: 50000, Clicks: 2500), // ROAS = 5.0 (Scale)
                new("camp-avg", "Lookalike Top 1%", "Google", Spend: 400m, Revenue: 1200m, Conversions: 24, Impressions: 80000, Clicks: 3000), // ROAS = 3.0 (Maintain)
                new("camp-low", "Broad Interest Awareness", "Meta", Spend: 300m, Revenue: 300m, Conversions: 4, Impressions: 120000, Clicks: 1500) // ROAS = 1.0 (Trim)
            },
            MaxShiftPercentage: 0.30m
        );

        var result = _engine.OptimizeRoas(request);

        result.Should().NotBeNull();
        result.CurrentTotalSpend.Should().Be(1000m);
        result.BlendedCurrentRoas.Should().Be(3.0m);

        var scaleRec = result.Recommendations.First(r => r.CampaignId == "camp-high");
        scaleRec.AllocationAction.Should().Be("Scale");
        scaleRec.RecommendedSpend.Should().BeGreaterThan(300m); // Scaled up from trimmed pool
        scaleRec.ProjectedRoas.Should().BeLessThan(scaleRec.CurrentRoas); // Diminishing returns applied

        var trimRec = result.Recommendations.First(r => r.CampaignId == "camp-low");
        trimRec.AllocationAction.Should().Be("Trim");
        trimRec.RecommendedSpend.Should().Be(210m); // 300 - 30% = 210m
    }

    [Fact]
    public void OptimizeRoas_WithZeroConversionsAndSpend_RecommendsPauseAndFreesBudget()
    {
        var request = new RoasOptimizationRequest(
            TenantId: "tenant_roas_pause",
            TotalBudget: 500m,
            TargetRoas: 2.5m,
            Campaigns: new List<CampaignPerformanceRecord>
            {
                new("camp-winner", "Search Brand Terms", "Google", Spend: 300m, Revenue: 1200m, Conversions: 20, Impressions: 10000, Clicks: 1500),
                new("camp-waste", "Defunct Display Network", "Google", Spend: 200m, Revenue: 0m, Conversions: 0, Impressions: 45000, Clicks: 300)
            }
        );

        var result = _engine.OptimizeRoas(request);

        var pausedRec = result.Recommendations.First(r => r.CampaignId == "camp-waste");
        pausedRec.AllocationAction.Should().Be("Pause");
        pausedRec.RecommendedSpend.Should().Be(0m);

        var winnerRec = result.Recommendations.First(r => r.CampaignId == "camp-winner");
        winnerRec.AllocationAction.Should().Be("Scale");
        winnerRec.RecommendedSpend.Should().Be(500m); // Absorbed the 200m paused budget
    }
}
