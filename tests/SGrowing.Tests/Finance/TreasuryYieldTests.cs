using System;
using System.Collections.Generic;
using FluentAssertions;
using SGrowing.Api.Modules.Finance.TreasuryYield;
using Xunit;

namespace SGrowing.Tests.Finance;

public class TreasuryYieldTests
{
    private readonly TreasuryYieldEngine _engine = new();

    [Fact]
    public void GetOverview_CalculatesCashReserveAndYieldProjections()
    {
        var overview = _engine.GetOverview();

        overview.Should().NotBeNull();
        overview.CurrentCashReserve.Should().Be(7450000m);
        overview.RequiredOperatingBuffer.Should().Be(2500000m);
        overview.SurplusDeployableCapital.Should().Be(4950000m);
        overview.BlendedPortfolioApy.Should().BeGreaterThan(4.5);
        overview.Projections.Should().HaveCount(3);
        overview.Allocations.Should().HaveCount(3);
    }

    [Fact]
    public void SweepSurplus_AllocatesCapitalAndCalculatesAdditionalYield()
    {
        var command = new SweepTreasurySurplusCommand("US-TBILL-4W", 500000m, "CorporateTreasuryBoardScope");
        var result = _engine.SweepSurplus(command);

        result.Should().NotBeNull();
        result.InstrumentId.Should().Be("US-TBILL-4W");
        result.NewAllocation.Should().Be(result.PreviousAllocation + 500000m);
        result.AdditionalYieldEarned.Should().Be(500000m * 0.0525m);
        result.ConfirmationReference.Should().StartWith("TRSY-SWEEP-");
    }

    [Fact]
    public void SweepSurplus_ThrowsWhenInstrumentNotFound()
    {
        var command = new SweepTreasurySurplusCommand("UNKNOWN-INSTRUMENT", 100000m, "Scope");
        var act = () => _engine.SweepSurplus(command);

        act.Should().Throw<KeyNotFoundException>();
    }
}
