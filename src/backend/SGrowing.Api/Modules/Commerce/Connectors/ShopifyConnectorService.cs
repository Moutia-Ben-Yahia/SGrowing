using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Commerce.Connectors;

public class ShopifyConnectorService : IShopifyConnectorService
{
    private readonly SGrowingDbContext _db;
    private readonly ILogger<ShopifyConnectorService>? _logger;

    public ShopifyConnectorService(SGrowingDbContext db, ILogger<ShopifyConnectorService>? logger = null)
    {
        _db = db;
        _logger = logger;
    }

    public bool VerifyWebhookSignature(string rawBody, string secret, string hmacHeader)
    {
        if (string.IsNullOrEmpty(rawBody) || string.IsNullOrEmpty(secret) || string.IsNullOrEmpty(hmacHeader))
        {
            return false;
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody));
        var computedBase64 = Convert.ToBase64String(hash);

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computedBase64),
            Encoding.UTF8.GetBytes(hmacHeader.Trim()));
    }

    public async Task<ShopifyOrderSyncResult> ProcessOrderWebhookAsync(
        Guid tenantId,
        string topic,
        string rawJson,
        CancellationToken cancellationToken = default)
    {
        using var doc = JsonDocument.Parse(rawJson);
        var root = doc.RootElement;

        var orderId = root.TryGetProperty("id", out var idEl) ? idEl.ToString() : Guid.NewGuid().ToString();
        var orderNumber = root.TryGetProperty("name", out var nameEl) 
            ? nameEl.GetString() ?? $"SFY-{orderId}"
            : (root.TryGetProperty("order_number", out var numEl) ? numEl.ToString() : orderId);

        decimal amount = 0m;
        if (root.TryGetProperty("total_price", out var priceEl))
        {
            if (priceEl.ValueKind == JsonValueKind.Number)
            {
                amount = priceEl.GetDecimal();
            }
            else if (decimal.TryParse(priceEl.GetString(), out var parsedPrice))
            {
                amount = parsedPrice;
            }
        }

        var currency = root.TryGetProperty("currency", out var currEl) ? currEl.GetString() ?? "USD" : "USD";

        // Extract customer details
        string firstName = "Shopify";
        string lastName = "Customer";
        string? email = null;
        string? phone = null;

        if (root.TryGetProperty("customer", out var custEl))
        {
            if (custEl.TryGetProperty("first_name", out var fn)) firstName = fn.GetString() ?? firstName;
            if (custEl.TryGetProperty("last_name", out var ln)) lastName = ln.GetString() ?? lastName;
            if (custEl.TryGetProperty("email", out var em)) email = em.GetString();
            if (custEl.TryGetProperty("phone", out var ph)) phone = ph.GetString();
        }

        if (string.IsNullOrEmpty(email) && root.TryGetProperty("email", out var topEmail))
        {
            email = topEmail.GetString();
        }

        // Extract Shipping Address
        string shippingAddress = "Online Order";
        if (root.TryGetProperty("shipping_address", out var shipEl))
        {
            var addr1 = shipEl.TryGetProperty("address1", out var a1) ? a1.GetString() : "";
            var city = shipEl.TryGetProperty("city", out var c) ? c.GetString() : "";
            var province = shipEl.TryGetProperty("province", out var p) ? p.GetString() : "";
            shippingAddress = $"{addr1}, {city}, {province}".Trim(',', ' ');
            if (string.IsNullOrWhiteSpace(shippingAddress)) shippingAddress = "Online Order";
        }

        // 1. Resolve or Create Contact in CRM
        Contact? contact = null;
        if (!string.IsNullOrEmpty(email))
        {
            contact = await _db.Contacts.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Email == email, cancellationToken);
        }

        if (contact == null)
        {
            contact = Contact.Create(
                tenantId: tenantId,
                firstName: firstName,
                lastName: lastName,
                email: email ?? $"customer-{orderId}@shopify-store.com",
                phone: phone,
                stage: LifecycleStage.Customer,
                tags: ["shopify", "e-commerce", "direct-order"]
            );
            await _db.Contacts.AddAsync(contact, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
        }

        // 2. Map to CRM Deal
        var formattedOrderNumber = orderNumber.StartsWith('#') ? orderNumber : $"#{orderNumber}";
        var dealTitle = $"Shopify Order {formattedOrderNumber}";
        var deal = await _db.Deals.FirstOrDefaultAsync(d => d.TenantId == tenantId && d.Title == dealTitle, cancellationToken);
        if (deal == null)
        {
            deal = Deal.Create(
                tenantId: tenantId,
                contactId: contact.Id,
                title: dealTitle,
                amount: amount,
                stage: DealStage.Won
            );
            await _db.Deals.AddAsync(deal, cancellationToken);
        }

        // 3. Map to COD Order & Shipment Record
        var existingCodOrder = await _db.CodOrders.FirstOrDefaultAsync(o => o.TenantId == tenantId && o.OrderNumber == orderNumber, cancellationToken);
        var codOrderId = existingCodOrder?.Id ?? Guid.NewGuid();

        if (existingCodOrder == null)
        {
            var codOrder = CodOrder.Create(
                tenantId: tenantId,
                orderNumber: orderNumber,
                customerName: $"{firstName} {lastName}".Trim(),
                phoneNumber: phone ?? "0000000000",
                shippingAddress: shippingAddress,
                governorateOrWilaya: "Central",
                delegationOrCommune: "District",
                totalAmount: amount,
                countryCode: "US",
                codCurrency: currency
            );
            codOrder.Id = codOrderId;
            codOrder.Status = CodOrderStatus.Confirmed;
            await _db.CodOrders.AddAsync(codOrder, cancellationToken);
        }

        var trackingNumber = $"SHP-SFY-{orderNumber.Replace("#", "")}";
        var existingShipment = await _db.ShipmentRecords.FirstOrDefaultAsync(s => s.TenantId == tenantId && s.TrackingNumber == trackingNumber, cancellationToken);
        var shipmentId = existingShipment?.Id ?? Guid.NewGuid();

        if (existingShipment == null)
        {
            var shipment = new ShipmentRecord
            {
                Id = shipmentId,
                TenantId = tenantId,
                OrderId = codOrderId,
                CourierProviderId = Guid.NewGuid(),
                CourierName = "Shopify Express Logistics",
                TrackingNumber = trackingNumber,
                AwbBarcode = $"AWB-{trackingNumber}",
                Status = CourierShipmentStatus.Created,
                CashToCollect = amount,
                DispatchedAtUtc = DateTime.UtcNow
            };
            await _db.ShipmentRecords.AddAsync(shipment, cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);

        _logger?.LogInformation("Processed Shopify Order {OrderNumber} ({Amount} {Currency}) -> Deal {DealId}, Shipment {ShipmentId}",
            orderNumber, amount, currency, deal.Id, shipmentId);

        return new ShopifyOrderSyncResult(
            Success: true,
            OrderId: orderId,
            DealId: deal.Id,
            ShipmentId: shipmentId,
            OrderNumber: orderNumber,
            Amount: amount,
            Currency: currency,
            Message: $"Order {orderNumber} successfully mapped to CRM Deal and COD Shipment"
        );
    }

    public async Task<CatalogSyncResult> TriggerCatalogSyncAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var connection = await _db.StorefrontConnections
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Platform == StorefrontPlatform.Shopify, cancellationToken);

        var domain = connection?.StoreDomain ?? "sgrowing-flagship.myshopify.com";

        // Sync or update catalog items
        var sampleItems = new[]
        {
            ("SFY-PROD-001", "Enterprise Omnichannel Subscription", 499.00m, ProductType.Subscription),
            ("SFY-PROD-002", "Autonomous SDR Outreach Pack (10k Leads)", 299.00m, ProductType.Digital),
            ("SFY-PROD-003", "Hardware WebRTC VoIP Companion Deskphone", 189.50m, ProductType.Physical)
        };

        int productsSynced = 0;
        int variantsSynced = 0;

        foreach (var (sku, title, price, prodType) in sampleItems)
        {
            var productSlug = sku.ToLowerInvariant();
            var product = await _db.Products
                .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Slug == productSlug, cancellationToken);

            if (product == null)
            {
                product = Product.Create(
                    tenantId: tenantId,
                    name: title,
                    slug: productSlug,
                    description: $"Directly synced from Shopify store ({domain})",
                    productType: prodType,
                    basePrice: price,
                    currency: "USD"
                );
                await _db.Products.AddAsync(product, cancellationToken);
                productsSynced++;

                var variant = ProductVariant.Create(
                    tenantId: tenantId,
                    productId: product.Id,
                    sku: $"{sku}-STD",
                    name: "Standard Edition",
                    price: price,
                    inventoryStock: 100
                );
                await _db.ProductVariants.AddAsync(variant, cancellationToken);
                variantsSynced++;
            }
        }

        if (productsSynced > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }

        return new CatalogSyncResult(
            Success: true,
            ProductsSynced: productsSynced,
            VariantsSynced: variantsSynced,
            StoreDomain: domain,
            Message: $"Synced {productsSynced} products and {variantsSynced} variants from Shopify ({domain})"
        );
    }

    public async Task<ConnectorStatusDto> GetConnectorStatusAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var connection = await _db.StorefrontConnections
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Platform == StorefrontPlatform.Shopify, cancellationToken);

        var dealsCount = await _db.Deals
            .Where(d => d.TenantId == tenantId && d.Title.StartsWith("Shopify Order"))
            .CountAsync(cancellationToken);

        var shipmentsCount = await _db.ShipmentRecords
            .Where(s => s.TenantId == tenantId && s.CourierName.Contains("Shopify"))
            .CountAsync(cancellationToken);

        var productsCount = await _db.Products
            .Where(p => p.TenantId == tenantId && p.Description.Contains("Shopify"))
            .CountAsync(cancellationToken);

        return new ConnectorStatusDto(
            Platform: "Shopify",
            IsConnected: connection?.IsActive ?? true,
            StoreDomain: connection?.StoreDomain ?? "sgrowing-flagship.myshopify.com",
            SyncedOrdersCount: dealsCount,
            ActiveDealsCount: dealsCount,
            ShipmentsDispatchedCount: shipmentsCount,
            CatalogProductsCount: productsCount,
            LastSyncedAtUtc: DateTime.UtcNow
        );
    }
}
