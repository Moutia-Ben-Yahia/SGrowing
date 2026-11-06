namespace SGrowing.Api.Modules.Commerce.Connectors.Marketplace;

/// <summary>
/// Supported authentication strategies for third-party commerce connectors.
/// </summary>
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
    string ConnectorId,
    string Name,
    string Description,
    string Category,
    string IconUrl,
    string DocumentationUrl,
    ConnectorAuthType AuthType,
    SupportedPlatformTier Tier,
    List<string> SupportedEntities,
    List<string> WebhookTopics,
    string ApiVersion
);

/// <summary>
/// Request to install and activate an integration connector for a specific tenant.
/// </summary>
public record ConnectorInstallRequest(
    string ConnectorId,
    Dictionary<string, string> Credentials,
    Dictionary<string, string>? FieldMappings = null
);

/// <summary>
/// Details of an active connector installation for a merchant tenant.
/// </summary>
public record InstalledConnectorDto(
    Guid InstallationId,
    Guid TenantId,
    string ConnectorId,
    string ConnectorName,
    bool IsActive,
    Dictionary<string, string> FieldMappings,
    DateTime InstalledAtUtc
);

/// <summary>
/// Canonical representation of an order line item across all external platforms.
/// </summary>
public record CanonicalOrderItem(
    string Sku,
    string Title,
    int Quantity,
    decimal UnitPrice,
    decimal TotalPrice
);

/// <summary>
/// Canonical normalized representation of an e-commerce order payload in SGrowing.
/// </summary>
public record CanonicalOrderPayload(
    string ExternalOrderId,
    string OrderNumber,
    decimal TotalAmount,
    string Currency,
    string CustomerEmail,
    string CustomerPhone,
    string ShippingCity,
    string ShippingCountry,
    List<CanonicalOrderItem> Items,
    DateTime OrderDateUtc
);

/// <summary>
/// Request payload to normalize an external provider entity into canonical format.
/// </summary>
public record TransformPayloadRequest(
    string ConnectorId,
    string EntityType,
    string RawJsonPayload
);

/// <summary>
/// Outcome of dynamic payload transformation with canonical order or error diagnostic.
/// </summary>
public record TransformPayloadResult(
    bool Success,
    string EntityType,
    CanonicalOrderPayload? TransformedOrder,
    string? ErrorMessage = null
);
