using System.Text.Json.Serialization;

namespace SGrowing.Client.Models;

/// <summary>
/// Supported authentication strategies for third-party commerce connectors.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ConnectorAuthType
{
    OAuth2,
    ApiKey,
    WebhookSecret,
    CustomToken
}

/// <summary>
/// Operational tier and deployment scope of the e-commerce provider.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SupportedPlatformTier
{
    RegionalMENA,
    GlobalEnterprise,
    OpenSource
}

/// <summary>
/// Pluggable connector manifest definition specifying platform metadata, auth mechanisms, and supported capabilities.
/// </summary>
public record ConnectorManifest(
    [property: JsonPropertyName("connectorId")] string ConnectorId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("iconUrl")] string IconUrl,
    [property: JsonPropertyName("documentationUrl")] string DocumentationUrl,
    [property: JsonPropertyName("authType")] ConnectorAuthType AuthType,
    [property: JsonPropertyName("tier")] SupportedPlatformTier Tier,
    [property: JsonPropertyName("supportedEntities")] List<string> SupportedEntities,
    [property: JsonPropertyName("webhookTopics")] List<string> WebhookTopics,
    [property: JsonPropertyName("apiVersion")] string ApiVersion
);

/// <summary>
/// Canonical representation of an order line item across all external platforms.
/// </summary>
public record CanonicalOrderItem(
    [property: JsonPropertyName("sku")] string Sku,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("quantity")] int Quantity,
    [property: JsonPropertyName("unitPrice")] decimal UnitPrice,
    [property: JsonPropertyName("totalPrice")] decimal TotalPrice
);

/// <summary>
/// Canonical normalized representation of an e-commerce order payload in SGrowing.
/// </summary>
public record CanonicalOrderPayload(
    [property: JsonPropertyName("externalOrderId")] string ExternalOrderId,
    [property: JsonPropertyName("orderNumber")] string OrderNumber,
    [property: JsonPropertyName("totalAmount")] decimal TotalAmount,
    [property: JsonPropertyName("currency")] string Currency,
    [property: JsonPropertyName("customerEmail")] string CustomerEmail,
    [property: JsonPropertyName("customerPhone")] string CustomerPhone,
    [property: JsonPropertyName("shippingCity")] string ShippingCity,
    [property: JsonPropertyName("shippingCountry")] string ShippingCountry,
    [property: JsonPropertyName("items")] List<CanonicalOrderItem> Items,
    [property: JsonPropertyName("orderDateUtc")] DateTime OrderDateUtc,
    [property: JsonPropertyName("taxAmount")] decimal TaxAmount = 0m
);

/// <summary>
/// Request payload to transform an external commerce provider order.
/// </summary>
public record TransformOrderRequest(
    [property: JsonPropertyName("rawJsonPayload")] string RawJsonPayload,
    [property: JsonPropertyName("fieldMappings")] Dictionary<string, string>? FieldMappings = null
);

/// <summary>
/// Outcome of dynamic payload transformation with canonical order or error diagnostic.
/// </summary>
public record TransformPayloadResult(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("entityType")] string EntityType,
    [property: JsonPropertyName("transformedOrder")] CanonicalOrderPayload? TransformedOrder,
    [property: JsonPropertyName("errorMessage")] string? ErrorMessage = null
);
