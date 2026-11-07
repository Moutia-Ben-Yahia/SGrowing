namespace SGrowing.Api.Modules.AI.Marketplace;

/// <summary>
/// Categorization of items available in the Agent & Workflow Marketplace.
/// </summary>
public enum MarketplaceItemType
{
    AgentSwarm,
    WorkflowTemplate,
    ConnectorExtension,
    PromptPack
}

/// <summary>
/// Verification and security tier of a marketplace listing.
/// </summary>
public enum MarketplaceTier
{
    Official,
    VerifiedPartner,
    Community
}

/// <summary>
/// A curated or community listing available in the marketplace catalog.
/// </summary>
public record MarketplaceListingItem(
    string Id,
    string Slug,
    string Name,
    string Tagline,
    string Description,
    MarketplaceItemType ItemType,
    MarketplaceTier Tier,
    string Author,
    double Rating,
    int InstallCount,
    decimal PriceSar,
    string Version,
    IReadOnlyList<string> Tags,
    string IconUrl,
    string ConfigurationSchemaJson,
    string ManifestPayloadJson,
    DateTime CreatedAtUtc
);

/// <summary>
/// Request to install a marketplace item into a tenant's workspace.
/// </summary>
public record InstallMarketplaceItemRequest(
    string TenantId,
    string ListingId,
    Dictionary<string, string>? CustomSettings = null
);

/// <summary>
/// Represents an active or historical installation of a marketplace item for a tenant.
/// </summary>
public record TenantInstalledItem(
    Guid InstallationId,
    string TenantId,
    string ListingId,
    string InstalledVersion,
    Dictionary<string, string> CustomSettings,
    DateTime InstalledAtUtc,
    bool IsActive = true
);
