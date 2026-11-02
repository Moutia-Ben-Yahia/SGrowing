namespace SGrowing.Api.Modules.SupplyChain.WarehouseBinCycleCounting;

public record WarehouseBin(
    string BinId,
    string WarehouseId,
    string Zone,
    string Aisle,
    string Rack,
    string Shelf,
    string BinCode,
    double MaxVolumeCubicMeters,
    double MaxWeightKg,
    bool IsPickable = true
);

public record BinStockItem(
    string Sku,
    string BinCode,
    string LotNumber,
    DateTime? ExpirationDateUtc,
    double BookQuantity,
    decimal UnitCost,
    string AbcCategory = "B" // "A" (Top 80% value), "B" (15% value), "C" (5% value)
);

public record CycleCountEntry(
    string Sku,
    string BinCode,
    string LotNumber,
    double CountedQuantity,
    string AuditorId,
    DateTime CountedAtUtc,
    string? Notes = null
);

public record CycleCountAdjustment(
    string Sku,
    string BinCode,
    double BookQuantity,
    double PhysicalQuantity,
    double VarianceQuantity,
    decimal VarianceAmount,
    string AdjustmentReason, // "Shrinkage", "Overage", "MisplacedFound", "DamagedScrapped"
    bool RequiresSupervisorSignoff
);

public record CycleCountAuditRequest(
    string TenantId,
    string WarehouseId,
    string AuditSessionName,
    List<WarehouseBin> Bins,
    List<BinStockItem> BookStock,
    List<CycleCountEntry> PhysicalCounts,
    decimal HighValueThresholdForSignoff = 100.0m
);

public record CycleCountAuditResult(
    string AuditId,
    string TenantId,
    string WarehouseId,
    DateTime CompletedAtUtc,
    int TotalBinsAudited,
    int TotalItemsAudited,
    double NetInventoryAccuracyPercentage,
    decimal TotalNetVarianceValue,
    decimal TotalAbsoluteDiscrepancyValue,
    List<CycleCountAdjustment> Adjustments,
    int AdjustmentsRequiringSignoffCount,
    string AuditGrade // "AuditGradeA_Exceptional", "AuditGradeB_Compliant", "AuditGradeC_RequiresRecount"
);
