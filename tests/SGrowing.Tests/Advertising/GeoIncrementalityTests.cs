using System;
using System.Linq;
using SGrowing.Api.Modules.Advertising.GeoIncrementality;
using Xunit;

namespace SGrowing.Tests.Advertising;

public sealed class GeoIncrementalityTests
{
    [Fact]
    public void GetOverview_ReturnsInitialExperimentsAndCells()
    {
        var engine = new GeoIncrementalityEngine();
        var overview = engine.GetOverview();

        Assert.NotNull(overview);
        Assert.True(overview.ActiveExperimentsCount >= 1);
        Assert.True(overview.TotalDmaRegionsCalibrated >= 2);
        Assert.True(overview.MeanTrueIncrementalRoas > 1.0);
        Assert.NotEmpty(overview.Experiments);
        Assert.NotEmpty(overview.CalibratedCells);
    }

    [Fact]
    public void LaunchExperiment_CreatesHoldoutDesign()
    {
        var engine = new GeoIncrementalityEngine();
        var request = new LaunchGeoExperimentRequest(
            "Streaming CTV Holiday Incrementality",
            80000.00m,
            4,
            new[] { "DFW", "ATL", "PHX" }
        );

        var exp = engine.LaunchExperiment(request);

        Assert.NotNull(exp);
        Assert.StartsWith("GEO-EXP-", exp.ExperimentId);
        Assert.Equal(80000.00m, exp.TotalAdSpendUsd);
        Assert.Equal("RUNNING_HOLD_OUT", exp.Status);

        var overview = engine.GetOverview();
        Assert.Contains(overview.Experiments, e => e.ExperimentId == exp.ExperimentId);
    }

    [Fact]
    public void EvaluateLift_CalculatesPureCausalRoas()
    {
        var engine = new GeoIncrementalityEngine();
        var request = new EvaluateLiftRequest(
            "GEO-EXP-2026-01",
            850000.00m, // Treatment group revenue
            250000.00m  // Synthetic control counterfactual
        );

        var exp = engine.EvaluateLift(request);

        Assert.NotNull(exp);
        Assert.Equal(600000.0, exp.MeasuredIncrementalRevenueUsd);
        Assert.Equal(4.0, exp.TrueIncrementalRoasMultiplier);
        Assert.Equal("COMPLETED_SIGNIFICANT", exp.Status);
    }
}
