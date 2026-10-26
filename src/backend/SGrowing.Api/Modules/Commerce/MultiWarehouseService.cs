using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Commerce;

public interface IMultiWarehouseService
{
    Task<ProximityAllocationResult> AllocateOrderToOptimalWarehouseAsync(Guid tenantId, string governorate, string sku, int quantity = 1);
    Task<InterHubStockTransfer> CreateInterHubTransferAsync(Guid tenantId, Guid sourceWhId, Guid destWhId, string sku, int quantity);
    Task<InterHubStockTransfer> DispatchInterHubTransferAsync(Guid tenantId, Guid transferId);
    Task<InterHubStockTransfer> ReceiveInterHubTransferAsync(Guid tenantId, Guid transferId);
    Task<SupplierPurchaseOrder?> EvaluateReorderPointAndGeneratePoAsync(Guid tenantId, Guid warehouseId, string sku, int dailyDemand = 5, int leadDays = 7, decimal unitCost = 25.0m);
}

public class MultiWarehouseService : IMultiWarehouseService
{
    private readonly SGrowingDbContext _db;

    public MultiWarehouseService(SGrowingDbContext db)
    {
        _db = db;
    }

    public async Task<ProximityAllocationResult> AllocateOrderToOptimalWarehouseAsync(Guid tenantId, string governorate, string sku, int quantity = 1)
    {
        var cleanGov = governorate.Trim();
        var rules = await _db.RegionalHubProximityRules
            .Where(r => r.TenantId == tenantId && r.GovernorateOrWilaya == cleanGov && r.IsActive)
            .OrderBy(r => r.Priority)
            .ToListAsync();

        var inventories = await _db.WarehouseInventories
            .Where(w => w.TenantId == tenantId && w.Sku == sku)
            .ToListAsync();

        // 1. Try regional proximity hub first
        foreach (var rule in rules)
        {
            var inv = inventories.FirstOrDefault(i => i.WarehouseId == rule.WarehouseId);
            if (inv != null && inv.AvailableStock >= quantity)
            {
                return new ProximityAllocationResult(
                    WarehouseId: rule.WarehouseId,
                    WarehouseName: rule.WarehouseName,
                    AvailableStock: inv.AvailableStock,
                    MatchReason: $"Optimal Regional Proximity Hub for {cleanGov} ({inv.AvailableStock} units available)");
            }
        }

        // 2. Fallback to Primary central warehouse
        var primaryWarehouse = await _db.Warehouses
            .FirstOrDefaultAsync(w => w.TenantId == tenantId && w.IsPrimary);

        var primaryWhId = primaryWarehouse?.Id ?? (inventories.FirstOrDefault()?.WarehouseId ?? Guid.NewGuid());
        var primaryInv = inventories.FirstOrDefault(i => i.WarehouseId == primaryWhId);

        return new ProximityAllocationResult(
            WarehouseId: primaryWhId,
            WarehouseName: primaryWarehouse?.Name ?? "Central Fulfillment Hub",
            AvailableStock: primaryInv?.AvailableStock ?? 0,
            MatchReason: $"Fallback to Central Primary Warehouse for {cleanGov}");
    }

    public async Task<InterHubStockTransfer> CreateInterHubTransferAsync(Guid tenantId, Guid sourceWhId, Guid destWhId, string sku, int quantity)
    {
        var sourceWh = await _db.Warehouses.FindAsync(sourceWhId);
        var destWh = await _db.Warehouses.FindAsync(destWhId);

        var itemsJson = JsonSerializer.Serialize(new[]
        {
            new { sku = sku, quantity = quantity }
        });

        var transfer = InterHubStockTransfer.Create(
            tenantId,
            sourceWhId,
            sourceWh?.Name ?? "Source Warehouse",
            destWhId,
            destWh?.Name ?? "Destination Warehouse",
            itemsJson);

        _db.InterHubStockTransfers.Add(transfer);
        await _db.SaveChangesAsync();
        return transfer;
    }

    public async Task<InterHubStockTransfer> DispatchInterHubTransferAsync(Guid tenantId, Guid transferId)
    {
        var transfer = await _db.InterHubStockTransfers
            .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Id == transferId);

        if (transfer == null)
            throw new InvalidOperationException($"Transfer {transferId} not found.");

        transfer.Status = InterHubTransferStatus.InTransit;
        transfer.DispatchedAtUtc = DateTime.UtcNow;

        // Defensive WMS: Decrement source PhysicalStock, increment source InTransitStock
        var sourceInv = await _db.WarehouseInventories
            .FirstOrDefaultAsync(w => w.TenantId == tenantId && w.WarehouseId == transfer.SourceWarehouseId);

        if (sourceInv != null)
        {
            sourceInv.PhysicalStock = Math.Max(0, sourceInv.PhysicalStock - 1);
            sourceInv.InTransitStock += 1;
            sourceInv.LastUpdatedUtc = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        return transfer;
    }

    public async Task<InterHubStockTransfer> ReceiveInterHubTransferAsync(Guid tenantId, Guid transferId)
    {
        var transfer = await _db.InterHubStockTransfers
            .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Id == transferId);

        if (transfer == null)
            throw new InvalidOperationException($"Transfer {transferId} not found.");

        transfer.Status = InterHubTransferStatus.Received;
        transfer.ReceivedAtUtc = DateTime.UtcNow;

        // Defensive WMS: Clear source InTransitStock, increment destination PhysicalStock
        var sourceInv = await _db.WarehouseInventories
            .FirstOrDefaultAsync(w => w.TenantId == tenantId && w.WarehouseId == transfer.SourceWarehouseId);

        if (sourceInv != null && sourceInv.InTransitStock > 0)
        {
            sourceInv.InTransitStock = Math.Max(0, sourceInv.InTransitStock - 1);
            sourceInv.LastUpdatedUtc = DateTime.UtcNow;
        }

        var destInv = await _db.WarehouseInventories
            .FirstOrDefaultAsync(w => w.TenantId == tenantId && w.WarehouseId == transfer.DestinationWarehouseId);

        if (destInv != null)
        {
            destInv.PhysicalStock += 1;
            destInv.LastUpdatedUtc = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        return transfer;
    }

    public async Task<SupplierPurchaseOrder?> EvaluateReorderPointAndGeneratePoAsync(
        Guid tenantId,
        Guid warehouseId,
        string sku,
        int dailyDemand = 5,
        int leadDays = 7,
        decimal unitCost = 25.0m)
    {
        var inv = await _db.WarehouseInventories
            .FirstOrDefaultAsync(w => w.TenantId == tenantId && w.WarehouseId == warehouseId && w.Sku == sku);

        if (inv == null) return null;

        // Reorder point ROP = (dailyDemand * leadDays) + safety stock (10)
        int rop = (dailyDemand * leadDays) + 10;

        if (inv.AvailableStock <= rop)
        {
            int orderQuantity = Math.Max(50, (rop * 2) - inv.AvailableStock);
            decimal totalCost = orderQuantity * unitCost;

            var po = new SupplierPurchaseOrder
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                PoNumber = $"PO-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..4].ToUpperInvariant()}",
                SupplierName = "Laboratoire Carthage Aromatiques",
                DestinationWarehouseId = warehouseId,
                TotalCost = totalCost,
                Currency = "TND",
                Status = "Draft",
                ItemsJson = JsonSerializer.Serialize(new[]
                {
                    new { sku = sku, quantity = orderQuantity, unitPrice = unitCost }
                }),
                CreatedAtUtc = DateTime.UtcNow
            };

            _db.SupplierPurchaseOrders.Add(po);
            await _db.SaveChangesAsync();
            return po;
        }

        return null;
    }
}
