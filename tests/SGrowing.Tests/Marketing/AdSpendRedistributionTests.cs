using System;
using System.Collections.Generic;
using FluentAssertions;
using SGrowing.Api.Modules.Marketing.AdSpendRedistribution;
using Xunit;

namespace SGrowing.Tests.Marketing;

public class AdSpendRedistributionTests
{
    private readonly AdSpendRedistributionEngine _engine = new();

    [Fact]
    public void GetOverview_CalculatesFairShapleyAttribution()
    {
        var overview = _engine.GetOverview();

        overview.Should().NotBeNull();
        overview.TotalPipelineArr.Should().BeGreaterThan(1000000m);
        overview.OverallBlendedRoas.Should().BeGreaterThan(0);
        overview.Channels.Should().HaveCount(5);
        overview.RecentJourneys.Should().NotBeEmpty();

        var webinars = overview.Channels.Find(c => c.ChannelId == "Webinars");
        webinars.Should().NotBeNull();
        webinars!.AttributionSkewPct.Should().BeGreaterThan(0);
        webinars.RecommendedSpendShift.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ReallocateBudget_UpdatesSpendAndReturnsLog()
    {
        var command = new ReallocateBudgetCommand("Webinars", 10000m, "OptimalYield");
        var result = _engine.ReallocateBudget(command);

        result.Should().NotBeNull();
        result.ChannelId.Should().Be("Webinars");
        result.NewSpend.Should().Be(result.PreviousSpend + 10000m);
        result.ExpectedRoas.Should().BeGreaterThan(4.0m);
        result.ExecutionLog.Should().Contain("Webinars");
    }

    [Fact]
    public void ReallocateBudget_ThrowsWhenChannelNotFound()
    {
        var command = new ReallocateBudgetCommand("InvalidChannel", 5000m, "OptimalYield");
        var act = () => _engine.ReallocateBudget(command);

        act.Should().Throw<KeyNotFoundException>();
    }
}
