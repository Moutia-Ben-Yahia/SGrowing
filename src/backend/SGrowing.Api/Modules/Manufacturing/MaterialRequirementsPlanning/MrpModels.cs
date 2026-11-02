namespace SGrowing.Api.Modules.Manufacturing.MaterialRequirementsPlanning;

public record MrpItemRecord(
    string ItemId,
    string Sku,
    string Description,
    int LeadTimeDays,
    double SafetyStock,
    double CurrentOnHand,
    double AllocatedStock,
    double MinimumOrderQuantity,
    double BatchMultiple
);

public record MrpBomComponent(
    string ParentSku,
    string ComponentSku,
    double QuantityPerParent,
    double ScrapPercentage = 0.0
);

public record MrpGrossDemand(
    string DemandId,
    string Sku,
    DateTime RequiredDate,
    double Quantity,
    string Source = "SalesOrder"
);

public record MrpScheduledReceipt(
    string ReceiptId,
    string Sku,
    DateTime ArrivalDate,
    double Quantity,
    string SourceType = "PurchaseOrder"
);

public record MrpTimeBucket(
    int BucketIndex,
    DateTime BucketDate,
    double GrossRequirements,
    double ScheduledReceipts,
    double ProjectedOnHand,
    double NetRequirements,
    double PlannedOrderReceipts,
    double PlannedOrderReleases
);

public record MrpActionMessage(
    string Sku,
    string ActionType, // "ReleaseOrder", "ExpediteOrder", "DeferOrder", "CancelOrder"
    DateTime ActionDate,
    double Quantity,
    string Reason
);

public record MrpExplosionRequest(
    string TenantId,
    DateTime PlanStartDate,
    int PlanningHorizonDays,
    int BucketSizeDays,
    List<MrpItemRecord> Items,
    List<MrpBomComponent> BillOfMaterials,
    List<MrpGrossDemand> GrossDemands,
    List<MrpScheduledReceipt> ScheduledReceipts
);

public record MrpExplosionResult(
    string PlanId,
    string TenantId,
    DateTime GeneratedAtUtc,
    int TotalBuckets,
    Dictionary<string, List<MrpTimeBucket>> ItemBuckets,
    List<MrpActionMessage> ActionMessages,
    int TotalPlannedOrdersCount,
    double TotalStockoutExposureUnits
);
