using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Organizations;

public static class OrganizationHierarchyEndpoints
{
    private static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private static Guid ResolveTenantId(Guid? tenantId, Guid? headerTenantId)
    {
        return tenantId ?? headerTenantId ?? DefaultTenantId;
    }

    public static IEndpointRouteBuilder MapOrganizationHierarchyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/organizations")
            .WithTags("Multi-Organization Hierarchy & Margin Tracking");

        group.MapGet("/tree", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IOrganizationHierarchyService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var tree = await service.GetHierarchyTreeAsync(effectiveTenantId, ct);
            return Results.Ok(tree);
        })
        .WithName("GetOrganizationHierarchyTree")
        .WithSummary("Retrieve multi-tiered organizational hierarchy tree (Tenant -> Organization -> Branch -> Team)");

        group.MapPost("/profitability", async (
            [FromBody] DealMarginCalculationRequest req,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IOrganizationHierarchyService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var result = await service.CalculateDealMarginAsync(effectiveTenantId, req, ct);
            return Results.Ok(result);
        })
        .WithName("CalculateDealGrossMargin")
        .WithSummary("Calculate True Gross Margin: Revenue - COGS - Courier Fees - Operator Labor Hours");

        group.MapGet("/profitability", async (
            [FromQuery] Guid? organizationId,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IOrganizationHierarchyService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var summary = await service.GetProfitabilitySummaryAsync(effectiveTenantId, organizationId, ct);
            return Results.Ok(summary);
        })
        .WithName("GetOrganizationProfitabilitySummary")
        .WithSummary("Get consolidated True Gross Margin and COGS summary across organizations and deals");

        return app;
    }
}
