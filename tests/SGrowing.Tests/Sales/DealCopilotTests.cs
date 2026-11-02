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
}
