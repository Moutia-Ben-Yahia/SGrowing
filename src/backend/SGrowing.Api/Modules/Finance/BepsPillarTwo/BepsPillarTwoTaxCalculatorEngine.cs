namespace SGrowing.Api.Modules.Finance.BepsPillarTwo;

public class BepsPillarTwoTaxCalculatorEngine
{
    private const decimal DeMinimisRevenueThresholdEur = 10_000_000m;
    private const decimal DeMinimisIncomeThresholdEur = 1_000_000m;

    public BepsPillarTwoCalculationResult CalculateTopUpTax(BepsPillarTwoCalculationRequest request)
    {
        var jurisdictions = request.Jurisdictions ?? [];
        var assessments = new List<JurisdictionAssessment>();

        decimal totalGloBeIncome = 0m;
        decimal totalCoveredTaxes = 0m;
        decimal totalTopUpTaxPayable = 0m;

        foreach (var j in jurisdictions)
        {
            totalGloBeIncome += j.GloBeNetIncomeEur;
            totalCoveredTaxes += j.CoveredTaxesEur;

            // Check De Minimis exclusion (Art 5.5 GloBE rules)
            bool isDeMinimis = j.TotalRevenueEur < DeMinimisRevenueThresholdEur &&
                               j.GloBeNetIncomeEur < DeMinimisIncomeThresholdEur;

            if (isDeMinimis)
            {
                decimal etr = j.GloBeNetIncomeEur > 0m
                    ? Math.Round(j.CoveredTaxesEur / j.GloBeNetIncomeEur, 4)
                    : 0m;

                assessments.Add(new JurisdictionAssessment(
                    JurisdictionCode: j.JurisdictionCode,
                    CountryName: j.CountryName,
                    EffectiveTaxRate: etr,
                    TopUpTaxPercentage: 0m,
                    SbieCarveOutEur: 0m,
                    ExcessProfitEur: 0m,
                    TopUpTaxPayableEur: 0m,
                    IsDeMinimisExcluded: true,
                    StatusReason: "De Minimis Exclusion Safe Harbor (Revenue < €10M, Income < €1M)."
                ));
                continue;
            }

            if (j.GloBeNetIncomeEur <= 0m)
            {
                // Net loss jurisdiction
                assessments.Add(new JurisdictionAssessment(
                    JurisdictionCode: j.JurisdictionCode,
                    CountryName: j.CountryName,
                    EffectiveTaxRate: 0m,
                    TopUpTaxPercentage: 0m,
                    SbieCarveOutEur: 0m,
                    ExcessProfitEur: 0m,
                    TopUpTaxPayableEur: 0m,
                    IsDeMinimisExcluded: false,
                    StatusReason: "Loss jurisdiction; no GloBE Top-Up Tax applicable."
                ));
                continue;
            }

            decimal effectiveTaxRate = Math.Round(j.CoveredTaxesEur / j.GloBeNetIncomeEur, 4);

            if (effectiveTaxRate >= request.MinimumTaxRate)
            {
                assessments.Add(new JurisdictionAssessment(
                    JurisdictionCode: j.JurisdictionCode,
                    CountryName: j.CountryName,
                    EffectiveTaxRate: effectiveTaxRate,
                    TopUpTaxPercentage: 0m,
                    SbieCarveOutEur: 0m,
                    ExcessProfitEur: 0m,
                    TopUpTaxPayableEur: 0m,
                    IsDeMinimisExcluded: false,
                    StatusReason: $"Compliant: ETR ({effectiveTaxRate:P2}) exceeds minimum 15.0% threshold."
                ));
            }
            else
            {
                decimal topUpPercentage = request.MinimumTaxRate - effectiveTaxRate;

                // Substance-Based Income Exclusion (SBIE) carve-out
                decimal payrollCarveOut = Math.Round(j.EligiblePayrollCostsEur * request.PayrollCarveOutRate, 2);
                decimal assetCarveOut = Math.Round(j.EligibleTangibleAssetBookValueEur * request.TangibleAssetCarveOutRate, 2);
                decimal totalSbieCarveOut = payrollCarveOut + assetCarveOut;

                decimal excessProfit = Math.Max(0m, j.GloBeNetIncomeEur - totalSbieCarveOut);
                decimal topUpPayable = Math.Round(excessProfit * topUpPercentage, 2);

                totalTopUpTaxPayable += topUpPayable;

                assessments.Add(new JurisdictionAssessment(
                    JurisdictionCode: j.JurisdictionCode,
                    CountryName: j.CountryName,
                    EffectiveTaxRate: effectiveTaxRate,
                    TopUpTaxPercentage: topUpPercentage,
                    SbieCarveOutEur: totalSbieCarveOut,
                    ExcessProfitEur: excessProfit,
                    TopUpTaxPayableEur: topUpPayable,
                    IsDeMinimisExcluded: false,
                    StatusReason: $"Under-taxed: ETR ({effectiveTaxRate:P2}) triggers GloBE Top-Up Tax."
                ));
            }
        }

        decimal globalEtr = totalGloBeIncome > 0m
            ? Math.Round(totalCoveredTaxes / totalGloBeIncome, 4)
            : 0m;

        string complianceVerdict = totalTopUpTaxPayable > 0m
            ? $"Action Required: €{totalTopUpTaxPayable:N2} GloBE Top-Up Tax payable across under-taxed jurisdictions (QDMTT)."
            : "Fully Compliant: All operations meet or exceed 15.0% minimum tax threshold or qualify for safe harbor.";

        return new BepsPillarTwoCalculationResult(
            FiscalYear: request.FiscalYear,
            TotalJurisdictionsEvaluated: jurisdictions.Count,
            TotalGloBeNetIncomeEur: totalGloBeIncome,
            TotalCoveredTaxesEur: totalCoveredTaxes,
            GlobalEffectiveTaxRate: globalEtr,
            TotalTopUpTaxPayableEur: totalTopUpTaxPayable,
            Assessments: assessments,
            ComplianceVerdict: complianceVerdict
        );
    }
}
