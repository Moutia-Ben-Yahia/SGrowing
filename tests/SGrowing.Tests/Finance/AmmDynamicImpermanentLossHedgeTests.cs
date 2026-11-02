namespace SGrowing.Tests.Finance;

using System;
using FluentAssertions;
using SGrowing.Api.Modules.Finance.AmmDynamicImpermanentLossHedge;
using Xunit;

public class AmmDynamicImpermanentLossHedgeTests
{
    private readonly AmmDynamicImpermanentLossHedgeEngine _engine = new();

    [Fact]
    public void AnalyzeHedge_NominalDeltaNeutral_ReturnsOptimalHarvestRegime()
    {
        var telemetry = new AmmDynamicHedgeTelemetry(
            PoolPositionId: "AMM-ETH-USDC-01",
            PoolPairSymbol: "ETH/USDC",
            InitialEntryPriceRatio: 2500.0,
            CurrentMarketPriceRatio: 2600.0, // Mild drift k = 1.04
            TotalLiquidityPoolValueUsd: 100_000.0,
            CumulativeTradingFeesEarnedUsd: 2_400.0,
            PerpetualFuturesFundingRateBasisPoints: 2.5,
            DynamicDeltaHedgeRebalanceThresholdPercent: 5.0,
            ImpliedVolatilityAnnualized: 0.60
        );

        var result = _engine.AnalyzeHedge(telemetry);

        result.PoolPositionId.Should().Be("AMM-ETH-USDC-01");
        result.ImpermanentLossHedgeRegime.Should().Be("OPTIMAL_DELTA_NEUTRAL_FEE_HARVEST_REGIME");
        result.IsHedgeOptimal.Should().BeTrue();
        result.PriceDivergenceRatioK.Should().BeApproximately(1.04, 0.01);
        result.UnhedgedImpermanentLossPercent.Should().BeLessThan(1.0); // Mild IL < 1%
        result.NetHedgedPortfolioReturnUsd.Should().BeGreaterThan(2000.0);
        result.DeltaRebalancingGuidance.Should().ContainEquivalentOf("fee harvest yield maximized");
    }

    [Fact]
    public void AnalyzeHedge_ModerateDrift_ReturnsDeltaRebalanceTrigger()
    {
        var telemetry = new AmmDynamicHedgeTelemetry(
            PoolPositionId: "AMM-SOL-USDC-02",
            PoolPairSymbol: "SOL/USDC",
            InitialEntryPriceRatio: 120.0,
            CurrentMarketPriceRatio: 165.0, // Drift k = 1.375 > 1.20, IL ~ 4.5%
            TotalLiquidityPoolValueUsd: 80_000.0,
            CumulativeTradingFeesEarnedUsd: 3_200.0,
            PerpetualFuturesFundingRateBasisPoints: 3.0,
            DynamicDeltaHedgeRebalanceThresholdPercent: 5.0,
            ImpliedVolatilityAnnualized: 0.85
        );

        var result = _engine.AnalyzeHedge(telemetry);

        result.ImpermanentLossHedgeRegime.Should().Be("DELTA_REBALANCE_TRIGGER_ACTIVE");
        result.IsHedgeOptimal.Should().BeTrue();
        result.UnhedgedImpermanentLossPercent.Should().BeGreaterThan(1.0);
        result.DeltaRebalancingGuidance.Should().ContainEquivalentOf("Execute perpetual futures order");
    }

    [Fact]
    public void AnalyzeHedge_SevereDivergence_ReturnsSevereGammaDrag()
    {
        var telemetry = new AmmDynamicHedgeTelemetry(
            PoolPositionId: "AMM-MEME-ETH-03",
            PoolPairSymbol: "MEME/ETH",
            InitialEntryPriceRatio: 1.0,
            CurrentMarketPriceRatio: 3.2, // Massive breakout k = 3.2 > 2.5
            TotalLiquidityPoolValueUsd: 50_000.0,
            CumulativeTradingFeesEarnedUsd: 4_500.0,
            PerpetualFuturesFundingRateBasisPoints: 10.0,
            DynamicDeltaHedgeRebalanceThresholdPercent: 5.0,
            ImpliedVolatilityAnnualized: 1.40
        );

        var result = _engine.AnalyzeHedge(telemetry);

        result.ImpermanentLossHedgeRegime.Should().Be("SEVERE_PRICE_DIVERGENCE_GAMMA_DRAG");
        result.IsHedgeOptimal.Should().BeFalse();
        result.UnhedgedImpermanentLossPercent.Should().BeGreaterThan(14.0);
        result.DeltaRebalancingGuidance.Should().ContainEquivalentOf("options collar");
    }

    [Fact]
    public void AnalyzeHedge_HighFundingDrag_ReturnsFundingRateWarning()
    {
        var telemetry = new AmmDynamicHedgeTelemetry(
            PoolPositionId: "AMM-BTC-USDC-04",
            PoolPairSymbol: "BTC/USDC",
            InitialEntryPriceRatio: 60_000.0,
            CurrentMarketPriceRatio: 65_000.0,
            TotalLiquidityPoolValueUsd: 200_000.0,
            CumulativeTradingFeesEarnedUsd: 150.0, // Low fees earned
            PerpetualFuturesFundingRateBasisPoints: 250.0, // Extreme funding cost > 60% of fees
            DynamicDeltaHedgeRebalanceThresholdPercent: 5.0,
            ImpliedVolatilityAnnualized: 0.50
        );

        var result = _engine.AnalyzeHedge(telemetry);

        result.ImpermanentLossHedgeRegime.Should().Be("PERPETUAL_FUNDING_RATE_DRAG_WARNING");
        result.IsHedgeOptimal.Should().BeFalse();
        result.DeltaRebalancingGuidance.Should().ContainEquivalentOf("dated quarterly futures");
    }

    [Fact]
    public void AnalyzeHedge_NullTelemetry_ThrowsArgumentNullException()
    {
        var act = () => _engine.AnalyzeHedge(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
