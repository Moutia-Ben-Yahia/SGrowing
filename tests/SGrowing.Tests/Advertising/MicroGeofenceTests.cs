using SGrowing.Api.Modules.Advertising.MicroGeofence;
using Xunit;

namespace SGrowing.Tests.Advertising;

public class MicroGeofenceTests
{
    [Fact]
    public void GetOverview_ReportsPolygonsDwellsAndLift()
    {
        var engine = new MicroGeofenceEngine();

        var overview = engine.GetOverview();

        Assert.NotNull(overview);
        Assert.True(overview.ActivePolygonsCount >= 2);
        Assert.True(overview.DailyAudienceDwellDetections > 1000);
        Assert.True(overview.FootfallConversionLiftPct > 40.0);
        Assert.True(overview.TotalAttributedRevenueUsd > 50_000m);
        Assert.NotEmpty(overview.Polygons);
        Assert.NotEmpty(overview.RecentEvents);
    }

    [Fact]
    public void DefinePolygon_RegistersPerimeterAndBidMultiplier()
    {
        var engine = new MicroGeofenceEngine();
        var request = new DefinePolygonRequest(
            "Javits Center NYC",
            "CONVENTION_CENTER",
            "[[40.758,-74.002],[40.760,-74.000],[40.757,-73.998],[40.755,-74.001]]",
            300.0,
            1.95m
        );

        var poly = engine.DefinePolygon(request);

        Assert.NotNull(poly);
        Assert.StartsWith("GEO-POLY-", poly.PolygonId);
        Assert.Equal("Javits Center NYC", poly.VenueName);
        Assert.Equal(1.95m, poly.BidMultiplier);
        Assert.True(poly.IsActive);

        var overview = engine.GetOverview();
        Assert.Contains(overview.Polygons, p => p.PolygonId == poly.PolygonId);
    }

    [Fact]
    public void RecordFootfall_AttributesIncrementalRevenue()
    {
        var engine = new MicroGeofenceEngine();
        var request = new RecordFootfallRequest(
            "DEV-TEST-0091",
            "GEO-POLY-001",
            60,
            true,
            2450.00m
        );

        var evt = engine.RecordFootfall(request);

        Assert.NotNull(evt);
        Assert.StartsWith("FT-EVT-", evt.EventId);
        Assert.Equal("GEO-POLY-001", evt.PolygonId);
        Assert.True(evt.AttributedStoreVisit);
        Assert.Equal(2450.00m, evt.IncrementalValueGeneratedUsd);

        var overview = engine.GetOverview();
        Assert.Contains(overview.RecentEvents, e => e.EventId == evt.EventId);
    }
}
