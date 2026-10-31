using FluentAssertions;
using SGrowing.Api.Modules.Analytics.PredictiveLtv;
using Xunit;

namespace SGrowing.Tests.Analytics;

public class PredictiveLtvServiceTests
{
    private readonly PredictiveLtvService _service = new();
    private readonly Guid _tenantId = Guid.NewGuid();

    [Fact]
    public async Task PredictCustomerLtvAsync_ShouldClassifyChampionWhale_WhenHighFrequencyAndHighSpend()
    {
        // Frequency = 12, Recency = 280, Total = 300, AOV = $250
        var history = new CustomerTransactionHistory(
            Guid.NewGuid(),
            "Whale Corp",
            "whale@corp.com",
            Frequency: 12,
            RecencyDays: 280,
            TotalObservationDays: 300,
            HistoricalAverageOrderValue: 250m,
            TotalLifetimeRevenue: 3000m
        );

        var result = await _service.PredictCustomerLtvAsync(_tenantId, history);

        result.ProbabilityAlive.Should().BeGreaterThan(0.70);
        result.PredictedResidualLtv365d.Should().BeGreaterThan(2000m);
        result.Tier.Should().Be(CustomerPltvTier.ChampionWhale);
        result.RequiresAutomatedWinBackIntervention.Should().BeFalse();
    }

    [Fact]
    public async Task PredictCustomerLtvAsync_ShouldFlagAtRiskDefecting_WhenHighPastSpendAndLongInactivity()
    {
        // High historic spend ($1500), but hasn't purchased in 250 days (Total = 365, Recency = 115)
        var history = new CustomerTransactionHistory(
            Guid.NewGuid(),
            "Defecting VIP",
            "vip@defecting.com",
            Frequency: 8,
            RecencyDays: 115,
            TotalObservationDays: 365,
            HistoricalAverageOrderValue: 180m,
            TotalLifetimeRevenue: 1440m
        );

        var result = await _service.PredictCustomerLtvAsync(_tenantId, history);

        result.ProbabilityAlive.Should().BeLessThan(0.45);
        result.Tier.Should().Be(CustomerPltvTier.AtRiskDefecting);
        result.RequiresAutomatedWinBackIntervention.Should().BeTrue();
    }

    [Fact]
    public async Task PredictCustomerLtvAsync_ShouldClassifyHighPotentialNewbie_WhenRecentAndHighFirstOrder()
    {
        var history = new CustomerTransactionHistory(
            Guid.NewGuid(),
            "Promising Newbie",
            "newbie@startup.io",
            Frequency: 1,
            RecencyDays: 10,
            TotalObservationDays: 15,
            HistoricalAverageOrderValue: 350m,
            TotalLifetimeRevenue: 350m
        );

        var result = await _service.PredictCustomerLtvAsync(_tenantId, history);

        result.ProbabilityAlive.Should().BeGreaterThanOrEqualTo(0.75);
        result.Tier.Should().Be(CustomerPltvTier.HighPotentialNewbie);
    }

    [Fact]
    public async Task PredictCustomerLtvAsync_ShouldClassifyLostDormant_WhenExtremelyLongInactivity()
    {
        var history = new CustomerTransactionHistory(
            Guid.NewGuid(),
            "Dormant User",
            "lost@old.com",
            Frequency: 1,
            RecencyDays: 10,
            TotalObservationDays: 700,
            HistoricalAverageOrderValue: 40m,
            TotalLifetimeRevenue: 40m
        );

        var result = await _service.PredictCustomerLtvAsync(_tenantId, history);

        result.ProbabilityAlive.Should().BeLessThan(0.20);
        result.Tier.Should().Be(CustomerPltvTier.LostDormant);
    }

    [Fact]
    public async Task PredictCustomerLtvAsync_ShouldThrow_WhenRecencyExceedsTotalObservationTime()
    {
        var history = new CustomerTransactionHistory(
            Guid.NewGuid(),
            "Invalid User",
            "invalid@time.com",
            Frequency: 2,
            RecencyDays: 150,
            TotalObservationDays: 100, // Invalid: recency > total
            HistoricalAverageOrderValue: 50m,
            TotalLifetimeRevenue: 100m
        );

        var act = () => _service.PredictCustomerLtvAsync(_tenantId, history);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Recency cannot exceed total observation days.*");
    }

    [Fact]
    public async Task PredictCohortBatchAsync_ShouldSummarizeCohortAccurately()
    {
        var customers = new List<CustomerTransactionHistory>
        {
            new(Guid.NewGuid(), "Cust 1", "c1@test.com", 12, 290, 300, 300m, 3600m),
            new(Guid.NewGuid(), "Cust 2", "c2@test.com", 8, 100, 365, 200m, 1600m),
            new(Guid.NewGuid(), "Cust 3", "c3@test.com", 0, 0, 800, 25m, 25m)
        };

        var request = new CalculatePltvRequest(_tenantId, customers);
        var summary = await _service.PredictCohortBatchAsync(request);

        summary.TotalAnalyzedCustomers.Should().Be(3);
        summary.ChampionWhalesCount.Should().Be(1);
        summary.AtRiskDefectingCount.Should().Be(1);
        summary.LostDormantCount.Should().Be(1);
        summary.TotalPredictedResidualRevenue365d.Should().BeGreaterThan(0m);
        summary.TopVipRiskCustomers.Should().ContainSingle();
    }
}
