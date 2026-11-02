using System;

namespace SGrowing.Api.Modules.Finance.OrderFlowAdverseSelectionDrain;

public sealed class OrderFlowAdverseSelectionDrainEngine : IOrderFlowAdverseSelectionDrainEngine
{
    public OrderFlowAdverseSelectionAnalysisResult AnalyzeAdverseSelectionDrain(OrderFlowAdverseSelectionTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        if (string.IsNullOrWhiteSpace(telemetry.OrderBookInstrumentId))
        {
            throw new ArgumentException("Instrument ID must be provided.", nameof(telemetry));
        }

        if (telemetry.DepthReplenishmentRateSharesPerSecond < 0 || telemetry.InboundAggressiveRunVelocitySharesPerSecond < 0)
        {
            throw new ArgumentException("Flow velocities cannot be negative.", nameof(telemetry));
        }

        var volumeThousand = Math.Max(1.0, Math.Abs(telemetry.SignedTradeVolumeShares) / 1000.0);
        var kyleLambda = Math.Abs(telemetry.MidPricePostTradeImpactBps) / volumeThousand;

        var netDrain = telemetry.InboundAggressiveRunVelocitySharesPerSecond - telemetry.DepthReplenishmentRateSharesPerSecond;
        var replenishFloor = Math.Max(100.0, telemetry.DepthReplenishmentRateSharesPerSecond);
        var toxicityDrainRatio = telemetry.InboundAggressiveRunVelocitySharesPerSecond / replenishFloor;

        string regime;
        string guidance;
        bool isPullbackMandatory = toxicityDrainRatio > 2.5 || telemetry.ImmediatePostFillAdverseMoveBps > 3.5;

        if (toxicityDrainRatio > 3.0 && telemetry.ImmediatePostFillAdverseMoveBps > 5.0)
        {
            regime = "CRITICAL_TOXIC_SNIPING_ADVERSE_SELECTION_DRAIN";
            guidance = $"Severe toxic latency arbitrage detected. Immediate adverse move ({telemetry.ImmediatePostFillAdverseMoveBps:F1} bps) and drain ratio ({toxicityDrainRatio:F1}x) exhaust quotes. Immediately cancel resting quotes and skew spreads.";
        }
        else if (toxicityDrainRatio > 2.0 || kyleLambda > 1.5)
        {
            regime = "HIGH_ASYMMETRIC_INFORMED_FLOW_HAZARD";
            guidance = $"Informed aggressive orders dominating depth replenishment (Lambda = {kyleLambda:F2} bps/k-shares). Widen bid-ask quotes and reduce resting order sizes to limit inventory bleed.";
        }
        else if (netDrain <= 0 && telemetry.ImmediatePostFillAdverseMoveBps < 1.0)
        {
            regime = "BENIGN_UNINFORMED_BALANCED_LIQUIDITY_REGIME";
            guidance = "Market making spread capture optimal. Order flow is predominantly uninformed with stable book replenishment exceeding execution velocity.";
        }
        else
        {
            regime = "MODERATE_ORDER_FLOW_IMBALANCE_REGIME";
            guidance = $"Order flow exhibiting mild directional pressure. Net book drain velocity is {netDrain:F0} shares/s. Maintain standard inventory risk bounds.";
        }

        return new OrderFlowAdverseSelectionAnalysisResult(
            telemetry.OrderBookInstrumentId,
            Math.Round(kyleLambda, 3),
            Math.Round(netDrain, 1),
            Math.Round(toxicityDrainRatio, 2),
            regime,
            guidance,
            isPullbackMandatory
        );
    }
}
