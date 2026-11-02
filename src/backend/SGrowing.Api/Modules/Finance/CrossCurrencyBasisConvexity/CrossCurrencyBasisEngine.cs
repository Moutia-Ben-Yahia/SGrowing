namespace SGrowing.Api.Modules.Finance.CrossCurrencyBasisConvexity;

public interface ICrossCurrencyBasisEngine
{
    CrossCurrencyBasisEvaluation AnalyzePortfolio(CrossCurrencyBasisTelemetry telemetry);
}

public class CrossCurrencyBasisEngine : ICrossCurrencyBasisEngine
{
    public CrossCurrencyBasisEvaluation AnalyzePortfolio(CrossCurrencyBasisTelemetry telemetry)
    {
        var regimes = new List<string>();
        var mitigations = new List<string>();

        // Absolute CIP deviation in bps
        double absCipBps = Math.Abs(telemetry.CoveredInterestParityBasisBps);

        // Forced duration impact from negative convexity: dD = Convexity * dy
        // dy in decimals = yieldShockBps / 10000
        double dy = telemetry.ProjectedYieldShockBasisPoints / 10000.0;
        double forcedDurationImpact = Math.Abs(telemetry.PortfolioConvexityYearsSquared * dy);

        bool isCipBounded = absCipBps <= 25.0;
        bool isConvexityContained = telemetry.DurationMismatchYears <= 1.5 && forcedDurationImpact <= 1.2;
        bool isCollateralAligned = telemetry.CollateralDiscountCurveSpreadBps <= 20.0;

        if (absCipBps > 32.0)
        {
            regimes.Add("CROSS_CURRENCY_CIP_BASIS_BLOWOUT");
            mitigations.Add("Access central bank liquidity swap lines and engage fx swap synthetic borrowing to arbitrage the negative basis spread.");
        }

        if (telemetry.DurationMismatchYears > 1.6 || forcedDurationImpact > 1.2)
        {
            regimes.Add("CONVEXITY_CONUNDRUM_FORCED_DURATION_CASCADE");
            mitigations.Add("Re-hedge mortgage prepayment negative convexity via swaption straddles and Treasury futures curve overlays.");
        }

        if (!isCollateralAligned)
        {
            regimes.Add("COLLATERAL_CURVE_DISCOUNT_DISLOCATION");
            mitigations.Add("Switch multi-currency CSA collateral posting eligibility to minimize cross-discounting divergence.");
        }

        bool isSound = isCipBounded && isConvexityContained && isCollateralAligned;

        return new CrossCurrencyBasisEvaluation
        {
            CurrencyPairOrPortfolioUid = telemetry.CurrencyPairOrPortfolioUid,
            AbsoluteCipDeviationBps = Math.Round(absCipBps, 2),
            ForcedDurationHedgingImpactYears = Math.Round(forcedDurationImpact, 3),
            IsCipBasisArbitrageBounded = isCipBounded,
            IsConvexityCascadeContained = isConvexityContained,
            IsCollateralCurveAligned = isCollateralAligned,
            IsCrossCurrencyPortfolioHedgingSound = isSound,
            ActiveRegimes = regimes,
            RecommendedMitigations = mitigations
        };
    }
}
