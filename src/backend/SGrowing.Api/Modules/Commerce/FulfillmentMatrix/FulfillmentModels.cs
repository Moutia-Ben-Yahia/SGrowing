namespace SGrowing.Api.Modules.Commerce.FulfillmentMatrix;

public record WarehouseFacility(
    string WarehouseId,
    string Code,
    string Name,
    double Latitude,
    double Longitude,
    string Country,
    int TotalStockUnits,
    int SafetyBufferUnits
);

public record FulfillmentLineItem(
    string Sku,
    string Name,
    int Quantity,
    decimal UnitPriceUsd
);

public record FulfillmentPackage(
    string PackageId,
    string WarehouseId,
    string WarehouseName,
    List<FulfillmentLineItem> Items,
    double DistanceKm,
    decimal EstimatedShippingUsd
);

public record OrderFulfillmentPlan(
    string PlanId,
    string OrderId,
    string DestinationCountry,
    double DestLatitude,
    double DestLongitude,
    List<FulfillmentPackage> Packages,
    bool IsSplitShipment,
    decimal TotalShippingUsd,
    DateTime PlannedAtUtc
);

public record PlanFulfillmentRequest(
    string OrderId,
    string DestinationCountry,
    double DestLatitude,
    double DestLongitude,
    List<FulfillmentLineItem> Items
);

public record StockTransferRecommendation(
    string Sku,
    string FromWarehouseId,
    string ToWarehouseId,
    int RecommendedUnits,
    string Reason
);
