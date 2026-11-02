namespace SGrowing.Api.Modules.Contracts.ChurnRescue;

public record AccountHealthSignal(
    string AccountId,
    string AccountName,
    decimal AnnualRecurringRevenue,
    DateTime ContractExpiresAtUtc,
    double UsageDropPercentage,
    int LoginFrequencyScore,
    double SupportTicketSentiment,
    bool ChampionTurnoverFlag
);

public record ChurnRiskAssessment(
    string AccountId,
    string AccountName,
    decimal AnnualRecurringRevenue,
    int ChurnPropensityScore,
    string RiskLevel,
    IReadOnlyList<string> PrimaryRiskDrivers,
    int DaysUntilRenewal,
    string RecommendedPlaybook
);

public record TriggerPlaybookRequest(
    string AccountId,
    string PlaybookType,
    int? CustomDiscountPercent = null,
    string? Notes = null
);

public record PlaybookExecutionResult(
    string PlaybookId,
    string AccountId,
    string PlaybookType,
    string ActionSummary,
    string EscrowAgreementUrl,
    string Status,
    DateTime ExecutedAtUtc
);

public record GenerateRenewalAgreementRequest(
    string AccountId,
    decimal AnnualContractValue,
    int DiscountPercent,
    int LockYears
);

public record RenewalAgreement(
    string AgreementId,
    string AccountId,
    decimal AnnualContractValue,
    int DiscountAppliedPercent,
    DateTime NewContractEndDateUtc,
    int PriceLockYears,
    string SignatureStatus,
    DateTime CreatedAtUtc,
    DateTime? SignedAtUtc = null
);

public record RescueMetricsSummary(
    decimal TotalArrAtRisk,
    decimal RescuedArrAmount,
    int HighRiskAccountsCount,
    double NetRevenueRetentionRate,
    int RescuedContractsCount
);
