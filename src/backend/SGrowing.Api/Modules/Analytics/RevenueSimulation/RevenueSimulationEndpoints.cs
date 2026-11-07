using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SGrowing.Api.Common;
using SGrowing.Core.Tenancy;

namespace SGrowing.Api.Modules.Analytics.RevenueSimulation;

public class RevenueSimulationEndpoints : IEndpointModule
{
    public RevenueSimulationEndpoints()
    {
    }

    private static Guid ResolveTenantId(HttpContext context, ITenantContext? tenantContext, string? queryTenantId = null)
    {
        if (!string.IsNullOrWhiteSpace(queryTenantId) && Guid.TryParse(queryTenantId, out var parsedQueryId))
        {
            return parsedQueryId;
        }

        if (tenantContext != null && tenantContext.HasTenant && tenantContext.TenantId != Guid.Empty)
        {
            return tenantContext.TenantId;
        }

        if (context.Request.Headers.TryGetValue("X-Tenant-Id", out var headerVal) && Guid.TryParse(headerVal.ToString(), out var parsedHeaderId))
        {
            return parsedHeaderId;
        }

        return RevenueSimulationService.DefaultTenantId;
    }

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/analytics/revenue-simulation")
            .WithTags("Predictive Revenue Forecasting & Monte Carlo Simulation");

        group.MapGet("/opportunities", (
            HttpContext context,
            ITenantContext tenantContext,
            string? tenantId,
            IRevenueSimulationService service) =>
        {
            var targetTenant = ResolveTenantId(context, tenantContext, tenantId);
            var deals = service.GetOpportunities(targetTenant);
            return Results.Ok(deals);
        })
        .WithName("GetSimulationOpportunities")
        .WithSummary("Retrieve pipeline opportunities mapped for Monte Carlo forecasting simulation.");

        group.MapPost("/opportunities", (
            HttpContext context,
            ITenantContext tenantContext,
            AddOpportunityInputRequest request,
            string? tenantId,
            IRevenueSimulationService service) =>
        {
            var targetTenant = ResolveTenantId(context, tenantContext, tenantId);
            var created = service.AddOpportunity(targetTenant, request);
            return Results.Created($"/api/v1/analytics/revenue-simulation/opportunities/{created.Id}", created);
        })
        .WithName("AddSimulationOpportunity")
        .WithSummary("Add a new pipeline deal with min/most likely/max valuation bounds for simulation.");

        group.MapPost("/simulate", (
            HttpContext context,
            ITenantContext tenantContext,
            RunMonteCarloSimulationRequest? request,
            string? tenantId,
            IRevenueSimulationService service) =>
        {
            var targetTenant = ResolveTenantId(context, tenantContext, tenantId);
            var result = service.RunSimulation(targetTenant, request);
            return Results.Ok(result);
        })
        .WithName("RunMonteCarloSimulation")
        .WithSummary("Execute multi-iteration Monte Carlo revenue simulation computing P10, P50, P90, and quota confidence.");

        group.MapGet("/slippage-alerts", (
            HttpContext context,
            ITenantContext tenantContext,
            string? tenantId,
            IRevenueSimulationService service) =>
        {
            var targetTenant = ResolveTenantId(context, tenantContext, tenantId);
            var alerts = service.GetSlippageAlerts(targetTenant);
            return Results.Ok(alerts);
        })
        .WithName("GetDealSlippageAlerts")
        .WithSummary("Retrieve real-time slippage risk alerts and recommended AI interventions for stalled pipeline deals.");

        group.MapGet("/overview", (
            HttpContext context,
            ITenantContext tenantContext,
            double? quotaTarget,
            string? tenantId,
            IRevenueSimulationService service) =>
        {
            var targetTenant = ResolveTenantId(context, tenantContext, tenantId);
            var overview = service.GetOverviewReport(targetTenant, quotaTarget);
            return Results.Ok(overview);
        })
        .WithName("GetRevenueSimulationOverview")
        .WithSummary("Retrieve comprehensive revenue simulation executive report including P10/P50/P90 distributions and radar alerts.");
    }
}
