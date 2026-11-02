namespace SGrowing.Api.Modules.Tenancy.DataSovereignty;

public enum SovereignJurisdiction
{
    AlgeriaLaw1807, // National Personal Data Protection Authority (ANPDP)
    EuGdpr,         // European General Data Protection Regulation
    SaudiPdpl,      // Saudi Personal Data Protection Law
    UaeFederal45,   // UAE Federal Decree-Law No. 45
    UnrestrictedGlobal
}

public enum DataClassificationLevel
{
    Public,
    InternalBusiness,
    ConfidentialPii,
    RestrictedSovereignState
}

public sealed record GeodeticBoundaryVertex(double Latitude, double Longitude);

public sealed record SovereignDataZone(
    string ZoneId,
    SovereignJurisdiction Jurisdiction,
    string Name,
    string PrimaryDataCenterRegion,
    IReadOnlyList<GeodeticBoundaryVertex> BoundaryPolygon,
    bool RequiresProofOfResidency,
    bool AllowCrossBorderTransferWithAdequacy
);

public sealed record DataResidencyVerificationRequest(
    string TenantId,
    SovereignJurisdiction Jurisdiction,
    DataClassificationLevel Classification,
    double NodeLatitude,
    double NodeLongitude,
    string StorageRegionId
);

public sealed record DataResidencyAttestation(
    string AttestationId,
    string TenantId,
    SovereignJurisdiction Jurisdiction,
    bool IsCompliant,
    bool BoundaryPolygonRespected,
    string ComplianceRationale,
    string CryptographicProofOfResidencyHex,
    DateTime AttestedAtUtc
);

public sealed record CrossBorderTransferAuditResult(
    string TransferId,
    string TenantId,
    SovereignJurisdiction SourceJurisdiction,
    SovereignJurisdiction TargetJurisdiction,
    DataClassificationLevel Classification,
    bool TransferPermitted,
    string EnforcementAction,
    DateTime AuditedAtUtc
);
