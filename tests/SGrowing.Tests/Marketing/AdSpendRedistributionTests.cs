using System;
using System.Collections.Generic;
using System.Linq;
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

    [Fact]
    public void ReallocateBudget_EnforcesMinimumSpendFloorOf5000()
    {
        // Arrange: ContentSeo has 30,000 spend; cutting by 40,000 should floor at 5,000
        var command = new ReallocateBudgetCommand("ContentSeo", -40000m, "DefensiveSqueeze");

        // Act
        var result = _engine.ReallocateBudget(command);

        // Assert
        result.PreviousSpend.Should().Be(30000m);
        result.NewSpend.Should().Be(5000m);
        result.ExecutionLog.Should().Contain("$5,000");
    }

    [Fact]
    public void ReallocateBudget_ClampsRecommendedSpendShiftAtZero_WhenDeltaExceedsRecommended()
    {
        // Arrange: PartnerEcosystem has +5,000 recommended shift; adding 20,000 should clamp remaining shift to 0
        var command = new ReallocateBudgetCommand("PartnerEcosystem", 20000m, "AggressiveScale");

        // Act
        var result = _engine.ReallocateBudget(command);
        var overview = _engine.GetOverview();
        var partnerChannel = overview.Channels.First(c => c.ChannelId == "PartnerEcosystem");

        // Assert
        result.NewSpend.Should().Be(45000m);
        partnerChannel.RecommendedSpendShift.Should().Be(0m);
    }

    [Fact]
    public void GetOverview_SortsChannelsByShapleyShareDescending()
    {
        // Act
        var overview = _engine.GetOverview();

        // Assert
        overview.Channels.Should().BeInDescendingOrder(c => c.ShapleySharePct);
        overview.Channels[0].ChannelId.Should().Be("Webinars"); // 29.4%
    }

    [Fact]
    public void GetOverview_CalculatesAccurateProjectedArrUplift()
    {
        // Act
        var overview = _engine.GetOverview();

        // Assert: ProjectedArrUplift should be exactly 18.5% of total pipeline ARR
        var expectedUplift = Math.Round(overview.TotalPipelineArr * 0.185m, 2);
        overview.ProjectedArrUplift.Should().Be(expectedUplift);
        overview.RecentJourneys.Count.Should().BeLessThanOrEqualTo(5);
    }

    [Fact]
    public void ReallocateBudget_MultipleChannels_MutatesStateIndependently()
    {
        // Arrange & Act
        var r1 = _engine.ReallocateBudget(new ReallocateBudgetCommand("PaidSearch", -10000m, "EfficiencyCut"));
        var r2 = _engine.ReallocateBudget(new ReallocateBudgetCommand("SocialRetargeting", 15000m, "ABMPush"));

        var overview = _engine.GetOverview();
        var paidSearch = overview.Channels.First(c => c.ChannelId == "PaidSearch");
        var social = overview.Channels.First(c => c.ChannelId == "SocialRetargeting");

        // Assert
        r1.NewSpend.Should().Be(50000m);
        paidSearch.CurrentSpend.Should().Be(50000m);

        r2.NewSpend.Should().Be(100000m);
        social.CurrentSpend.Should().Be(100000m);
    }
}
