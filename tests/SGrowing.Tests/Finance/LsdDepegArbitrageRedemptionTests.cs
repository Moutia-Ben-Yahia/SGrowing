namespace SGrowing.Tests.Finance;

using System;
using FluentAssertions;
using SGrowing.Api.Modules.Finance.LsdDepegArbitrageRedemption;
using Xunit;

public class LsdDepegArbitrageRedemptionTests
{
    private readonly LsdDepegArbitrageRedemptionEngine _engine = new();

    [Fact]
    public void AnalyzeLsdDepegArbitrage_HealthyDiscountShortQueue_ReturnsProfitableOpportunity()
    {
        var telemetry = new LsdArbitrageTelemetry(
            PoolId: "CURVE-ETH-STETH",
            LsdTokenSymbol: "stETH",
            SecondaryMarketPrice: 0.982, // 1.8% discount
            CanonicalRedemptionRate: 1.000,
            ValidatorExitQueueDelayDays: 4.5,
            SecondaryPoolImbalanceRatio: 0.65,
            SimulatedTradeSizeEth: 500.0,
            EstimatedGasCostEth: 0.045,
            EstimatedPriceImpactPercent: 0.20,
            AnnualizedCapitalCostHurdleRatePercent: 4.5
        );

        var result = _engine.AnalyzeLsdDepegArbitrage(telemetry);

        result.PoolId.Should().Be("CURVE-ETH-STETH");
        result.ArbitrageRegime.Should().Be("PROFITABLE_DEPEG_ARBITRAGE_OPPORTUNITY");
        result.IsArbitrageViable.Should().BeTrue();
        result.DepegDiscountBasisPoints.Should().BeApproximately(180.0, 1.0);
        result.ExpectedProfitPerTradeEth.Should().BeGreaterThan(5.0);
        result.NetArbitrageYieldAnnualizedPercent.Should().BeGreaterThan(50.0);
        result.ExecutionStrategy.Should().ContainEquivalentOf("flash loan swap");
    }

    [Fact]
    public void AnalyzeLsdDepegArbitrage_SevereDepegAndImbalance_ReturnsContagionHalt()
    {
        var telemetry = new LsdArbitrageTelemetry(
            PoolId: "CURVE-ETH-RETH",
            LsdTokenSymbol: "rETH",
            SecondaryMarketPrice: 0.925, // 7.5% depeg > 600 bps
            CanonicalRedemptionRate: 1.000,
            ValidatorExitQueueDelayDays: 12.0,
            SecondaryPoolImbalanceRatio: 0.89, // Severe pool imbalance
            SimulatedTradeSizeEth: 100.0,
            EstimatedGasCostEth: 0.05,
            EstimatedPriceImpactPercent: 1.2,
            AnnualizedCapitalCostHurdleRatePercent: 5.0
        );

        var result = _engine.AnalyzeLsdDepegArbitrage(telemetry);

        result.ArbitrageRegime.Should().Be("SYSTEMIC_LST_RUN_AND_DEPEG_CONTAGION");
        result.IsArbitrageViable.Should().BeFalse();
        result.ExecutionStrategy.Should().ContainEquivalentOf("contagion circuit breaker");
    }

    [Fact]
    public void AnalyzeLsdDepegArbitrage_TrivialDiscountConsumedByFees_ReturnsNegativeYield()
    {
        var telemetry = new LsdArbitrageTelemetry(
            PoolId: "CURVE-ETH-CBETH",
            LsdTokenSymbol: "cbETH",
            SecondaryMarketPrice: 0.9992, // Only 8 bps discount
            CanonicalRedemptionRate: 1.000,
            ValidatorExitQueueDelayDays: 8.0,
            SecondaryPoolImbalanceRatio: 0.52,
            SimulatedTradeSizeEth: 10.0, // Small size high relative gas
            EstimatedGasCostEth: 0.05,
            EstimatedPriceImpactPercent: 0.15,
            AnnualizedCapitalCostHurdleRatePercent: 4.5
        );

        var result = _engine.AnalyzeLsdDepegArbitrage(telemetry);

        result.ArbitrageRegime.Should().Be("NEGATIVE_NET_YIELD_POST_EXPENSES");
        result.IsArbitrageViable.Should().BeFalse();
        result.ExpectedProfitPerTradeEth.Should().BeLessThanOrEqualTo(0.0);
        result.ExecutionStrategy.Should().ContainEquivalentOf("Abort transaction execution");
    }

    [Fact]
    public void AnalyzeLsdDepegArbitrage_ExtendedExitQueue_ReturnsHedgedStrategy()
    {
        var telemetry = new LsdArbitrageTelemetry(
            PoolId: "CURVE-ETH-WSTETH",
            LsdTokenSymbol: "wstETH",
            SecondaryMarketPrice: 0.975, // 2.5% discount
            CanonicalRedemptionRate: 1.000,
            ValidatorExitQueueDelayDays: 19.5, // > 14 days queue
            SecondaryPoolImbalanceRatio: 0.70,
            SimulatedTradeSizeEth: 400.0,
            EstimatedGasCostEth: 0.045,
            EstimatedPriceImpactPercent: 0.25,
            AnnualizedCapitalCostHurdleRatePercent: 4.0
        );

        var result = _engine.AnalyzeLsdDepegArbitrage(telemetry);

        result.ArbitrageRegime.Should().Be("EXTENDED_VALIDATOR_EXIT_QUEUE_EXPOSURE");
        result.IsArbitrageViable.Should().BeTrue();
        result.ExpectedProfitPerTradeEth.Should().BeGreaterThan(0.0);
        result.ExecutionStrategy.Should().ContainEquivalentOf("perpetual futures hedge");
    }

    [Fact]
    public void AnalyzeLsdDepegArbitrage_NullTelemetry_ThrowsArgumentNullException()
    {
        var act = () => _engine.AnalyzeLsdDepegArbitrage(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
