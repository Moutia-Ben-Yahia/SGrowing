namespace SGrowing.Tests.Finance;

using System;
using FluentAssertions;
using SGrowing.Api.Modules.Finance.CrossAssetLiquiditySpillover;
using Xunit;

public class CrossAssetLiquiditySpilloverTests
{
    private readonly CrossAssetLiquiditySpilloverEngine _engine = new();

    [Fact]
    public void AnalyzeCrossAssetSpillover_DecoupledMarkets_ReturnsResilientLiquidity()
    {
        var telemetry = new CrossAssetSpilloverTelemetry(
            PrimaryAssetSymbol: "BTC",
            CorrelatedAssetSymbol: "ETH",
            PrimaryAssetOwnImpactLambda: 0.00040,
            CrossAssetImpactLambda: 0.00008, // Spillover ratio = 0.20 < 0.35
            PrimaryAssetOrderFlowVolume: 50.0,
            CorrelatedAssetOrderFlowVolume: 80.0,
            CrossAssetCovarianceEigenvalueRatio: 0.30,
            BaselineBidAskSpreadBasisPoints: 1.5
        );

        var result = _engine.AnalyzeCrossAssetSpillover(telemetry);

        result.PrimaryAssetSymbol.Should().Be("BTC");
        result.SpilloverRegime.Should().Be("DECOUPLED_RESILIENT_LIQUIDITY_REGIME");
        result.IsSpilloverContained.Should().BeTrue();
        result.CrossAssetLiquiditySpilloverRatio.Should().BeApproximately(0.20, 0.02);
        result.CrossMarketSlippageSpreadBasisPoints.Should().BeLessThan(25.0);
        result.ExecutionRiskGuidance.Should().ContainEquivalentOf("trading impact decoupled and locally absorbed");
    }

    [Fact]
    public void AnalyzeCrossAssetSpillover_HighContagion_ReturnsContagionCascade()
    {
        var telemetry = new CrossAssetSpilloverTelemetry(
            PrimaryAssetSymbol: "SOL",
            CorrelatedAssetSymbol: "ETH",
            PrimaryAssetOwnImpactLambda: 0.00045,
            CrossAssetImpactLambda: 0.00035, // Spillover ratio = 0.77 > 0.65
            PrimaryAssetOrderFlowVolume: 60.0,
            CorrelatedAssetOrderFlowVolume: 120.0,
            CrossAssetCovarianceEigenvalueRatio: 0.85, // Exceeds 0.80
            BaselineBidAskSpreadBasisPoints: 2.0
        );

        var result = _engine.AnalyzeCrossAssetSpillover(telemetry);

        result.SpilloverRegime.Should().Be("CROSS_ASSET_LIQUIDITY_CONTAGION_CASCADE");
        result.IsSpilloverContained.Should().BeFalse();
        result.ExecutionRiskGuidance.Should().ContainEquivalentOf("correlated liquidation cascade");
    }

    [Fact]
    public void AnalyzeCrossAssetSpillover_ElevatedCrossSlippage_ReturnsSlippageWarning()
    {
        var telemetry = new CrossAssetSpilloverTelemetry(
            PrimaryAssetSymbol: "AVAX",
            CorrelatedAssetSymbol: "SOL",
            PrimaryAssetOwnImpactLambda: 0.00050,
            CrossAssetImpactLambda: 0.00025, // Spillover ratio = 0.50
            PrimaryAssetOrderFlowVolume: 40.0,
            CorrelatedAssetOrderFlowVolume: 220.0, // High cross volume -> slippage > 45 bps
            CrossAssetCovarianceEigenvalueRatio: 0.60,
            BaselineBidAskSpreadBasisPoints: 2.5
        );

        var result = _engine.AnalyzeCrossAssetSpillover(telemetry);

        result.SpilloverRegime.Should().Be("ELEVATED_CROSS_MARKET_SLIPPAGE_WARNING");
        result.IsSpilloverContained.Should().BeFalse();
        result.CrossMarketSlippageSpreadBasisPoints.Should().BeGreaterThan(45.0);
        result.ExecutionRiskGuidance.Should().ContainEquivalentOf("time-weighted buckets");
    }

    [Fact]
    public void AnalyzeCrossAssetSpillover_ModerateCoupling_ReturnsModerateRegime()
    {
        var telemetry = new CrossAssetSpilloverTelemetry(
            PrimaryAssetSymbol: "BNB",
            CorrelatedAssetSymbol: "BTC",
            PrimaryAssetOwnImpactLambda: 0.00040,
            CrossAssetImpactLambda: 0.00018, // Spillover ratio = 0.45 in [0.35, 0.65]
            PrimaryAssetOrderFlowVolume: 30.0,
            CorrelatedAssetOrderFlowVolume: 40.0,
            CrossAssetCovarianceEigenvalueRatio: 0.50,
            BaselineBidAskSpreadBasisPoints: 1.8
        );

        var result = _engine.AnalyzeCrossAssetSpillover(telemetry);

        result.SpilloverRegime.Should().Be("MODERATE_INTERMARKET_COUPLING");
        result.IsSpilloverContained.Should().BeTrue();
        result.CrossAssetLiquiditySpilloverRatio.Should().BeInRange(0.35, 0.65);
        result.ExecutionRiskGuidance.Should().ContainEquivalentOf("risk aversion parameter");
    }

    [Fact]
    public void AnalyzeCrossAssetSpillover_NullTelemetry_ThrowsArgumentNullException()
    {
        var act = () => _engine.AnalyzeCrossAssetSpillover(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
