using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;

namespace SGrowing.Api.Modules.Commerce.Connectors.Marketplace;

/// <summary>
/// Service interface defining marketplace registry, tenant connector installs, and dynamic payload transformation.
/// </summary>
public interface IConnectorMarketplaceService
{
    /// <summary>
    /// Returns the catalogue of all supported e-commerce platform connector manifests.
    /// </summary>
    IReadOnlyList<ConnectorManifest> GetAvailableManifests();

    /// <summary>
    /// Retrieves a connector manifest by its identifier.
    /// </summary>
    ConnectorManifest? GetManifest(string connectorId);

    /// <summary>
    /// Installs and activates a connector for a merchant tenant.
    /// </summary>
    InstalledConnectorDto InstallConnector(Guid tenantId, ConnectorInstallRequest request);

    /// <summary>
    /// Lists all installed connectors for a merchant tenant.
    /// </summary>
    List<InstalledConnectorDto> GetInstalledConnectors(Guid tenantId);

    /// <summary>
    /// Dynamically transforms provider-specific order JSON into canonical SGrowing format.
    /// </summary>
    TransformPayloadResult TransformOrderPayload(string connectorId, string rawJsonPayload, Dictionary<string, string>? customFieldMappings = null);
}

/// <summary>
/// Pluggable registry and transformation service for regional and global e-commerce connectors.
/// </summary>
public class ConnectorMarketplaceService : IConnectorMarketplaceService
{
    private readonly Dictionary<string, ConnectorManifest> _manifests = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<Guid, List<InstalledConnectorDto>> _installedConnectors = new();

    public ConnectorMarketplaceService()
    {
        RegisterDefaultManifests();
    }

    public IReadOnlyList<ConnectorManifest> GetAvailableManifests()
    {
        return _manifests.Values.ToList().AsReadOnly();
    }

    public ConnectorManifest? GetManifest(string connectorId)
    {
        if (string.IsNullOrWhiteSpace(connectorId))
            return null;

        return _manifests.TryGetValue(connectorId.Trim(), out var manifest) ? manifest : null;
    }

    public InstalledConnectorDto InstallConnector(Guid tenantId, ConnectorInstallRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var manifest = GetManifest(request.ConnectorId);
        var connectorName = manifest?.Name ?? request.ConnectorId;

        var installation = new InstalledConnectorDto(
            InstallationId: Guid.NewGuid(),
            TenantId: tenantId,
            ConnectorId: request.ConnectorId.Trim().ToLowerInvariant(),
            ConnectorName: connectorName,
            IsActive: true,
            FieldMappings: request.FieldMappings != null 
                ? new Dictionary<string, string>(request.FieldMappings, StringComparer.OrdinalIgnoreCase) 
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            InstalledAtUtc: DateTime.UtcNow
        );

        _installedConnectors.AddOrUpdate(
            tenantId,
            _ => [installation],
            (_, existingList) =>
            {
                lock (existingList)
                {
                    existingList.RemoveAll(x => x.ConnectorId.Equals(request.ConnectorId, StringComparison.OrdinalIgnoreCase));
                    existingList.Add(installation);
                }
                return existingList;
            });

        return installation;
    }

    public List<InstalledConnectorDto> GetInstalledConnectors(Guid tenantId)
    {
        if (_installedConnectors.TryGetValue(tenantId, out var list))
        {
            lock (list)
            {
                return [.. list];
            }
        }

        return [];
    }

    public TransformPayloadResult TransformOrderPayload(string connectorId, string rawJsonPayload, Dictionary<string, string>? customFieldMappings = null)
    {
        if (string.IsNullOrWhiteSpace(rawJsonPayload))
        {
            return new TransformPayloadResult(false, "Order", null, "Payload cannot be empty.");
        }

        try
        {
            using var doc = JsonDocument.Parse(rawJsonPayload);
            var root = doc.RootElement;

            // Detect if wrapped in "data" object (common for Salla, Zid, and standard webhook envelopes)
            JsonElement primaryScope = root;
            JsonElement? fallbackScope = null;

            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var dataElem) && dataElem.ValueKind == JsonValueKind.Object)
            {
                primaryScope = dataElem;
                fallbackScope = root;
            }

            var canonicalId = ExtractString(
                primaryScope, fallbackScope,
                BuildCandidatePaths("ExternalOrderId", customFieldMappings, "id", "data.id", "order_id", "reference_id", "order.id"))
                ?? Guid.NewGuid().ToString();

            var orderNumber = ExtractString(
                primaryScope, fallbackScope,
                BuildCandidatePaths("OrderNumber", customFieldMappings, "order_number", "reference_id", "number", "name", "code", "id"))
                ?? canonicalId;

            var totalAmount = ExtractDecimal(
                primaryScope, fallbackScope,
                BuildCandidatePaths("TotalAmount", customFieldMappings, "total.amount", "total_price", "amounts.total.amount", "total", "order_total", "current_total_price", "grand_total"));

            var defaultCurrency = connectorId.ToLowerInvariant() switch
            {
                "salla" or "zid" => "SAR",
                "youcan" => "MAD",
                _ => "USD"
            };

            var currency = ExtractString(
                primaryScope, fallbackScope,
                BuildCandidatePaths("Currency", customFieldMappings, "total.currency", "currency", "currency_code", "data.currency"))
                ?? defaultCurrency;

            var email = ExtractString(
                primaryScope, fallbackScope,
                BuildCandidatePaths("CustomerEmail", customFieldMappings, "customer.email", "email", "contact_email", "billing.email", "buyer.email", "client_email"))
                ?? string.Empty;

            var phone = ExtractString(
                primaryScope, fallbackScope,
                BuildCandidatePaths("CustomerPhone", customFieldMappings, "customer.mobile", "customer.phone", "customer.telephone", "phone", "mobile", "shipping_address.phone", "billing.phone"))
                ?? string.Empty;

            var city = ExtractString(
                primaryScope, fallbackScope,
                BuildCandidatePaths("ShippingCity", customFieldMappings, "shipping.address.city", "shipping_address.city", "shipping.city", "customer.city", "customer_city", "billing.city", "city"))
                ?? string.Empty;

            var country = ExtractString(
                primaryScope, fallbackScope,
                BuildCandidatePaths("ShippingCountry", customFieldMappings, "shipping.address.country", "shipping_address.country", "shipping.country", "customer.country", "customer_country", "billing.country", "country"))
                ?? (connectorId.Equals("salla", StringComparison.OrdinalIgnoreCase) || connectorId.Equals("zid", StringComparison.OrdinalIgnoreCase) ? "Saudi Arabia" : string.Empty);

            var orderDateUtc = ExtractDateTime(
                primaryScope, fallbackScope,
                BuildCandidatePaths("OrderDateUtc", customFieldMappings, "created_at", "data.created_at", "processed_at", "order_date", "date_created"));

            var items = ExtractItems(primaryScope, fallbackScope, customFieldMappings);

            var canonicalOrder = new CanonicalOrderPayload(
                ExternalOrderId: canonicalId,
                OrderNumber: orderNumber,
                TotalAmount: totalAmount,
                Currency: currency,
                CustomerEmail: email,
                CustomerPhone: phone,
                ShippingCity: city,
                ShippingCountry: country,
                Items: items,
                OrderDateUtc: orderDateUtc
            );

            return new TransformPayloadResult(true, "Order", canonicalOrder);
        }
        catch (JsonException jEx)
        {
            return new TransformPayloadResult(false, "Order", null, $"Malformed JSON payload: {jEx.Message}");
        }
        catch (Exception ex)
        {
            return new TransformPayloadResult(false, "Order", null, $"Transformation failed: {ex.Message}");
        }
    }

    private void RegisterDefaultManifests()
    {
        // 1. Shopify
        _manifests["shopify"] = new ConnectorManifest(
            ConnectorId: "shopify",
            Name: "Shopify Global Storefront",
            Description: "Enterprise multi-channel commerce connector with bidirectional catalog and inventory sync, and real-time order webhook ingestion.",
            Category: "Global Enterprise Commerce",
            IconUrl: "https://assets.sgrowing.io/connectors/shopify.svg",
            DocumentationUrl: "https://docs.sgrowing.io/connectors/shopify",
            AuthType: ConnectorAuthType.OAuth2,
            Tier: SupportedPlatformTier.GlobalEnterprise,
            SupportedEntities: ["Products", "Orders", "Customers", "Inventory"],
            WebhookTopics: ["orders/create", "products/update", "inventory_levels/update"],
            ApiVersion: "2024-01"
        );

        // 2. Salla
        _manifests["salla"] = new ConnectorManifest(
            ConnectorId: "salla",
            Name: "Salla MENA E-Commerce Platform",
            Description: "Leading regional Saudi and MENA commerce platform integration with COD support, local payment gateways, and shipment tracking.",
            Category: "Regional MENA Commerce",
            IconUrl: "https://assets.sgrowing.io/connectors/salla.svg",
            DocumentationUrl: "https://docs.sgrowing.io/connectors/salla",
            AuthType: ConnectorAuthType.OAuth2,
            Tier: SupportedPlatformTier.RegionalMENA,
            SupportedEntities: ["Products", "Orders", "Customers", "Inventory"],
            WebhookTopics: ["order.created", "product.updated", "shipment.created"],
            ApiVersion: "v2"
        );

        // 3. Zid
        _manifests["zid"] = new ConnectorManifest(
            ConnectorId: "zid",
            Name: "Zid Saudi Retail Cloud",
            Description: "Cloud retail enablement ecosystem for Saudi merchants featuring headless storefronts, branch inventory, and COD tracking.",
            Category: "Regional MENA Commerce",
            IconUrl: "https://assets.sgrowing.io/connectors/zid.svg",
            DocumentationUrl: "https://docs.sgrowing.io/connectors/zid",
            AuthType: ConnectorAuthType.ApiKey,
            Tier: SupportedPlatformTier.RegionalMENA,
            SupportedEntities: ["Products", "Orders", "Customers", "Inventory"],
            WebhookTopics: ["order_create", "order_status_change"],
            ApiVersion: "v1"
        );

        // 4. YouCan
        _manifests["youcan"] = new ConnectorManifest(
            ConnectorId: "youcan",
            Name: "YouCan North Africa Commerce",
            Description: "High-conversion COD commerce platform optimized for Morocco, Algeria, Egypt, and North African cross-border logistics.",
            Category: "Regional MENA Commerce",
            IconUrl: "https://assets.sgrowing.io/connectors/youcan.svg",
            DocumentationUrl: "https://docs.sgrowing.io/connectors/youcan",
            AuthType: ConnectorAuthType.ApiKey,
            Tier: SupportedPlatformTier.RegionalMENA,
            SupportedEntities: ["Products", "Orders", "Customers", "Inventory"],
            WebhookTopics: ["order.placed", "inventory.deducted"],
            ApiVersion: "v1"
        );

        // 5. WooCommerce
        _manifests["woocommerce"] = new ConnectorManifest(
            ConnectorId: "woocommerce",
            Name: "WooCommerce WordPress Store",
            Description: "Open-source WordPress e-commerce store connector with REST API synchronizer, custom checkout fields, and webhook dispatcher.",
            Category: "Open Source Commerce",
            IconUrl: "https://assets.sgrowing.io/connectors/woocommerce.svg",
            DocumentationUrl: "https://docs.sgrowing.io/connectors/woocommerce",
            AuthType: ConnectorAuthType.CustomToken,
            Tier: SupportedPlatformTier.OpenSource,
            SupportedEntities: ["Products", "Orders", "Customers", "Inventory"],
            WebhookTopics: ["woocommerce_order_created"],
            ApiVersion: "v3"
        );
    }

    private static List<CanonicalOrderItem> ExtractItems(JsonElement primary, JsonElement? fallback, Dictionary<string, string>? customMappings)
    {
        var itemsList = new List<CanonicalOrderItem>();

        JsonElement arrayElem = default;
        bool foundArray = false;

        string[] candidateArrayProps = ["items", "line_items", "products", "variants"];

        if (customMappings != null && customMappings.TryGetValue("Items", out var mappedItemsProp))
        {
            if (TryGetElement(primary, mappedItemsProp, out var customElem) && customElem.ValueKind == JsonValueKind.Array)
            {
                arrayElem = customElem;
                foundArray = true;
            }
        }

        if (!foundArray)
        {
            foreach (var propName in candidateArrayProps)
            {
                if ((TryGetElement(primary, propName, out arrayElem) || (fallback.HasValue && TryGetElement(fallback.Value, propName, out arrayElem))) 
                    && arrayElem.ValueKind == JsonValueKind.Array)
                {
                    foundArray = true;
                    break;
                }
            }
        }

        if (!foundArray)
            return itemsList;

        foreach (var itemElem in arrayElem.EnumerateArray())
        {
            if (itemElem.ValueKind != JsonValueKind.Object)
                continue;

            var sku = ExtractString(itemElem, null, BuildCandidatePaths("Sku", customMappings, "sku", "code", "product_sku", "id")) ?? "UNKNOWN";
            var title = ExtractString(itemElem, null, BuildCandidatePaths("Title", customMappings, "name", "title", "product_name", "item_name")) ?? "Item";
            var quantity = ExtractInt(itemElem, null, BuildCandidatePaths("Quantity", customMappings, "quantity", "qty", "count"));
            if (quantity <= 0) quantity = 1;

            var unitPrice = ExtractDecimal(itemElem, null, BuildCandidatePaths("UnitPrice", customMappings, "amounts.price_without_tax.amount", "price.amount", "price", "unit_price", "amount"));
            var totalPrice = ExtractDecimal(itemElem, null, BuildCandidatePaths("TotalPrice", customMappings, "amounts.total.amount", "total.amount", "total", "line_price"));

            if (totalPrice == 0m && unitPrice > 0m)
            {
                totalPrice = unitPrice * quantity;
            }
            else if (unitPrice == 0m && totalPrice > 0m && quantity > 0)
            {
                unitPrice = Math.Round(totalPrice / quantity, 2);
            }

            itemsList.Add(new CanonicalOrderItem(sku, title, quantity, unitPrice, totalPrice));
        }

        return itemsList;
    }

    private static List<string> BuildCandidatePaths(string canonicalName, Dictionary<string, string>? customFieldMappings, params string[] defaultPaths)
    {
        var candidates = new List<string>();

        if (customFieldMappings != null)
        {
            foreach (var kvp in customFieldMappings)
            {
                if (kvp.Key.Equals(canonicalName, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(kvp.Value))
                {
                    candidates.Add(kvp.Value.Trim());
                }
                else if (kvp.Value.Equals(canonicalName, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(kvp.Key))
                {
                    candidates.Add(kvp.Key.Trim());
                }
            }
        }

        candidates.AddRange(defaultPaths);
        return candidates;
    }

    private static string? ExtractString(JsonElement root, JsonElement? fallbackScope, IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            if (TryGetElement(root, path, out var elem) || (fallbackScope.HasValue && TryGetElement(fallbackScope.Value, path, out elem)))
            {
                if (elem.ValueKind == JsonValueKind.String)
                    return elem.GetString();
                if (elem.ValueKind == JsonValueKind.Number)
                    return elem.GetRawText();
                if (elem.ValueKind is JsonValueKind.True or JsonValueKind.False)
                    return elem.GetRawText();
            }
        }
        return null;
    }

    private static decimal ExtractDecimal(JsonElement root, JsonElement? fallbackScope, IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            if (TryGetElement(root, path, out var elem) || (fallbackScope.HasValue && TryGetElement(fallbackScope.Value, path, out elem)))
            {
                if (elem.ValueKind == JsonValueKind.Number && elem.TryGetDecimal(out var dec))
                    return dec;
                if (elem.ValueKind == JsonValueKind.String && decimal.TryParse(elem.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
                    return parsed;
            }
        }
        return 0m;
    }

    private static int ExtractInt(JsonElement root, JsonElement? fallbackScope, IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            if (TryGetElement(root, path, out var elem) || (fallbackScope.HasValue && TryGetElement(fallbackScope.Value, path, out elem)))
            {
                if (elem.ValueKind == JsonValueKind.Number && elem.TryGetInt32(out var num))
                    return num;
                if (elem.ValueKind == JsonValueKind.String && int.TryParse(elem.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
                    return parsed;
            }
        }
        return 1;
    }

    private static DateTime ExtractDateTime(JsonElement root, JsonElement? fallbackScope, IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            if (TryGetElement(root, path, out var elem) || (fallbackScope.HasValue && TryGetElement(fallbackScope.Value, path, out elem)))
            {
                if (elem.ValueKind == JsonValueKind.String && DateTime.TryParse(elem.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var dt))
                    return dt;
            }
        }
        return DateTime.UtcNow;
    }

    private static bool TryGetElement(JsonElement root, string path, out JsonElement element)
    {
        var segments = path.Split(['.', '/'], StringSplitOptions.RemoveEmptyEntries);
        var current = root;

        foreach (var segment in segments)
        {
            if (current.ValueKind != JsonValueKind.Object)
            {
                element = default;
                return false;
            }

            bool matched = false;
            foreach (var prop in current.EnumerateObject())
            {
                if (prop.Name.Equals(segment, StringComparison.OrdinalIgnoreCase))
                {
                    current = prop.Value;
                    matched = true;
                    break;
                }
            }

            if (!matched)
            {
                element = default;
                return false;
            }
        }

        element = current;
        return true;
    }
}
