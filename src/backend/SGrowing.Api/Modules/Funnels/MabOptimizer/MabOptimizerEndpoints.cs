using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Funnels.MabOptimizer;

public static class MabOptimizerEndpoints
{
    public static IServiceCollection AddMabOptimizerServices(this IServiceCollection services)
    {
        services.AddSingleton<IMabOptimizerEngine, MabOptimizerEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapMabOptimizerEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/funnels/mab")
            .WithTags("Domain C1 & C2: Visual Funnel Builder & Multi-Armed Bandit A/B Split Engine");

        // 1. Create Funnel
        group.MapPost("/create", (
            [FromBody] CreateFunnelRequest request,
            IMabOptimizerEngine engine) =>
        {
            var created = engine.CreateFunnel(request);
            return Results.Created($"/api/v1/funnels/mab/{created.TenantId}/{created.FunnelId}", created);
        })
        .WithName("CreateFunnelMab")
        .WithSummary("Creates a new funnel page with modular block schema and initial A/B variants.");

        // 2. Get Funnel
        group.MapGet("/{tenantId}/{funnelId}", (
            string tenantId,
            string funnelId,
            IMabOptimizerEngine engine) =>
        {
            var funnel = engine.GetFunnel(tenantId, funnelId);
            return funnel is not null ? Results.Ok(funnel) : Results.NotFound();
        })
        .WithName("GetFunnelMab")
        .WithSummary("Retrieves a funnel page and its block layout and variant weights.");

        // 3. Update Blocks
        group.MapPut("/blocks", (
            [FromBody] UpdateBlocksRequest request,
            IMabOptimizerEngine engine) =>
        {
            try
            {
                var updated = engine.UpdateBlocks(request);
                return Results.Ok(updated);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .WithName("UpdateFunnelBlocks")
        .WithSummary("Updates the drag-and-drop block ordering and configuration.");

        // 4. Select Variant (MAB Thompson Sampling)
        group.MapGet("/{tenantId}/{funnelId}/select-variant", (
            string tenantId,
            string funnelId,
            IMabOptimizerEngine engine) =>
        {
            try
            {
                var selected = engine.SelectVariant(tenantId, funnelId);
                return Results.Ok(selected);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .WithName("SelectMabVariant")
        .WithSummary("Selects the optimal traffic variant using Bayesian Thompson Sampling.");

        // 5. Record Impression
        group.MapPost("/impression", (
            [FromBody] RecordImpressionRequest request,
            IMabOptimizerEngine engine) =>
        {
            try
            {
                var updated = engine.RecordImpression(request);
                return Results.Ok(updated);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .WithName("RecordMabImpression")
        .WithSummary("Records an impression and dynamically recalculates variant traffic weights.");

        // 6. Record Conversion
        group.MapPost("/conversion", (
            [FromBody] RecordConversionRequest request,
            IMabOptimizerEngine engine) =>
        {
            try
            {
                var updated = engine.RecordConversion(request);
                return Results.Ok(updated);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .WithName("RecordMabConversion")
        .WithSummary("Records a conversion and updates Bayesian posterior scores.");

        // 7. Get Report
        group.MapGet("/{tenantId}/{funnelId}/report", (
            string tenantId,
            string funnelId,
            IMabOptimizerEngine engine) =>
        {
            try
            {
                var report = engine.GetOptimizationReport(tenantId, funnelId);
                return Results.Ok(report);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .WithName("GetMabOptimizationReport")
        .WithSummary("Returns Bayesian statistical confidence, variant uplift, and Schema.org JSON-LD.");

        return endpoints;
    }
}
