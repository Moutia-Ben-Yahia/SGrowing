namespace SGrowing.Api.Modules.Finance.AccountsPayableThreeWayMatching;

public record PurchaseOrderLine(
    string LineId,
    string ItemSku,
    string Description,
    double OrderedQuantity,
    decimal UnitPrice,
    decimal TolerancePercentage = 2.0m, // e.g. 2% price variance tolerated
    decimal MaxPriceToleranceAmount = 10.0m
);

public record GoodsReceiptLine(
    string LineId,
    string ItemSku,
    double ReceivedQuantity,
    double AcceptedQuantity,
    double RejectedQuantity,
    DateTime ReceivedDateUtc
);

public record VendorInvoiceLine(
    string LineId,
    string ItemSku,
    double InvoicedQuantity,
    decimal InvoicedUnitPrice,
    decimal LineTotal,
    decimal TaxAmount = 0.0m
);

public record MatchDiscrepancy(
    string ItemSku,
    string DiscrepancyType, // "PriceVariance", "QuantityVariance", "MissingGoodsReceipt", "OverBilledQuantity"
    decimal VarianceAmount,
    double VariancePercentage,
    string Description,
    bool IsAutoResolvable
);

public record ThreeWayMatchRequest(
    string TenantId,
    string VendorId,
    string InvoiceNumber,
    string PurchaseOrderNumber,
    string Currency,
    List<PurchaseOrderLine> PoLines,
    List<GoodsReceiptLine> GrnLines,
    List<VendorInvoiceLine> InvoiceLines,
    decimal AutoApprovalMaxDiscrepancy = 5.0m
);

public record ThreeWayMatchResult(
    string MatchId,
    string TenantId,
    string InvoiceNumber,
    string PurchaseOrderNumber,
    string MatchStatus, // "PerfectMatch", "WithinTolerance", "UnderReview", "DiscrepancyBlocked"
    decimal TotalPoAmount,
    decimal TotalGrnAmount,
    decimal TotalInvoiceAmount,
    decimal TotalVarianceAmount,
    List<MatchDiscrepancy> Discrepancies,
    string RecommendedAction, // "ApproveForPayment", "HoldPaymentRequireApproval", "RejectInvoice", "IssueVendorDebitMemo"
    bool IsAutoApproved,
    DateTime ReconciledAtUtc
);
