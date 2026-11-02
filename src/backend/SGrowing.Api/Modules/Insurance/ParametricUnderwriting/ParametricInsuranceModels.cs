namespace SGrowing.Api.Modules.Insurance.ParametricUnderwriting;

public enum CatastropheRiskType
{
    EarthquakeRichter,
    TropicalCycloneWindKmh,
    SubZeroFreezeWaveHours,
    ExtremePrecipitationMm
}

public sealed record AssetGeoLocation(
    double Latitude,
    double Longitude
);

public sealed record InsuredAssetPolicy(
    string PolicyId,
    string TenantId,
    string AssetName,
    AssetGeoLocation Location,
    CatastropheRiskType Peril,
    double TriggerThreshold,
    double CatastrophicCeiling,
    decimal MaxSumInsuredEur,
    decimal AnnualPremiumEur,
    bool IsActive = true
);

public sealed record CatastropheOracleEvent(
    string EventId,
    CatastropheRiskType Peril,
    AssetGeoLocation EpicenterLocation,
    double RawIntensityValue,
    string OracleSource,
    string CryptographicSignatureHex,
    DateTime OccurredAtUtc
);

public sealed record ParametricClaimSettlement(
    string ClaimId,
    string PolicyId,
    string TenantId,
    double DistanceToEpicenterKm,
    double EffectiveLocalIntensity,
    double TriggerThreshold,
    bool PayoutTriggered,
    decimal SettlementAmountEur,
    string AdjudicationSummary,
    DateTime SettledAtUtc
);

public sealed record SolvencyAdequacyAuditResult(
    string PoolId,
    decimal TotalCapitalReservesEur,
    decimal TotalSumInsuredExposureEur,
    decimal StressTestPmlEur, // Probable Maximum Loss (1-in-200 year event)
    double SolvencyRatioPercent,
    bool IsSolvencyIiCompliant,
    DateTime AuditedAtUtc
);

public sealed record AdjudicateEventRequest(
    CatastropheOracleEvent OracleEvent,
    IReadOnlyList<InsuredAssetPolicy> ActivePolicies
);

public sealed record AuditSolvencyRequest(
    string PoolId,
    decimal CapitalReservesEur,
    IReadOnlyList<InsuredAssetPolicy> ActivePolicies
);
