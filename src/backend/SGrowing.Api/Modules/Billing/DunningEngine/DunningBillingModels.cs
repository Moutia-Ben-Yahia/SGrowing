namespace SGrowing.Api.Modules.Billing.DunningEngine;

public enum SubscriptionTier
{
    Starter,
    Growth,
    Enterprise,
    Custom
}

public enum InvoiceStatus
{
    Draft,
    Paid,
    PastDue,
    Uncollectible,
    Voided
}

public enum DunningActionType
{
    RetryPayment,
    SendEmailWarning,
    SendSmsWarning,
    DowngradeTier,
    SuspendAccount
}

public enum TaxExemptionReason
{
    None,
    EuViesReverseCharge,
    UsWholesaleResale,
    NonProfit
}

public record TaxCalculationResult(
    decimal Subtotal,
    decimal TaxRatePercent,
    decimal TaxAmount,
    decimal TotalAmount,
    string TaxJurisdiction,
    TaxExemptionReason ExemptionReason
);

public record BillingInvoice(
    string InvoiceId,
    string TenantId,
    string CustomerId,
    decimal Subtotal,
    decimal TaxAmount,
    decimal Total,
    string Currency,
    InvoiceStatus Status,
    int DunningAttempts,
    DateTime DueDateUtc,
    DateTime? PaidAtUtc,
    string DigitalSealHash
);

public record DunningEvent(
    string EventId,
    string InvoiceId,
    DunningActionType Action,
    bool Succeeded,
    string Details,
    DateTime TimestampUtc
);

public record CreateInvoiceRequest(
    string TenantId,
    string CustomerId,
    decimal Subtotal,
    string Currency,
    string CountryCode,
    string? VatNumber = null
);

public record ProcessDunningRetryRequest(
    string TenantId,
    string InvoiceId,
    bool SimulatePaymentSuccess
);

public record DunningRecoverySummary(
    string InvoiceId,
    InvoiceStatus Status,
    int AttemptNumber,
    decimal RecoveredAmount,
    string NextAction
);
