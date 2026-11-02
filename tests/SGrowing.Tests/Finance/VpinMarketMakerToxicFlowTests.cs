namespace SGrowing.Tests.Finance;

using System;
using FluentAssertions;
using SGrowing.Api.Modules.Finance.VpinMarketMakerToxicFlow;
using Xunit;

public class VpinMarketMakerToxicFlowTests
{
    private readonly VpinMarketMakerToxicFlowEngine _engine = new();

    [Fact]
    public void AnalyzeOrderFlowToxicity_BalancedFlow_ReturnsBenignLiquidity()
    {
        var telemetry = new VpinTelemetry(
            Symbol: "BTC-USDT",
            BuyerInitiatedVolume: 51_500.0,
            SellerInitiatedVolume: 48_500.0,
            TotalVolumeBucketSize: 10_000.0,
            NumberOfBucketsSampled: 10,
            CurrentInventorySkewFraction: 0.15,
            BaselineBidAskSpreadBasisPoints: 1.5,
            HistoricalVpinPercentileRank: 0.22
        );

        var result = _engine.AnalyzeOrderFlowToxicity(telemetry);

        result.Symbol.Should().Be("BTC-USDT");
        result.MarketMakerRiskRegime.Should().Be("BENIGN_BALANCED_LIQUIDITY_FLOW");
        result.IsMarketMakerSafe.Should().BeTrue();
        result.VpinScore.Should().BeApproximately(0.03, 0.01);
        result.RecommendedSpreadMultiplier.Should().BeApproximately(1.0, 0.05);
        result.AdjustedBidAskSpreadBasisPoints.Should().BeApproximately(1.5, 0.1);
        result.QuotingDefenseAction.Should().ContainEquivalentOf("tightest tick increments");
    }

    [Fact]
    public void AnalyzeOrderFlowToxicity_SevereOneSidedToxicDump_ReturnsFlashCrashRegime()
    {
        var telemetry = new VpinTelemetry(
            Symbol: "ETH-USDT",
            BuyerInitiatedVolume: 12_000.0,
            SellerInitiatedVolume: 88_000.0, // Massive seller dump VPIN = 0.76 > 0.42
            TotalVolumeBucketSize: 10_000.0,
            NumberOfBucketsSampled: 10,
            CurrentInventorySkewFraction: 0.30,
            BaselineBidAskSpreadBasisPoints: 2.0,
            HistoricalVpinPercentileRank: 0.99
        );

        var result = _engine.AnalyzeOrderFlowToxicity(telemetry);

        result.MarketMakerRiskRegime.Should().Be("FLASH_CRASH_TOXIC_FLOW_AVALANCHE");
        result.IsMarketMakerSafe.Should().BeFalse();
        result.VpinScore.Should().BeGreaterThan(0.70);
        result.RecommendedSpreadMultiplier.Should().BeGreaterThan(3.0);
        result.QuotingDefenseAction.Should().ContainEquivalentOf("Pull resting limit orders");
    }

    [Fact]
    public void AnalyzeOrderFlowToxicity_HeavyInventorySkew_ReturnsSkewHazard()
    {
        var telemetry = new VpinTelemetry(
            Symbol: "SOL-USDT",
            BuyerInitiatedVolume: 52_000.0,
            SellerInitiatedVolume: 48_000.0, // Balanced VPIN ~ 0.04
            TotalVolumeBucketSize: 10_000.0,
            NumberOfBucketsSampled: 10,
            CurrentInventorySkewFraction: 0.88, // Skew > 0.75
            BaselineBidAskSpreadBasisPoints: 3.0,
            HistoricalVpinPercentileRank: 0.40
        );

        var result = _engine.AnalyzeOrderFlowToxicity(telemetry);

        result.MarketMakerRiskRegime.Should().Be("ASYMMETRIC_INVENTORY_SKEW_HAZARD");
        result.IsMarketMakerSafe.Should().BeFalse();
        result.QuotingDefenseAction.Should().ContainEquivalentOf("Skew quotes aggressively");
    }

    [Fact]
    public void AnalyzeOrderFlowToxicity_ModerateInformedActivity_ReturnsModerateRegime()
    {
        var telemetry = new VpinTelemetry(
            Symbol: "AVAX-USDT",
            BuyerInitiatedVolume: 66_000.0,
            SellerInitiatedVolume: 34_000.0, // VPIN = 32 / 100 = 0.32
            TotalVolumeBucketSize: 10_000.0,
            NumberOfBucketsSampled: 10,
            CurrentInventorySkewFraction: 0.20,
            BaselineBidAskSpreadBasisPoints: 2.5,
            HistoricalVpinPercentileRank: 0.74
        );

        var result = _engine.AnalyzeOrderFlowToxicity(telemetry);

        result.MarketMakerRiskRegime.Should().Be("MODERATE_INFORMED_TRADER_ACTIVITY");
        result.IsMarketMakerSafe.Should().BeTrue();
        result.VpinScore.Should().BeInRange(0.28, 0.40);
        result.RecommendedSpreadMultiplier.Should().BeGreaterThan(1.2);
        result.QuotingDefenseAction.Should().ContainEquivalentOf("Dynamically widen bid-ask spread");
    }

    [Fact]
    public void AnalyzeOrderFlowToxicity_NullTelemetry_ThrowsArgumentNullException()
    {
        var act = () => _engine.AnalyzeOrderFlowToxicity(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
