namespace SGrowing.Api.Modules.Finance.CreditRiskUnderwriting;

public enum SolvencyZone
{
    SafeZone,
    GreyZone,
    DistressZone
}

public sealed record CorporateFinancialStatement(
    string CompanyTaxId,
    string LegalEntityName,
    decimal WorkingCapitalEur,
    decimal RetainedEarningsEur,
    decimal EbitOperatingIncomeEur,
    decimal TotalAssetsEur,
    decimal BookValueOfEquityEur,
    decimal TotalLiabilitiesEur,
    int HistoricalDsoDays = 45
);

public sealed record InvoiceFactoringApplication(
    string InvoiceId,
    string CreditorTenantId,
    string DebtorTaxId,
    decimal InvoiceFaceValueEur,
    DateTime IssueDateUtc,
    DateTime DueDateUtc,
    CorporateFinancialStatement DebtorFinancials
);

public sealed record CreditUnderwritingAssessment(
    string AssessmentId,
    string InvoiceId,
    double AltmanZScore,
    SolvencyZone Zone,
    double EstimatedProbabilityOfDefault,
    bool IsApproved,
    double ApprovedAdvanceRatePercent,
    decimal AdvanceAmountEur,
    decimal DiscountFeeEur,
    decimal NetDisbursedCashEur,
    double EffectiveAnnualizedAprPercent,
    string UnderwritingNotes,
    DateTime AssessedAtUtc
);
