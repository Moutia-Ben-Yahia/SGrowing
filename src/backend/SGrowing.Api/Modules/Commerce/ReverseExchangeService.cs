using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Commerce;

public interface IReverseExchangeService
{
    Task<OrderExchangeRequest> RequestExchangeAsync(
        Guid tenantId,
        Guid originalOrderId,
        string originalSku,
        string replacementSku,
        string replacementVariantDescription,
        string reason,
        decimal cashAdjustment = 0m);

    Task<ExchangeCourierSwapManifest> ApproveAndDispatchSwapManifestAsync(
        Guid tenantId,
        Guid exchangeRequestId,
        Guid courierProviderId,
        string courierName,
        decimal swapDeliveryFee = 9.5m);

    Task<OrderExchangeRequest> ConfirmDoorstepSwapAsync(
        Guid tenantId,
        Guid manifestId,
        bool handoverNewParcelConfirmed,
        bool returnParcelCollectedConfirmed);

    Task<ItemReturnInspection> ProcessWarehouseReturnIntakeAsync(
        Guid tenantId,
        Guid exchangeRequestId,
        SwapInspectionResult grade,
        string inspectorNotes);

    Task<List<OrderExchangeRequest>> ListExchangeRequestsAsync(Guid tenantId, ExchangeStatus? status = null);
    Task<OrderExchangeRequest?> GetExchangeDetailsAsync(Guid tenantId, Guid exchangeRequestId);
}

public class ReverseExchangeService : IReverseExchangeService
{
    private readonly SGrowingDbContext _db;

    public ReverseExchangeService(SGrowingDbContext db)
    {
        _db = db;
    }

    public async Task<OrderExchangeRequest> RequestExchangeAsync(
        Guid tenantId,
        Guid originalOrderId,
        string originalSku,
        string replacementSku,
        string replacementVariantDescription,
        string reason,
        decimal cashAdjustment = 0m)
    {
        var order = await _db.CodOrders
            .FirstOrDefaultAsync(o => o.TenantId == tenantId && o.Id == originalOrderId);

        if (order == null)
            throw new InvalidOperationException($"Order {originalOrderId} not found.");

        // Check inventory for replacement SKU
        var replacementInventory = await _db.WarehouseInventories
            .FirstOrDefaultAsync(w => w.TenantId == tenantId && w.Sku == replacementSku);

        if (replacementInventory != null)
        {
            if (replacementInventory.AvailableStock < 1)
            {
                throw new InvalidOperationException($"Insufficient available stock for replacement SKU '{replacementSku}'. Available: {replacementInventory.AvailableStock}");
            }
            // Reserve replacement unit defensively
            replacementInventory.ReservedStock += 1;
            replacementInventory.LastUpdatedUtc = DateTime.UtcNow;
        }

        var exchangeRequest = new OrderExchangeRequest
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OriginalOrderId = order.Id,
            OriginalTrackingNumber = order.OrderNumber,
            CustomerPhone = order.PhoneNumber,
            CustomerName = order.CustomerName,
            Reason = reason,
            OriginalSku = originalSku.Trim(),
            OriginalVariantDescription = "Original Delivered Item",
            ReplacementSku = replacementSku.Trim(),
            ReplacementVariantDescription = replacementVariantDescription.Trim(),
            AdditionalCashAdjustment = cashAdjustment,
            Status = ExchangeStatus.Requested,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.OrderExchangeRequests.Add(exchangeRequest);
        await _db.SaveChangesAsync();

        return exchangeRequest;
    }

    public async Task<ExchangeCourierSwapManifest> ApproveAndDispatchSwapManifestAsync(
        Guid tenantId,
        Guid exchangeRequestId,
        Guid courierProviderId,
        string courierName,
        decimal swapDeliveryFee = 9.5m)
    {
        var request = await _db.OrderExchangeRequests
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == exchangeRequestId);

        if (request == null)
            throw new InvalidOperationException($"Exchange request {exchangeRequestId} not found.");

        var originalOrder = await _db.CodOrders
            .FirstOrDefaultAsync(o => o.TenantId == tenantId && o.Id == request.OriginalOrderId);

        string customerAddress = originalOrder != null
            ? $"{originalOrder.ShippingAddress}, {originalOrder.DelegationOrCommune}"
            : "Customer Address";
        string governorate = originalOrder?.GovernorateOrWilaya ?? "Tunis";

        var manifest = new ExchangeCourierSwapManifest
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ExchangeRequestId = request.Id,
            CourierProviderId = courierProviderId,
            CourierName = courierName.Trim(),
            SwapTrackingBarcode = $"SWAP-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString()[..4].ToUpper()}",
            CustomerAddress = customerAddress,
            Governorate = governorate,
            BundledSwapDeliveryFee = swapDeliveryFee,
            CashToCollectOnDelivery = request.AdditionalCashAdjustment,
            HandoverNewParcelConfirmed = false,
            ReturnParcelCollectedConfirmed = false,
            DispatchedAtUtc = DateTime.UtcNow
        };

        request.Status = ExchangeStatus.DispatchedForSwap;
        request.SwapManifestId = manifest.Id;

        _db.ExchangeCourierSwapManifests.Add(manifest);
        await _db.SaveChangesAsync();

        return manifest;
    }

    public async Task<OrderExchangeRequest> ConfirmDoorstepSwapAsync(
        Guid tenantId,
        Guid manifestId,
        bool handoverNewParcelConfirmed,
        bool returnParcelCollectedConfirmed)
    {
        var manifest = await _db.ExchangeCourierSwapManifests
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Id == manifestId);

        if (manifest == null)
            throw new InvalidOperationException($"Swap manifest {manifestId} not found.");

        manifest.HandoverNewParcelConfirmed = handoverNewParcelConfirmed;
        manifest.ReturnParcelCollectedConfirmed = returnParcelCollectedConfirmed;
        manifest.DeliveredAtUtc = DateTime.UtcNow;

        var request = await _db.OrderExchangeRequests
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == manifest.ExchangeRequestId);

        if (request == null)
            throw new InvalidOperationException($"Exchange request {manifest.ExchangeRequestId} not found.");

        if (handoverNewParcelConfirmed && returnParcelCollectedConfirmed)
        {
            request.Status = ExchangeStatus.SwapCompletedAtDoorstep;

            // Update WMS: Deduct replacement item from physical and reserved
            var replacementInventory = await _db.WarehouseInventories
                .FirstOrDefaultAsync(w => w.TenantId == tenantId && w.Sku == request.ReplacementSku);

            if (replacementInventory != null)
            {
                replacementInventory.PhysicalStock = Math.Max(0, replacementInventory.PhysicalStock - 1);
                replacementInventory.ReservedStock = Math.Max(0, replacementInventory.ReservedStock - 1);
                replacementInventory.LastUpdatedUtc = DateTime.UtcNow;
            }
        }

        await _db.SaveChangesAsync();
        return request;
    }

    public async Task<ItemReturnInspection> ProcessWarehouseReturnIntakeAsync(
        Guid tenantId,
        Guid exchangeRequestId,
        SwapInspectionResult grade,
        string inspectorNotes)
    {
        var request = await _db.OrderExchangeRequests
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == exchangeRequestId);

        if (request == null)
            throw new InvalidOperationException($"Exchange request {exchangeRequestId} not found.");

        bool restocked = false;

        // Grade A = Brand new condition, restore back into physical inventory
        if (grade == SwapInspectionResult.GradeA_RestockedSellable)
        {
            var originalInventory = await _db.WarehouseInventories
                .FirstOrDefaultAsync(w => w.TenantId == tenantId && w.Sku == request.OriginalSku);

            if (originalInventory != null)
            {
                originalInventory.PhysicalStock += 1;
                originalInventory.LastUpdatedUtc = DateTime.UtcNow;
                restocked = true;
            }
        }
        else if (grade == SwapInspectionResult.GradeC_DamagedWriteOff)
        {
            var originalInventory = await _db.WarehouseInventories
                .FirstOrDefaultAsync(w => w.TenantId == tenantId && w.Sku == request.OriginalSku);

            if (originalInventory != null)
            {
                originalInventory.DamagedStock += 1;
                originalInventory.LastUpdatedUtc = DateTime.UtcNow;
            }
        }

        var inspection = new ItemReturnInspection
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ExchangeRequestId = exchangeRequestId,
            ReturnedSku = request.OriginalSku,
            QualityGrade = grade,
            InspectorNotes = inspectorNotes.Trim(),
            RestockedToPhysicalWms = restocked,
            InspectedAtUtc = DateTime.UtcNow
        };

        request.Status = ExchangeStatus.ReturnInspectedAndRestocked;
        request.CompletedAtUtc = DateTime.UtcNow;

        _db.ItemReturnInspections.Add(inspection);
        await _db.SaveChangesAsync();

        return inspection;
    }

    public async Task<List<OrderExchangeRequest>> ListExchangeRequestsAsync(Guid tenantId, ExchangeStatus? status = null)
    {
        var query = _db.OrderExchangeRequests.Where(r => r.TenantId == tenantId);
        if (status.HasValue)
        {
            query = query.Where(r => r.Status == status.Value);
        }
        return await query.OrderByDescending(r => r.CreatedAtUtc).ToListAsync();
    }

    public async Task<OrderExchangeRequest?> GetExchangeDetailsAsync(Guid tenantId, Guid exchangeRequestId)
    {
        return await _db.OrderExchangeRequests
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == exchangeRequestId);
    }
}
