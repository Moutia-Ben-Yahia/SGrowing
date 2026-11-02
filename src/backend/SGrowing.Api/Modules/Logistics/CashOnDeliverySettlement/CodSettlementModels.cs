namespace SGrowing.Api.Modules.Logistics.CashOnDeliverySettlement;

public record CodShipmentOrder(
    string TrackingNumber,
    string OrderId,
    string CustomerName,
    decimal CodAmountDue,
    string CarrierId,
    string DeliveryStatus, // "DeliveredCollected", "DeliveryFailed", "ReturnedToOrigin", "CustomerRefused"
    decimal CollectedCashAmount,
    DateTime? DeliveredAtUtc = null
);

public record CarrierRemittanceDeposit(
    string RemittanceReference,
    string CarrierId,
    decimal TotalTransferredAmount,
    DateTime BankDepositDateUtc,
    string BankTransactionId
);

public record CodOrderDiscrepancy(
    string TrackingNumber,
    string OrderId,
    decimal ExpectedCodAmount,
    decimal CarrierReportedAmount,
    decimal VarianceAmount,
    string DiscrepancyType // "Shortfall", "OverCollection", "UnremittedDeliveredOrder", "UnexpectedReturnFee"
);

public record CodSettlementRequest(
    string TenantId,
    string CarrierId,
    DateTime SettlementBatchDateUtc,
    CarrierRemittanceDeposit Remittance,
    List<CodShipmentOrder> ManifestOrders,
    decimal CarrierShippingCommissionPerOrder = 5.0m,
    decimal ReturnToOriginFeePerOrder = 2.5m
);

public record CodSettlementResult(
    string SettlementId,
    string TenantId,
    string CarrierId,
    string RemittanceReference,
    DateTime ReconciledAtUtc,
    int TotalShipmentsProcessed,
    int DeliveredOrdersCount,
    int ReturnedToOriginCount,
    decimal TotalCodDue,
    decimal TotalCashCollected,
    decimal TotalCarrierDeliveryFees,
    decimal TotalCarrierRtoFees,
    decimal NetPayableToMerchant,
    decimal ActualRemittanceReceived,
    decimal SettlementVariance,
    List<CodOrderDiscrepancy> Discrepancies,
    bool IsBalanced,
    string SettlementStatus // "BalancedAndSettled", "CarrierUnderRemitted", "CarrierOverRemitted", "RequiresInvestigation"
);
