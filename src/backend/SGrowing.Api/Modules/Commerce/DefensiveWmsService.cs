using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Commerce;

public interface IDefensiveWmsService
{
    Task<WarehouseInventory> GetOrCreateInventoryAsync(Guid tenantId, Guid warehouseId, string sku, int initialPhysical = 0, Guid? productId = null);
    Task<WarehouseInventory> ReserveStockOnCheckoutAsync(Guid tenantId, Guid warehouseId, string sku, int quantity, Guid orderId, string performedBy = "Checkout");
    Task<WarehouseInventory> CommitStockOnConfirmationAsync(Guid tenantId, Guid warehouseId, string sku, int quantity, Guid orderId, string performedBy = "ConfirmationAgent");
    Task<WarehouseInventory> ReleaseStockOnCancellationAsync(Guid tenantId, Guid warehouseId, string sku, int quantity, Guid orderId, string performedBy = "CancellationWorker");
    Task<WarehouseInventory> DispatchStockAsync(Guid tenantId, Guid warehouseId, string sku, int quantity, Guid orderId, string performedBy = "LogisticsCourier");
    Task<WarehouseInventory> ReceiveCourierReturnToQuarantineAsync(Guid tenantId, Guid warehouseId, string sku, int quantity, Guid orderId, string performedBy = "CourierWebhook");
    Task<WarehouseInventory> ScanAndRestockQuarantineItemAsync(Guid tenantId, Guid warehouseId, string sku, int quantity, bool isDamaged, string inspectorId = "WarehouseInspector");
    Task<WmsStockSummary> GetStockSummaryAsync(Guid tenantId, Guid warehouseId, string sku);
}

public record WmsStockSummary(string Sku, int Physical, int Reserved, int Committed, int InTransit, int QuarantineReturned, int Damaged, int Available);

public class DefensiveWmsService : IDefensiveWmsService
{
    private readonly SGrowingDbContext _dbContext;

    public DefensiveWmsService(SGrowingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<WarehouseInventory> GetOrCreateInventoryAsync(Guid tenantId, Guid warehouseId, string sku, int initialPhysical = 0, Guid? productId = null)
    {
        var inventory = await _dbContext.WarehouseInventories
            .FirstOrDefaultAsync(i => i.TenantId == tenantId && i.WarehouseId == warehouseId && i.Sku == sku);

        if (inventory == null)
        {
            inventory = new WarehouseInventory
            {
                TenantId = tenantId,
                WarehouseId = warehouseId,
                ProductId = productId ?? Guid.NewGuid(),
                Sku = sku.ToUpperInvariant(),
                PhysicalStock = initialPhysical,
                ReservedStock = 0,
                CommittedStock = 0,
                InTransitStock = 0,
                QuarantineReturnedStock = 0,
                DamagedStock = 0,
                LastUpdatedUtc = DateTime.UtcNow
            };
            _dbContext.WarehouseInventories.Add(inventory);
            await _dbContext.SaveChangesAsync();
        }

        return inventory;
    }

    public async Task<WarehouseInventory> ReserveStockOnCheckoutAsync(Guid tenantId, Guid warehouseId, string sku, int quantity, Guid orderId, string performedBy = "Checkout")
    {
        var inv = await GetOrCreateInventoryAsync(tenantId, warehouseId, sku);
        inv.ReservedStock += quantity;
        inv.LastUpdatedUtc = DateTime.UtcNow;

        _dbContext.StockMovements.Add(new StockMovement
        {
            TenantId = tenantId,
            WarehouseId = warehouseId,
            ProductId = inv.ProductId,
            Sku = sku,
            OrderId = orderId,
            MovementType = StockMovementType.Reservation,
            QuantityChange = quantity,
            PreviousPhysical = inv.PhysicalStock,
            NewPhysical = inv.PhysicalStock,
            PerformedBy = performedBy,
            Notes = $"Reserved {quantity} units on checkout order {orderId}",
            TimestampUtc = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync();
        return inv;
    }

    public async Task<WarehouseInventory> CommitStockOnConfirmationAsync(Guid tenantId, Guid warehouseId, string sku, int quantity, Guid orderId, string performedBy = "ConfirmationAgent")
    {
        var inv = await GetOrCreateInventoryAsync(tenantId, warehouseId, sku);
        inv.ReservedStock = Math.Max(0, inv.ReservedStock - quantity);
        inv.CommittedStock += quantity;
        inv.LastUpdatedUtc = DateTime.UtcNow;

        _dbContext.StockMovements.Add(new StockMovement
        {
            TenantId = tenantId,
            WarehouseId = warehouseId,
            ProductId = inv.ProductId,
            Sku = sku,
            OrderId = orderId,
            MovementType = StockMovementType.Commitment,
            QuantityChange = quantity,
            PreviousPhysical = inv.PhysicalStock,
            NewPhysical = inv.PhysicalStock,
            PerformedBy = performedBy,
            Notes = $"Committed {quantity} units upon confirmation of order {orderId}",
            TimestampUtc = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync();
        return inv;
    }

    public async Task<WarehouseInventory> ReleaseStockOnCancellationAsync(Guid tenantId, Guid warehouseId, string sku, int quantity, Guid orderId, string performedBy = "CancellationWorker")
    {
        var inv = await GetOrCreateInventoryAsync(tenantId, warehouseId, sku);
        inv.ReservedStock = Math.Max(0, inv.ReservedStock - quantity);
        inv.LastUpdatedUtc = DateTime.UtcNow;

        _dbContext.StockMovements.Add(new StockMovement
        {
            TenantId = tenantId,
            WarehouseId = warehouseId,
            ProductId = inv.ProductId,
            Sku = sku,
            OrderId = orderId,
            MovementType = StockMovementType.CancellationRestock,
            QuantityChange = -quantity,
            PreviousPhysical = inv.PhysicalStock,
            NewPhysical = inv.PhysicalStock,
            PerformedBy = performedBy,
            Notes = $"Released {quantity} reserved units due to order {orderId} cancellation",
            TimestampUtc = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync();
        return inv;
    }

    public async Task<WarehouseInventory> DispatchStockAsync(Guid tenantId, Guid warehouseId, string sku, int quantity, Guid orderId, string performedBy = "LogisticsCourier")
    {
        var inv = await GetOrCreateInventoryAsync(tenantId, warehouseId, sku);
        inv.CommittedStock = Math.Max(0, inv.CommittedStock - quantity);
        inv.InTransitStock += quantity;
        inv.PhysicalStock = Math.Max(0, inv.PhysicalStock - quantity); // physically leaves warehouse building
        inv.LastUpdatedUtc = DateTime.UtcNow;

        _dbContext.StockMovements.Add(new StockMovement
        {
            TenantId = tenantId,
            WarehouseId = warehouseId,
            ProductId = inv.ProductId,
            Sku = sku,
            OrderId = orderId,
            MovementType = StockMovementType.Dispatch,
            QuantityChange = -quantity,
            PreviousPhysical = inv.PhysicalStock + quantity,
            NewPhysical = inv.PhysicalStock,
            PerformedBy = performedBy,
            Notes = $"Dispatched {quantity} units to courier for order {orderId}",
            TimestampUtc = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync();
        return inv;
    }

    public async Task<WarehouseInventory> ReceiveCourierReturnToQuarantineAsync(Guid tenantId, Guid warehouseId, string sku, int quantity, Guid orderId, string performedBy = "CourierWebhook")
    {
        var inv = await GetOrCreateInventoryAsync(tenantId, warehouseId, sku);
        inv.InTransitStock = Math.Max(0, inv.InTransitStock - quantity);
        inv.QuarantineReturnedStock += quantity;
        inv.LastUpdatedUtc = DateTime.UtcNow;

        _dbContext.StockMovements.Add(new StockMovement
        {
            TenantId = tenantId,
            WarehouseId = warehouseId,
            ProductId = inv.ProductId,
            Sku = sku,
            OrderId = orderId,
            MovementType = StockMovementType.CourierReturnQuarantine,
            QuantityChange = quantity,
            PreviousPhysical = inv.PhysicalStock,
            NewPhysical = inv.PhysicalStock,
            PerformedBy = performedBy,
            Notes = $"Courier returned {quantity} units into quarantine area for order {orderId}",
            TimestampUtc = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync();
        return inv;
    }

    public async Task<WarehouseInventory> ScanAndRestockQuarantineItemAsync(Guid tenantId, Guid warehouseId, string sku, int quantity, bool isDamaged, string inspectorId = "WarehouseInspector")
    {
        var inv = await GetOrCreateInventoryAsync(tenantId, warehouseId, sku);
        inv.QuarantineReturnedStock = Math.Max(0, inv.QuarantineReturnedStock - quantity);

        if (isDamaged)
        {
            inv.DamagedStock += quantity;
            _dbContext.StockMovements.Add(new StockMovement
            {
                TenantId = tenantId,
                WarehouseId = warehouseId,
                ProductId = inv.ProductId,
                Sku = sku,
                MovementType = StockMovementType.DamageWriteoff,
                QuantityChange = quantity,
                PreviousPhysical = inv.PhysicalStock,
                NewPhysical = inv.PhysicalStock,
                PerformedBy = inspectorId,
                Notes = $"Quarantine inspection: {quantity} units written off as damaged",
                TimestampUtc = DateTime.UtcNow
            });
        }
        else
        {
            var prevPhysical = inv.PhysicalStock;
            inv.PhysicalStock += quantity;
            _dbContext.StockMovements.Add(new StockMovement
            {
                TenantId = tenantId,
                WarehouseId = warehouseId,
                ProductId = inv.ProductId,
                Sku = sku,
                MovementType = StockMovementType.InspectionRestock,
                QuantityChange = quantity,
                PreviousPhysical = prevPhysical,
                NewPhysical = inv.PhysicalStock,
                PerformedBy = inspectorId,
                Notes = $"Quarantine barcode scan: {quantity} units verified and restocked to physical inventory",
                TimestampUtc = DateTime.UtcNow
            });
        }

        inv.LastUpdatedUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();
        return inv;
    }

    public async Task<WmsStockSummary> GetStockSummaryAsync(Guid tenantId, Guid warehouseId, string sku)
    {
        var inv = await GetOrCreateInventoryAsync(tenantId, warehouseId, sku);
        return new WmsStockSummary(
            inv.Sku,
            inv.PhysicalStock,
            inv.ReservedStock,
            inv.CommittedStock,
            inv.InTransitStock,
            inv.QuarantineReturnedStock,
            inv.DamagedStock,
            inv.AvailableStock);
    }
}
