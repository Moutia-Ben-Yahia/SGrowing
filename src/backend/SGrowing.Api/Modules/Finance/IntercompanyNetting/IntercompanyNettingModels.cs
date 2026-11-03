namespace SGrowing.Api.Modules.Finance.IntercompanyNetting;

public enum SettlementStatus
{
    PendingInvoiced,
    MatrixNetted,
    ClearedSettled
}

public sealed record IntercompanyInvoice(
    string InvoiceId,
    string DebtorSubsidiaryId,
    string DebtorSubsidiaryName,
    string CreditorSubsidiaryId,
    string CreditorSubsidiaryName,
    double InvoiceAmountUsd,
    string CurrencyIso,
    string TransferPricingCategory,
    SettlementStatus Status
);

public sealed record NettingParticipantSummary(
    string SubsidiaryId,
    string SubsidiaryName,
    double GrossPayableUsd,
    double GrossReceivableUsd,
    double NetPositionUsd // Positive = Net Creditor, Negative = Net Debtor
);

public sealed record NettingSettlementReceipt(
    string ReceiptId,
    int NettingCycleNumber,
    double TotalGrossVolumeUsd,
    double TotalNetVolumeSettledUsd,
    double SettlementEfficiencyPercentage, // Typically 75-85% reduction
    DateTime SettledAtUtc,
    string CryptographicAuditHashSha256
);

public sealed record IntercompanyNettingOverview(
    int TotalSubsidiariesParticipating,
    int PendingInvoicesCount,
    double TotalGrossInvoicedVolumeUsd,
    double ProjectedCashSettlementRequiredUsd,
    double HistoricalEfficiencyPercentage,
    IReadOnlyList<IntercompanyInvoice> RecentInvoices,
    IReadOnlyList<NettingParticipantSummary> ParticipantSummaries
);
