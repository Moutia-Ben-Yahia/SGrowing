namespace SGrowing.Api.Modules.Trade.RulesOfOriginTariff;

public record BomMaterialComponent(
    string ComponentPartNumber,
    string Description,
    string HsCode, // 6-digit Harmonized System code, e.g. "8542.31"
    string CountryOfOrigin,
    decimal UnitCostUsd,
    bool IsOriginating
);

public record RulesOfOriginEvaluationRequest(
    string FinishedProductPartNumber,
    string FinishedProductHsCode, // 6-digit HS code, e.g. "8504.40"
    string ExportingCountry,
    string DestinationCountry,
    string TradeAgreementCode, // "USMCA", "CPTPP", "EU_UK_TCA", "MFN_DEFAULT"
    decimal TransactionValueUsd,
    decimal NetCostUsd,
    IReadOnlyList<BomMaterialComponent> Components
);

public record TariffAssessmentResult(
    string FinishedProductPartNumber,
    string TradeAgreementCode,
    decimal RegionalValueContentPercent,
    bool QualifiesForPreferentialTariff,
    decimal AppliedTariffRatePercent,
    decimal StandardMfnTariffRatePercent,
    bool AntiDumpingCircumventionRisk,
    decimal EstimatedTariffSavingsUsd,
    string ComplianceJustification
);
