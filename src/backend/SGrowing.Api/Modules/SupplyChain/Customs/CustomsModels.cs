using System.Text.Json.Serialization;

namespace SGrowing.Api.Modules.SupplyChain.Customs;

#region Enums

/// <summary>
/// International customs declaration document types.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CustomsDeclarationType
{
    /// <summary>
    /// Document Administratif Unique (DAU) / Single Administrative Document (SAD) for commercial clearance.
    /// </summary>
    SingleAdministrativeDocument = 0,

    /// <summary>
    /// EUR.1 Movement Certificate for preferential tariff treatment between EU and trade partners (e.g. Algeria).
    /// </summary>
    Eur1CertificateOfOrigin = 1,

    /// <summary>
    /// ATA Carnet for temporary duty-free admission of commercial samples, exhibition goods, and professional gear.
    /// </summary>
    AtaCarnet = 2
}

/// <summary>
/// Lifecycle state of a digital customs declaration.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DeclarationStatus
{
    Draft = 0,
    Submitted = 1,
    Approved = 2,
    Verified = 3,
    Revoked = 4,
    Expired = 5
}

#endregion

#region Classification Models

/// <summary>
/// Request to classify a raw product description into Harmonized System (HS) codes and national tariff lines.
/// </summary>
public record ProductClassificationRequest(
    string Title,
    string? Category = null,
    string? Material = null,
    string? Description = null,
    string? DestinationCountry = null);

/// <summary>
/// National tariff line entry extending a 6-digit WCO HS code with national subheadings and duty/tax rates.
/// </summary>
public record NationalTariffLine(
    string CountryCode,
    string TariffCode,
    decimal MfnDutyRate,
    decimal VatRate,
    decimal? DapsSurchargeRate = null,
    string? Notes = null);

/// <summary>
/// Complete reference details for a 6-digit WCO Harmonized System code.
/// </summary>
public record HsCodeDetails(
    string HsCode,
    string Heading,
    string Description,
    string Category,
    IReadOnlyList<string> TypicalMaterials,
    IReadOnlyList<NationalTariffLine> NationalTariffLines,
    decimal StandardMfnDutyRate,
    decimal StandardVatRate,
    decimal StandardExciseRate = 0m,
    bool IsRestrictedOrDualUse = false,
    string? RestrictedNotes = null);

/// <summary>
/// Result of classifying a product into a 6-digit HS code and applicable national tariff lines.
/// </summary>
public record ProductClassificationResult(
    string HsCode,
    string Heading,
    string Description,
    string Category,
    double ConfidenceScore,
    bool RequiresBrokerReview,
    string? ReviewReason,
    IReadOnlyList<NationalTariffLine> NationalTariffLines,
    IReadOnlyList<string> MatchedKeywords,
    DateTime ClassifiedAtUtc);

/// <summary>
/// Suggestion candidate when searching or auto-completing HS codes.
/// </summary>
public record HsCandidateSuggestion(
    string HsCode,
    string Description,
    string Category,
    double RelevanceScore);

#endregion

#region Tariff & Landed Cost Models

/// <summary>
/// Request to compute the full landed customs tariff for an import shipment.
/// </summary>
public record TariffCalculationRequest(
    decimal FobPrice,
    decimal InternationalFreight,
    decimal MarineInsurance,
    string DestinationCountry,
    string OriginCountry,
    string HsCode,
    bool HasCertificateOfOrigin = false,
    bool IsCommercial = true,
    decimal? CustomDutyRate = null,
    decimal? CustomVatRate = null,
    decimal? CustomExciseRate = null);

/// <summary>
/// Evaluation of De Minimis statutory duty/tax exemption thresholds.
/// </summary>
public record DeMinimisEvaluationResult(
    bool IsExemptFromDuty,
    bool IsExemptFromVat,
    decimal StatutoryThresholdUsd,
    decimal CifValueUsd,
    string DestinationCountry,
    string LegalBasis);

/// <summary>
/// Evaluation of Preferential Trade Agreement (PTA) eligibility for duty reduction.
/// </summary>
public record PtaEvaluationResult(
    bool IsPtaApplicable,
    string AgreementName,
    decimal StandardMfnDutyRate,
    decimal PreferentialDutyRate,
    decimal DutyDiscountPercentage,
    bool CertificateOfOriginVerified,
    string CertificateType,
    string EligibilityReason);

/// <summary>
/// Complete landed cost breakdown including CIF, duties, surcharges, excise, VAT, and administrative fees.
/// </summary>
public record TariffCalculationResult(
    decimal FobPrice,
    decimal InternationalFreight,
    decimal MarineInsurance,
    decimal CifValue,
    decimal DutyAmount,
    decimal EffectiveDutyRate,
    decimal DapsAmount,
    decimal DapsRate,
    decimal ExciseAmount,
    decimal ExciseRate,
    decimal VatAmount,
    decimal VatRate,
    decimal AdministrativeFee,
    decimal StatisticalFee,
    decimal CustomsFeeTotal,
    decimal TotalLandedCost,
    string Currency,
    bool DeMinimisApplied,
    PtaEvaluationResult PtaEvaluation,
    DateTime CalculatedAtUtc);

#endregion

#region Digital Customs Declaration Models

/// <summary>
/// Commercial line item included in a customs declaration.
/// </summary>
public record DeclarationItem(
    string ItemId,
    string Description,
    string HsCode,
    int Quantity,
    string UnitOfMeasure,
    decimal UnitPriceFob,
    decimal TotalFob,
    decimal CifValue,
    string CountryOfOrigin,
    decimal GrossWeightKg,
    decimal NetWeightKg);

/// <summary>
/// Declarant or authorized customs broker entity.
/// </summary>
public record DeclarantParty(
    string PartyId,
    string Name,
    string TaxIdOrEori,
    string Address,
    string Country,
    string ContactEmail,
    string? BrokerLicenseNumber = null);

/// <summary>
/// Consignee or importing entity receiving the goods.
/// </summary>
public record ConsigneeParty(
    string PartyId,
    string Name,
    string TaxIdOrEori,
    string Address,
    string Country);

/// <summary>
/// Request to issue a signed digital customs declaration document.
/// </summary>
public record CreateDeclarationRequest(
    CustomsDeclarationType Type,
    DeclarantParty Declarant,
    ConsigneeParty Consignee,
    string OriginCountry,
    string DestinationCountry,
    IReadOnlyList<DeclarationItem> Items,
    decimal Freight,
    decimal Insurance,
    string Currency = "USD",
    string? SecretSigningKey = null,
    int ValidityDays = 30,
    string? TransportMode = "Maritime",
    string? BillOfLadingOrAirwayBill = null);

/// <summary>
/// Authenticated digital customs declaration document with cryptographic signatures and QR verification payload.
/// </summary>
public record DigitalDeclaration(
    string DeclarationId,
    CustomsDeclarationType Type,
    DeclarationStatus Status,
    DeclarantParty Declarant,
    ConsigneeParty Consignee,
    string OriginCountry,
    string DestinationCountry,
    IReadOnlyList<DeclarationItem> Items,
    decimal TotalFob,
    decimal TotalCif,
    decimal TotalDuty,
    decimal TotalVat,
    decimal TotalExcise,
    decimal TotalCustomsFees,
    decimal TotalLandedCost,
    string Currency,
    string TransportMode,
    string? BillOfLading,
    string ItemsHashSha256,
    string HmacSignature,
    string QrPayload,
    DateTime IssuedAtUtc,
    DateTime ExpiresAtUtc,
    DateTime? RevokedAtUtc = null,
    string? RevocationReason = null,
    IReadOnlyDictionary<string, string>? Metadata = null);

/// <summary>
/// Border verification result when scanning a digital declaration QR payload.
/// </summary>
public record BorderVerificationResult(
    bool IsValid,
    string DeclarationId,
    CustomsDeclarationType? Type,
    string? DeclarantName,
    string? DestinationCountry,
    decimal? LandedValue,
    string? Currency,
    DateTime? IssuedAtUtc,
    DateTime? ExpiresAtUtc,
    bool IsExpired,
    bool IsRevoked,
    string? RevocationReason,
    string VerificationMessage);

/// <summary>
/// Result of verifying a declaration's digital signature in the system.
/// </summary>
public record DeclarationVerificationResult(
    bool IsValid,
    string DeclarationId,
    string Status,
    string Message);

/// <summary>
/// Request to verify a scanned border QR payload against a customs secret key.
/// </summary>
public record VerifyDeclarationQrRequest(
    string QrPayload,
    string SecretKey);

/// <summary>
/// Request to revoke an existing customs declaration.
/// </summary>
public record RevokeDeclarationRequest(
    string Reason);

#endregion
