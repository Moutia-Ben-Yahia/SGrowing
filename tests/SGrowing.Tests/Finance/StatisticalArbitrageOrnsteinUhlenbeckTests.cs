using System;
using SGrowing.Api.Modules.Finance.StatisticalArbitrageOrnsteinUhlenbeck;
using Xunit;

namespace SGrowing.Tests.Finance;

public sealed class StatisticalArbitrageOrnsteinUhlenbeckTests
{
    private readonly StatisticalArbitrageOrnsteinUhlenbeckEngine _engine = new();

    [Fact]
    public void AnalyzePairMeanReversion_NullTelemetry_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _engine.AnalyzePairMeanReversion(null!));
    }

    [Fact]
    public void AnalyzePairMeanReversion_NegativeVolatility_ThrowsArgumentException()
    {
        var telemetry = new StatisticalArbitrageTelemetry(
            PairSymbol: "AAPL-MSFT",
            CointegrationHedgeRatioBeta: 1.12,
            CurrentSpreadResidual: 0.025,
            LongTermMeanMu: 0.0,
            MeanReversionSpeedTheta: 0.15,
            VolatilitySigma: -0.01, // Negative!
            AugmentedDickeyFullerPValue: 0.02,
            DaysSinceStructuralBreak: 120.0
        );

        Assert.Throws<ArgumentException>(() => _engine.AnalyzePairMeanReversion(telemetry));
    }

    [Fact]
    public void AnalyzePairMeanReversion_ActiveEntryOpportunity_ReturnsActiveEntryRegime()
    {
        // theta = 0.14 -> half-life = ln(2)/0.14 = 4.95 days
        // sigma = 0.015 -> sigma_eq = 0.015 / sqrt(0.28) = 0.0283
        // mu = 0.0, residual = 0.065 -> Z = 0.065 / 0.0283 = 2.29 sigma
        var telemetry = new StatisticalArbitrageTelemetry(
            PairSymbol: "GOOGL-GOOG",
            CointegrationHedgeRatioBeta: 1.01,
            CurrentSpreadResidual: 0.065,
            LongTermMeanMu: 0.0,
            MeanReversionSpeedTheta: 0.14,
            VolatilitySigma: 0.015,
            AugmentedDickeyFullerPValue: 0.008,
            DaysSinceStructuralBreak: 250.0
        );

        var result = _engine.AnalyzePairMeanReversion(telemetry);

        Assert.NotNull(result);
        Assert.Equal("GOOGL-GOOG", result.PairSymbol);
        Assert.True(result.MeanReversionHalfLifeDays <= 30.0);
        Assert.True(result.NormalizedSpreadZScore >= 2.0);
        Assert.True(result.IsStatArbTradeValid);
        Assert.Equal("ACTIVE_STAT_ARB_MEAN_REVERSION_ENTRY_OPPORTUNITY", result.StatArbRegime);
        Assert.Contains("Short spread", result.QuantitativeExecutionGuidance);
    }

    [Fact]
    public void AnalyzePairMeanReversion_BrokenCointegration_ReturnsRandomWalkRegime()
    {
        var telemetry = new StatisticalArbitrageTelemetry(
            PairSymbol: "NVDA-INTC",
            CointegrationHedgeRatioBeta: 0.45,
            CurrentSpreadResidual: 0.12,
            LongTermMeanMu: 0.0,
            MeanReversionSpeedTheta: -0.01, // Negative theta (diverging)
            VolatilitySigma: 0.035,
            AugmentedDickeyFullerPValue: 0.45, // High p-value (non-stationary)
            DaysSinceStructuralBreak: 15.0
        );

        var result = _engine.AnalyzePairMeanReversion(telemetry);

        Assert.NotNull(result);
        Assert.Equal("BROKEN_COINTEGRATION_RANDOM_WALK_REGIME", result.StatArbRegime);
        Assert.False(result.IsStatArbTradeValid);
        Assert.Contains("non-stationarity", result.QuantitativeExecutionGuidance);
    }

    [Fact]
    public void AnalyzePairMeanReversion_ExtremeDivergence_ReturnsStructuralBreakHazard()
    {
        var telemetry = new StatisticalArbitrageTelemetry(
            PairSymbol: "DIS-NFLX",
            CointegrationHedgeRatioBeta: 0.85,
            CurrentSpreadResidual: 0.25, // Huge residual -> Z > 4.0
            LongTermMeanMu: 0.01,
            MeanReversionSpeedTheta: 0.10,
            VolatilitySigma: 0.012,
            AugmentedDickeyFullerPValue: 0.03,
            DaysSinceStructuralBreak: 40.0
        );

        var result = _engine.AnalyzePairMeanReversion(telemetry);

        Assert.NotNull(result);
        Assert.Equal("STRUCTURAL_BREAK_STOP_LOSS_DIVERGENCE_HAZARD", result.StatArbRegime);
        Assert.False(result.IsStatArbTradeValid);
        Assert.Contains("stop-loss", result.QuantitativeExecutionGuidance);
    }
}
