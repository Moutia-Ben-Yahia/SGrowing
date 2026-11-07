using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SGrowing.Api.Common;

namespace SGrowing.Api.Modules.AI.Marketplace;

/// <summary>
/// Minimal API endpoints for exploring, installing, and managing marketplace agent swarms, workflows, and templates.
/// </summary>
public class AgentWorkflowMarketplaceEndpoints : IEndpointModule
{
    private const string DefaultTenantId = "00000000-0000-0000-0000-000000000001";

    public AgentWorkflowMarketplaceEndpoints()
    {
    }

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/marketplace")
            .WithTags("Agent & Workflow Marketplace Hub");

        // 1. GET /api/v1/marketplace/listings
        group.MapGet("/listings", (
            [FromQuery] string? query,
            [FromQuery] MarketplaceItemType? type,
            [FromQuery] MarketplaceTier? tier,
            IAgentWorkflowMarketplaceService service) =>
        {
            var listings = service.SearchListings(query, type, tier);
            return Results.Ok(listings);
        })
        .WithName("SearchMarketplaceListings")
        .WithSummary("Search and filter community and official agent swarms, workflows, connectors, and prompt packs");

        // 2. GET /api/v1/marketplace/listings/{id}
        group.MapGet("/listings/{id}", (
            string id,
            IAgentWorkflowMarketplaceService service) =>
        {
            var listing = service.GetListingById(id);
            return listing is not null
                ? Results.Ok(listing)
                : Results.NotFound(new { error = $"Marketplace listing '{id}' was not found." });
        })
        .WithName("GetMarketplaceListingById")
        .WithSummary("Retrieve detailed specification, schema, and manifest payload for a marketplace listing");

        // 3. POST /api/v1/marketplace/install
        group.MapPost("/install", (
            [FromBody] InstallMarketplaceItemRequest request,
            [FromHeader(Name = "X-Tenant-Id")] string? headerTenantId,
            IAgentWorkflowMarketplaceService service) =>
        {
            var tenantId = !string.IsNullOrWhiteSpace(request.TenantId)
                ? request.TenantId
                : (!string.IsNullOrWhiteSpace(headerTenantId) ? headerTenantId : DefaultTenantId);

            try
            {
                var installed = service.InstallItem(tenantId, request.ListingId, request.CustomSettings);
                return Results.Ok(installed);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("InstallMarketplaceItem")
        .WithSummary("Install an agent swarm or workflow template into the active tenant workspace");

        // 4. DELETE /api/v1/marketplace/installations/{id}
        group.MapDelete("/installations/{id}", (
            string id,
            [FromHeader(Name = "X-Tenant-Id")] string? headerTenantId,
            [FromQuery] string? tenantId,
            IAgentWorkflowMarketplaceService service) =>
        {
            var effectiveTenantId = !string.IsNullOrWhiteSpace(tenantId)
                ? tenantId
                : (!string.IsNullOrWhiteSpace(headerTenantId) ? headerTenantId : DefaultTenantId);

            var uninstalled = service.UninstallItem(effectiveTenantId, id);
            return uninstalled
                ? Results.Ok(new { success = true, message = $"Installation '{id}' uninstalled successfully." })
                : Results.NotFound(new { error = $"Active installation '{id}' for tenant '{effectiveTenantId}' not found." });
        })
        .WithName("UninstallMarketplaceItem")
        .WithSummary("Uninstall an installed marketplace item from the active tenant workspace");

        // 5. GET /api/v1/marketplace/installed
        group.MapGet("/installed", (
            [FromHeader(Name = "X-Tenant-Id")] string? headerTenantId,
            [FromQuery] string? tenantId,
            IAgentWorkflowMarketplaceService service) =>
        {
            var effectiveTenantId = !string.IsNullOrWhiteSpace(tenantId)
                ? tenantId
                : (!string.IsNullOrWhiteSpace(headerTenantId) ? headerTenantId : DefaultTenantId);

            var installed = service.GetInstalledItems(effectiveTenantId);
            return Results.Ok(installed);
        })
        .WithName("GetInstalledMarketplaceItems")
        .WithSummary("Retrieve all installed marketplace items in the active tenant workspace");
    }
}

public static class AgentWorkflowMarketplaceServiceExtensions
{
    public static IServiceCollection AddAgentWorkflowMarketplaceServices(this IServiceCollection services)
    {
        services.AddSingleton<IAgentWorkflowMarketplaceService, AgentWorkflowMarketplaceService>();
        return services;
    }
}
