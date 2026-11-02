namespace SGrowing.Api.Modules.Finance.CdoGaussianCopulaTrancheCorrelation;

public interface ICdoCopulaTrancheEngine
{
    CdoCopulaTrancheEvaluation AnalyzeTranche(CdoCopulaTrancheTelemetry telemetry);
}

public class CdoCopulaTrancheEngine : ICdoCopulaTrancheEngine
{
    public CdoCopulaTrancheEvaluation AnalyzeTranche(CdoCopulaTrancheTelemetry telemetry)
    {
        var regimes = new List<string>();
        var mitigations = new List<string>();

        double a = telemetry.TrancheAttachmentPointPercent;
        double b = Math.Max(a + 0.1, telemetry.TrancheDetachmentPointPercent);
        double trancheWidth = b - a;

        double pd = telemetry.AverageObligorAnnualDefaultProbabilityPercent / 100.0;
        double lgd = 1.0 - (telemetry.ExpectedPortfolioRecoveryRatePercent / 100.0);
        double fiveYearCumPd = 1.0 - Math.Exp(-5.0 * pd);
        double expectedPortfolioLossPercent = fiveYearCumPd * lgd * 100.0;

        // Gaussian Copula tranche expected loss approximation
        double rho = Math.Clamp(telemetry.AssetPairwiseDefaultCorrelation, 0.01, 0.95);
        double correlationStress = Math.Clamp(rho * telemetry.ExtremeTailCorrelationMultiplier, 0.01, 0.99);

        // Expected tranche loss fraction f(L)
        double expectedLoss;
        if (expectedPortfolioLossPercent <= a)
        {
            // Senior/Mezzanine subordination buffers
            double buffer = a - expectedPortfolioLossPercent;
            expectedLoss = Math.Max(0.5, (expectedPortfolioLossPercent * rho * 1.5) / (1.0 + buffer * 0.2));
        }
        else if (expectedPortfolioLossPercent >= b)
        {
            expectedLoss = trancheWidth * 0.95;
        }
        else
        {
            expectedLoss = (expectedPortfolioLossPercent - a) * (1.0 + rho * 0.5);
        }
        double trancheLossPercent = Math.Clamp((expectedLoss / trancheWidth) * 100.0, 0.0, 100.0);

        // Stressed tail loss under systemic correlation jump
        double tailLossPercent = Math.Clamp(trancheLossPercent * (1.0 + correlationStress * 1.2), 0.0, 100.0);

        // Par spread basis points
        double parSpread = (trancheLossPercent * 35.0) + (telemetry.FiveYearCdsSpreadBasisPoints * (rho + 0.2));

        bool isCorrelationContained = telemetry.AssetPairwiseDefaultCorrelation <= 0.45 &&
                                      (telemetry.AssetPairwiseDefaultCorrelation * telemetry.ExtremeTailCorrelationMultiplier) <= 0.70;
        bool isProtected = trancheLossPercent <= 15.0;
        bool isSuperSeniorSafe = tailLossPercent <= 30.0;

        if (!isCorrelationContained)
        {
            regimes.Add("SYSTEMIC_CORRELATION_SPIKE_TAIL_CONTAGION");
            mitigations.Add("Execute credit index tranche swaption hedges (iTraxx/CDX tranche hedges) or purchase super-senior correlation skew overlays.");
        }

        if (a <= 0.5 && trancheLossPercent >= 75.0)
        {
            regimes.Add("EQUITY_TRANCHE_FIRST_LOSS_DEPLETION");
            mitigations.Add("Restructure deal capital stack by sizing equity overcollateralization reserve or diverting senior interest waterfall cash flows.");
        }

        if (!isProtected && a >= 3.0)
        {
            regimes.Add("MEZZANINE_SUBORDINATION_EROSION_ALERT");
            mitigations.Add("Re-underwrite collateral obligations with high covenant quality and diversify industry concentration below 12% per sector.");
        }

        return new CdoCopulaTrancheEvaluation
        {
            DealIdentifier = telemetry.DealIdentifier,
            CalculatedTrancheExpectedLossPercent = Math.Round(trancheLossPercent, 2),
            StressedTailContagionLossPercent = Math.Round(tailLossPercent, 2),
            BaseCorrelationParSpreadBps = Math.Round(parSpread, 1),
            IsTrancheInvestmentGradeProtected = isProtected,
            IsCorrelationRiskContained = isCorrelationContained,
            IsSuperSeniorSafeFromSpillover = isSuperSeniorSafe,
            ActiveRegimes = regimes,
            RecommendedMitigations = mitigations
        };
    }
}
