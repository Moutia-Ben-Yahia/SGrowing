using System;

namespace SGrowing.Api.Modules.Finance.VpinMarketMakerToxicFlow;

public sealed class VpinMarketMakerToxicFlowEngine : IVpinMarketMakerToxicFlowEngine
{
    public VpinAnalysisResult AnalyzeOrderFlowToxicity(VpinTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        var vBuy = Math.Max(0.0, telemetry.BuyerInitiatedVolume);
        var vSell = Math.Max(0.0, telemetry.SellerInitiatedVolume);
        var vTotal = vBuy + vSell;
        var tradeImbalance = Math.Abs(vBuy - vSell);

        // VPIN = sum(|V_tau^B - V_tau^S|) / (N * V)
        var vpin = vTotal > 0.0 ? Math.Clamp(tradeImbalance / vTotal, 0.0, 1.0) : 0.0;

        // Spread widening multiplier based on toxic flow and inventory skew
        var toxicPenalty = 3.8 * Math.Max(0.0, vpin - 0.20);
        var skewFraction = Math.Clamp(telemetry.CurrentInventorySkewFraction, 0.0, 1.0);
        var skewPenalty = 1.4 * Math.Max(0.0, skewFraction - 0.50);
        var spreadMultiplier = 1.0 + toxicPenalty + skewPenalty;

        var baseSpread = Math.Max(0.1, telemetry.BaselineBidAskSpreadBasisPoints);
        var adjustedSpread = baseSpread * spreadMultiplier;

        string regime;
        string action;
        bool isSafe;

        var imbalanceRatio = vTotal > 0.0 ? tradeImbalance / vTotal : 0.0;

        if (vpin > 0.42 || (vTotal > 100.0 && imbalanceRatio > 0.85))
        {
            regime = "FLASH_CRASH_TOXIC_FLOW_AVALANCHE";
            action = "Volume-Synchronized Probability of Toxicity exceeds critical threshold (VPIN > 0.42). Pull resting limit orders, widen spreads 300%, and hedge net inventory on fast futures.";
            isSafe = false;
        }
        else if (skewFraction > 0.75)
        {
            regime = "ASYMMETRIC_INVENTORY_SKEW_HAZARD";
            action = "Market maker inventory heavily skewed (> 75% max limit). Skew quotes aggressively to incentivize counterparty rebalancing.";
            isSafe = false;
        }
        else if (vpin > 0.28)
        {
            regime = "MODERATE_INFORMED_TRADER_ACTIVITY";
            action = "Informed flow detected in order flow. Dynamically widen bid-ask spread and reduce posted quoting size at BBO.";
            isSafe = true;
        }
        else
        {
            regime = "BENIGN_BALANCED_LIQUIDITY_FLOW";
            action = "Order flow balanced and non-toxic (VPIN < 0.28). Post maximum quoting size at tightest tick increments.";
            isSafe = true;
        }

        return new VpinAnalysisResult(
            Symbol: telemetry.Symbol,
            VpinScore: vpin,
            RecommendedSpreadMultiplier: spreadMultiplier,
            AdjustedBidAskSpreadBasisPoints: adjustedSpread,
            MarketMakerRiskRegime: regime,
            QuotingDefenseAction: action,
            IsMarketMakerSafe: isSafe
        );
    }
}
