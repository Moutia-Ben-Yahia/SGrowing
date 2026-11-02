using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Integrations.Marketplace;

public interface IAppMarketplaceEngine
{
    List<AppManifest> ListAvailableApps(AppCategory? category = null);
    AppManifest? GetAppManifest(string appId);
    InstalledApp InstallApp(InstallAppRequest request);
    bool UninstallApp(UninstallAppRequest request);
    List<InstalledApp> ListInstalledApps(string tenantId);
    AppManifest RegisterAppManifest(AppManifest manifest);
}

public class AppMarketplaceEngine : IAppMarketplaceEngine
{
    private readonly ConcurrentDictionary<string, AppManifest> _catalog = new();
    private readonly ConcurrentDictionary<string, InstalledApp> _installedApps = new();

    public AppMarketplaceEngine()
    {
        SeedDefaultCatalog();
    }

    private static string BuildInstallKey(string tenantId, string appId) => $"{tenantId}::{appId}";

    private void SeedDefaultCatalog()
    {
        var apps = new List<AppManifest>
        {
            new(
                AppId: "app_hubspot_sync",
                Name: "HubSpot Two-Way CRM Connector",
                Description: "Bidirectional sync of Contacts, Companies, and Deals with HubSpot Enterprise.",
                Category: AppCategory.CRM,
                Version: "2.4.0",
                Icon: "hubspot-orange",
                Author: "SGrowing Official",
                RequiredPermissions: new() { "crm:read", "crm:write" },
                WebhookEvents: new() { "contact.created", "deal.updated" },
                SandboxedIframeUrl: "https://marketplace.sgrowing.io/apps/hubspot-sync/sandbox",
                IsVerified: true
            ),
            new(
                AppId: "app_shopify_commerce",
                Name: "Shopify Storefront & Inventory Bridge",
                Description: "Automate cart sync, live order webhooks, and product SKU catalog updates.",
                Category: AppCategory.Commerce,
                Version: "3.1.2",
                Icon: "shopify-green",
                Author: "Shopify Certified",
                RequiredPermissions: new() { "orders:read", "inventory:write" },
                WebhookEvents: new() { "orders/create", "inventory_levels/update" },
                SandboxedIframeUrl: "https://marketplace.sgrowing.io/apps/shopify-bridge/sandbox",
                IsVerified: true
            ),
            new(
                AppId: "app_stripe_billing",
                Name: "Stripe Billing & Payment Gateway",
                Description: "Synchronize customer subscriptions, payment intents, and invoice dunning webhooks.",
                Category: AppCategory.Commerce,
                Version: "4.0.0",
                Icon: "stripe-purple",
                Author: "Stripe Partner",
                RequiredPermissions: new() { "payments:read", "subscriptions:write" },
                WebhookEvents: new() { "invoice.payment_succeeded", "charge.refunded" },
                SandboxedIframeUrl: "https://marketplace.sgrowing.io/apps/stripe/sandbox",
                IsVerified: true
            ),
            new(
                AppId: "app_meta_ads_capi",
                Name: "Meta Conversion API & Pixel Sync",
                Description: "Direct server-side event transmission to Meta Graph API for sub-second ROAS matching.",
                Category: AppCategory.Advertising,
                Version: "1.8.0",
                Icon: "meta-blue",
                Author: "SGrowing Official",
                RequiredPermissions: new() { "ads:write", "telemetry:read" },
                WebhookEvents: new() { "conversion.attributed" },
                SandboxedIframeUrl: "https://marketplace.sgrowing.io/apps/meta-capi/sandbox",
                IsVerified: true
            ),
            new(
                AppId: "app_gemini_intelligence",
                Name: "Gemini 2.5 Multi-Modal Agent Copilot",
                Description: "Deep semantic embeddings, live video analysis, and autonomous prompt reasoning.",
                Category: AppCategory.AI,
                Version: "1.0.0",
                Icon: "gemini-sparkle",
                Author: "Google Cloud Ecosystem",
                RequiredPermissions: new() { "ai:execute", "prompts:read" },
                WebhookEvents: new() { "agent.completed" },
                SandboxedIframeUrl: "https://marketplace.sgrowing.io/apps/gemini-ai/sandbox",
                IsVerified: true
            )
        };

        foreach (var app in apps)
        {
            _catalog[app.AppId] = app;
        }
    }

    public List<AppManifest> ListAvailableApps(AppCategory? category = null)
    {
        var query = _catalog.Values.AsEnumerable();
        if (category.HasValue)
        {
            query = query.Where(a => a.Category == category.Value);
        }
        return query.OrderBy(a => a.Name).ToList();
    }

    public AppManifest? GetAppManifest(string appId)
    {
        _catalog.TryGetValue(appId, out var manifest);
        return manifest;
    }

    public AppManifest RegisterAppManifest(AppManifest manifest)
    {
        _catalog[manifest.AppId] = manifest;
        return manifest;
    }

    public InstalledApp InstallApp(InstallAppRequest request)
    {
        var manifest = GetAppManifest(request.AppId)
            ?? throw new KeyNotFoundException($"App with ID '{request.AppId}' is not found in the marketplace catalog.");

        var installationId = "inst_" + Guid.NewGuid().ToString("N")[..8];
        var key = BuildInstallKey(request.TenantId, request.AppId);

        var installed = new InstalledApp(
            InstallationId: installationId,
            TenantId: request.TenantId,
            AppId: request.AppId,
            Status: InstallationStatus.Installed,
            Config: new Dictionary<string, string>(request.Config),
            InstalledAtUtc: DateTime.UtcNow,
            LastSyncedUtc: DateTime.UtcNow
        );

        _installedApps[key] = installed;
        return installed;
    }

    public bool UninstallApp(UninstallAppRequest request)
    {
        var key = BuildInstallKey(request.TenantId, request.AppId);
        return _installedApps.TryRemove(key, out _);
    }

    public List<InstalledApp> ListInstalledApps(string tenantId)
    {
        return _installedApps.Values
            .Where(i => i.TenantId == tenantId)
            .OrderByDescending(i => i.InstalledAtUtc)
            .ToList();
    }
}
