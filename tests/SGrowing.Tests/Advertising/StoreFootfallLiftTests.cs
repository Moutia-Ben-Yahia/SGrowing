using SGrowing.Api.Modules.Advertising.StoreFootfallLift;
using Xunit;

namespace SGrowing.Tests.Advertising;

public class StoreFootfallLiftTests
{
    private readonly StoreFootfallLiftEngine _engine = new();

    [Fact]
    public void SeededStores_ContainPolygonVerticesAndDwellThresholds()
    {
        var stores = _engine.GetStores();
        Assert.NotEmpty(stores);
        var flagship = stores.FirstOrDefault(s => s.StoreId == "STORE-NYC-5TH-AVE");
        Assert.NotNull(flagship);
        Assert.Equal(7, flagship.MinimumDwellMinutes);
        Assert.True(flagship.BoundingVertices.Count >= 4);
    }

    [Fact]
    public void CalculateLift_ComputesIncrementalVisitsAndCpiv()
    {
        var request = new CalculateLiftRequest(
            StoreId: "STORE-LON-OXFORD-ST",
            CampaignId: "CAMP-RETAIL-AUTUMN",
            ExposedAudience: 100000,
            ControlAudience: 100000
        );

        var analysis = _engine.CalculateLift(request);

        Assert.NotNull(analysis);
        Assert.Equal("STORE-LON-OXFORD-ST", analysis.StoreId);
        Assert.True(analysis.IncrementalLiftPercentage > 0m);
        Assert.True(analysis.ExposedStoreVisits > analysis.ControlStoreVisits);
        Assert.True(analysis.CostPerIncrementalVisitEur > 0m);
    }

    [Fact]
    public void GetMetrics_AggregatesVisitsAndAttributedRevenue()
    {
        var metrics = _engine.GetMetrics();

        Assert.NotNull(metrics);
        Assert.True(metrics.TotalMonitoredRetailStores >= 2);
        Assert.True(metrics.TotalVerifiedStoreVisits > 0);
        Assert.True(metrics.TotalAttributedStoreRevenueEur > 0m);
    }
}
