namespace SGrowing.Api.Modules.SupplyChain.EdiReplenishment;

public record InventorySkuStock(
    string SkuId,
    string SkuName,
    string WarehouseId,
    int OnHandQty,
    int AllocatedQty,
    double DailyVelocity,
    int LeadTimeDays,
    int SafetyStock,
    int ReorderPoint,
    int EconomicOrderQty,
    decimal UnitCost,
    string StockStatus
);

public record SupplierProfile(
    string SupplierId,
    string SupplierName,
    string EdiPartnerId,
    string ContactEmail,
    int TypicalLeadTimeDays,
    double OnTimeDeliveryRate,
    double FillRatePercent,
    decimal MinOrderValue
);

public record ReplenishmentRecommendation(
    string SkuId,
    string SkuName,
    string WarehouseId,
    int CurrentAvailable,
    int ReorderPoint,
    int SuggestedOrderQty,
    decimal EstimatedCost,
    string RecommendedSupplierId,
    string Severity,
    int EstimatedDaysUntilStockout
);

public record EdiLineItem(
    int LineNumber,
    string SkuId,
    string Description,
    int Quantity,
    decimal UnitPrice,
    decimal ExtendedAmount
);

public record EdiPurchaseOrder(
    string PoNumber,
    string SupplierId,
    string WarehouseId,
    IReadOnlyList<EdiLineItem> LineItems,
    decimal TotalAmount,
    string Edi850RawPayload,
    string Status,
    DateTime CreatedAtUtc,
    DateTime? DispatchedAtUtc = null
);

public record GeneratePoRequest(
    string SupplierId,
    string WarehouseId,
    IReadOnlyList<string> SkuIds
);

public record SupplierScorecard(
    string SupplierId,
    string SupplierName,
    int TotalOrdersFulfilled,
    double OnTimeDeliveryPercent,
    double FillRatePercent,
    double QualityScoreOutOf100,
    string ReliabilityTier
);
