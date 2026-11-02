namespace SGrowing.Api.Modules.Trade.RulesOfOriginTariff;

public interface IRulesOfOriginTariffEngine
{
    TariffAssessmentResult EvaluateOriginAndTariffs(RulesOfOriginEvaluationRequest request);
}

public class RulesOfOriginTariffEngine : IRulesOfOriginTariffEngine
{
    public TariffAssessmentResult EvaluateOriginAndTariffs(RulesOfOriginEvaluationRequest request)
    {
        var components = request.Components ?? [];
        decimal totalBomCost = components.Sum(c => c.UnitCostUsd);
        decimal nonOriginatingCost = components.Where(c => !c.IsOriginating).Sum(c => c.UnitCostUsd);

        // Compute Regional Value Content (RVC) using Transaction Value or Net Cost method
        decimal rvcPercent = 0.0m;
        if (request.TransactionValueUsd > 0)
        {
            rvcPercent = Math.Clamp(((request.TransactionValueUsd - nonOriginatingCost) / request.TransactionValueUsd) * 100m, 0m, 100m);
        }
        else if (request.NetCostUsd > 0)
        {
            rvcPercent = Math.Clamp(((request.NetCostUsd - nonOriginatingCost) / request.NetCostUsd) * 100m, 0m, 100m);
        }

        // Determine agreement threshold
        decimal requiredRvcThreshold = request.TradeAgreementCode.ToUpperInvariant() switch
        {
            "USMCA" => 60.0m,
            "CPTPP" => 45.0m,
            "EU_UK_TCA" => 50.0m,
            _ => 50.0m
        };

        // Tariff Heading substantial transformation check (CTH)
        string finishedHeading = GetHeading(request.FinishedProductHsCode);
        bool allNonOriginatingSatisfyCth = components
            .Where(c => !c.IsOriginating)
            .All(c => GetHeading(c.HsCode) != finishedHeading);

        bool qualifiesForPreferential = rvcPercent >= requiredRvcThreshold || (components.Count > 0 && allNonOriginatingSatisfyCth && rvcPercent >= 40.0m);

        // Circumvention check: heavy non-originating input (>65% BOM) with identical tariff heading
        bool circumventionRisk = totalBomCost > 0 && (nonOriginatingCost / totalBomCost) > 0.65m && !allNonOriginatingSatisfyCth;

        decimal standardMfnRate = 8.5m; // Standard WTO Most-Favoured-Nation rate
        decimal appliedTariffRate;
        decimal estimatedSavings;
        string justification;

        if (circumventionRisk)
        {
            // Circumvention incurs anti-dumping penalty rate
            appliedTariffRate = standardMfnRate + 25.0m;
            estimatedSavings = 0.0m;
            justification = "CIRCUMVENTION_AUDIT_FAIL: Substantial transformation not met; >65% inputs non-originating sharing identical tariff heading. Subject to 25% Anti-Dumping/Countervailing duty.";
        }
        else if (qualifiesForPreferential)
        {
            appliedTariffRate = 0.0m;
            estimatedSavings = (standardMfnRate / 100m) * request.TransactionValueUsd;
            justification = $"PREFERENTIAL_ORIGIN_GRANTED: Qualifies under {request.TradeAgreementCode} with RVC of {rvcPercent:F1}% (Threshold: {requiredRvcThreshold}%). Applied tariff 0.0%.";
        }
        else
        {
            appliedTariffRate = standardMfnRate;
            estimatedSavings = 0.0m;
            justification = $"STANDARD_MFN_APPLIED: RVC of {rvcPercent:F1}% fails minimum threshold of {requiredRvcThreshold}%. Applied standard MFN tariff rate {standardMfnRate}%.";
        }

        return new TariffAssessmentResult(
            FinishedProductPartNumber: request.FinishedProductPartNumber,
            TradeAgreementCode: request.TradeAgreementCode,
            RegionalValueContentPercent: Math.Round(rvcPercent, 2),
            QualifiesForPreferentialTariff: qualifiesForPreferential && !circumventionRisk,
            AppliedTariffRatePercent: appliedTariffRate,
            StandardMfnTariffRatePercent: standardMfnRate,
            AntiDumpingCircumventionRisk: circumventionRisk,
            EstimatedTariffSavingsUsd: Math.Round(estimatedSavings, 2),
            ComplianceJustification: justification
        );
    }

    private static string GetHeading(string hsCode)
    {
        if (string.IsNullOrWhiteSpace(hsCode)) return string.Empty;
        var clean = hsCode.Replace(".", "").Trim();
        return clean.Length >= 4 ? clean[..4] : clean;
    }
}
