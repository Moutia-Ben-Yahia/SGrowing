using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Finance.PayrollTaxWithholding;

public sealed record PayrollTaxOverview(
    int MonitoredJurisdictionsCount,
    decimal TotalMonthlyWithheldUsd,
    decimal TotalRemittedQuarterToDateUsd,
    int PendingStatutoryFilingsCount,
    IReadOnlyList<TaxJurisdictionProfile> Jurisdictions,
    IReadOnlyList<StatutoryFilingRecord> RecentFilings
);

public sealed record TaxJurisdictionProfile(
    string JurisdictionCode,
    string CountryName,
    string Currency,
    double IncomeTaxRatePct,
    double SocialSecurityRatePct,
    double PensionEmployerRatePct,
    string RegulatoryAuthority,
    string FilingFrequency
);

public sealed record StatutoryFilingRecord(
    string FilingId,
    string JurisdictionCode,
    string TaxPeriod,
    decimal TotalTaxRemitted,
    string Currency,
    string AuthorityConfirmationCode,
    string Status,
    DateTime FiledAt
);

public sealed record CalculateWithholdingRequest(
    string EmployeeId,
    string JurisdictionCode,
    decimal GrossPayAmount,
    string PayFrequency,
    int TaxExemptionsCount
);

public sealed record WithholdingCalculationResult(
    string CalculationId,
    string EmployeeId,
    string JurisdictionCode,
    decimal GrossPay,
    decimal IncomeTaxWithheld,
    decimal SocialSecurityWithheld,
    decimal PensionWithheld,
    decimal NetPay,
    decimal EmployerSocialContribution,
    string Currency,
    DateTime CalculatedAt
);

public sealed record GenerateFilingReceiptRequest(
    string JurisdictionCode,
    string TaxPeriod,
    decimal TotalTaxRemitted,
    string RemittanceBankReference
);

public sealed record SovereignFilingReceipt(
    string ReceiptId,
    string JurisdictionCode,
    string TaxPeriod,
    decimal TotalTaxRemitted,
    string MerkleAuditDigest,
    string StatutoryConfirmationCode,
    string Status,
    DateTime Timestamp
);
