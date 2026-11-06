using FluentAssertions;
using SGrowing.Api.Modules.Commerce.Connectors.Marketplace;
using Xunit;

namespace SGrowing.Tests.Unit;

public class ConnectorMarketplaceTests
{
    [Fact]
    public void GetAvailableManifests_ContainsAllFivePlatforms()
    {
        // Arrange
        var service = new ConnectorMarketplaceService();

        // Act
        var manifests = service.GetAvailableManifests();

        // Assert
        manifests.Should().HaveCount(5);

        // 1. Shopify
        var shopify = manifests.FirstOrDefault(m => m.ConnectorId == "shopify");
        shopify.Should().NotBeNull();
        shopify!.Name.Should().Be("Shopify Global Storefront");
        shopify.Tier.Should().Be(SupportedPlatformTier.GlobalEnterprise);
        shopify.AuthType.Should().Be(ConnectorAuthType.OAuth2);
        shopify.WebhookTopics.Should().Contain(["orders/create", "products/update", "inventory_levels/update"]);
        shopify.SupportedEntities.Should().Contain(["Products", "Orders", "Customers", "Inventory"]);

        // 2. Salla
        var salla = manifests.FirstOrDefault(m => m.ConnectorId == "salla");
        salla.Should().NotBeNull();
        salla!.Name.Should().Be("Salla MENA E-Commerce Platform");
        salla.Tier.Should().Be(SupportedPlatformTier.RegionalMENA);
        salla.AuthType.Should().Be(ConnectorAuthType.OAuth2);
        salla.WebhookTopics.Should().Contain(["order.created", "product.updated", "shipment.created"]);

        // 3. Zid
        var zid = manifests.FirstOrDefault(m => m.ConnectorId == "zid");
        zid.Should().NotBeNull();
        zid!.Name.Should().Be("Zid Saudi Retail Cloud");
        zid.Tier.Should().Be(SupportedPlatformTier.RegionalMENA);
        zid.AuthType.Should().Be(ConnectorAuthType.ApiKey);
        zid.WebhookTopics.Should().Contain(["order_create", "order_status_change"]);

        // 4. YouCan
        var youcan = manifests.FirstOrDefault(m => m.ConnectorId == "youcan");
        youcan.Should().NotBeNull();
        youcan!.Name.Should().Be("YouCan North Africa Commerce");
        youcan.Tier.Should().Be(SupportedPlatformTier.RegionalMENA);
        youcan.AuthType.Should().Be(ConnectorAuthType.ApiKey);
        youcan.WebhookTopics.Should().Contain(["order.placed", "inventory.deducted"]);

        // 5. WooCommerce
        var woo = manifests.FirstOrDefault(m => m.ConnectorId == "woocommerce");
        woo.Should().NotBeNull();
        woo!.Name.Should().Be("WooCommerce WordPress Store");
        woo.Tier.Should().Be(SupportedPlatformTier.OpenSource);
        woo.AuthType.Should().Be(ConnectorAuthType.CustomToken);
        woo.WebhookTopics.Should().Contain("woocommerce_order_created");

        // Single Lookup
        service.GetManifest("shopify").Should().NotBeNull();
        service.GetManifest("salla").Should().NotBeNull();
    }

    [Fact]
    public void TransformOrderPayload_WithSallaOrderJson_NormalizesToCanonicalOrder()
    {
        // Arrange
        var service = new ConnectorMarketplaceService();
        var sallaJson = """
        {
          "event": "order.created",
          "data": {
            "id": "salla-ord-9921",
            "reference_id": "ORD-9921",
            "total": {
              "amount": 420.50,
              "currency": "SAR"
            },
            "customer": {
              "first_name": "Tariq",
              "last_name": "Mansoor",
              "email": "customer@salla.sa",
              "mobile": "+966512345678"
            },
            "shipping": {
              "address": {
                "city": "Riyadh",
                "country": "Saudi Arabia"
              }
            },
            "items": [
              {
                "sku": "SKU-SA-101",
                "name": "Arabian Oud Oil",
                "quantity": 2,
                "amounts": {
                  "price_without_tax": {
                    "amount": 210.25
                  },
                  "total": {
                    "amount": 420.50
                  }
                }
              }
            ],
            "created_at": "2026-11-06T12:00:00Z"
          }
        }
        """;

        // Act
        var result = service.TransformOrderPayload("salla", sallaJson);

        // Assert
        result.Success.Should().BeTrue();
        result.EntityType.Should().Be("Order");
        result.TransformedOrder.Should().NotBeNull();

        var order = result.TransformedOrder!;
        order.ExternalOrderId.Should().Be("salla-ord-9921");
        order.OrderNumber.Should().Be("ORD-9921");
        order.TotalAmount.Should().Be(420.50m);
        order.Currency.Should().Be("SAR");
        order.CustomerEmail.Should().Be("customer@salla.sa");
        order.CustomerPhone.Should().Be("+966512345678");
        order.ShippingCity.Should().Be("Riyadh");
        order.ShippingCountry.Should().Be("Saudi Arabia");
        order.Items.Should().HaveCount(1);

        var item = order.Items[0];
        item.Sku.Should().Be("SKU-SA-101");
        item.Title.Should().Be("Arabian Oud Oil");
        item.Quantity.Should().Be(2);
        item.UnitPrice.Should().Be(210.25m);
        item.TotalPrice.Should().Be(420.50m);
    }

    [Fact]
    public void TransformOrderPayload_WithShopifyOrderJson_NormalizesToCanonicalOrder()
    {
        // Arrange
        var service = new ConnectorMarketplaceService();
        var shopifyJson = """
        {
          "id": 55891238491,
          "order_number": 8841,
          "name": "#8841",
          "total_price": "249.00",
          "currency": "USD",
          "email": "sarah.smith@example.com",
          "phone": "+14155552671",
          "shipping_address": {
            "city": "San Francisco",
            "country": "United States"
          },
          "line_items": [
            {
              "sku": "SHOPIFY-SKU-99",
              "title": "Ergonomic Desk Chair",
              "quantity": 1,
              "price": "249.00"
            }
          ],
          "created_at": "2026-11-06T11:45:00Z"
        }
        """;

        // Act
        var result = service.TransformOrderPayload("shopify", shopifyJson);

        // Assert
        result.Success.Should().BeTrue();
        result.EntityType.Should().Be("Order");
        result.TransformedOrder.Should().NotBeNull();

        var order = result.TransformedOrder!;
        order.ExternalOrderId.Should().Be("55891238491");
        order.OrderNumber.Should().Be("8841");
        order.TotalAmount.Should().Be(249.00m);
        order.Currency.Should().Be("USD");
        order.CustomerEmail.Should().Be("sarah.smith@example.com");
        order.CustomerPhone.Should().Be("+14155552671");
        order.ShippingCity.Should().Be("San Francisco");
        order.ShippingCountry.Should().Be("United States");
        order.Items.Should().HaveCount(1);

        var item = order.Items[0];
        item.Sku.Should().Be("SHOPIFY-SKU-99");
        item.Title.Should().Be("Ergonomic Desk Chair");
        item.Quantity.Should().Be(1);
        item.UnitPrice.Should().Be(249.00m);
        item.TotalPrice.Should().Be(249.00m);
    }

    [Fact]
    public void InstallConnector_RecordsInstallationWithCustomFieldMappings()
    {
        // Arrange
        var service = new ConnectorMarketplaceService();
        var tenantId = Guid.NewGuid();
        var fieldMappings = new Dictionary<string, string>
        {
            ["CustomerEmail"] = "buyer_contact.email_address",
            ["ShippingCity"] = "destination.city_name"
        };
        var request = new ConnectorInstallRequest(
            ConnectorId: "salla",
            Credentials: new Dictionary<string, string>
            {
                ["AccessToken"] = "oauth_token_12345",
                ["WebhookSecret"] = "secret_hash_abcdef"
            },
            FieldMappings: fieldMappings
        );

        // Act
        var installed = service.InstallConnector(tenantId, request);
        var tenantConnectors = service.GetInstalledConnectors(tenantId);

        // Assert
        installed.Should().NotBeNull();
        installed.TenantId.Should().Be(tenantId);
        installed.ConnectorId.Should().Be("salla");
        installed.ConnectorName.Should().Be("Salla MENA E-Commerce Platform");
        installed.IsActive.Should().BeTrue();
        installed.FieldMappings.Should().ContainKey("CustomerEmail");
        installed.FieldMappings["CustomerEmail"].Should().Be("buyer_contact.email_address");

        tenantConnectors.Should().HaveCount(1);
        tenantConnectors[0].InstallationId.Should().Be(installed.InstallationId);
    }

    [Fact]
    public void TransformOrderPayload_WithCustomFieldMappings_OverridesDefaultProperties()
    {
        // Arrange
        var service = new ConnectorMarketplaceService();
        var customPayload = """
        {
          "legacy_order_ref": "LEGACY-900",
          "grand_sum": "520.00",
          "buyer": {
            "primary_mail": "custom_buyer@example.com"
          },
          "destination_location": {
            "city": "Dubai",
            "country": "United Arab Emirates"
          },
          "items": [
            {
              "sku": "CUSTOM-01",
              "name": "Custom Widget",
              "quantity": 1,
              "price": 520.00
            }
          ]
        }
        """;

        var customMappings = new Dictionary<string, string>
        {
            ["ExternalOrderId"] = "legacy_order_ref",
            ["TotalAmount"] = "grand_sum",
            ["CustomerEmail"] = "buyer.primary_mail",
            ["ShippingCity"] = "destination_location.city",
            ["ShippingCountry"] = "destination_location.country"
        };

        // Act
        var result = service.TransformOrderPayload("custom-platform", customPayload, customMappings);

        // Assert
        result.Success.Should().BeTrue();
        var order = result.TransformedOrder!;
        order.ExternalOrderId.Should().Be("LEGACY-900");
        order.TotalAmount.Should().Be(520.00m);
        order.CustomerEmail.Should().Be("custom_buyer@example.com");
        order.ShippingCity.Should().Be("Dubai");
        order.ShippingCountry.Should().Be("United Arab Emirates");
    }

    [Fact]
    public void TransformOrderPayload_WithEmptyOrWhitespacePayload_ReturnsFailure()
    {
        // Arrange
        var service = new ConnectorMarketplaceService();

        // Act
        var result = service.TransformOrderPayload("shopify", "   ");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("cannot be empty");
    }

    [Fact]
    public void GetManifest_WithUnknownConnectorId_ReturnsNull()
    {
        // Arrange
        var service = new ConnectorMarketplaceService();

        // Act
        var manifest = service.GetManifest("non-existent-connector");

        // Assert
        manifest.Should().BeNull();
    }
}
