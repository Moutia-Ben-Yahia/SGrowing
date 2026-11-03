namespace SGrowing.Api.Modules.Finance.TaxExemption;

public record TaxExemptionCertificate(
    string CertificateId,
    string CounterpartyName,
    string JurisdictionCode, // EU_DE, EU_FR, UK_GB, US_NY, US_CA, SG_SG
    string TaxIdentificationNumber, // e.g. DE123456789, GB987654321
    string ExemptionType, // REVERSE_CHARGE_B2B, RESELLER_CERTIFICATE, DIPLOMATIC_EXEMPT, CHARITY_NON_PROFIT
    string VerificationRegistry, // EU_VIES, UK_HMRC, US_SST_GOV
    string CryptographicProofHash,
    DateTime ValidFrom,
    DateTime ValidUntil,
    string Status // VALID, EXPIRED, REVOKED, PENDING_REVIEW
);

public record VatClearanceInvoiceRecord(
    string InvoiceClearanceId,
    string CertificateId,
    string InvoiceReferenceNumber,
    decimal NetAmountUsd,
    decimal AppliedVatRatePct, // 0.0% for reverse charge
    decimal CalculatedTaxUsd,
    string MandatoryStatutoryNotice, // e.g. "VAT Reverse Charge: Article 196 EU Directive 2006/112/EC"
    DateTime IssuedAt
);

public record TaxExemptionOverview(
    int TotalValidCertificatesCount,
    int ReverseChargeClearancesCount,
    decimal TotalTaxShieldedUsd,
    double AuditComplianceScorePct, // e.g. 99.4%
    List<TaxExemptionCertificate> ActiveCertificates,
    List<VatClearanceInvoiceRecord> RecentClearances
);

public record ValidateCertificateRequest(
    string CounterpartyName,
    string JurisdictionCode,
    string TaxIdentificationNumber,
    string ExemptionType,
    int ValidMonths
);

public record IssueVatClearanceRequest(
    string CertificateId,
    string InvoiceReferenceNumber,
    decimal NetAmountUsd
);
