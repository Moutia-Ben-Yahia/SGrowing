using System;

namespace SGrowing.Api.Modules.Finance.CrossAssetLiquiditySpillover;

public sealed class CrossAssetLiquiditySpilloverEngine : ICrossAssetLiquiditySpilloverEngine
{
    public CrossAssetSpilloverAnalysisResult AnalyzeCrossAssetSpillover(CrossAssetSpilloverTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        var ownLambda = Math.Max(0.00001, telemetry.PrimaryAssetOwnImpactLambda);
        var crossLambda = Math.Max(0.0, telemetry.CrossAssetImpactLambda);

        // Spillover ratio: rho = Lambda_ij / Lambda_ii
        var spilloverRatio = crossLambda / ownLambda;

        // Price impacts
        var qOwn = Math.Max(0.0, telemetry.PrimaryAssetOrderFlowVolume);
        var qCross = Math.Max(0.0, telemetry.CorrelatedAssetOrderFlowVolume);

        var deltaPOwn = ownLambda * qOwn;
        var deltaPCross = crossLambda * qCross;
        var totalImpactBps = (deltaPOwn + deltaPCross) * 10_000.0;

        var baseSpread = Math.Max(0.1, telemetry.BaselineBidAskSpreadBasisPoints);
        var crossSlippageBps = (deltaPCross * 10_000.0 * spilloverRatio) + (baseSpread * spilloverRatio);

        string regime;
        string guidance;
        bool isContained;

        if (spilloverRatio > 0.65 || telemetry.CrossAssetCovarianceEigenvalueRatio > 0.80)
        {
            regime = "CROSS_ASSET_LIQUIDITY_CONTAGION_CASCADE";
            guidance = "Cross-asset impact spillover exceeds systemic resilience threshold (> 65%). High risk of correlated liquidation cascade. Engage portfolio risk firewalls and throttle cross-market orders.";
            isContained = false;
        }
        else if (crossSlippageBps > 45.0)
        {
            regime = "ELEVATED_CROSS_MARKET_SLIPPAGE_WARNING";
            guidance = "Off-diagonal impact tensor creating severe cross-market execution slippage (> 45 bps). Split orders across venue time-weighted buckets.";
            isContained = false;
        }
        else if (spilloverRatio > 0.35)
        {
            regime = "MODERATE_INTERMARKET_COUPLING";
            guidance = "Intermarket cross-impact active but manageable. Adjust Almgren-Chriss trajectory risk aversion parameter.";
            isContained = true;
        }
        else
        {
            regime = "DECOUPLED_RESILIENT_LIQUIDITY_REGIME";
            guidance = "Asset trading impact decoupled and locally absorbed. Cross-market spillover minimal; optimal execution proceeding with tight tracking.";
            isContained = true;
        }

        return new CrossAssetSpilloverAnalysisResult(
            PrimaryAssetSymbol: telemetry.PrimaryAssetSymbol,
            CrossAssetLiquiditySpilloverRatio: spilloverRatio,
            ProjectedTotalCrossMarketImpactBasisPoints: totalImpactBps,
            CrossMarketSlippageSpreadBasisPoints: crossSlippageBps,
            SpilloverRegime: regime,
            ExecutionRiskGuidance: guidance,
            IsSpilloverContained: isContained
        );
    }
}
