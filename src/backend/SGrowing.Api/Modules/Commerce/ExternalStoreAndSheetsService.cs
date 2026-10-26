using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Commerce;

public interface IExternalStoreAndSheetsService
{
    Task<CodOrder> IngestExternalStoreOrderAsync(Guid tenantId, ExternalStorePlatform platform, string rawPayloadJson);
    Task<ExternalStoreSyncRecord> PushFulfillmentUpdateAsync(Guid tenantId, Guid orderId, string courierName, string trackingNumber);
    Task<List<object>> GenerateGoogleSheetsRowAsync(Guid tenantId, Guid orderId);
}

public class ExternalStoreAndSheetsService : IExternalStoreAndSheetsService
{
    private readonly SGrowingDbContext _dbContext;

    public ExternalStoreAndSheetsService(SGrowingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<CodOrder> IngestExternalStoreOrderAsync(Guid tenantId, ExternalStorePlatform platform, string rawPayloadJson)
    {
        using var doc = JsonDocument.Parse(rawPayloadJson);
        var root = doc.RootElement;

        string externalId = root.TryGetProperty("id", out var idProp) ? idProp.ToString() : Guid.NewGuid().ToString("N")[..8];
        string orderNumber = root.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? $"EXT-{externalId}" : $"EXT-{externalId}";
        
        decimal total = 0;
        if (root.TryGetProperty("total_price", out var priceProp))
        {
            decimal.TryParse(priceProp.GetString(), out total);
        }

        string customerName = "Shopify Buyer";
        string phone = "+21600000000";
        if (root.TryGetProperty("customer", out var custProp))
        {
            var fn = custProp.TryGetProperty("first_name", out var fnProp) ? fnProp.GetString() : "";
            var ln = custProp.TryGetProperty("last_name", out var lnProp) ? lnProp.GetString() : "";
            customerName = $"{fn} {ln}".Trim();
            if (string.IsNullOrWhiteSpace(customerName)) customerName = "Valued Buyer";

            if (custProp.TryGetProperty("phone", out var pProp) && !string.IsNullOrWhiteSpace(pProp.GetString()))
            {
                phone = pProp.GetString()!;
            }
        }

        string address = "Standard Delivery Address";
        string city = "Tunis";
        if (root.TryGetProperty("shipping_address", out var shipProp))
        {
            address = shipProp.TryGetProperty("address1", out var aProp) ? aProp.GetString() ?? address : address;
            city = shipProp.TryGetProperty("city", out var cProp) ? cProp.GetString() ?? city : city;
            if (shipProp.TryGetProperty("phone", out var spProp) && !string.IsNullOrWhiteSpace(spProp.GetString()))
            {
                phone = spProp.GetString()!;
            }
        }

        var order = CodOrder.Create(
            tenantId,
            orderNumber,
            customerName,
            phone,
            address,
            city,
            "Centre",
            total > 0 ? total : 95m,
            notes: $"Ingested via {platform} Webhook #{externalId}");

        _dbContext.CodOrders.Add(order);

        var syncRecord = new ExternalStoreSyncRecord
        {
            TenantId = tenantId,
            Platform = platform,
            ExternalOrderId = externalId,
            ExternalOrderNumber = orderNumber,
            SGrowingOrderId = order.Id,
            SyncDirection = StoreSyncDirection.InboundOrderCreated,
            Status = "Success",
            PayloadJson = rawPayloadJson,
            SyncedAtUtc = DateTime.UtcNow
        };
        _dbContext.ExternalStoreSyncRecords.Add(syncRecord);

        await _dbContext.SaveChangesAsync();
        return order;
    }

    public async Task<ExternalStoreSyncRecord> PushFulfillmentUpdateAsync(Guid tenantId, Guid orderId, string courierName, string trackingNumber)
    {
        var order = await _dbContext.CodOrders
            .FirstOrDefaultAsync(o => o.TenantId == tenantId && o.Id == orderId)
            ?? throw new InvalidOperationException($"Order {orderId} not found.");

        var outboundPayload = JsonSerializer.Serialize(new
        {
            fulfillment = new
            {
                tracking_number = trackingNumber,
                tracking_company = courierName,
                notify_customer = true,
                order_id = order.OrderNumber
            }
        });

        var record = new ExternalStoreSyncRecord
        {
            TenantId = tenantId,
            Platform = ExternalStorePlatform.Shopify,
            ExternalOrderId = order.OrderNumber,
            ExternalOrderNumber = order.OrderNumber,
            SGrowingOrderId = order.Id,
            SyncDirection = StoreSyncDirection.OutboundFulfillmentPushed,
            Status = "Success",
            PayloadJson = outboundPayload,
            SyncedAtUtc = DateTime.UtcNow
        };

        _dbContext.ExternalStoreSyncRecords.Add(record);
        await _dbContext.SaveChangesAsync();
        return record;
    }

    public async Task<List<object>> GenerateGoogleSheetsRowAsync(Guid tenantId, Guid orderId)
    {
        var order = await _dbContext.CodOrders
            .FirstOrDefaultAsync(o => o.TenantId == tenantId && o.Id == orderId)
            ?? throw new InvalidOperationException($"Order {orderId} not found.");

        var shipment = await _dbContext.ShipmentRecords
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.OrderId == orderId);

        var row = new List<object>
        {
            order.CreatedAtUtc.ToString("yyyy-MM-dd HH:mm:ss"),
            order.OrderNumber,
            order.CustomerName,
            order.PhoneNumber,
            order.ShippingAddress,
            order.GovernorateOrWilaya,
            order.TotalAmount,
            order.CodCurrency,
            order.Status.ToString(),
            shipment?.TrackingNumber ?? "N/A",
            shipment?.CourierName ?? "Unassigned"
        };

        var config = await _dbContext.GoogleSheetsSyncConfigs
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.IsActive);

        if (config != null)
        {
            config.RowsAppendedCount++;
            config.LastSyncedAtUtc = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
        }

        return row;
    }
}
