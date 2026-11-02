namespace SGrowing.Api.Modules.SupplyChain.HsClassification;

public enum PreferentialTradeAgreement
{
    NoneWtoMfn,
    AfCfta, // African Continental Free Trade Area
    Gafta,  // Greater Arab Free Trade Area
    EuAlgeriaAssociation
}

public sealed record HsTariffEntry(
    string HsCode, // 6-digit standard, e.g. "8471.30"
    string Chapter, // "84"
    string Heading, // "8471"
    string Subheading, // "30"
    string Description,
    IReadOnlyList<string> Keywords,
    decimal StandardMfnDutyRatePercent,
    decimal VatRatePercent,
    decimal ExciseTaxRatePercent = 0.0m,
    bool RequiresPhytosanitaryPermit = false,
    bool RequiresHalalCertificate = false
);

public sealed record HsClassificationCandidate(
    string HsCode,
    string Description,
    double ConfidenceScore,
    decimal StandardDutyRatePercent,
    decimal VatRatePercent
);

public sealed record LandedCostCalculationRequest(
    string ProductDescription,
    string HsCode,
    decimal FobValueEur,
    decimal InternationalFreightEur,
    decimal MarineInsuranceEur,
    string OriginCountryIso,
    string DestinationCountryIso,
    PreferentialTradeAgreement TradeAgreement,
    bool HasCertifiedProofOfOrigin
);

public sealed record LandedCostBreakdown(
    string CalculationId,
    string HsCode,
    decimal FobValueEur,
    decimal FreightEur,
    decimal InsuranceEur,
    decimal CifValueEur,
    decimal ApplicableDutyRatePercent,
    decimal CustomsDutyEur,
    decimal ExciseTaxEur,
    decimal VatAmountEur,
    decimal TotalCustomsAndTaxesEur,
    decimal TotalLandedCostEur,
    PreferentialTradeAgreement AppliedTradeAgreement,
    string TariffDutyNotes,
    DateTime CalculatedAtUtc
);

public sealed record SingleAdministrativeDocument(
    string DeclarationId,
    string ImporterTaxId,
    string ExporterName,
    string CountryOfExport,
    string CountryOfDestination,
    string CustomsOfficeCode,
    IReadOnlyList<SadLineItem> Items,
    decimal TotalStatisticalValueEur,
    decimal TotalPayableDutyAndTaxesEur,
    string DigitalCustomsSealHex,
    DateTime FiledAtUtc
);

public sealed record SadLineItem(
    int ItemNumber,
    string HsCode,
    string CommercialDescription,
    double NetMassKg,
    decimal ItemInvoiceValueEur,
    decimal CalculatedDutyEur,
    decimal CalculatedVatEur
);
