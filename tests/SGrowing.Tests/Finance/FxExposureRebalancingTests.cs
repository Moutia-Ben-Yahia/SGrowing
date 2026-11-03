using System;
using System.Collections.Generic;
using FluentAssertions;
using SGrowing.Api.Modules.Finance.FxExposureRebalancing;
using Xunit;

namespace SGrowing.Tests.Finance;

public class FxExposureRebalancingTests
{
    private readonly FxExposureRebalancingEngine _engine = new();

    [Fact]
    public void GetOverview_ReturnsPortfolioVarAndHedgeRatio()
    {
        var overview = _engine.GetOverview();

        overview.Should().NotBeNull();
        overview.TotalPortfolioVarUsd.Should().BeGreaterThan(500000m);
        overview.TotalHedgedNotionalUsd.Should().BeGreaterThan(5000000m);
        overview.HedgeRatioPct.Should().Be(84.5);
        overview.Exposures.Should().HaveCount(4);
        overview.ActiveContracts.Should().HaveCount(3);
    }

    [Fact]
    public void EvaluateExposure_IdentifiesHighRiskDriftCurrencies()
    {
        var command = new EvaluateExposureCommand("USD", 95.0);
        var result = _engine.EvaluateExposure(command);

        result.Should().NotBeNull();
        result.TotalNetExposureUsd.Should().BeGreaterThan(7000000m);
        result.HighRiskCurrencies.Should().Contain("GBP");
        result.HighRiskCurrencies.Should().Contain("JPY");
        result.RebalancingRequired.Should().BeTrue();
    }

    [Fact]
    public void ExecuteForwardHedge_LocksForwardRateWithMaturity()
    {
        var command = new ExecuteForwardHedgeCommand("EUR/USD", 1500000m, 90, "JPMorgan Chase");
        var result = _engine.ExecuteForwardHedge(command);

        result.Should().NotBeNull();
        result.CurrencyPair.Should().Be("EUR/USD");
        result.NotionalAmount.Should().Be(1500000m);
        result.LockedRate.Should().Be(1.0875m);
        result.MaturityDate.Should().BeAfter(DateTime.UtcNow);
        result.HedgeExecutionReceipt.Should().Contain("AUDIT-FX-HEDGE");
    }
}
