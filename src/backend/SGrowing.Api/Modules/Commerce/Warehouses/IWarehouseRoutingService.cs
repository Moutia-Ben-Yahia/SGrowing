using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Commerce.Warehouses;

public interface IWarehouseRoutingService
{
    Task<WarehouseRoutingResult> RouteOrderAsync(RoutingRequest req, CancellationToken ct = default);
    Task<IEnumerable<WarehouseRecord>> GetWarehousesAsync(Guid tenantId, CancellationToken ct = default);
    Task<WarehouseRecord> CreateWarehouseAsync(Guid tenantId, CreateWarehouseRequest req, CancellationToken ct = default);
    Task<IEnumerable<WarehouseStockRecord>> GetStockLevelsAsync(Guid tenantId, Guid? warehouseId = null, string? sku = null, CancellationToken ct = default);
    Task<StockTransferManifest> CreateStockTransferAsync(Guid tenantId, CreateStockTransferRequest req, CancellationToken ct = default);
    Task<IEnumerable<StockTransferManifest>> GetTransferManifestsAsync(Guid tenantId, CancellationToken ct = default);
    Task<SpatialWarehouseSearchResult> SpatialSearchAsync(Guid tenantId, SpatialWarehouseQueryRequest req, CancellationToken ct = default);
}
