namespace SGrowing.Tests.Finance;

using System;
using FluentAssertions;
using SGrowing.Api.Modules.Finance.CrossCurrencyFlashSwapArbitrage;
using Xunit;

public class CrossCurrencyFlashSwapArbitrageTests
{
    private readonly CrossCurrencyFlashSwapArbitrageEngine _engine = new();

    [Fact]
    public void AnalyzeTriangularArbitrage_ProfitableRoute_ReturnsProfitableRegime()
    {
        var telemetry = new CrossCurrencyFlashSwapTelemetry(
            ArbitragePathId: "ARB-TRI-001",
            TriangularRouteDescription: "USDC -> WETH -> WBTC -> USDC",
            ProposedBorrowAmountUsd: 250_000.0,
            Leg1PoolFeeTierPercent: 0.05,
            Leg2PoolFeeTierPercent: 0.30,
            Leg3PoolFeeTierPercent: 0.05,
            ProductOfExchangeRates: 1.018, // 1.8% raw triangular rate discrepancy
            EstimatedTotalPriceImpactBasisPoints: 18.0,
            PriorityBuilderTipFeeGwei: 35.0,
            EthereumBaseGasFeeGwei: 15.0,
            IsPrivateMevBuilderRelayUsed: true
        );

        var result = _engine.AnalyzeTriangularArbitrage(telemetry);

        result.ArbitragePathId.Should().Be("ARB-TRI-001");
        result.ArbitrageExecutionRegime.Should().Be("ATOMIC_TRIANGULAR_ARBITRAGE_PROFITABLE_REGIME");
        result.IsArbitrageProfitable.Should().BeTrue();
        result.OptimalArbitrageTradeSizeUsd.Should().BeGreaterThan(50_000.0);
        result.GrossTriangularProfitUsd.Should().BeGreaterThan(500.0);
        result.NetRiskAdjustedArbitrageProfitUsd.Should().BeGreaterThan(400.0);
        result.MevBundleSubmissionGuidance.Should().ContainEquivalentOf("Atomic bundle verified for direct block builder inclusion");
    }

    [Fact]
    public void AnalyzeTriangularArbitrage_PublicMempool_ReturnsSandwichHazard()
    {
        var telemetry = new CrossCurrencyFlashSwapTelemetry(
            ArbitragePathId: "ARB-TRI-002",
            TriangularRouteDescription: "USDC -> DAI -> WETH -> USDC",
            ProposedBorrowAmountUsd: 100_000.0,
            Leg1PoolFeeTierPercent: 0.01,
            Leg2PoolFeeTierPercent: 0.05,
            Leg3PoolFeeTierPercent: 0.05,
            ProductOfExchangeRates: 1.012,
            EstimatedTotalPriceImpactBasisPoints: 12.0,
            PriorityBuilderTipFeeGwei: 10.0,
            EthereumBaseGasFeeGwei: 12.0,
            IsPrivateMevBuilderRelayUsed: false // Public mempool submission!
        );

        var result = _engine.AnalyzeTriangularArbitrage(telemetry);

        result.ArbitrageExecutionRegime.Should().Be("PUBLIC_MEMPOOL_SANDWICH_EXPLOITATION_HAZARD");
        result.IsArbitrageProfitable.Should().BeFalse();
        result.MevBundleSubmissionGuidance.Should().ContainEquivalentOf("Route bundle exclusively through Flashbots/MEV-Share");
    }

    [Fact]
    public void AnalyzeTriangularArbitrage_NegativeMargin_ReturnsNegativeEvFailure()
    {
        var telemetry = new CrossCurrencyFlashSwapTelemetry(
            ArbitragePathId: "ARB-TRI-003",
            TriangularRouteDescription: "USDC -> LINK -> WETH -> USDC",
            ProposedBorrowAmountUsd: 50_000.0,
            Leg1PoolFeeTierPercent: 0.30,
            Leg2PoolFeeTierPercent: 0.30,
            Leg3PoolFeeTierPercent: 0.30,
            ProductOfExchangeRates: 1.002, // Only 0.2% raw, but fees eat 0.9%
            EstimatedTotalPriceImpactBasisPoints: 25.0,
            PriorityBuilderTipFeeGwei: 20.0,
            EthereumBaseGasFeeGwei: 15.0,
            IsPrivateMevBuilderRelayUsed: true
        );

        var result = _engine.AnalyzeTriangularArbitrage(telemetry);

        result.ArbitrageExecutionRegime.Should().Be("NEGATIVE_EXPECTED_VALUE_EXECUTION_FAILURE");
        result.IsArbitrageProfitable.Should().BeFalse();
        result.MevBundleSubmissionGuidance.Should().ContainEquivalentOf("Abort flash loan transaction");
    }

    [Fact]
    public void AnalyzeTriangularArbitrage_ExcessiveSlippage_ReturnsSlippageDeficit()
    {
        var telemetry = new CrossCurrencyFlashSwapTelemetry(
            ArbitragePathId: "ARB-TRI-004",
            TriangularRouteDescription: "USDC -> UNI -> WETH -> USDC",
            ProposedBorrowAmountUsd: 500_000.0,
            Leg1PoolFeeTierPercent: 0.05,
            Leg2PoolFeeTierPercent: 0.30,
            Leg3PoolFeeTierPercent: 0.05,
            ProductOfExchangeRates: 1.035, // High rate discrepancy
            EstimatedTotalPriceImpactBasisPoints: 110.0, // High slippage > 80 bps
            PriorityBuilderTipFeeGwei: 25.0,
            EthereumBaseGasFeeGwei: 15.0,
            IsPrivateMevBuilderRelayUsed: true
        );

        var result = _engine.AnalyzeTriangularArbitrage(telemetry);

        result.ArbitrageExecutionRegime.Should().Be("EXCESSIVE_SLIPPAGE_CONVEXITY_DEFICIT");
        result.IsArbitrageProfitable.Should().BeFalse();
        result.MevBundleSubmissionGuidance.Should().ContainEquivalentOf("Triangular liquidity route shallow");
    }

    [Fact]
    public void AnalyzeTriangularArbitrage_NullTelemetry_ThrowsArgumentNullException()
    {
        var act = () => _engine.AnalyzeTriangularArbitrage(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
