using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
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

    [Fact]
    public void CalculateAttribution_EmptyEventsList_ReturnsEmptyReportWithTakeaway()
    {
        var request = new CalculateShapleyRequest("deal_empty", "Empty Journey Account", 25000m, new List<TouchpointEvent>());
        var report = _engine.CalculateAttribution(request);

        report.Should().NotBeNull();
        report.DealId.Should().Be("deal_empty");
        report.JourneyEvents.Should().BeEmpty();
        report.ChannelScores.Should().BeEmpty();
        report.KeyTakeaway.Should().Be("No touchpoint events recorded.");
    }

    [Fact]
    public void CalculateAttribution_NullEventsList_HandledGracefullyWithoutNullReference()
    {
        var request = new CalculateShapleyRequest("deal_null_events", "Null Account", 12000m, null);
        var report = _engine.CalculateAttribution(request);

        report.Should().NotBeNull();
        report.DealId.Should().Be("deal_null_events");
        report.JourneyEvents.Should().BeEmpty();
        report.ChannelScores.Should().BeEmpty();
        report.KeyTakeaway.Should().Be("No touchpoint events recorded.");
    }

    [Fact]
    public void CalculateAttribution_ZeroDealValue_ProducesZeroAttributedRevenueForChannels()
    {
        var events = new List<TouchpointEvent>
        {
            new("ev_1", TouchpointType.PaidSearchAd, DateTime.UtcNow.AddDays(-10), 120, 10m),
            new("ev_2", TouchpointType.WebinarAttendance, DateTime.UtcNow.AddDays(-2), 1800, 50m)
        };

        var request = new CalculateShapleyRequest("deal_zero_val", "Free Tier Evaluation", 0m, events);
        var report = _engine.CalculateAttribution(request);

        report.ChannelScores.Should().HaveCount(2);
        foreach (var score in report.ChannelScores)
        {
            score.AttributedRevenueAmount.Should().Be(0m);
        }
    }

    [Fact]
    public void CalculateAttribution_FirstAndLastTouchOnly_Distributes100PercentExclusively()
    {
        var now = DateTime.UtcNow;
        var events = new List<TouchpointEvent>
        {
            new("ev_first", TouchpointType.PaidSearchAd, now.AddDays(-30), 60, 25m),
            new("ev_mid", TouchpointType.OrganicSeoArticle, now.AddDays(-15), 300, 0m),
            new("ev_last", TouchpointType.ExecutiveDemoMeeting, now.AddDays(-1), 3600, 150m)
        };

        var request = new CalculateShapleyRequest("deal_journey_3", "Mid-Market Growth", 40000m, events);
        var report = _engine.CalculateAttribution(request);

        report.ChannelScores.Should().HaveCount(3);

        var firstScore = report.ChannelScores.First(s => s.Channel == TouchpointType.PaidSearchAd);
        firstScore.FirstTouchPercentage.Should().Be(100.0);
        firstScore.LastTouchPercentage.Should().Be(0.0);

        var midScore = report.ChannelScores.First(s => s.Channel == TouchpointType.OrganicSeoArticle);
        midScore.FirstTouchPercentage.Should().Be(0.0);
        midScore.LastTouchPercentage.Should().Be(0.0);

        var lastScore = report.ChannelScores.First(s => s.Channel == TouchpointType.ExecutiveDemoMeeting);
        lastScore.FirstTouchPercentage.Should().Be(0.0);
        lastScore.LastTouchPercentage.Should().Be(100.0);
    }

    [Fact]
    public void CalculateAttribution_HighLeverageVsLowLeverage_GeneratesAppropriateRecommendations()
    {
        var now = DateTime.UtcNow;
        var events = new List<TouchpointEvent>
        {
            // Low engagement social touch (10 seconds)
            new("ev_low", TouchpointType.RetargetingSocialAd, now.AddDays(-10), 10, 5m),
            // High engagement executive demo touch (7200 seconds = 2 hours)
            new("ev_high", TouchpointType.ExecutiveDemoMeeting, now.AddDays(-5), 7200, 200m)
        };

        var request = new CalculateShapleyRequest("deal_rec_check", "SaaS Scale", 80000m, events);
        var report = _engine.CalculateAttribution(request);

        var highTouch = report.ChannelScores.First(s => s.Channel == TouchpointType.ExecutiveDemoMeeting);
        var lowTouch = report.ChannelScores.First(s => s.Channel == TouchpointType.RetargetingSocialAd);

        highTouch.RecommendedBudgetAction.Should().Be("Increase (High Leverage Touch)");
        lowTouch.RecommendedBudgetAction.Should().Be("Trim (Over-Credited Heuristic)");
    }

    [Fact]
    public void GetReportByDealId_ReturnsCachedReportOrNullForMissing()
    {
        // Seeded deal with case-insensitivity
        var seeded = _engine.GetReportByDealId("DEAL_APEX_701");
        seeded.Should().NotBeNull();
        seeded!.CustomerName.Should().Be("Apex Logistics Enterprise");

        // Non-existent deal
        var missing = _engine.GetReportByDealId("non_existent_deal_xyz");
        missing.Should().BeNull();
    }
}
