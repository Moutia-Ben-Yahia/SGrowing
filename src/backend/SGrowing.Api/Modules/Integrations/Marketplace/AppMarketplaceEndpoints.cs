namespace SGrowing.Api.Modules.Integrations.Marketplace;

public static class AppMarketplaceEndpoints
{
    public static IServiceCollection AddAppMarketplaceServices(this IServiceCollection services)
    {
        services.AddSingleton<IAppMarketplaceEngine, AppMarketplaceEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapAppMarketplaceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/integrations/marketplace")
            .WithTags("App Marketplace");

        group.MapGet("/apps", (IAppMarketplaceEngine engine, AppCategory? category) =>
        {
            var apps = engine.ListAvailableApps(category);
            return Results.Ok(apps);
        })
        .WithName("ListAvailableApps")
        .WithSummary("List all verified and third-party marketplace apps filtered optionally by category");

        group.MapGet("/apps/{appId}", (IAppMarketplaceEngine engine, string appId) =>
        {
            var manifest = engine.GetAppManifest(appId);
            return manifest is not null ? Results.Ok(manifest) : Results.NotFound(new { error = $"App '{appId}' not found" });
        })
        .WithName("GetAppManifest")
        .WithSummary("Retrieve the full manifest and permission scopes of a marketplace app");

        group.MapPost("/install", (IAppMarketplaceEngine engine, InstallAppRequest request) =>
        {
            try
            {
                var installed = engine.InstallApp(request);
                return Results.Ok(installed);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .WithName("InstallApp")
        .WithSummary("Install a marketplace app into the tenant workspace with sandboxed credentials");

        group.MapPost("/uninstall", (IAppMarketplaceEngine engine, UninstallAppRequest request) =>
        {
            var success = engine.UninstallApp(request);
            return success 
                ? Results.Ok(new { success = true, message = $"App '{request.AppId}' uninstalled successfully." })
                : Results.NotFound(new { success = false, message = $"Installation not found for app '{request.AppId}'." });
        })
        .WithName("UninstallApp")
        .WithSummary("Uninstall an installed marketplace app and revoke tenant tokens");

        group.MapGet("/installed", (IAppMarketplaceEngine engine, string? tenantId) =>
        {
            var targetTenant = tenantId ?? "tenant_default";
            var installed = engine.ListInstalledApps(targetTenant);
            return Results.Ok(installed);
        })
        .WithName("ListInstalledApps")
        .WithSummary("List all apps currently installed and configured for the specified tenant");

        group.MapPost("/register", (IAppMarketplaceEngine engine, AppManifest manifest) =>
        {
            var registered = engine.RegisterAppManifest(manifest);
            return Results.Created($"/api/v1/integrations/marketplace/apps/{registered.AppId}", registered);
        })
        .WithName("RegisterAppManifest")
        .WithSummary("Register a new custom sandboxed app manifest into the ecosystem catalog");

        return app;
    }
}
