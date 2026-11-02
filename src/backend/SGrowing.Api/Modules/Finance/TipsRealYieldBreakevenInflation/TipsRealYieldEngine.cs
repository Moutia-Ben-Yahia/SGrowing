namespace SGrowing.Api.Modules.Finance.TipsRealYieldBreakevenInflation;

public interface ITipsRealYieldEngine
{
    TipsRealYieldEvaluation AnalyzeRealYield(TipsRealYieldTelemetry telemetry);
}

public class TipsRealYieldEngine : ITipsRealYieldEngine
{
    public TipsRealYieldEvaluation AnalyzeRealYield(TipsRealYieldTelemetry telemetry)
    {
        var regimes = new List<string>();
        var mitigations = new List<string>();

        // Breakeven Inflation Rate (BEIR) = y_nom - y_real
        double calculatedBeir = telemetry.NominalTreasuryYieldPercent - telemetry.TipsRealYieldPercent;

        // Implied Expected Inflation = BEIR - (RP_infl - LP_tips) / 100
        double netPremiumPercent = (telemetry.EstimatedInflationRiskPremiumBps - telemetry.EstimatedTipsLiquidityDiscountBps) / 100.0;
        double impliedExpectedInflation = calculatedBeir - netPremiumPercent;

        // Curve slope in basis points: (LongTerm - ShortTerm) * 100
        double curveSlopeBps = (telemetry.LongTermBreakevenInflationPercent - telemetry.ShortTermBreakevenInflationPercent) * 100.0;

        // Inversion check: short-term > long-term by > 120 bps (i.e. slope < -120 bps)
        bool isStructureNormal = curveSlopeBps >= -120.0;
        bool isLiquidityNormal = telemetry.EstimatedTipsLiquidityDiscountBps <= 45.0;
        bool isDeflationContained = !(telemetry.TipsRealYieldPercent > 3.2 && telemetry.DeflationFloorOptionProbabilityPercent > 15.0);

        if (!isStructureNormal)
        {
            regimes.Add("BREAKEVEN_INFLATION_INVERSION_DISLOCATION");
            mitigations.Add("Implement inflation swap forward curve overlay and rebalance shorter tenors against front-end stagflation spikes.");
        }

        if (!isLiquidityNormal)
        {
            regimes.Add("TIPS_LIQUIDITY_DISCOUNT_DISLOCATION");
            mitigations.Add("Execute off-the-run to on-the-run switch transactions and widen bid-ask execution concessions for illiquid maturities.");
        }

        if (!isDeflationContained)
        {
            regimes.Add("DEFLATION_FLOOR_UNDERPRICING_VULNERABILITY");
            mitigations.Add("Hedge maturity par floor optionality via synthetic zero-coupon inflation floors or deep out-of-the-money put spreads.");
        }

        bool isOptimal = isStructureNormal && isLiquidityNormal && isDeflationContained && calculatedBeir > 0.50;

        return new TipsRealYieldEvaluation
        {
            SecurityCusipOrPortfolioId = telemetry.SecurityCusipOrPortfolioId,
            CalculatedBreakevenInflationPercent = Math.Round(calculatedBeir, 3),
            ImpliedExpectedInflationPercent = Math.Round(impliedExpectedInflation, 3),
            BreakevenCurveSlopeBps = Math.Round(curveSlopeBps, 1),
            IsBreakevenStructureNormal = isStructureNormal,
            IsTipsLiquidityNormal = isLiquidityNormal,
            IsDeflationFloorRiskContained = isDeflationContained,
            IsInflationHedgingOptimal = isOptimal,
            ActiveRegimes = regimes,
            RecommendedMitigations = mitigations
        };
    }
}
