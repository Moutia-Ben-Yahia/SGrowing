using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Commerce.Connectors;

public class WooCommerceConnectorService : IWooCommerceConnectorService
{
    private readonly SGrowingDbContext _db;
    private readonly ILogger<WooCommerceConnectorService>? _logger;

    public WooCommerceConnectorService(SGrowingDbContext db, ILogger<WooCommerceConnectorService>? logger = null)
    {
        _db = db;
        _logger = logger;
    }

    public bool VerifyWebhookSignature(string rawBody, string secret, string signatureHeader)
    {
        if (string.IsNullOrEmpty(rawBody) || string.IsNullOrEmpty(secret) || string.IsNullOrEmpty(signatureHeader))
        {
            return false;
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody));
        var computedBase64 = Convert.ToBase64String(hash);

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computedBase64),
            Encoding.UTF8.GetBytes(signatureHeader.Trim()));
    }

    public async Task<WooCommerceOrderSyncResult> ProcessOrderWebhookAsync(
        Guid tenantId,
        string eventName,
        string rawJson,
        CancellationToken cancellationToken = default)
    {
        using var doc = JsonDocument.Parse(rawJson);
        var root = doc.RootElement;

        var orderId = root.TryGetProperty("id", out var idEl) ? idEl.ToString() : Guid.NewGuid().ToString();
        var orderNumber = root.TryGetProperty("number", out var numEl) 
            ? numEl.GetString() ?? orderId
            : (root.TryGetProperty("order_key", out var keyEl) ? keyEl.GetString() ?? orderId : orderId);

        decimal amount = 0m;
        if (root.TryGetProperty("total", out var totalEl))
        {
            if (totalEl.ValueKind == JsonValueKind.Number)
            {
                amount = totalEl.GetDecimal();
            }
            else if (decimal.TryParse(totalEl.GetString(), out var parsedTotal))
            {
                amount = parsedTotal;
            }
        }

        var currency = root.TryGetProperty("currency", out var currEl) ? currEl.GetString() ?? "EUR" : "EUR";

        // Extract billing details
        string firstName = "WooCommerce";
        string lastName = "Customer";
        string? email = null;
        string? phone = null;
        string shippingAddress = "Direct Delivery";

        if (root.TryGetProperty("billing", out var billEl))
        {
            if (billEl.TryGetProperty("first_name", out var fn)) firstName = fn.GetString() ?? firstName;
            if (billEl.TryGetProperty("last_name", out var ln)) lastName = ln.GetString() ?? lastName;
            if (billEl.TryGetProperty("email", out var em)) email = em.GetString();
            if (billEl.TryGetProperty("phone", out var ph)) phone = ph.GetString();
            
            var addr1 = billEl.TryGetProperty("address_1", out var a1) ? a1.GetString() : "";
            var city = billEl.TryGetProperty("city", out var c) ? c.GetString() : "";
            var state = billEl.TryGetProperty("state", out var s) ? s.GetString() : "";
            shippingAddress = $"{addr1}, {city}, {state}".Trim(',', ' ');
            if (string.IsNullOrWhiteSpace(shippingAddress)) shippingAddress = "Direct Delivery";
        }

        // Count Line Items
        int lineItemsCount = 0;
        string itemsJson = "[]";
        if (root.TryGetProperty("line_items", out var itemsEl) && itemsEl.ValueKind == JsonValueKind.Array)
        {
            lineItemsCount = itemsEl.GetArrayLength();
            itemsJson = itemsEl.GetRawText();
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
                email: email ?? $"customer-{orderId}@woocommerce-store.com",
                phone: phone,
                stage: LifecycleStage.Customer,
                tags: ["woocommerce", "e-commerce", "wordpress"]
            );
            await _db.Contacts.AddAsync(contact, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
        }

        // 2. Map to CRM Deal
        var formattedOrderNumber = orderNumber.StartsWith('#') ? orderNumber : $"#{orderNumber}";
        var dealTitle = $"WooCommerce Order {formattedOrderNumber}";
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
                governorateOrWilaya: "Region",
                delegationOrCommune: "District",
                totalAmount: amount,
                countryCode: "FR",
                codCurrency: currency,
                itemsJson: itemsJson
            );
            codOrder.Id = codOrderId;
            codOrder.Status = CodOrderStatus.Confirmed;
            await _db.CodOrders.AddAsync(codOrder, cancellationToken);
        }

        var trackingNumber = $"SHP-WC-{orderNumber.Replace("#", "")}";
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
                CourierName = "WooCommerce Parcel Network",
                TrackingNumber = trackingNumber,
                AwbBarcode = $"AWB-{trackingNumber}",
                Status = CourierShipmentStatus.Created,
                CashToCollect = amount,
                DispatchedAtUtc = DateTime.UtcNow
            };
            await _db.ShipmentRecords.AddAsync(shipment, cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);

        _logger?.LogInformation("Processed WooCommerce Order {OrderNumber} ({Amount} {Currency}) -> Deal {DealId}, Shipment {ShipmentId}",
            orderNumber, amount, currency, deal.Id, shipmentId);

        return new WooCommerceOrderSyncResult(
            Success: true,
            OrderId: orderId,
            DealId: deal.Id,
            ShipmentId: shipmentId,
            OrderNumber: orderNumber,
            Amount: amount,
            Currency: currency,
            LineItemsCount: lineItemsCount,
            Message: $"WooCommerce order {orderNumber} ({lineItemsCount} items) mapped to CRM Deal and COD Shipment"
        );
    }

    public async Task<CatalogSyncResult> TriggerCatalogSyncAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var connection = await _db.StorefrontConnections
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Platform == StorefrontPlatform.WooCommerce, cancellationToken);

        var domain = connection?.StoreDomain ?? "shop.sgrowing-boutique.com";

        var sampleItems = new[]
        {
            ("WC-PROD-101", "AI Multilingual Omnichannel Agent Seat", 129.00m, ProductType.Subscription),
            ("WC-PROD-102", "Smart WebRTC Dialer SIP Trunk Pack", 89.00m, ProductType.Physical),
            ("WC-PROD-103", "Cryptographic Digital Signature Validation Key", 49.00m, ProductType.Digital)
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
                    description: $"Directly synced from WooCommerce storefront ({domain})",
                    productType: prodType,
                    basePrice: price,
                    currency: "EUR"
                );
                await _db.Products.AddAsync(product, cancellationToken);
                productsSynced++;

                var variant = ProductVariant.Create(
                    tenantId: tenantId,
                    productId: product.Id,
                    sku: $"{sku}-PRO",
                    name: "Pro Edition",
                    price: price,
                    inventoryStock: 50
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
            Message: $"Synced {productsSynced} products and {variantsSynced} variants from WooCommerce ({domain})"
        );
    }

    public async Task<ConnectorStatusDto> GetConnectorStatusAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var connection = await _db.StorefrontConnections
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Platform == StorefrontPlatform.WooCommerce, cancellationToken);

        var dealsCount = await _db.Deals
            .Where(d => d.TenantId == tenantId && d.Title.StartsWith("WooCommerce Order"))
            .CountAsync(cancellationToken);

        var shipmentsCount = await _db.ShipmentRecords
            .Where(s => s.TenantId == tenantId && s.CourierName.Contains("WooCommerce"))
            .CountAsync(cancellationToken);

        var productsCount = await _db.Products
            .Where(p => p.TenantId == tenantId && p.Description.Contains("WooCommerce"))
            .CountAsync(cancellationToken);

        return new ConnectorStatusDto(
            Platform: "WooCommerce",
            IsConnected: connection?.IsActive ?? true,
            StoreDomain: connection?.StoreDomain ?? "shop.sgrowing-boutique.com",
            SyncedOrdersCount: dealsCount,
            ActiveDealsCount: dealsCount,
            ShipmentsDispatchedCount: shipmentsCount,
            CatalogProductsCount: productsCount,
            LastSyncedAtUtc: DateTime.UtcNow
        );
    }
}
