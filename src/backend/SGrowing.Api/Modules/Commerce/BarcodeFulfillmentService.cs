using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Commerce;

public record PickVerificationResult(bool Success, string Message, int PickedCount, int RequestedCount, bool IsFullyPicked);

public interface IBarcodeFulfillmentService
{
    Task<WarehousePickSlip> CreatePickSlipAsync(Guid tenantId, Guid orderId, Guid warehouseId);
    Task<PickVerificationResult> ScanItemToPickSlipAsync(Guid tenantId, Guid pickSlipId, string scannedBarcodeOrSku);
    Task<WarehousePickSlip> CompletePackAndAssignAwbAsync(Guid tenantId, Guid pickSlipId, string trackingNumber, string courierName);
    Task<CourierManifestRunSheet> ScanParcelToManifestAsync(Guid tenantId, Guid manifestId, string trackingNumber);
    Task<CourierManifestRunSheet> CloseAndSignManifestAsync(Guid tenantId, Guid manifestId, string driverName, string driverPhone, string driverSignature);
    Task<WarehouseRmaInspection> InspectAndRestockRmaParcelAsync(Guid tenantId, string trackingNumber, string inspectorId, RmaItemCondition condition, string? notes, Guid warehouseId);
}

public class BarcodeFulfillmentService : IBarcodeFulfillmentService
{
    private readonly SGrowingDbContext _db;

    public BarcodeFulfillmentService(SGrowingDbContext db)
    {
        _db = db;
    }

    public async Task<WarehousePickSlip> CreatePickSlipAsync(Guid tenantId, Guid orderId, Guid warehouseId)
    {
        var order = await _db.CodOrders.FirstOrDefaultAsync(o => o.TenantId == tenantId && o.Id == orderId);
        if (order == null)
            throw new InvalidOperationException($"Order {orderId} not found.");

        var pickSlip = WarehousePickSlip.Create(tenantId, orderId, order.OrderNumber, warehouseId);

        // Parse items
        if (!string.IsNullOrWhiteSpace(order.ItemsJson) && order.ItemsJson != "[]")
        {
            try
            {
                using var doc = JsonDocument.Parse(order.ItemsJson);
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    var sku = el.TryGetProperty("sku", out var s) ? s.GetString() ?? "ITEM" : "ITEM";
                    var name = el.TryGetProperty("title", out var t) ? t.GetString() ?? sku : sku;
                    var qty = el.TryGetProperty("quantity", out var q) ? q.GetInt32() : 1;
                    var barcode = el.TryGetProperty("barcode", out var b) ? b.GetString() ?? sku : sku;

                    pickSlip.Items.Add(new PickSlipItem
                    {
                        Id = Guid.NewGuid(),
                        PickSlipId = pickSlip.Id,
                        Sku = sku,
                        ProductName = name,
                        Barcode = barcode,
                        QuantityRequested = qty,
                        QuantityPicked = 0
                    });
                }
            }
            catch
            {
                pickSlip.Items.Add(new PickSlipItem
                {
                    Id = Guid.NewGuid(),
                    PickSlipId = pickSlip.Id,
                    Sku = "DEFAULT-SKU",
                    ProductName = "Order Items",
                    Barcode = "DEFAULT-SKU",
                    QuantityRequested = 1,
                    QuantityPicked = 0
                });
            }
        }
        else
        {
            pickSlip.Items.Add(new PickSlipItem
            {
                Id = Guid.NewGuid(),
                PickSlipId = pickSlip.Id,
                Sku = "DEFAULT-SKU",
                ProductName = "Order Items",
                Barcode = "DEFAULT-SKU",
                QuantityRequested = 1,
                QuantityPicked = 0
            });
        }

        _db.WarehousePickSlips.Add(pickSlip);
        _db.PickSlipItems.AddRange(pickSlip.Items);
        await _db.SaveChangesAsync();
        return pickSlip;
    }

    public async Task<PickVerificationResult> ScanItemToPickSlipAsync(Guid tenantId, Guid pickSlipId, string scannedBarcodeOrSku)
    {
        var pickSlip = await _db.WarehousePickSlips
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Id == pickSlipId);

        if (pickSlip == null)
            throw new InvalidOperationException($"Pick slip {pickSlipId} not found.");

        var cleanBarcode = scannedBarcodeOrSku.Trim();
        var item = pickSlip.Items.FirstOrDefault(i => 
            string.Equals(i.Barcode, cleanBarcode, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(i.Sku, cleanBarcode, StringComparison.OrdinalIgnoreCase));

        if (item == null)
        {
            return new PickVerificationResult(
                Success: false,
                Message: $"Mismatch! Item '{cleanBarcode}' is not part of pick slip {pickSlip.PickSlipNumber}.",
                PickedCount: 0,
                RequestedCount: 0,
                IsFullyPicked: false);
        }

        item.QuantityPicked++;
        if (pickSlip.Status == PickSlipStatus.Pending)
        {
            pickSlip.Status = PickSlipStatus.Picking;
        }

        var isFullyPicked = pickSlip.Items.All(i => i.IsVerified);
        if (isFullyPicked)
        {
            pickSlip.Status = PickSlipStatus.Picked;
        }

        await _db.SaveChangesAsync();

        return new PickVerificationResult(
            Success: true,
            Message: $"Verified item '{item.ProductName}'. ({item.QuantityPicked}/{item.QuantityRequested})",
            PickedCount: item.QuantityPicked,
            RequestedCount: item.QuantityRequested,
            IsFullyPicked: isFullyPicked);
    }

    public async Task<WarehousePickSlip> CompletePackAndAssignAwbAsync(
        Guid tenantId,
        Guid pickSlipId,
        string trackingNumber,
        string courierName)
    {
        var pickSlip = await _db.WarehousePickSlips
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Id == pickSlipId);

        if (pickSlip == null)
            throw new InvalidOperationException($"Pick slip {pickSlipId} not found.");

        pickSlip.Status = PickSlipStatus.Packed;
        pickSlip.CompletedAtUtc = DateTime.UtcNow;

        var order = await _db.CodOrders.FirstOrDefaultAsync(o => o.TenantId == tenantId && o.Id == pickSlip.OrderId);
        if (order != null)
        {
            order.Status = CodOrderStatus.Dispatched;
            order.Notes = (order.Notes ?? "") + $" [Packed & Assigned Tracking #{trackingNumber} ({courierName})]";
        }

        // Defensive WMS: Move from Committed to InTransit and decrement physical warehouse shelf stock
        var inventories = await _db.WarehouseInventories.Where(w => w.TenantId == tenantId).ToListAsync();
        foreach (var inv in inventories)
        {
            if (inv.CommittedStock > 0)
            {
                inv.CommittedStock = Math.Max(0, inv.CommittedStock - 1);
                inv.InTransitStock += 1;
                inv.PhysicalStock = Math.Max(0, inv.PhysicalStock - 1);
                inv.LastUpdatedUtc = DateTime.UtcNow;
            }
        }

        await _db.SaveChangesAsync();
        return pickSlip;
    }

    public async Task<CourierManifestRunSheet> ScanParcelToManifestAsync(Guid tenantId, Guid manifestId, string trackingNumber)
    {
        var manifest = await _db.CourierManifestRunSheets
            .Include(m => m.Parcels)
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Id == manifestId);

        if (manifest == null)
            throw new InvalidOperationException($"Manifest {manifestId} not found.");

        var cleanTracking = trackingNumber.Trim();
        var existingParcel = manifest.Parcels.FirstOrDefault(p => p.TrackingNumber == cleanTracking);
        if (existingParcel != null)
        {
            return manifest; // Already scanned
        }

        var order = await _db.CodOrders.FirstOrDefaultAsync(o => o.TenantId == tenantId && (o.Notes != null && o.Notes.Contains(cleanTracking)));
        var codAmount = order?.TotalAmount ?? 50.0m;
        var customerName = order?.CustomerName ?? "Customer";
        var gov = order?.GovernorateOrWilaya ?? "Tunis";

        var parcelItem = new ManifestParcelItem
        {
            Id = Guid.NewGuid(),
            ManifestId = manifest.Id,
            OrderId = order?.Id ?? Guid.NewGuid(),
            TrackingNumber = cleanTracking,
            CustomerName = customerName,
            Governorate = gov,
            CodAmount = codAmount,
            ScannedAtUtc = DateTime.UtcNow
        };

        _db.ManifestParcelItems.Add(parcelItem);
        manifest.Parcels.Add(parcelItem);
        manifest.TotalParcels = manifest.Parcels.Count;
        manifest.TotalCodValue = manifest.Parcels.Sum(p => p.CodAmount);

        await _db.SaveChangesAsync();
        return manifest;
    }

    public async Task<CourierManifestRunSheet> CloseAndSignManifestAsync(
        Guid tenantId,
        Guid manifestId,
        string driverName,
        string driverPhone,
        string driverSignature)
    {
        var manifest = await _db.CourierManifestRunSheets
            .Include(m => m.Parcels)
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Id == manifestId);

        if (manifest == null)
            throw new InvalidOperationException($"Manifest {manifestId} not found.");

        manifest.DriverName = driverName.Trim();
        manifest.DriverPhone = driverPhone.Trim();
        manifest.DriverSignatureData = driverSignature;
        manifest.Status = ManifestStatus.HandedOverToDriver;
        manifest.HandedOverAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return manifest;
    }

    public async Task<WarehouseRmaInspection> InspectAndRestockRmaParcelAsync(
        Guid tenantId,
        string trackingNumber,
        string inspectorId,
        RmaItemCondition condition,
        string? notes,
        Guid warehouseId)
    {
        var cleanTracking = trackingNumber.Trim();
        var order = await _db.CodOrders.FirstOrDefaultAsync(o => o.TenantId == tenantId && (o.Notes != null && o.Notes.Contains(cleanTracking)));

        var rma = new WarehouseRmaInspection
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TrackingNumber = cleanTracking,
            OrderId = order?.Id ?? Guid.NewGuid(),
            InspectorId = inspectorId,
            Condition = condition,
            InspectorNotes = notes,
            RestockedToWarehouseId = warehouseId,
            InspectedAtUtc = DateTime.UtcNow
        };

        if (order != null)
        {
            order.Status = CodOrderStatus.Returned;
            order.Notes = (order.Notes ?? "") + $" [RMA Inspected: {condition}. Restocked to WMS]";
        }

        // Defensive WMS: Handle RMA stock based on condition
        var inventory = await _db.WarehouseInventories
            .FirstOrDefaultAsync(w => w.TenantId == tenantId && w.WarehouseId == warehouseId);

        if (inventory != null)
        {
            if (inventory.InTransitStock > 0)
            {
                inventory.InTransitStock = Math.Max(0, inventory.InTransitStock - 1);
            }

            if (condition == RmaItemCondition.IntactRestock)
            {
                // Returned in perfect condition -> Restock into Physical/Available
                inventory.PhysicalStock += 1;
            }
            else if (condition == RmaItemCondition.DamagedWriteoff)
            {
                // Damaged item -> Write off into DamagedStock
                inventory.DamagedStock += 1;
            }

            inventory.LastUpdatedUtc = DateTime.UtcNow;
        }

        _db.WarehouseRmaInspections.Add(rma);
        await _db.SaveChangesAsync();
        return rma;
    }
}
