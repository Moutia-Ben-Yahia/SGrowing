using SGrowing.Api.Modules.Sales.DealCopilot;

namespace SGrowing.Tests.Sales;

public class DealCopilotTests
{
    private readonly DealCopilotEngine _engine = new();

    [Fact]
    public void GetPipelineDeals_ReturnsSeededDealsSortedByAmount()
    {
        var deals = _engine.GetPipelineDeals("tenant_sales_test");

        Assert.NotEmpty(deals);
        Assert.True(deals[0].Amount >= deals[1].Amount);
        Assert.Contains(deals, d => d.DealId == "deal_quantum_corp");
    }

    [Fact]
    public void ExecuteIntervention_IncreasesWinProbability()
    {
        var initial = _engine.GetDeal("deal_quantum_corp");
        Assert.NotNull(initial);
        var initialProbability = initial.WinProbabilityPercent;

        var req = new InterventionActionRequest("deal_quantum_corp", "DraftReengagementEmail");
        var result = _engine.ExecuteIntervention(req);

        Assert.True(result.Success);
        Assert.True(result.NewWinProbabilityPercent > initialProbability);

        var updated = _engine.GetDeal("deal_quantum_corp");
        Assert.NotNull(updated);
        Assert.Equal("Healthy", updated.RiskLevel);
    }

    [Fact]
    public void CalculateAggregatePipelineHealth_ReturnsPositiveAverage()
    {
        var score = _engine.CalculateAggregatePipelineHealth("tenant_sales_test");
        Assert.True(score > 50.0);
    }

    [Fact]
    public void ExecuteIntervention_WhenDealNotFound_ThrowsKeyNotFoundException()
    {
        var req = new InterventionActionRequest("deal_nonexistent_999", "DraftReengagementEmail");
        Assert.Throws<KeyNotFoundException>(() => _engine.ExecuteIntervention(req));
    }

    [Fact]
    public void ExecuteIntervention_HighInitialProbability_CapsAt95Percent()
    {
        var req = new InterventionActionRequest("deal_acme_retail", "TransmitPKCS7Packet");
        var result = _engine.ExecuteIntervention(req);

        Assert.True(result.Success);
        Assert.Equal(95.0, result.NewWinProbabilityPercent);

        var deal = _engine.GetDeal("deal_acme_retail");
        Assert.NotNull(deal);
        Assert.Equal(95.0, deal.WinProbabilityPercent);
        Assert.Equal(0, deal.DaysInactive);
        Assert.Equal("Healthy", deal.RiskLevel);
    }

    [Fact]
    public void GetDeal_NonExistentDeal_ReturnsNull()
    {
        var deal = _engine.GetDeal("deal_missing_xyz");
        Assert.Null(deal);
    }

    [Fact]
    public void ExecuteIntervention_ModerateRiskDeal_AdvancesWinProbabilityByTwentyPercentAndResetsInactivity()
    {
        var initial = _engine.GetDeal("deal_fintech_hyper");
        Assert.NotNull(initial);
        Assert.Equal(60.0, initial.WinProbabilityPercent);
        Assert.Equal(7, initial.DaysInactive);
        Assert.Equal("ModerateRisk", initial.RiskLevel);

        var req = new InterventionActionRequest("deal_fintech_hyper", "ScheduleExecutiveAlignment");
        var result = _engine.ExecuteIntervention(req);

        Assert.True(result.Success);
        Assert.Equal(80.0, result.NewWinProbabilityPercent);
        Assert.Contains("ScheduleExecutiveAlignment", result.ExecutionMessage);

        var updated = _engine.GetDeal("deal_fintech_hyper");
        Assert.NotNull(updated);
        Assert.Equal(80.0, updated.WinProbabilityPercent);
        Assert.Equal(0, updated.DaysInactive);
        Assert.Equal("Healthy", updated.RiskLevel);
    }

    [Fact]
    public void CalculateAggregatePipelineHealth_ComputesExactAverageAcrossPipeline()
    {
        var score = _engine.CalculateAggregatePipelineHealth("tenant_sales_test");
        // (45.0 + 82.0 + 60.0) / 3 = 187 / 3 = 62.333... rounded to 1 decimal is 62.3
        Assert.Equal(62.3, score);
    }
}
