using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using SGrowing.Api.Modules.CRM;
using Xunit;

namespace SGrowing.Tests.CRM;

public class CustomerRetentionDaemonServiceTests
{
    private readonly CustomerRetentionDaemonService _service = new();
    private readonly Guid _tenantId = Guid.NewGuid();

    [Fact]
    public void CalculateRfmQuintiles_Should_Return_Highest_Quintiles_For_Recent_Frequent_High_Spenders()
    {
        // Act: 5 days ago, 15 orders, $4500 spend
        var quintiles = _service.CalculateRfmQuintiles(recencyDays: 5, frequencyOrders: 15, monetarySpend: 4500m);

        // Assert
        quintiles.RecencyScore.Should().Be(5);
        quintiles.FrequencyScore.Should().Be(5);
        quintiles.MonetaryScore.Should().Be(5);
        quintiles.Cell.Should().Be("5-5-5");
    }

    [Fact]
    public void CalculateRfmQuintiles_Should_Return_Lowest_Quintiles_For_Inactive_Infrequent_Low_Spenders()
    {
        // Act: 150 days ago, 1 order, $50 spend
        var quintiles = _service.CalculateRfmQuintiles(recencyDays: 150, frequencyOrders: 1, monetarySpend: 50m);

        // Assert
        quintiles.RecencyScore.Should().Be(1);
        quintiles.FrequencyScore.Should().Be(1);
        quintiles.MonetaryScore.Should().Be(1);
        quintiles.Cell.Should().Be("1-1-1");
    }

    [Theory]
    [InlineData(5, 5, 5, RfmSegment.Champions)]
    [InlineData(4, 4, 4, RfmSegment.Champions)]
    [InlineData(3, 4, 3, RfmSegment.LoyalCustomers)]
    [InlineData(5, 2, 2, RfmSegment.PotentialLoyalists)]
    [InlineData(1, 4, 5, RfmSegment.AtRisk)]
    [InlineData(2, 3, 4, RfmSegment.AtRisk)]
    [InlineData(2, 1, 1, RfmSegment.AboutToSleep)]
    [InlineData(2, 2, 2, RfmSegment.Hibernating)]
    [InlineData(1, 1, 1, RfmSegment.Lost)]
    public void ClassifyRfmSegment_Should_Map_Quintile_Combinations_Accurately(
        int recency, int frequency, int monetary, RfmSegment expectedSegment)
    {
        // Act
        var segment = _service.ClassifyRfmSegment(recency, frequency, monetary);

        // Assert
        segment.Should().Be(expectedSegment);
    }

    [Fact]
    public void CalculateChurnProbability_Should_Stay_Strictly_Bounded_Between_Zero_And_One()
    {
        // Act: Best case (low churn)
        var lowChurn = _service.CalculateChurnProbability(recencyScore: 5, frequencyScore: 5, monetaryScore: 5, unresolvedTickets: 0, negativeCsatCount: 0);

        // Act: Worst case (critical churn)
        var highChurn = _service.CalculateChurnProbability(recencyScore: 1, frequencyScore: 1, monetaryScore: 1, unresolvedTickets: 8, negativeCsatCount: 5);

        // Assert
        lowChurn.Should().BeGreaterThan(0.0).And.BeLessThan(0.05);
        highChurn.Should().BeGreaterThan(0.95).And.BeLessThanOrEqualTo(1.0);
    }

    [Fact]
    public void CalculateChurnProbability_Should_Significantly_Increase_With_Negative_Csat_And_Unresolved_Tickets()
    {
        // Arrange
        int r = 3, f = 3, m = 3;

        // Act
        var baseline = _service.CalculateChurnProbability(r, f, m, unresolvedTickets: 0, negativeCsatCount: 0);
        var elevated = _service.CalculateChurnProbability(r, f, m, unresolvedTickets: 2, negativeCsatCount: 2);

        // Assert
        elevated.Should().BeGreaterThan(baseline);
        (elevated - baseline).Should().BeGreaterThan(0.40, "Unresolved tickets and negative CSAT carry heavy penalty weights in logistic model");
    }

    [Theory]
    [InlineData(0.12, ChurnRiskTier.Low)]
    [InlineData(0.35, ChurnRiskTier.Moderate)]
    [InlineData(0.62, ChurnRiskTier.AtRisk)]
    [InlineData(0.88, ChurnRiskTier.Critical)]
    public void ClassifyRiskTier_Should_Correctly_Partition_Probability_Bands(double probability, ChurnRiskTier expectedTier)
    {
        // Act
        var tier = _service.ClassifyRiskTier(probability);

        // Assert
        tier.Should().Be(expectedTier);
    }

    [Fact]
    public async Task EvaluateCustomerAsync_Should_Compute_And_Store_Profile_Correctly()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var request = new EvaluateCustomerRequest(
            CustomerId: customerId,
            TenantId: _tenantId,
            CustomerName: "Dr. Lilia Khemir",
            CustomerEmail: "lilia@clinic-consult.tn",
            CustomerPhone: "+216 71 222 333",
            Company: "Khemir Health",
            RecencyDays: 85,        // Score: 2
            FrequencyOrders: 8,     // Score: 4
            MonetaryTotalLtv: 2600m,// Score: 5
            UnresolvedTickets: 1,
            NegativeCsatCount: 1
        );

        // Act
        var profile = await _service.EvaluateCustomerAsync(_tenantId, request);

        // Assert
        profile.CustomerId.Should().Be(customerId);
        profile.RecencyScore.Should().Be(2);
        profile.FrequencyScore.Should().Be(4);
        profile.MonetaryScore.Should().Be(5);
        profile.RfmCell.Should().Be("2-4-5");
        profile.Segment.Should().Be(RfmSegment.AtRisk);
        profile.ChurnProbability.Should().BeInRange(0.20, 0.35);
        profile.RiskTier.Should().Be(ChurnRiskTier.Moderate);

        // Fetch back
        var fetched = await _service.GetCustomerProfileByIdAsync(_tenantId, customerId);
        fetched.Should().NotBeNull();
        fetched!.CustomerName.Should().Be("Dr. Lilia Khemir");
    }

    [Fact]
    public async Task RunPreemptiveWinbackDaemonScanAsync_Should_Auto_Dispatch_Playbooks_For_Breached_Accounts()
    {
        // Arrange: Seed initial tenant accounts
        await _service.GetOverviewKpisAsync(_tenantId);

        // Act: Run daemon scan with 0.50 threshold
        var scanResult = await _service.RunPreemptiveWinbackDaemonScanAsync(_tenantId, churnThreshold: 0.50);

        // Assert
        scanResult.TotalAccountsScanned.Should().BeGreaterThanOrEqualTo(8);
        scanResult.BreachedAccountsCount.Should().BeGreaterThanOrEqualTo(3);
        scanResult.PlaybooksDispatchedCount.Should().BeGreaterThanOrEqualTo(1);

        foreach (var playbook in scanResult.DispatchedPlaybooks)
        {
            playbook.Status.Should().Be(WinbackStatus.Dispatched);
            playbook.DispatchedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
            playbook.ExpiresAtUtc.Should().BeCloseTo(DateTime.UtcNow.AddDays(30), TimeSpan.FromSeconds(5));
            playbook.OfferDetails.Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public async Task TriggerWinbackPlaybookAsync_Should_Select_PulseSurvey_When_Customer_Has_Negative_Csat()
    {
        // Arrange: Evaluate customer with negative CSAT
        var customerId = Guid.NewGuid();
        await _service.EvaluateCustomerAsync(_tenantId, new EvaluateCustomerRequest(
            CustomerId: customerId,
            TenantId: _tenantId,
            CustomerName: "Dissatisfied Client",
            CustomerEmail: "dissatisfied@corp.tn",
            CustomerPhone: "+216 99 111 222",
            Company: "Corp TN",
            RecencyDays: 45,
            FrequencyOrders: 4,
            MonetaryTotalLtv: 800m,
            UnresolvedTickets: 0,
            NegativeCsatCount: 2
        ));

        // Act: Trigger winback without specifying playbook type
        var campaign = await _service.TriggerWinbackPlaybookAsync(_tenantId, new TriggerWinbackRequest(
            TenantId: _tenantId,
            CustomerId: customerId
        ));

        // Assert
        campaign.PlaybookType.Should().Be(WinbackPlaybookType.ExecutivePulseSurvey);
        campaign.Channel.Should().Be(WinbackChannel.WhatsApp);
        campaign.OfferDetails.Should().Contain("Executive Pulse");
    }

    [Fact]
    public async Task TriggerWinbackPlaybookAsync_Should_Select_SuccessManagerReachout_For_Critical_Risk_Or_High_Value()
    {
        // Arrange: High value customer who lapsed with unresolved tickets
        var customerId = Guid.NewGuid();
        await _service.EvaluateCustomerAsync(_tenantId, new EvaluateCustomerRequest(
            CustomerId: customerId,
            TenantId: _tenantId,
            CustomerName: "VIP Lapsed Partner",
            CustomerEmail: "partner@vippartner.com",
            CustomerPhone: "+216 71 555 666",
            Company: "VIP Partner",
            RecencyDays: 110,
            FrequencyOrders: 14,
            MonetaryTotalLtv: 18000m,
            UnresolvedTickets: 2,
            NegativeCsatCount: 0
        ));

        // Act
        var campaign = await _service.TriggerWinbackPlaybookAsync(_tenantId, new TriggerWinbackRequest(
            TenantId: _tenantId,
            CustomerId: customerId
        ));

        // Assert
        campaign.PlaybookType.Should().Be(WinbackPlaybookType.SuccessManagerReachout);
        campaign.Channel.Should().Be(WinbackChannel.PhoneCall);
        campaign.AssignedAgent.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task RecordWinbackConversionAsync_Should_Transition_Status_To_Converted_And_Update_Recovered_Revenue()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        await _service.EvaluateCustomerAsync(_tenantId, new EvaluateCustomerRequest(
            CustomerId: customerId,
            TenantId: _tenantId,
            CustomerName: "Target Conversion Customer",
            CustomerEmail: "target@conversion.tn",
            CustomerPhone: "+216 98 444 555",
            Company: "Target Co",
            RecencyDays: 65,
            FrequencyOrders: 3,
            MonetaryTotalLtv: 900m,
            UnresolvedTickets: 1,
            NegativeCsatCount: 1
        ));

        var campaign = await _service.TriggerWinbackPlaybookAsync(_tenantId, new TriggerWinbackRequest(
            TenantId: _tenantId,
            CustomerId: customerId
        ));

        // Act: Record conversion of $1,350 purchase
        var conversionResult = await _service.RecordWinbackConversionAsync(_tenantId, campaign.Id, new RecordConversionRequest(
            ConvertedOrderAmount: 1350.00m,
            OrderReference: "ORD-WINBACK-2026-991",
            ConversionNotes: "Customer purchased annual plan upgrade via VIP coupon"
        ));

        // Assert
        conversionResult.Status.Should().Be(WinbackStatus.Converted);
        conversionResult.ConvertedAtUtc.Should().NotBeNull();
        conversionResult.RecoveredRevenue.Should().Be(1350.00m);

        // Verify customer profile was reactivated (recency reset to 1 day, frequency increased, LTV incremented)
        var updatedProfile = await _service.GetCustomerProfileByIdAsync(_tenantId, customerId);
        updatedProfile.Should().NotBeNull();
        updatedProfile!.RecencyDays.Should().Be(1);
        updatedProfile.RecencyScore.Should().Be(5);
        updatedProfile.FrequencyOrders.Should().Be(4);
        updatedProfile.MonetaryTotalLtv.Should().Be(2250.00m);
        updatedProfile.NegativeCsatCount.Should().Be(0);
        updatedProfile.ChurnProbability.Should().BeLessThan(0.20, "Reactivated customer churn risk drops significantly");
    }

    [Fact]
    public async Task RecordWinbackConversionAsync_Should_Throw_When_Campaign_Not_Found()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act & Assert
        var act = async () => await _service.RecordWinbackConversionAsync(_tenantId, nonExistentId, new RecordConversionRequest(
            ConvertedOrderAmount: 100m
        ));

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage($"*{nonExistentId}*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-50)]
    public async Task RecordWinbackConversionAsync_Should_Throw_When_Amount_Is_Zero_Or_Negative(decimal invalidAmount)
    {
        // Arrange: Seed campaigns
        await _service.GetOverviewKpisAsync(_tenantId);
        var campaigns = await _service.GetWinbackCampaignsAsync(_tenantId);
        var activeCampaign = campaigns.First(c => c.Status != WinbackStatus.Converted);

        // Act & Assert
        var act = async () => await _service.RecordWinbackConversionAsync(_tenantId, activeCampaign.Id, new RecordConversionRequest(
            ConvertedOrderAmount: invalidAmount
        ));

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*greater than zero*");
    }

    [Fact]
    public async Task GetOverviewKpisAsync_Should_Compute_Accurate_Aggregation_Metrics()
    {
        // Arrange: Initialize default tenant data
        var kpis = await _service.GetOverviewKpisAsync(_tenantId);

        // Assert
        kpis.TotalCustomersEvaluated.Should().Be(8);
        kpis.AtRiskAccountsCount.Should().BeGreaterThan(0);
        kpis.CriticalAccountsCount.Should().BeGreaterThan(0);
        kpis.ChurnRiskRatePercent.Should().BeGreaterThan(0);
        kpis.RecoveredRevenue.Should().BeGreaterThan(0);
        kpis.TotalWinbacksDispatched.Should().BeGreaterThanOrEqualTo(3);
        kpis.WinbackConversionRatePercent.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetRfmCohortsAsync_Should_Return_All_Seven_Segments_With_Valid_Distribution()
    {
        // Act
        var cohorts = await _service.GetRfmCohortsAsync(_tenantId);

        // Assert
        cohorts.Should().HaveCount(7);
        cohorts.Select(c => c.Segment).Should().Contain(new[]
        {
            RfmSegment.Champions,
            RfmSegment.LoyalCustomers,
            RfmSegment.PotentialLoyalists,
            RfmSegment.AtRisk,
            RfmSegment.AboutToSleep,
            RfmSegment.Hibernating,
            RfmSegment.Lost
        });

        // Each cohort has valid metrics
        foreach (var cohort in cohorts)
        {
            cohort.SegmentLabel.Should().NotBeNullOrWhiteSpace();
            cohort.RecommendedAction.Should().NotBeNullOrWhiteSpace();
            cohort.ColorBadge.Should().NotBeNullOrWhiteSpace();
            cohort.Percentage.Should().BeGreaterThanOrEqualTo(0);
        }
    }

    [Fact]
    public async Task UpdateWinbackStatusAsync_Should_Update_Status_And_OpenedAtUtc()
    {
        // Arrange
        await _service.GetOverviewKpisAsync(_tenantId);
        var campaigns = await _service.GetWinbackCampaignsAsync(_tenantId, WinbackStatus.Dispatched);
        var target = campaigns.First();

        // Act
        var updated = await _service.UpdateWinbackStatusAsync(_tenantId, target.Id, WinbackStatus.Opened);

        // Assert
        updated.Status.Should().Be(WinbackStatus.Opened);
        updated.OpenedAtUtc.Should().NotBeNull();
        updated.OpenedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }
}
