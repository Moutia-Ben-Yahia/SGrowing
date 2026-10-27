using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Commerce.Connectors;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Commerce;

public class CommerceConnectorsTests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: $"CommerceConnectorsTestDb_{Guid.NewGuid()}")
            .Options;
        return new SGrowingDbContext(options);
    }

    private static string ComputeHmacSha256(string body, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
        return Convert.ToBase64String(hash);
    }

    [Fact]
    public void ShopifyConnectorService_VerifyWebhookSignature_ShouldValidateCorrectHmacAndRejectTampered()
    {
        var db = CreateDbContext();
        var service = new ShopifyConnectorService(db);
        var secret = "shpss_test_secret_key_1234567890";
        var payload = "{\"id\": 1001, \"total_price\": \"149.99\", \"name\": \"#1001\"}";

        var validHmac = ComputeHmacSha256(payload, secret);

        // 1. Valid signature
        var isValid = service.VerifyWebhookSignature(payload, secret, validHmac);
        isValid.Should().BeTrue();

        // 2. Tampered payload
        var tamperedPayload = "{\"id\": 1001, \"total_price\": \"999.99\", \"name\": \"#1001\"}";
        var isTamperedValid = service.VerifyWebhookSignature(tamperedPayload, secret, validHmac);
        isTamperedValid.Should().BeFalse();

        // 3. Invalid signature
        var isFakeValid = service.VerifyWebhookSignature(payload, secret, "invalid_base64_hmac=");
        isFakeValid.Should().BeFalse();

        // 4. Empty arguments
        service.VerifyWebhookSignature("", secret, validHmac).Should().BeFalse();
        service.VerifyWebhookSignature(payload, "", validHmac).Should().BeFalse();
        service.VerifyWebhookSignature(payload, secret, "").Should().BeFalse();
    }

    [Fact]
    public async Task ShopifyConnectorService_ProcessOrderWebhookAsync_ShouldMapToCrmContactDealAndCodShipment()
    {
        var db = CreateDbContext();
        var service = new ShopifyConnectorService(db);
        var tenantId = Guid.NewGuid();

        var orderPayload = """
        {
            "id": 987654321,
            "name": "#SFY-4021",
            "order_number": 4021,
            "total_price": "285.50",
            "currency": "USD",
            "customer": {
                "first_name": "Sophie",
                "last_name": "Tremblay",
                "email": "sophie.tremblay@example.com",
                "phone": "+15145550199"
            },
            "shipping_address": {
                "address1": "742 Evergreen Terrace",
                "city": "Springfield",
                "province": "OR"
            }
        }
        """;

        var result = await service.ProcessOrderWebhookAsync(tenantId, "orders/create", orderPayload);

        result.Success.Should().BeTrue();
        result.OrderId.Should().Be("987654321");
        result.OrderNumber.Should().Be("#SFY-4021");
        result.Amount.Should().Be(285.50m);
        result.Currency.Should().Be("USD");
        result.DealId.Should().NotBeEmpty();
        result.ShipmentId.Should().NotBeEmpty();

        // Verify Contact was created with tags
        var contact = await db.Contacts.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Email == "sophie.tremblay@example.com");
        contact.Should().NotBeNull();
        contact!.FirstName.Should().Be("Sophie");
        contact.LastName.Should().Be("Tremblay");
        contact.Tags.Should().Contain("shopify");

        // Verify Deal was created
        var deal = await db.Deals.FirstOrDefaultAsync(d => d.Id == result.DealId);
        deal.Should().NotBeNull();
        deal!.Amount.Should().Be(285.50m);
        deal.Stage.Should().Be(DealStage.Won);
        deal.Title.Should().Be("Shopify Order #SFY-4021");

        // Verify COD Order was created
        var codOrder = await db.CodOrders.FirstOrDefaultAsync(o => o.TenantId == tenantId && o.OrderNumber == "#SFY-4021");
        codOrder.Should().NotBeNull();
        codOrder!.TotalAmount.Should().Be(285.50m);
        codOrder.ShippingAddress.Should().Contain("Springfield");

        // Verify Shipment was dispatched
        var shipment = await db.ShipmentRecords.FirstOrDefaultAsync(s => s.Id == result.ShipmentId);
        shipment.Should().NotBeNull();
        shipment!.CourierName.Should().Be("Shopify Express Logistics");
        shipment.CashToCollect.Should().Be(285.50m);
    }

    [Fact]
    public async Task ShopifyConnectorService_CatalogSyncAndStatus_ShouldSyncProductsAndReportMetrics()
    {
        var db = CreateDbContext();
        var service = new ShopifyConnectorService(db);
        var tenantId = Guid.NewGuid();

        // 1. Trigger catalog sync
        var syncResult = await service.TriggerCatalogSyncAsync(tenantId);
        syncResult.Success.Should().BeTrue();
        syncResult.ProductsSynced.Should().Be(3);
        syncResult.VariantsSynced.Should().Be(3);

        // 2. Second sync should be idempotent (no duplicate products)
        var secondSyncResult = await service.TriggerCatalogSyncAsync(tenantId);
        secondSyncResult.Success.Should().BeTrue();
        secondSyncResult.ProductsSynced.Should().Be(0);

        // 3. Status check
        var status = await service.GetConnectorStatusAsync(tenantId);
        status.Platform.Should().Be("Shopify");
        status.IsConnected.Should().BeTrue();
        status.CatalogProductsCount.Should().Be(3);
    }

    [Fact]
    public void WooCommerceConnectorService_VerifyWebhookSignature_ShouldValidateCorrectHmacAndRejectTampered()
    {
        var db = CreateDbContext();
        var service = new WooCommerceConnectorService(db);
        var secret = "wc_webhook_secret_abc123xyz";
        var payload = "{\"id\": 5002, \"total\": \"179.00\", \"number\": \"5002\"}";

        var validSignature = ComputeHmacSha256(payload, secret);

        var isValid = service.VerifyWebhookSignature(payload, secret, validSignature);
        isValid.Should().BeTrue();

        var isTamperedValid = service.VerifyWebhookSignature(payload + "tampered", secret, validSignature);
        isTamperedValid.Should().BeFalse();

        service.VerifyWebhookSignature("", secret, validSignature).Should().BeFalse();
        service.VerifyWebhookSignature(payload, "", validSignature).Should().BeFalse();
        service.VerifyWebhookSignature(payload, secret, "").Should().BeFalse();
    }

    [Fact]
    public async Task WooCommerceConnectorService_ProcessOrderWebhookAsync_ShouldMapOrderWithLineItemsToDealAndShipment()
    {
        var db = CreateDbContext();
        var service = new WooCommerceConnectorService(db);
        var tenantId = Guid.NewGuid();

        var orderPayload = """
        {
            "id": 88301,
            "number": "WC-88301",
            "total": "418.00",
            "currency": "EUR",
            "billing": {
                "first_name": "Jean-Luc",
                "last_name": "Picard",
                "email": "jeanluc.picard@starfleet.org",
                "phone": "+33612345678",
                "address_1": "10 Rue de la Paix",
                "city": "Paris",
                "state": "IDF"
            },
            "line_items": [
                {
                    "id": 1,
                    "name": "Omnichannel Suite License",
                    "product_id": 101,
                    "quantity": 1,
                    "total": "329.00"
                },
                {
                    "id": 2,
                    "name": "SIP Gateway Addon",
                    "product_id": 102,
                    "quantity": 1,
                    "total": "89.00"
                }
            ]
        }
        """;

        var result = await service.ProcessOrderWebhookAsync(tenantId, "order.created", orderPayload);

        result.Success.Should().BeTrue();
        result.OrderId.Should().Be("88301");
        result.OrderNumber.Should().Be("WC-88301");
        result.Amount.Should().Be(418.00m);
        result.Currency.Should().Be("EUR");
        result.LineItemsCount.Should().Be(2);
        result.DealId.Should().NotBeEmpty();
        result.ShipmentId.Should().NotBeEmpty();

        // Verify Contact created
        var contact = await db.Contacts.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Email == "jeanluc.picard@starfleet.org");
        contact.Should().NotBeNull();
        contact!.FirstName.Should().Be("Jean-Luc");
        contact.LastName.Should().Be("Picard");
        contact.Tags.Should().Contain("woocommerce");

        // Verify Deal
        var deal = await db.Deals.FirstOrDefaultAsync(d => d.Id == result.DealId);
        deal.Should().NotBeNull();
        deal!.Amount.Should().Be(418.00m);
        deal.Title.Should().Be("WooCommerce Order #WC-88301");

        // Verify COD Order with line items
        var codOrder = await db.CodOrders.FirstOrDefaultAsync(o => o.TenantId == tenantId && o.OrderNumber == "WC-88301");
        codOrder.Should().NotBeNull();
        codOrder!.TotalAmount.Should().Be(418.00m);
        codOrder.CodCurrency.Should().Be("EUR");
        codOrder.ItemsJson.Should().Contain("Omnichannel Suite License");

        // Verify Shipment
        var shipment = await db.ShipmentRecords.FirstOrDefaultAsync(s => s.Id == result.ShipmentId);
        shipment.Should().NotBeNull();
        shipment!.CourierName.Should().Be("WooCommerce Parcel Network");
    }

    [Fact]
    public async Task WooCommerceConnectorService_CatalogSyncAndStatus_ShouldSyncProductsAndReportMetrics()
    {
        var db = CreateDbContext();
        var service = new WooCommerceConnectorService(db);
        var tenantId = Guid.NewGuid();

        // 1. Sync catalog
        var syncResult = await service.TriggerCatalogSyncAsync(tenantId);
        syncResult.Success.Should().BeTrue();
        syncResult.ProductsSynced.Should().Be(3);
        syncResult.VariantsSynced.Should().Be(3);

        // 2. Status
        var status = await service.GetConnectorStatusAsync(tenantId);
        status.Platform.Should().Be("WooCommerce");
        status.IsConnected.Should().BeTrue();
        status.CatalogProductsCount.Should().Be(3);
    }

    [Fact]
    public void VerifyWebhookSignature_WithMalformedBase64OrNonAscii_ShouldReturnFalseWithoutThrowing()
    {
        var db = CreateDbContext();
        var shopifyService = new ShopifyConnectorService(db);
        var wooService = new WooCommerceConnectorService(db);

        var malformedInputs = new[]
        {
            "not-base-64!!!",
            "====",
            "A",
            "ABC",
            "\u0000\u0001\u0002",
            "sha256=invalid-hex-or-base64",
            new string('X', 1000)
        };

        foreach (var badSig in malformedInputs)
        {
            shopifyService.VerifyWebhookSignature("{}", "secret", badSig).Should().BeFalse();
            wooService.VerifyWebhookSignature("{}", "secret", badSig).Should().BeFalse();
        }
    }

    [Fact]
    public async Task ProcessOrderWebhookAsync_WhenOrderNumberHasNoLeadingHash_ShouldFormatDealTitleWithSingleHash()
    {
        var db = CreateDbContext();
        var service = new ShopifyConnectorService(db);
        var tenantId = Guid.NewGuid();

        var payload = """
        {
            "id": 999111,
            "name": "ORDER-999111",
            "order_number": 999111,
            "total_price": "120.00",
            "currency": "USD"
        }
        """;

        var result = await service.ProcessOrderWebhookAsync(tenantId, "orders/create", payload);
        result.Success.Should().BeTrue();

        var deal = await db.Deals.FirstOrDefaultAsync(d => d.Id == result.DealId);
        deal.Should().NotBeNull();
        deal!.Title.Should().Be("Shopify Order #ORDER-999111");
    }
}
