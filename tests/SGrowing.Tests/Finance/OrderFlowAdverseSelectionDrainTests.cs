using System;
using SGrowing.Api.Modules.Finance.OrderFlowAdverseSelectionDrain;
using Xunit;

namespace SGrowing.Tests.Finance;

public sealed class OrderFlowAdverseSelectionDrainTests
{
    private readonly OrderFlowAdverseSelectionDrainEngine _engine = new();

    [Fact]
    public void AnalyzeAdverseSelectionDrain_NullTelemetry_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _engine.AnalyzeAdverseSelectionDrain(null!));
    }

    [Fact]
    public void AnalyzeAdverseSelectionDrain_NegativeFlow_ThrowsArgumentException()
    {
        var telemetry = new OrderFlowAdverseSelectionTelemetry(
            OrderBookInstrumentId: "ES-FUT-2026",
            SignedTradeVolumeShares: 500.0,
            MidPricePostTradeImpactBps: 2.0,
            ImmediatePostFillAdverseMoveBps: 1.5,
            DepthReplenishmentRateSharesPerSecond: -100.0, // Negative!
            InboundAggressiveRunVelocitySharesPerSecond: 1000.0,
            SpreadWideTightRatio: 1.1,
            CancellationToFillRatio: 10.0
        );

        Assert.Throws<ArgumentException>(() => _engine.AnalyzeAdverseSelectionDrain(telemetry));
    }

    [Fact]
    public void AnalyzeAdverseSelectionDrain_CriticalToxicSniping_ReturnsCriticalDrainRegime()
    {
        var telemetry = new OrderFlowAdverseSelectionTelemetry(
            OrderBookInstrumentId: "NVDA-EQUITY-LOB",
            SignedTradeVolumeShares: 120_000.0,
            MidPricePostTradeImpactBps: 14.5,
            ImmediatePostFillAdverseMoveBps: 6.8, // > 5.0 bps
            DepthReplenishmentRateSharesPerSecond: 10_000.0,
            InboundAggressiveRunVelocitySharesPerSecond: 45_000.0, // Ratio = 4.5 > 3.0
            SpreadWideTightRatio: 2.2,
            CancellationToFillRatio: 35.0
        );

        var result = _engine.AnalyzeAdverseSelectionDrain(telemetry);

        Assert.NotNull(result);
        Assert.Equal("NVDA-EQUITY-LOB", result.OrderBookInstrumentId);
        Assert.True(result.ToxicityDrainRatio > 3.0);
        Assert.True(result.IsQuoteWideningOrPullbackMandatory);
        Assert.Equal("CRITICAL_TOXIC_SNIPING_ADVERSE_SELECTION_DRAIN", result.MicrostructureRegime);
        Assert.Contains("cancel resting quotes", result.LiquidityManagementGuidance);
    }

    [Fact]
    public void AnalyzeAdverseSelectionDrain_BenignUninformedFlow_ReturnsBenignRegime()
    {
        var telemetry = new OrderFlowAdverseSelectionTelemetry(
            OrderBookInstrumentId: "EURUSD-FX-SPOT",
            SignedTradeVolumeShares: 5_000.0,
            MidPricePostTradeImpactBps: 0.2,
            ImmediatePostFillAdverseMoveBps: 0.1,
            DepthReplenishmentRateSharesPerSecond: 50_000.0,
            InboundAggressiveRunVelocitySharesPerSecond: 20_000.0, // Net drain < 0
            SpreadWideTightRatio: 1.0,
            CancellationToFillRatio: 4.0
        );

        var result = _engine.AnalyzeAdverseSelectionDrain(telemetry);

        Assert.NotNull(result);
        Assert.True(result.NetLiquidityDrainVelocitySharesPerSecond < 0);
        Assert.False(result.IsQuoteWideningOrPullbackMandatory);
        Assert.Equal("BENIGN_UNINFORMED_BALANCED_LIQUIDITY_REGIME", result.MicrostructureRegime);
    }

    [Fact]
    public void AnalyzeAdverseSelectionDrain_InformedFlowHazard_ReturnsInformedHazardRegime()
    {
        var telemetry = new OrderFlowAdverseSelectionTelemetry(
            OrderBookInstrumentId: "BTCUSDT-PERP",
            SignedTradeVolumeShares: 2_000.0,
            MidPricePostTradeImpactBps: 5.5, // High lambda = 5.5 / 2 = 2.75 > 1.5
            ImmediatePostFillAdverseMoveBps: 2.1,
            DepthReplenishmentRateSharesPerSecond: 15_000.0,
            InboundAggressiveRunVelocitySharesPerSecond: 35_000.0, // Ratio = 2.33 > 2.0
            SpreadWideTightRatio: 1.5,
            CancellationToFillRatio: 18.0
        );

        var result = _engine.AnalyzeAdverseSelectionDrain(telemetry);

        Assert.NotNull(result);
        Assert.Equal("HIGH_ASYMMETRIC_INFORMED_FLOW_HAZARD", result.MicrostructureRegime);
        Assert.Contains("Widen bid-ask quotes", result.LiquidityManagementGuidance);
    }
}
