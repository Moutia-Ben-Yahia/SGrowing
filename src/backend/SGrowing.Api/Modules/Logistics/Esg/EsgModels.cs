namespace SGrowing.Api.Modules.Logistics.Esg;

/// <summary>
/// GHG Protocol corporate standard emissions scopes.
/// </summary>
public enum EsgScope
{
    Scope1DirectFleet = 1,
    Scope2ElectricityColdStorage = 2,
    Scope3FreightAndPackaging = 3
}

/// <summary>
/// Logistics transport modality with standard carbon intensity factors.
/// </summary>
public enum TransportMode
{
    RoadDieselVan = 1,
    RoadElectricVehicle = 2,
    AirFreight = 3,
    MaritimeContainer = 4,
    RailFreight = 5
}

/// <summary>
/// Packaging material types and associated life-cycle carbon factors.
/// </summary>
public enum PackagingType
{
    StandardPoly = 1,
    CardboardRecycled = 2,
    BiodegradablePoly = 3
}

/// <summary>
/// High-integrity certified carbon offset project types.
/// </summary>
public enum OffsetProjectType
{
    AlgerianHighPlateauSolar = 1,
    SaharanAfforestation = 2,
    MediterraneanReforestation = 3,
    GoldStandardWind = 4
}

/// <summary>
/// CSRD and GRI ESG audit certification status.
/// </summary>
public enum AuditCertificationStatus
{
    SelfReported = 1,
    VerifiedAudited = 2,
    TamperDetected = 3
}

/// <summary>
/// Certified carbon footprint calculation for an individual consignment or shipment.
/// </summary>
public record ShipmentCarbonFootprint(
    string ShipmentId,
    double ConsignmentWeightKg,
    double DistanceKm,
    TransportMode Mode,
    PackagingType Packaging,
    double Scope1Co2eKg,
    double Scope2Co2eKg,
    double Scope3Co2eKg,
    double TotalCo2eKg,
    DateTime CalculationTimestampUtc,
    string TenantId = "",
    double PackagingWeightKg = 0,
    double ColdStorageDays = 0,
    double ColdStorageVolumeM3 = 0,
    bool IsDirectCompanyFleet = false);

/// <summary>
/// Non-fungible cryptographic carbon offset retirement certificate.
/// </summary>
public record CarbonOffsetCertificate(
    string CertificateId,
    string TenantId,
    OffsetProjectType ProjectType,
    double TotalKgOffset,
    decimal CostDzd,
    string RetirementTransactionId,
    string CryptographicProofHash,
    DateTime IssuedAtUtc);

/// <summary>
/// Annual ESG carbon accounting balance sheet with Merkle cryptographic root.
/// </summary>
public record EsgBalanceSheet(
    string TenantId,
    int ReportingYear,
    double TotalScope1Kg,
    double TotalScope2Kg,
    double TotalScope3Kg,
    double TotalGrossCo2eKg,
    double TotalOffsetsKg,
    double NetEmissionsCo2eKg,
    bool CarbonNeutralityAchieved,
    string MerkleAuditRoot);

/// <summary>
/// Request payload to calculate Scope-1, Scope-2, and Scope-3 emissions for a shipment.
/// </summary>
public record CalculateCarbonRequest(
    string ShipmentId,
    string TenantId,
    double ConsignmentWeightKg,
    double DistanceKm,
    TransportMode Mode,
    PackagingType Packaging,
    double PackagingWeightKg = 0,
    double ColdStorageDays = 0,
    double ColdStorageVolumeM3 = 0,
    bool? IsDirectCompanyFleet = null);

/// <summary>
/// Request payload to permanently retire carbon offset credits.
/// </summary>
public record PurchaseOffsetRequest(
    string TenantId,
    double KgToOffset,
    OffsetProjectType ProjectType,
    string AuthoritySecretKey);

/// <summary>
/// Request payload to generate an annual audited ESG balance sheet.
/// </summary>
public record AuditReportRequest(
    string TenantId,
    int Year,
    string AuthoritySecretKey);

/// <summary>
/// Request payload to verify cryptographic proof of a carbon offset certificate.
/// </summary>
public record VerifyAuditRequest(
    string CertificateId,
    string SecretKey);

/// <summary>
/// Response payload for certificate cryptographic verification.
/// </summary>
public record AuditVerificationResponse(
    string CertificateId,
    AuditCertificationStatus Status,
    bool IsValid,
    string Details,
    DateTime VerifiedAtUtc);

/// <summary>
/// Comprehensive summary response for tenant ESG accounting.
/// </summary>
public record EsgSummaryResponse(
    string TenantId,
    int ReportingYear,
    double TotalScope1Kg,
    double TotalScope2Kg,
    double TotalScope3Kg,
    double TotalGrossCo2eKg,
    double TotalOffsetsKg,
    double NetEmissionsCo2eKg,
    bool CarbonNeutralityAchieved,
    string MerkleAuditRoot,
    AuditCertificationStatus AuditStatus,
    int TotalShipmentsRecorded,
    int TotalCertificatesIssued,
    DateTime GeneratedAtUtc);

/// <summary>
/// Immutable cryptographic event record stored in the Merkle audit ledger.
/// </summary>
public record EsgEventRecord(
    int SequenceIndex,
    string EventId,
    string TenantId,
    string EventType,
    double Co2eDeltaKg,
    string MetadataHash,
    string PreviousHash,
    string CurrentHash,
    DateTime TimestampUtc,
    int ReportingYear = 0);
