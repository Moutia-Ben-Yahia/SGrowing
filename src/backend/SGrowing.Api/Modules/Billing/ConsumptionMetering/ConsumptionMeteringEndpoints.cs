using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Billing.ConsumptionMetering;

public static class ConsumptionMeteringEndpoints
{
    public static IServiceCollection AddConsumptionMeteringServices(this IServiceCollection services)
    {
        services.AddSingleton<IConsumptionMeteringEngine, ConsumptionMeteringEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapConsumptionMeteringEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/billing/consumption-metering")
            .WithTags("Billing - SaaS Consumption Metering");

        group.MapPost("/ingest", (IngestUsageEventRequest request, IConsumptionMeteringEngine engine) =>
        {
            var profile = engine.IngestUsage(request);
            return Results.Ok(profile);
        })
        .WithName("IngestUsageConsumptionEvent")
        .WithSummary("Ingests usage units and updates tenant burn rate and invoice projections");

        group.MapGet("/usage/{tenantId}", (string tenantId, IConsumptionMeteringEngine engine) =>
        {
            var profile = engine.GetUsageProfile(tenantId);
            return profile is not null ? Results.Ok(profile) : Results.NotFound();
        })
        .WithName("GetTenantConsumptionProfile")
        .WithSummary("Retrieves tenant real-time quota burn rates, warnings, and predicted invoice");

        group.MapPost("/upgrade-tier", (AutoUpgradeTierRequest request, IConsumptionMeteringEngine engine) =>
        {
            var updated = engine.AutoUpgradeTier(request);
            return Results.Ok(updated);
        })
        .WithName("AutoUpgradeTenantTier")
        .WithSummary("Scales quota boundaries and upgrades subscription tier without downtime");

        return endpoints;
    }
}
