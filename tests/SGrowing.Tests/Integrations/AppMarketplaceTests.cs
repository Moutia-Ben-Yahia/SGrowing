using SGrowing.Api.Modules.Integrations.Marketplace;

namespace SGrowing.Tests.Integrations;

public class AppMarketplaceTests
{
    private readonly AppMarketplaceEngine _engine = new();

    [Fact]
    public void ListAvailableApps_ReturnsDefaultSeedApps()
    {
        var apps = _engine.ListAvailableApps();

        Assert.NotNull(apps);
        Assert.True(apps.Count >= 5);
        Assert.Contains(apps, a => a.AppId == "app_hubspot_sync");
        Assert.Contains(apps, a => a.AppId == "app_shopify_commerce");
        Assert.Contains(apps, a => a.AppId == "app_stripe_billing");
        Assert.Contains(apps, a => a.AppId == "app_meta_ads_capi");
        Assert.Contains(apps, a => a.AppId == "app_gemini_intelligence");
    }

    [Fact]
    public void ListAvailableApps_WithCategoryFilter_FiltersCorrectly()
    {
        var crmApps = _engine.ListAvailableApps(AppCategory.CRM);
        var aiApps = _engine.ListAvailableApps(AppCategory.AI);

        Assert.All(crmApps, a => Assert.Equal(AppCategory.CRM, a.Category));
        Assert.All(aiApps, a => Assert.Equal(AppCategory.AI, a.Category));
    }

    [Fact]
    public void GetAppManifest_WhenExists_ReturnsManifest()
    {
        var manifest = _engine.GetAppManifest("app_hubspot_sync");

        Assert.NotNull(manifest);
        Assert.Equal("HubSpot Two-Way CRM Connector", manifest.Name);
        Assert.True(manifest.IsVerified);
        Assert.Contains("crm:read", manifest.RequiredPermissions);
    }

    [Fact]
    public void GetAppManifest_WhenNotFound_ReturnsNull()
    {
        var manifest = _engine.GetAppManifest("non_existent_app");
        Assert.Null(manifest);
    }

    [Fact]
    public void InstallAndUninstallApp_Lifecycle_Succeeds()
    {
        var tenantId = "tenant_test_marketplace";
        var installReq = new InstallAppRequest(
            TenantId: tenantId,
            AppId: "app_hubspot_sync",
            Config: new Dictionary<string, string> { { "apiKey", "test-key-123" } }
        );

        var installed = _engine.InstallApp(installReq);

        Assert.NotNull(installed);
        Assert.Equal("app_hubspot_sync", installed.AppId);
        Assert.Equal(InstallationStatus.Installed, installed.Status);
        Assert.Equal("test-key-123", installed.Config["apiKey"]);

        var installedList = _engine.ListInstalledApps(tenantId);
        Assert.Single(installedList);

        var uninstallReq = new UninstallAppRequest(tenantId, "app_hubspot_sync");
        var uninstalled = _engine.UninstallApp(uninstallReq);
        Assert.True(uninstalled);

        var emptyList = _engine.ListInstalledApps(tenantId);
        Assert.Empty(emptyList);
    }

    [Fact]
    public void InstallApp_WhenAppUnknown_ThrowsKeyNotFoundException()
    {
        var request = new InstallAppRequest("t1", "unknown_app_id", new Dictionary<string, string>());
        Assert.Throws<KeyNotFoundException>(() => _engine.InstallApp(request));
    }

    [Fact]
    public void RegisterAppManifest_AddsToCatalog()
    {
        var custom = new AppManifest(
            AppId: "app_custom_analytics",
            Name: "Custom ClickHouse Analytics Extension",
            Description: "Streams clickstream telemetry into on-prem ClickHouse cluster.",
            Category: AppCategory.Productivity,
            Version: "1.0.0",
            Icon: "clickhouse-yellow",
            Author: "In-house DevOps",
            RequiredPermissions: new() { "telemetry:read" },
            WebhookEvents: new() { "event.ingested" },
            SandboxedIframeUrl: "https://internal.company.com/sandbox",
            IsVerified: true
        );

        var registered = _engine.RegisterAppManifest(custom);
        Assert.Equal("app_custom_analytics", registered.AppId);

        var retrieved = _engine.GetAppManifest("app_custom_analytics");
        Assert.NotNull(retrieved);
        Assert.Equal("Custom ClickHouse Analytics Extension", retrieved.Name);
    }

    [Fact]
    public void UninstallApp_WhenNotInstalled_ReturnsFalse()
    {
        var uninstallReq = new UninstallAppRequest("tenant_random", "app_not_installed");
        var result = _engine.UninstallApp(uninstallReq);
        Assert.False(result);
    }
}
