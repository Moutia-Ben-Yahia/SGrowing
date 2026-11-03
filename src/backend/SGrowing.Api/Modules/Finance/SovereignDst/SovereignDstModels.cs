namespace SGrowing.Api.Modules.Finance.SovereignDst;

public record LocalDstLiabilityRecord(
    string DstRecordId,
    string JurisdictionCode, // UK_DST_2PCT, FR_DST_3PCT, IT_DST_3PCT, ES_DST_3PCT
    decimal LocalDigitalTurnoverEur,
    decimal StatutoryDstRatePct,
    decimal CalculatedTaxLiabilityEur,
    bool GlobalThresholdMet, // €750M global
    bool LocalThresholdMet,  // €25M local
    string Status // ASSESSED, CLEARED, ACCRUED
);

public record OecdGlobePillar2Assessment(
    string AssessmentId,
    string FiscalYear,
    string ConstituentEntityJurisdiction, // IE_DUBLIN, SG_SINGAPORE, CH_ZURICH, LU_LUXEMBOURG
    decimal FinancialNetIncomeEur,
    decimal CoveredTaxesPaidEur,
    double EffectiveTaxRatePct, // e.g. 11.2%
    double StatutoryGloBeFloorPct, // 15.0%
    decimal TopUpTaxLiabilityEur, // e.g. 3.8% * NetIncome
    string ClearanceTokenHash
);

public record SovereignDstOverview(
    int MonitoredTaxJurisdictionsCount,
    decimal TotalDstAccruedEur,
    decimal TotalPillar2TopUpTaxEur,
    double GlobalEffectiveTaxRatePct,
    List<LocalDstLiabilityRecord> DstRecords,
    List<OecdGlobePillar2Assessment> GlobeAssessments
);

public record ComputeDstRequest(
    string JurisdictionCode,
    decimal LocalDigitalTurnoverEur,
    decimal StatutoryDstRatePct
);

public record ComputeGlobeTaxRequest(
    string FiscalYear,
    string Jurisdiction,
    decimal FinancialNetIncomeEur,
    decimal CoveredTaxesPaidEur
);
