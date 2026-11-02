namespace SGrowing.Api.Modules.Finance.CreditRiskUnderwriting;

/// <summary>
/// Autonomous algorithmic corporate credit risk underwriting and dynamic invoice factoring engine.
/// Evaluates Altman Z-Score solvency indicators and computes risk-adjusted discounting spreads.
/// </summary>
public sealed class AlgorithmicCreditRiskEngine
{
    private const double BenchmarkBaseRate = 0.045; // 4.50% central bank rate
    private const double SafeZoneThreshold = 2.90;
    private const double DistressZoneThreshold = 1.23;

    public CreditUnderwritingAssessment UnderwriteInvoice(InvoiceFactoringApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var f = app.DebtorFinancials;
        var totalAssets = (double)Math.Max(1000m, f.TotalAssetsEur);
        var liabilities = (double)Math.Max(1000m, f.TotalLiabilitiesEur);

        // 1. Calculate Altman Z-Score
        var x1 = (double)f.WorkingCapitalEur / totalAssets;
        var x2 = (double)f.RetainedEarningsEur / totalAssets;
        var x3 = (double)f.EbitOperatingIncomeEur / totalAssets;
        var x4 = (double)f.BookValueOfEquityEur / liabilities;

        var zScore = (6.56 * x1) + (3.26 * x2) + (6.72 * x3) + (1.05 * x4);

        // 2. Determine Solvency Zone & Probability of Default
        SolvencyZone zone;
        double spread;
        double advanceRate;
        bool isApproved;

        if (zScore > SafeZoneThreshold)
        {
            zone = SolvencyZone.SafeZone;
            spread = 0.015; // +1.50%
            advanceRate = 90.0;
            isApproved = true;
        }
        else if (zScore >= DistressZoneThreshold)
        {
            zone = SolvencyZone.GreyZone;
            spread = 0.035; // +3.50%
            advanceRate = 80.0;
            isApproved = true;
        }
        else
        {
            zone = SolvencyZone.DistressZone;
            spread = 0.075; // +7.50%
            advanceRate = zScore >= 0.60 ? 65.0 : 0.0;
            isApproved = advanceRate > 0.0;
        }

        // Logistic default probability: 1 / (1 + exp(1.5 * (Z - 1.23)))
        var pd = 1.0 / (1.0 + Math.Exp(1.5 * (zScore - DistressZoneThreshold)));

        // 3. Tenor & Discounting Calculation
        var now = DateTime.UtcNow;
        var tenorDays = Math.Max(1, (int)Math.Ceiling((app.DueDateUtc - now).TotalDays));
        var annualApr = (BenchmarkBaseRate + spread) * 100.0;

        decimal advanceAmount = 0m;
        decimal discountFee = 0m;
        decimal netDisbursed = 0m;

        if (isApproved)
        {
            advanceAmount = app.InvoiceFaceValueEur * (decimal)(advanceRate / 100.0);
            var tenorDiscountRatio = (annualApr / 100.0) * (tenorDays / 360.0);
            discountFee = advanceAmount * (decimal)tenorDiscountRatio;
            netDisbursed = advanceAmount - discountFee;
        }

        var notes = isApproved
            ? $"UNDERWRITING APPROVED ({zone}): Altman Z-Score {zScore:F2}, PD: {pd * 100:F1}%. Advance: {advanceRate:F0}% at {annualApr:F2}% APR for {tenorDays} days."
            : $"UNDERWRITING REJECTED (DistressZone): Altman Z-Score {zScore:F2} below acceptable threshold (< 0.60). Default probability {pd * 100:F1}% exceeds limits.";

        return new CreditUnderwritingAssessment(
            AssessmentId: $"CRD-{Guid.NewGuid():N}"[..10].ToUpperInvariant(),
            InvoiceId: app.InvoiceId,
            AltmanZScore: Math.Round(zScore, 2),
            Zone: zone,
            EstimatedProbabilityOfDefault: Math.Round(pd, 3),
            IsApproved: isApproved,
            ApprovedAdvanceRatePercent: advanceRate,
            AdvanceAmountEur: advanceAmount,
            DiscountFeeEur: discountFee,
            NetDisbursedCashEur: netDisbursed,
            EffectiveAnnualizedAprPercent: Math.Round(annualApr, 2),
            UnderwritingNotes: notes,
            AssessedAtUtc: DateTime.UtcNow
        );
    }
}
