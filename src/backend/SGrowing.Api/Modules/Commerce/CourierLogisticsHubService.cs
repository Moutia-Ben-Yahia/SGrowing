using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Commerce;

public interface ICourierLogisticsHubService
{
    Task<ShipmentRecord> DispatchOrderAsync(Guid tenantId, Guid orderId, Guid courierProviderId);
    Task<ShippingManifest> GenerateBordereauManifestAsync(Guid tenantId, Guid courierProviderId, List<Guid> orderIds);
    Task<ShipmentRecord> ProcessTrackingWebhookAsync(Guid tenantId, string trackingNumber, CourierShipmentStatus status, decimal? cashRemitted = null, string? notes = null);
}

public class CourierLogisticsHubService : ICourierLogisticsHubService
{
    private readonly SGrowingDbContext _dbContext;

    public CourierLogisticsHubService(SGrowingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ShipmentRecord> DispatchOrderAsync(Guid tenantId, Guid orderId, Guid courierProviderId)
    {
        var order = await _dbContext.CodOrders.FirstOrDefaultAsync(o => o.TenantId == tenantId && o.Id == orderId)
            ?? throw new InvalidOperationException($"Order {orderId} not found.");

        var courier = await _dbContext.CourierProviders.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == courierProviderId)
            ?? throw new InvalidOperationException($"Courier provider {courierProviderId} not found.");

        var randomSuffix = Random.Shared.Next(100000, 999999);
        var trackingNumber = $"{courier.ApiCode.ToUpperInvariant()}-{order.OrderNumber}-{randomSuffix}";
        var awbBarcode = $"*AWB{randomSuffix}*";
        var labelPdfUrl = $"https://cdn.sgrowing.io/labels/{courier.ApiCode}/{trackingNumber}.pdf";

        var shipment = new ShipmentRecord
        {
            TenantId = tenantId,
            OrderId = orderId,
            CourierProviderId = courierProviderId,
            CourierName = courier.Name,
            TrackingNumber = trackingNumber,
            AwbBarcode = awbBarcode,
            LabelPdfUrl = labelPdfUrl,
            Status = CourierShipmentStatus.Created,
            CashToCollect = order.TotalAmount + order.ShippingFee,
            CashRemitted = 0,
            DispatchedAtUtc = DateTime.UtcNow
        };

        order.Status = CodOrderStatus.Dispatched;
        order.Notes = (order.Notes ?? "") + $" | Dispatched via {courier.Name} (Tracking: {trackingNumber})";

        _dbContext.ShipmentRecords.Add(shipment);
        await _dbContext.SaveChangesAsync();

        return shipment;
    }

    public async Task<ShippingManifest> GenerateBordereauManifestAsync(Guid tenantId, Guid courierProviderId, List<Guid> orderIds)
    {
        var courier = await _dbContext.CourierProviders.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == courierProviderId)
            ?? throw new InvalidOperationException($"Courier provider {courierProviderId} not found.");

        var orders = await _dbContext.CodOrders
            .Where(o => o.TenantId == tenantId && orderIds.Contains(o.Id))
            .ToListAsync();

        var totalCash = orders.Sum(o => o.TotalAmount + o.ShippingFee);
        var manifestNumber = $"MAN-{courier.ApiCode.ToUpperInvariant()}-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";

        var manifest = new ShippingManifest
        {
            TenantId = tenantId,
            CourierProviderId = courierProviderId,
            CourierName = courier.Name,
            ManifestNumber = manifestNumber,
            OrderCount = orders.Count,
            TotalCashAmount = totalCash,
            Status = "Generated",
            CreatedAtUtc = DateTime.UtcNow,
            OrderIdsJson = JsonSerializer.Serialize(orderIds)
        };

        _dbContext.ShippingManifests.Add(manifest);
        await _dbContext.SaveChangesAsync();

        return manifest;
    }

    public async Task<ShipmentRecord> ProcessTrackingWebhookAsync(Guid tenantId, string trackingNumber, CourierShipmentStatus status, decimal? cashRemitted = null, string? notes = null)
    {
        var shipment = await _dbContext.ShipmentRecords
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.TrackingNumber == trackingNumber)
            ?? throw new InvalidOperationException($"Shipment with tracking number {trackingNumber} not found.");

        shipment.Status = status;

        var order = await _dbContext.CodOrders.FirstOrDefaultAsync(o => o.TenantId == tenantId && o.Id == shipment.OrderId);

        if (status == CourierShipmentStatus.Delivered)
        {
            shipment.DeliveredAtUtc = DateTime.UtcNow;
            shipment.CashRemitted = cashRemitted ?? shipment.CashToCollect;

            if (order != null)
            {
                order.Status = CodOrderStatus.Delivered;
                order.Notes = (order.Notes ?? "") + $" | Delivered & Cash Remitted ({shipment.CashRemitted} {order.CodCurrency})";
            }
        }
        else if (status == CourierShipmentStatus.ReturnedToSender)
        {
            if (order != null)
            {
                order.Status = CodOrderStatus.Returned;
                order.Notes = (order.Notes ?? "") + $" | Returned to warehouse: {notes ?? "Customer refused or unreachable"}";
            }
        }

        await _dbContext.SaveChangesAsync();
        return shipment;
    }
}
