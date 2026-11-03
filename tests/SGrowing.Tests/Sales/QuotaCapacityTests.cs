using SGrowing.Api.Modules.Sales.QuotaCapacity;
using Xunit;

namespace SGrowing.Tests.Sales;

public class QuotaCapacityTests
{
    private readonly QuotaCapacityEngine _engine = new();

    [Fact]
    public void SeededTerritories_ContainCapacityAndPipelineCoverage()
    {
        var territories = _engine.GetTerritories();
        Assert.NotEmpty(territories);
        var east = territories.FirstOrDefault(t => t.TerritoryId == "TERR-US-EAST");
        Assert.NotNull(east);
        Assert.Equal("Balanced", east.CapacityStatus);
        Assert.True(east.CurrentPipelineCoverageRatio >= 3.5m);
    }

    [Fact]
    public void GetImbalanceAlerts_IdentifiesBurnoutAndWhitespace()
    {
        var alerts = _engine.GetImbalanceAlerts();
        Assert.NotEmpty(alerts);

        var burnout = alerts.FirstOrDefault(a => a.ImbalanceType == "QuotaBurnout");
        Assert.NotNull(burnout);
        Assert.True(burnout.RecommendedHeadcountDelta > 0);

        var whitespace = alerts.FirstOrDefault(a => a.ImbalanceType == "WhitespaceSurge");
        Assert.NotNull(whitespace);
        Assert.True(whitespace.GapAmountEur > 0m);
    }

    [Fact]
    public void RebalanceTerritory_OptimizesHeadcountAndTargetCoverage()
    {
        var request = new RebalanceRequest("TERR-EMEA-NORTH", 3, 3.6m);
        var result = _engine.RebalanceTerritory(request);

        Assert.NotNull(result);
        Assert.Equal(11, result.NewHeadcount); // 8 + 3 = 11
        Assert.Equal(3.6m, result.ProjectedPipelineCoverageRatio);
        Assert.True(result.OptimizedQuotaPerRepEur < 2500000m);

        var updated = _engine.GetTerritory("TERR-EMEA-NORTH");
        Assert.NotNull(updated);
        Assert.Equal("Balanced", updated.CapacityStatus);
    }
}
