using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.AI.Marketplace;

/// <summary>
/// Service interface for discovering, inspecting, installing, and managing marketplace listings.
/// </summary>
public interface IAgentWorkflowMarketplaceService
{
    /// <summary>
    /// Searches and filters listings by keyword, item type, and partner tier.
    /// </summary>
    IReadOnlyList<MarketplaceListingItem> SearchListings(string? query = null, MarketplaceItemType? type = null, MarketplaceTier? tier = null);

    /// <summary>
    /// Retrieves a listing by ID or slug.
    /// </summary>
    MarketplaceListingItem? GetListingById(string id);

    /// <summary>
    /// Installs a listing into the specified tenant's workspace with optional custom settings.
    /// </summary>
    TenantInstalledItem InstallItem(string tenantId, string listingId, Dictionary<string, string>? customSettings = null);

    /// <summary>
    /// Uninstalls an installed item from the specified tenant's workspace.
    /// </summary>
    bool UninstallItem(string tenantId, string installationId);

    /// <summary>
    /// Retrieves all active installations for the specified tenant.
    /// </summary>
    IReadOnlyList<TenantInstalledItem> GetInstalledItems(string tenantId);
}

/// <summary>
/// In-memory catalog and multi-tenant installation service for agent swarms and workflow templates.
/// </summary>
public class AgentWorkflowMarketplaceService : IAgentWorkflowMarketplaceService
{
    private readonly ConcurrentDictionary<string, MarketplaceListingItem> _catalog = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<Guid, TenantInstalledItem> _installedItems = new();
    private readonly object _syncLock = new();

    public AgentWorkflowMarketplaceService()
    {
        SeedCatalog();
    }

    private void SeedCatalog()
    {
        var items = new List<MarketplaceListingItem>
        {
            new(
                Id: "item-ramadan-cod-swarm",
                Slug: "ksa-ramadan-cod-campaign-swarm",
                Name: "KSA Ramadan COD Campaign Swarm",
                Tagline: "Autonomous multi-agent swarm orchestrating localized Saudi Ramadan COD campaigns across WhatsApp, SMS, and Snapchat.",
                Description: "Coordinates SMS, WhatsApp, and social ad flash campaigns aligned with Iftar/Suhoor peak conversion hours with automated cash-on-delivery risk score verification and anti-RTO address hygiene.",
                ItemType: MarketplaceItemType.AgentSwarm,
                Tier: MarketplaceTier.Official,
                Author: "SGrowing Core Fleet",
                Rating: 5.0,
                InstallCount: 1250,
                PriceSar: 0m,
                Version: "2.4.0",
                Tags: new[] { "Ramadan", "COD", "Saudi Arabia", "WhatsApp", "Multi-Agent" },
                IconUrl: "sparkles",
                ConfigurationSchemaJson: """
                {
                  "type": "object",
                  "properties": {
                    "dailyBudgetCapSar": { "type": "number", "default": 2500 },
                    "suhoorWindowStartUtc": { "type": "string", "default": "01:00" },
                    "maxCodRiskScore": { "type": "number", "default": 0.35 },
                    "telephonyGateway": { "type": "string", "default": "Unifonic" }
                  },
                  "required": ["dailyBudgetCapSar"]
                }
                """,
                ManifestPayloadJson: """
                {
                  "swarmCoordinator": "EvoNexus-Swarm-V3",
                  "agents": [
                    { "role": "RamadanTimingPlannerAgent", "model": "OpenClaude-3-7-Sonnet" },
                    { "role": "ArabicDialectCopyAgent", "model": "GPT-4o-Localized" },
                    { "role": "CodRiskAssessorAgent", "model": "SGrowing-Risk-Scorer-v2" }
                  ],
                  "slaMinutes": 5
                }
                """,
                CreatedAtUtc: DateTime.UtcNow.AddMonths(-3)
            ),
            new(
                Id: "item-salla-cart-recovery",
                Slug: "salla-whatsapp-cart-recovery-engine",
                Name: "Salla WhatsApp Cart Recovery Engine",
                Tagline: "Automated event-triggered WhatsApp cart abandonment recovery flow optimized for Saudi Salla merchants.",
                Description: "Delivers hyper-personalized conversational recovery sequences via WhatsApp Official Cloud API, incorporating one-click Mada/Apple Pay links.",
                ItemType: MarketplaceItemType.WorkflowTemplate,
                Tier: MarketplaceTier.VerifiedPartner,
                Author: "Gulf Commerce Labs",
                Rating: 4.9,
                InstallCount: 890,
                PriceSar: 149m,
                Version: "1.8.2",
                Tags: new[] { "Salla", "WhatsApp", "Cart Recovery", "KSA", "Workflow" },
                IconUrl: "shopping-bag",
                ConfigurationSchemaJson: """
                {
                  "type": "object",
                  "properties": {
                    "delayMinutes": { "type": "integer", "default": 15 },
                    "discountPercentage": { "type": "number", "default": 10 },
                    "sendMadaDirectLink": { "type": "boolean", "default": true }
                  }
                }
                """,
                ManifestPayloadJson: """
                {
                  "engine": "DurableWorkflowEngine",
                  "trigger": "salla.cart.abandoned",
                  "steps": ["EvaluateCartValue", "CheckCustomerEligibility", "DispatchWhatsAppMessage", "MonitorCheckoutConversion"]
                }
                """,
                CreatedAtUtc: DateTime.UtcNow.AddMonths(-2)
            ),
            new(
                Id: "item-b2b-abm-hunter",
                Slug: "b2b-outbound-cold-abm-hunter",
                Name: "B2B Outbound Cold ABM Hunter",
                Tagline: "Autonomous account-based prospecting agent swarm with waterfall enrichment and personalized hyper-targeted outreach.",
                Description: "Orchestrates research, firmographic scoring, decision-maker extraction, and multi-touch email/LinkedIn sequences.",
                ItemType: MarketplaceItemType.AgentSwarm,
                Tier: MarketplaceTier.Official,
                Author: "SGrowing Enterprise AI",
                Rating: 4.8,
                InstallCount: 640,
                PriceSar: 299m,
                Version: "3.1.0",
                Tags: new[] { "B2B", "ABM", "Cold Outreach", "LinkedIn", "Enrichment" },
                IconUrl: "crosshair",
                ConfigurationSchemaJson: """
                {
                  "type": "object",
                  "properties": {
                    "targetIndustry": { "type": "string", "default": "Logistics & E-Commerce" },
                    "maxAccountsPerDay": { "type": "integer", "default": 50 },
                    "enrichmentSource": { "type": "string", "default": "ApolloAndLinkedIn" }
                  }
                }
                """,
                ManifestPayloadJson: """
                {
                  "swarmCoordinator": "EvoNexus-Swarm-V3",
                  "agents": [
                    { "role": "LeadDiscoveryAgent", "model": "OpenClaude-3-7-Sonnet" },
                    { "role": "FirmographicVerifierAgent", "model": "GPT-4o-Mini" },
                    { "role": "ColdSequenceDraftAgent", "model": "OpenClaude-3-7-Sonnet" }
                  ]
                }
                """,
                CreatedAtUtc: DateTime.UtcNow.AddMonths(-4)
            ),
            new(
                Id: "item-spintax-generator",
                Slug: "omnichannel-spintax-copy-generator",
                Name: "Omnichannel Spintax Copy Generator",
                Tagline: "High-variance spintax template generator for bulk SMS, email, and social ad copywriting.",
                Description: "Generates thousands of unique high-converting Arabic and English copywriting variations preventing spam filter fatigue.",
                ItemType: MarketplaceItemType.PromptPack,
                Tier: MarketplaceTier.Community,
                Author: "GrowthHackerDXB",
                Rating: 4.7,
                InstallCount: 420,
                PriceSar: 0m,
                Version: "1.2.0",
                Tags: new[] { "Spintax", "Copywriting", "Prompt", "Anti-Spam", "Community" },
                IconUrl: "type",
                ConfigurationSchemaJson: """
                {
                  "type": "object",
                  "properties": {
                    "defaultLanguage": { "type": "string", "default": "Arabic-Gulf" },
                    "varianceFactor": { "type": "number", "default": 0.85 }
                  }
                }
                """,
                ManifestPayloadJson: """
                {
                  "promptTemplates": [
                    "RamadanFlashOfferPrompt",
                    "AbandonedCartSpintaxPrompt",
                    "VipCustomerReactivationPrompt"
                  ]
                }
                """,
                CreatedAtUtc: DateTime.UtcNow.AddMonths(-1)
            ),
            new(
                Id: "item-seo-geo-citability",
                Slug: "seo-geo-aeo-citability-booster",
                Name: "SEO GEO/AEO Citability Booster",
                Tagline: "Generative Engine Optimization (GEO) & Answer Engine Optimization (AEO) citation booster swarm.",
                Description: "Analyzes AI search engines (Perplexity, ChatGPT Search, Google AI Overviews) and restructures digital catalog entities for top citability.",
                ItemType: MarketplaceItemType.AgentSwarm,
                Tier: MarketplaceTier.Official,
                Author: "SGrowing Search Intelligence",
                Rating: 5.0,
                InstallCount: 1500,
                PriceSar: 399m,
                Version: "4.0.1",
                Tags: new[] { "SEO", "GEO", "AEO", "Perplexity", "Knowledge Graph" },
                IconUrl: "search",
                ConfigurationSchemaJson: """
                {
                  "type": "object",
                  "properties": {
                    "crawlIntervalHours": { "type": "integer", "default": 24 },
                    "targetEngines": { "type": "string", "default": "Perplexity,ChatGPT,GoogleAI" },
                    "autoUpdateJsonLd": { "type": "boolean", "default": true }
                  }
                }
                """,
                ManifestPayloadJson: """
                {
                  "swarmCoordinator": "EvoNexus-Swarm-V3",
                  "agents": [
                    { "role": "PerplexityAuditAgent", "model": "OpenClaude-3-7-Sonnet" },
                    { "role": "SchemaGeneratorAgent", "model": "GPT-4o-Mini" },
                    { "role": "EntityGraphPublisherAgent", "model": "DeepSeek-R1" }
                  ]
                }
                """,
                CreatedAtUtc: DateTime.UtcNow.AddMonths(-5)
            ),
            new(
                Id: "item-shopify-flash-sale",
                Slug: "shopify-flash-sale-surge-orchestrator",
                Name: "Shopify Flash Sale Surge Orchestrator",
                Tagline: "High-throughput flash sale workflow managing live inventory locks, queue throttling, and abandoned checkouts.",
                Description: "Synchronizes Shopify inventory with edge Redis counters during Black Friday / National Day flash sales to eliminate overselling.",
                ItemType: MarketplaceItemType.WorkflowTemplate,
                Tier: MarketplaceTier.VerifiedPartner,
                Author: "HyperScale Partners",
                Rating: 4.9,
                InstallCount: 780,
                PriceSar: 199m,
                Version: "2.0.5",
                Tags: new[] { "Shopify", "Flash Sale", "High Concurrency", "Workflow", "Redis" },
                IconUrl: "zap",
                ConfigurationSchemaJson: """
                {
                  "type": "object",
                  "properties": {
                    "redisTtlSeconds": { "type": "integer", "default": 300 },
                    "maxConcurrentCheckouts": { "type": "integer", "default": 10000 },
                    "autoReplenishBuffer": { "type": "boolean", "default": false }
                  }
                }
                """,
                ManifestPayloadJson: """
                {
                  "engine": "DurableWorkflowEngine",
                  "trigger": "shopify.campaign.surge_detected",
                  "steps": ["AcquireRedisLock", "ValidateInventoryState", "ThrottleVirtualQueue", "ReleaseReservationLock"]
                }
                """,
                CreatedAtUtc: DateTime.UtcNow.AddMonths(-2)
            )
        };

        foreach (var item in items)
        {
            _catalog[item.Id] = item;
        }

        // Pre-seed an active install for default tenant
        var demoTenantId = "00000000-0000-0000-0000-000000000001";
        var demoInstall = new TenantInstalledItem(
            InstallationId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
            TenantId: demoTenantId,
            ListingId: "item-ramadan-cod-swarm",
            InstalledVersion: "2.4.0",
            CustomSettings: new Dictionary<string, string>
            {
                ["dailyBudgetCapSar"] = "2500",
                ["telephonyGateway"] = "Unifonic"
            },
            InstalledAtUtc: DateTime.UtcNow.AddDays(-2),
            IsActive: true
        );
        _installedItems[demoInstall.InstallationId] = demoInstall;
    }

    public IReadOnlyList<MarketplaceListingItem> SearchListings(string? query = null, MarketplaceItemType? type = null, MarketplaceTier? tier = null)
    {
        var items = _catalog.Values.AsEnumerable();

        if (type.HasValue)
        {
            items = items.Where(i => i.ItemType == type.Value);
        }

        if (tier.HasValue)
        {
            items = items.Where(i => i.Tier == tier.Value);
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.Trim();
            items = items.Where(i =>
                i.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                i.Tagline.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                i.Description.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                i.Author.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                i.Slug.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                i.Tags.Any(t => t.Contains(q, StringComparison.OrdinalIgnoreCase))
            );
        }

        return items
            .OrderByDescending(i => i.Rating)
            .ThenByDescending(i => i.InstallCount)
            .ToList();
    }

    public MarketplaceListingItem? GetListingById(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;

        return _catalog.Values.FirstOrDefault(i =>
            i.Id.Equals(id, StringComparison.OrdinalIgnoreCase) ||
            i.Slug.Equals(id, StringComparison.OrdinalIgnoreCase)
        );
    }

    public TenantInstalledItem InstallItem(string tenantId, string listingId, Dictionary<string, string>? customSettings = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(listingId);

        var listing = GetListingById(listingId)
            ?? throw new KeyNotFoundException($"Marketplace listing '{listingId}' was not found.");

        lock (_syncLock)
        {
            // Business Rule: Prevent duplicate active installations for this tenant
            var existing = _installedItems.Values.FirstOrDefault(i =>
                i.TenantId.Equals(tenantId, StringComparison.OrdinalIgnoreCase) &&
                i.ListingId.Equals(listing.Id, StringComparison.OrdinalIgnoreCase) &&
                i.IsActive
            );

            if (existing is not null)
            {
                throw new InvalidOperationException($"Marketplace item '{listing.Name}' ({listing.Id}) is already installed for tenant '{tenantId}'.");
            }

            // Increment install count
            var updatedListing = listing with { InstallCount = listing.InstallCount + 1 };
            _catalog[listing.Id] = updatedListing;

            var installedItem = new TenantInstalledItem(
                InstallationId: Guid.NewGuid(),
                TenantId: tenantId,
                ListingId: listing.Id,
                InstalledVersion: listing.Version,
                CustomSettings: customSettings != null ? new Dictionary<string, string>(customSettings) : new Dictionary<string, string>(),
                InstalledAtUtc: DateTime.UtcNow,
                IsActive: true
            );

            _installedItems[installedItem.InstallationId] = installedItem;
            return installedItem;
        }
    }

    public bool UninstallItem(string tenantId, string installationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(installationId);

        lock (_syncLock)
        {
            var item = _installedItems.Values.FirstOrDefault(i =>
                i.TenantId.Equals(tenantId, StringComparison.OrdinalIgnoreCase) &&
                i.IsActive &&
                (i.InstallationId.ToString().Equals(installationId, StringComparison.OrdinalIgnoreCase) ||
                 i.ListingId.Equals(installationId, StringComparison.OrdinalIgnoreCase))
            );

            if (item is null)
            {
                return false;
            }

            var uninstalled = item with { IsActive = false };
            _installedItems[item.InstallationId] = uninstalled;
            return true;
        }
    }

    public IReadOnlyList<TenantInstalledItem> GetInstalledItems(string tenantId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        return _installedItems.Values
            .Where(i => i.TenantId.Equals(tenantId, StringComparison.OrdinalIgnoreCase) && i.IsActive)
            .OrderByDescending(i => i.InstalledAtUtc)
            .ToList();
    }
}
