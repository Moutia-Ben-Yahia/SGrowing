namespace SGrowing.Api.Modules.Sales.Proposals;

public enum ProposalStatus
{
    Draft,
    Presented,
    Accepted,
    Signed,
    Declined
}

public enum UsageMetricType
{
    ApiRequests,
    CapiConversions,
    AiTokens,
    ActiveContacts
}

public record QuoteLineItem(
    string Sku,
    string Description,
    int Quantity,
    decimal UnitPrice,
    decimal DiscountPercent,
    decimal TotalPrice,
    decimal CostPrice
);

public record CalculateQuoteRequest(
    string TenantId,
    string CustomerCompany,
    List<QuoteLineItem> Items,
    decimal CustomDiscountPercent = 0
);

public record QuoteCalculationResult(
    decimal Subtotal,
    decimal DiscountTotal,
    decimal TaxAmount,
    decimal TotalAmount,
    decimal TotalCost,
    decimal MarginPercent,
    bool MarginApproved,
    string MarginWarning
);

public record B2BProposal(
    Guid Id,
    string TenantId,
    string CustomerCompany,
    string CustomerEmail,
    List<QuoteLineItem> LineItems,
    decimal TotalAmount,
    decimal MarginPercent,
    ProposalStatus Status,
    string? SignedPdfHash,
    string? SignerName,
    DateTime? SignedAtUtc,
    DateTime CreatedAtUtc
);

public record SignProposalRequest(
    Guid ProposalId,
    string TenantId,
    string SignerName,
    string SignerEmail,
    string SignerIp
);

public record CreateProposalRequest(
    string TenantId,
    string CustomerCompany,
    string CustomerEmail,
    List<QuoteLineItem> Items,
    decimal DiscountPercent = 0
);

public record RecordUsageRequest(
    string TenantId,
    UsageMetricType MetricType,
    long Quantity
);

public record UsageInvoiceSummary(
    string TenantId,
    DateTime PeriodStartUtc,
    DateTime PeriodEndUtc,
    Dictionary<UsageMetricType, long> ConsumedUnits,
    Dictionary<UsageMetricType, decimal> OverageCharges,
    decimal TotalOverageAmount
);
