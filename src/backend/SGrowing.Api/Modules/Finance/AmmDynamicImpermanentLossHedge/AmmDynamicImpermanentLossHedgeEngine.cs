using System;

namespace SGrowing.Api.Modules.Finance.AmmDynamicImpermanentLossHedge;

public sealed class AmmDynamicImpermanentLossHedgeEngine : IAmmDynamicImpermanentLossHedgeEngine
{
    public AmmDynamicHedgeAnalysisResult AnalyzeHedge(AmmDynamicHedgeTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        var p0 = Math.Max(0.001, telemetry.InitialEntryPriceRatio);
        var pt = Math.Max(0.001, telemetry.CurrentMarketPriceRatio);
        var v0 = Math.Max(10.0, telemetry.TotalLiquidityPoolValueUsd);

        // Price divergence ratio: k = P_t / P_0
        var k = pt / p0;

        // Impermanent Loss formula: IL = 2*sqrt(k)/(1 + k) - 1
        var sqrtK = Math.Sqrt(k);
        var ilFraction = (2.0 * sqrtK) / (1.0 + k) - 1.0;
        var unhedgedIlPercent = Math.Abs(ilFraction) * 100.0;
        var unhedgedIlUsd = Math.Abs(ilFraction) * v0;

        // Dynamic short perpetual delta required to neutralize LP gamma drift (in base asset units)
        var optimalHedgeDeltaUnits = (v0 / (2.0 * Math.Sqrt(p0 * pt))) * (1.0 - sqrtK);

        // Perpetual futures funding cost drag
        var hedgeNotionalUsd = Math.Abs(optimalHedgeDeltaUnits * pt);
        var fundingDragBps = Math.Max(0.0, telemetry.PerpetualFuturesFundingRateBasisPoints);
        var dailyFundingCostUsd = hedgeNotionalUsd * (fundingDragBps / 10_000.0);

        // Net hedged portfolio return: Fees earned - residual hedged gamma cost - funding drag
        var feesUsd = Math.Max(0.0, telemetry.CumulativeTradingFeesEarnedUsd);
        var residualGammaCostUsd = unhedgedIlUsd * 0.15; // 85% IL eliminated via continuous delta rebalancing
        var netHedgedReturnUsd = feesUsd - residualGammaCostUsd - dailyFundingCostUsd;

        string regime;
        string guidance;
        bool isOptimal;

        if (unhedgedIlPercent > 10.0 && (k > 2.5 || k < 0.40))
        {
            regime = "SEVERE_PRICE_DIVERGENCE_GAMMA_DRAG";
            guidance = "Price divergence ratio k exceeds extreme thresholds (> 2.5 or < 0.4). High quadratic gamma slippage. Close LP position or establish wider asymmetric options collar.";
            isOptimal = false;
        }
        else if (feesUsd > 0.0 && dailyFundingCostUsd > feesUsd * 0.60)
        {
            regime = "PERPETUAL_FUNDING_RATE_DRAG_WARNING";
            guidance = "Perpetual futures short funding rate cost exceeds 60% of LP trading fee yield. Switch hedging instrument to dated quarterly futures or synthetic options.";
            isOptimal = false;
        }
        else if (unhedgedIlPercent > 1.0 && Math.Abs(k - 1.0) > 0.20)
        {
            regime = "DELTA_REBALANCE_TRIGGER_ACTIVE";
            guidance = "Price ratio drift breached rebalance threshold. Execute perpetual futures order to re-center hedge delta to optimal target.";
            isOptimal = true;
        }
        else
        {
            regime = "OPTIMAL_DELTA_NEUTRAL_FEE_HARVEST_REGIME";
            guidance = "AMM liquidity pool market-making operating in delta-neutral equilibrium. Impermanent loss mitigated and fee harvest yield maximized.";
            isOptimal = true;
        }

        return new AmmDynamicHedgeAnalysisResult(
            PoolPositionId: telemetry.PoolPositionId,
            PriceDivergenceRatioK: k,
            UnhedgedImpermanentLossPercent: unhedgedIlPercent,
            UnhedgedImpermanentLossUsd: unhedgedIlUsd,
            OptimalPerpetualHedgeDeltaUnits: optimalHedgeDeltaUnits,
            NetHedgedPortfolioReturnUsd: netHedgedReturnUsd,
            ImpermanentLossHedgeRegime: regime,
            DeltaRebalancingGuidance: guidance,
            IsHedgeOptimal: isOptimal
        );
    }
}
