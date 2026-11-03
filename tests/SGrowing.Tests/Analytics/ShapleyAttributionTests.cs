using System;
using System.Collections.Generic;
using SGrowing.Api.Modules.Analytics.ShapleyAttribution;
using Xunit;

namespace SGrowing.Tests.Analytics;

public class ShapleyAttributionTests
{
    private readonly ShapleyAttributionEngine _engine = new();

    [Fact]
    public void SeededDeal_ReturnsChannelScoresWithShapleyPercentages()
    {
        var report = _engine.GetReportByDealId("deal_apex_701");

        Assert.NotNull(report);
        Assert.Equal("Apex Logistics Enterprise", report.CustomerName);
        Assert.Equal(150000m, report.ClosedDealValue);
        Assert.Equal(5, report.JourneyEvents.Count);
        Assert.NotEmpty(report.ChannelScores);

        // High engagement touches should have highest Shapley fair value
        var demoScore = report.ChannelScores.Find(s => s.Channel == TouchpointType.ExecutiveDemoMeeting);
        Assert.NotNull(demoScore);
        Assert.True(demoScore.ShapleyFairValuePercentage > demoScore.LinearPercentage);
    }

    [Fact]
    public void SingleTouchpoint_Gets100PercentAttributionAcrossModels()
    {
        var events = new List<TouchpointEvent>
        {
            new("ev_single", TouchpointType.PaidSearchAd, DateTime.UtcNow, 300, 50m)
        };

        var request = new CalculateShapleyRequest("deal_quick", "Quick Deal", 5000m, events);
        var report = _engine.CalculateAttribution(request);

        Assert.Single(report.ChannelScores);
        var score = report.ChannelScores[0];
        Assert.Equal(100.0, score.FirstTouchPercentage);
        Assert.Equal(100.0, score.LastTouchPercentage);
        Assert.Equal(100.0, score.ShapleyFairValuePercentage);
        Assert.Equal(5000m, score.AttributedRevenueAmount);
    }
}
