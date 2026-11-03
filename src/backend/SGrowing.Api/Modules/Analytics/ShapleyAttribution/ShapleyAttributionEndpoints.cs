using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Analytics.ShapleyAttribution;

public static class ShapleyAttributionEndpoints
{
    public static IServiceCollection AddShapleyAttributionServices(this IServiceCollection services)
    {
        services.AddSingleton<IShapleyAttributionEngine, ShapleyAttributionEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapShapleyAttributionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/analytics/attribution-shapley")
            .WithTags("Analytics - Shapley Value Attribution");

        group.MapPost("/calculate", (CalculateShapleyRequest request, IShapleyAttributionEngine engine) =>
        {
            var report = engine.CalculateAttribution(request);
            return Results.Ok(report);
        })
        .WithName("CalculateShapleyAttribution")
        .WithSummary("Calculates cooperative game theory Shapley values across touchpoint journeys");

        group.MapGet("/{dealId}", (string dealId, IShapleyAttributionEngine engine) =>
        {
            var report = engine.GetReportByDealId(dealId);
            return report is not null ? Results.Ok(report) : Results.NotFound();
        })
        .WithName("GetShapleyAttributionByDealId")
        .WithSummary("Retrieves Shapley fairness model comparison and budget recommendations for a deal");

        return endpoints;
    }
}
