using FluentAssertions;
using SGrowing.Api.Modules.Attribution.MultiTouch;
using Xunit;

namespace SGrowing.Tests.Attribution;

public class MultiTouchAttributionTests
{
    private readonly MultiTouchAttributionEngine _engine = new();

    [Fact]
    public void CalculateAttribution_FirstTouch_Assigns100PercentToFirstChronologicalTouchpoint()
    {
        // Arrange
        const string tenantId = "tenant_growth_01";
        const string customerId = "cust_alpha_01";
        var baseTime = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);

        _engine.RecordTouchpoint(new CustomerTouchpoint(
            Id: Guid.NewGuid(),
            TenantId: tenantId,
            CustomerId: customerId,
            Channel: TouchpointChannel.MetaAds,
            CampaignId: "cmp_meta_awareness",
            Stage: TouchpointStage.FirstDiscovery,
            TouchCost: 150m,
            TimestampUtc: baseTime
        ));

        _engine.RecordTouchpoint(new CustomerTouchpoint(
            Id: Guid.NewGuid(),
            TenantId: tenantId,
            CustomerId: customerId,
            Channel: TouchpointChannel.GoogleAds,
            CampaignId: "cmp_google_search",
            Stage: TouchpointStage.LeadCreation,
            TouchCost: 200m,
            TimestampUtc: baseTime.AddDays(3)
        ));

        _engine.RecordTouchpoint(new CustomerTouchpoint(
            Id: Guid.NewGuid(),
            TenantId: tenantId,
            CustomerId: customerId,
            Channel: TouchpointChannel.Direct,
            CampaignId: "cmp_direct",
            Stage: TouchpointStage.DealClose,
            TouchCost: 0m,
            TimestampUtc: baseTime.AddDays(7)
        ));

        var request = new AttributionCalculationRequest(
            TenantId: tenantId,
            CustomerId: customerId,
            DealRevenue: 10000m,
            Model: AttributionModelType.FirstTouch
        );

        // Act
        var result = _engine.CalculateAttribution(request);

        // Assert
        result.Should().NotBeNull();
        result.CustomerId.Should().Be(customerId);
        result.Model.Should().Be(AttributionModelType.FirstTouch);
        result.TotalRevenue.Should().Be(10000m);
        result.ChannelShares.Should().HaveCount(1);

        var firstShare = result.ChannelShares.First();
        firstShare.Channel.Should().Be(TouchpointChannel.MetaAds);
        firstShare.WeightPercent.Should().Be(100.0);
        firstShare.AttributedRevenue.Should().Be(10000m);
        firstShare.TotalCost.Should().Be(150m);
        firstShare.Roas.Should().Be(Math.Round(10000m / 150m, 2));
    }

    [Fact]
    public void CalculateAttribution_LastTouch_Assigns100PercentToLastChronologicalTouchpoint()
    {
        // Arrange
        const string tenantId = "tenant_growth_01";
        const string customerId = "cust_alpha_02";
        var baseTime = new DateTime(2026, 2, 1, 10, 0, 0, DateTimeKind.Utc);

        _engine.RecordTouchpoint(new CustomerTouchpoint(
            Id: Guid.NewGuid(),
            TenantId: tenantId,
            CustomerId: customerId,
            Channel: TouchpointChannel.OrganicSearch,
            CampaignId: "cmp_seo_blog",
            Stage: TouchpointStage.FirstDiscovery,
            TouchCost: 20m,
            TimestampUtc: baseTime
        ));

        _engine.RecordTouchpoint(new CustomerTouchpoint(
            Id: Guid.NewGuid(),
            TenantId: tenantId,
            CustomerId: customerId,
            Channel: TouchpointChannel.LinkedInAds,
            CampaignId: "cmp_li_abm",
            Stage: TouchpointStage.OpportunityCreation,
            TouchCost: 500m,
            TimestampUtc: baseTime.AddDays(5)
        ));

        var request = new AttributionCalculationRequest(
            TenantId: tenantId,
            CustomerId: customerId,
            DealRevenue: 8000m,
            Model: AttributionModelType.LastTouch
        );

        // Act
        var result = _engine.CalculateAttribution(request);

        // Assert
        result.Should().NotBeNull();
        result.ChannelShares.Should().HaveCount(1);

        var lastShare = result.ChannelShares.First();
        lastShare.Channel.Should().Be(TouchpointChannel.LinkedInAds);
        lastShare.WeightPercent.Should().Be(100.0);
        lastShare.AttributedRevenue.Should().Be(8000m);
        lastShare.TotalCost.Should().Be(500m);
        lastShare.Roas.Should().Be(16.0m);
    }

    [Fact]
    public void CalculateAttribution_Linear_DistributesRevenueEquallyAcrossAllTouchpoints()
    {
        // Arrange
        const string tenantId = "tenant_growth_01";
        const string customerId = "cust_alpha_03";
        var baseTime = new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc);

        var channels = new[]
        {
            TouchpointChannel.OrganicSearch,
            TouchpointChannel.MetaAds,
            TouchpointChannel.GoogleAds,
            TouchpointChannel.EmailNurture
        };

        for (var i = 0; i < channels.Length; i++)
        {
            _engine.RecordTouchpoint(new CustomerTouchpoint(
                Id: Guid.NewGuid(),
                TenantId: tenantId,
                CustomerId: customerId,
                Channel: channels[i],
                CampaignId: $"cmp_step_{i}",
                Stage: TouchpointStage.MidFunnel,
                TouchCost: 100m,
                TimestampUtc: baseTime.AddDays(i * 2)
            ));
        }

        var request = new AttributionCalculationRequest(
            TenantId: tenantId,
            CustomerId: customerId,
            DealRevenue: 12000m,
            Model: AttributionModelType.Linear
        );

        // Act
        var result = _engine.CalculateAttribution(request);

        // Assert
        result.Should().NotBeNull();
        result.ChannelShares.Should().HaveCount(4);

        var sumRevenue = result.ChannelShares.Sum(c => c.AttributedRevenue);
        sumRevenue.Should().Be(12000m);

        foreach (var share in result.ChannelShares)
        {
            share.WeightPercent.Should().Be(25.0);
            share.AttributedRevenue.Should().Be(3000m);
            share.TotalCost.Should().Be(100m);
            share.Roas.Should().Be(30.0m);
        }
    }

    [Fact]
    public void CalculateAttribution_TimeDecay_Applies7DayHalfLifeExponentialDecayAndNormalizesTo100Percent()
    {
        // Arrange
        const string tenantId = "tenant_growth_01";
        const string customerId = "cust_alpha_04";
        var closeTime = new DateTime(2026, 4, 15, 12, 0, 0, DateTimeKind.Utc);

        // Day 14 (t_close) -> deltaDays = 0 -> weight = 2^0 = 1.0
        // Day 7 (t_close - 7d) -> deltaDays = 7 -> weight = 2^-1 = 0.5
        // Day 0 (t_close - 14d) -> deltaDays = 14 -> weight = 2^-2 = 0.25
        // Total raw weight = 1.0 + 0.5 + 0.25 = 1.75
        // Normalized:
        // Day 14: 1.0 / 1.75 = 4/7 (~57.14%)
        // Day 7:  0.5 / 1.75 = 2/7 (~28.57%)
        // Day 0:  0.25 / 1.75 = 1/7 (~14.29%)

        _engine.RecordTouchpoint(new CustomerTouchpoint(
            Id: Guid.NewGuid(),
            TenantId: tenantId,
            CustomerId: customerId,
            Channel: TouchpointChannel.OrganicSearch,
            CampaignId: "cmp_seo",
            Stage: TouchpointStage.FirstDiscovery,
            TouchCost: 50m,
            TimestampUtc: closeTime.AddDays(-14)
        ));

        _engine.RecordTouchpoint(new CustomerTouchpoint(
            Id: Guid.NewGuid(),
            TenantId: tenantId,
            CustomerId: customerId,
            Channel: TouchpointChannel.GoogleAds,
            CampaignId: "cmp_search",
            Stage: TouchpointStage.LeadCreation,
            TouchCost: 150m,
            TimestampUtc: closeTime.AddDays(-7)
        ));

        _engine.RecordTouchpoint(new CustomerTouchpoint(
            Id: Guid.NewGuid(),
            TenantId: tenantId,
            CustomerId: customerId,
            Channel: TouchpointChannel.OutboundSdr,
            CampaignId: "cmp_sdr",
            Stage: TouchpointStage.DealClose,
            TouchCost: 300m,
            TimestampUtc: closeTime
        ));

        var request = new AttributionCalculationRequest(
            TenantId: tenantId,
            CustomerId: customerId,
            DealRevenue: 7000m,
            Model: AttributionModelType.TimeDecay
        );

        // Act
        var result = _engine.CalculateAttribution(request);

        // Assert
        result.Should().NotBeNull();
        result.ChannelShares.Should().HaveCount(3);

        var sdrShare = result.ChannelShares.First(c => c.Channel == TouchpointChannel.OutboundSdr);
        var googleShare = result.ChannelShares.First(c => c.Channel == TouchpointChannel.GoogleAds);
        var organicShare = result.ChannelShares.First(c => c.Channel == TouchpointChannel.OrganicSearch);

        // Closest touchpoint has highest weight; oldest has lowest weight
        sdrShare.WeightPercent.Should().BeGreaterThan(googleShare.WeightPercent);
        googleShare.WeightPercent.Should().BeGreaterThan(organicShare.WeightPercent);

        sdrShare.AttributedRevenue.Should().Be(4000m);     // 7000 * (4/7)
        googleShare.AttributedRevenue.Should().Be(2000m);  // 7000 * (2/7)
        organicShare.AttributedRevenue.Should().Be(1000m); // 7000 * (1/7)

        var totalRevenue = result.ChannelShares.Sum(c => c.AttributedRevenue);
        totalRevenue.Should().Be(7000m);

        var totalWeight = result.ChannelShares.Sum(c => c.WeightPercent);
        totalWeight.Should().BeApproximately(100.0, 0.01);
    }

    [Fact]
    public void CalculateAttribution_WShaped_Assigns40PercentToKeyStagesAndRemainingToIntermediateTouches()
    {
        // Arrange
        const string tenantId = "tenant_growth_01";
        const string customerId = "cust_alpha_05";
        var baseTime = new DateTime(2026, 5, 1, 9, 0, 0, DateTimeKind.Utc);

        // 4 touchpoints:
        // Key 1: FirstDiscovery (OrganicSearch) -> raw 0.40
        // Key 2: LeadCreation (MetaAds) -> raw 0.40
        // Intermediate: MidFunnel (EmailNurture) -> raw 0.20 / 1 = 0.20
        // Key 3: OpportunityCreation (LinkedInAds) -> raw 0.40
        // Total raw = 1.40
        // Normalized:
        // Key stages: 0.40 / 1.40 = 2/7 (~28.5714%)
        // Intermediate: 0.20 / 1.40 = 1/7 (~14.2857%)

        _engine.RecordTouchpoint(new CustomerTouchpoint(
            Id: Guid.NewGuid(),
            TenantId: tenantId,
            CustomerId: customerId,
            Channel: TouchpointChannel.OrganicSearch,
            CampaignId: "cmp_seo",
            Stage: TouchpointStage.FirstDiscovery,
            TouchCost: 100m,
            TimestampUtc: baseTime
        ));

        _engine.RecordTouchpoint(new CustomerTouchpoint(
            Id: Guid.NewGuid(),
            TenantId: tenantId,
            CustomerId: customerId,
            Channel: TouchpointChannel.MetaAds,
            CampaignId: "cmp_meta",
            Stage: TouchpointStage.LeadCreation,
            TouchCost: 200m,
            TimestampUtc: baseTime.AddDays(2)
        ));

        _engine.RecordTouchpoint(new CustomerTouchpoint(
            Id: Guid.NewGuid(),
            TenantId: tenantId,
            CustomerId: customerId,
            Channel: TouchpointChannel.EmailNurture,
            CampaignId: "cmp_drip",
            Stage: TouchpointStage.MidFunnel,
            TouchCost: 50m,
            TimestampUtc: baseTime.AddDays(4)
        ));

        _engine.RecordTouchpoint(new CustomerTouchpoint(
            Id: Guid.NewGuid(),
            TenantId: tenantId,
            CustomerId: customerId,
            Channel: TouchpointChannel.LinkedInAds,
            CampaignId: "cmp_linkedin",
            Stage: TouchpointStage.OpportunityCreation,
            TouchCost: 400m,
            TimestampUtc: baseTime.AddDays(6)
        ));

        var request = new AttributionCalculationRequest(
            TenantId: tenantId,
            CustomerId: customerId,
            DealRevenue: 14000m,
            Model: AttributionModelType.WShaped
        );

        // Act
        var result = _engine.CalculateAttribution(request);

        // Assert
        result.Should().NotBeNull();
        result.ChannelShares.Should().HaveCount(4);

        var organic = result.ChannelShares.First(c => c.Channel == TouchpointChannel.OrganicSearch);
        var meta = result.ChannelShares.First(c => c.Channel == TouchpointChannel.MetaAds);
        var linkedin = result.ChannelShares.First(c => c.Channel == TouchpointChannel.LinkedInAds);
        var email = result.ChannelShares.First(c => c.Channel == TouchpointChannel.EmailNurture);

        organic.AttributedRevenue.Should().Be(4000m);
        meta.AttributedRevenue.Should().Be(4000m);
        linkedin.AttributedRevenue.Should().Be(4000m);
        email.AttributedRevenue.Should().Be(2000m);

        var totalRevenue = result.ChannelShares.Sum(c => c.AttributedRevenue);
        totalRevenue.Should().Be(14000m);

        var totalWeight = result.ChannelShares.Sum(c => c.WeightPercent);
        totalWeight.Should().BeApproximately(100.0, 0.01);
    }

    [Fact]
    public void CalculateAttribution_WShaped_WithFewerThan3Touches_DistributesEvenly()
    {
        // Arrange
        const string tenantId = "tenant_growth_01";
        const string customerId = "cust_alpha_06";
        var baseTime = new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc);

        _engine.RecordTouchpoint(new CustomerTouchpoint(
            Id: Guid.NewGuid(),
            TenantId: tenantId,
            CustomerId: customerId,
            Channel: TouchpointChannel.MetaAds,
            CampaignId: "cmp_meta",
            Stage: TouchpointStage.FirstDiscovery,
            TouchCost: 100m,
            TimestampUtc: baseTime
        ));

        _engine.RecordTouchpoint(new CustomerTouchpoint(
            Id: Guid.NewGuid(),
            TenantId: tenantId,
            CustomerId: customerId,
            Channel: TouchpointChannel.GoogleAds,
            CampaignId: "cmp_google",
            Stage: TouchpointStage.DealClose,
            TouchCost: 200m,
            TimestampUtc: baseTime.AddDays(3)
        ));

        var request = new AttributionCalculationRequest(
            TenantId: tenantId,
            CustomerId: customerId,
            DealRevenue: 6000m,
            Model: AttributionModelType.WShaped
        );

        // Act
        var result = _engine.CalculateAttribution(request);

        // Assert
        result.Should().NotBeNull();
        result.ChannelShares.Should().HaveCount(2);

        foreach (var share in result.ChannelShares)
        {
            share.WeightPercent.Should().Be(50.0);
            share.AttributedRevenue.Should().Be(3000m);
        }
    }

    [Fact]
    public void PredictCustomerLtv_HighTransactionCount_PredictsLowChurnAndHighRetentionLtv()
    {
        // Arrange
        var request = new PredictLtvRequest(
            TenantId: "tenant_growth_01",
            CustomerId: "cust_loyal_100",
            HistoricalRevenue: 24000m,
            CompletedTransactions: 12,
            FirstSeenUtc: DateTime.UtcNow.AddYears(-2)
        );

        // Act
        var result = _engine.PredictCustomerLtv(request);

        // Assert
        result.Should().NotBeNull();
        result.CustomerId.Should().Be("cust_loyal_100");
        result.ChurnProbabilityPercent.Should().BeLessThan(30.0);
        result.RetentionRiskTier.Should().Be("Low");
        result.Predicted12MonthLtv.Should().BeGreaterThan(request.HistoricalRevenue);
        result.Predicted36MonthLtv.Should().BeGreaterThan(result.Predicted12MonthLtv);
    }

    [Fact]
    public void PredictCustomerLtv_ZeroOrLowTransactions_PredictsHighRiskAndElevatedChurn()
    {
        // Arrange
        var request = new PredictLtvRequest(
            TenantId: "tenant_growth_01",
            CustomerId: "cust_churn_risk_01",
            HistoricalRevenue: 100m,
            CompletedTransactions: 0,
            FirstSeenUtc: DateTime.UtcNow.AddDays(-5)
        );

        // Act
        var result = _engine.PredictCustomerLtv(request);

        // Assert
        result.Should().NotBeNull();
        result.CustomerId.Should().Be("cust_churn_risk_01");
        result.ChurnProbabilityPercent.Should().Be(85.0);
        result.RetentionRiskTier.Should().Be("High");
        result.Predicted12MonthLtv.Should().Be(100m);
        result.Predicted36MonthLtv.Should().Be(100m);
    }

    [Fact]
    public void RecordTouchpoint_And_GetTouchpoints_MaintainsChronologicalOrderAndAssignsIds()
    {
        // Arrange
        const string tenantId = "tenant_growth_01";
        const string customerId = "cust_order_test";
        var t0 = new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Utc);

        // Insert in reverse order
        _engine.RecordTouchpoint(new CustomerTouchpoint(
            Id: Guid.Empty,
            TenantId: tenantId,
            CustomerId: customerId,
            Channel: TouchpointChannel.Direct,
            CampaignId: "cmp_3",
            Stage: TouchpointStage.DealClose,
            TouchCost: 10m,
            TimestampUtc: t0.AddDays(5)
        ));

        _engine.RecordTouchpoint(new CustomerTouchpoint(
            Id: Guid.Empty,
            TenantId: tenantId,
            CustomerId: customerId,
            Channel: TouchpointChannel.OrganicSearch,
            CampaignId: "cmp_1",
            Stage: TouchpointStage.FirstDiscovery,
            TouchCost: 0m,
            TimestampUtc: t0
        ));

        // Act
        var touchpoints = _engine.GetTouchpoints(tenantId, customerId);

        // Assert
        touchpoints.Should().HaveCount(2);
        touchpoints[0].Stage.Should().Be(TouchpointStage.FirstDiscovery);
        touchpoints[0].Id.Should().NotBeEmpty();
        touchpoints[1].Stage.Should().Be(TouchpointStage.DealClose);
        touchpoints[1].Id.Should().NotBeEmpty();
        touchpoints[0].TimestampUtc.Should().BeBefore(touchpoints[1].TimestampUtc);
    }
}
