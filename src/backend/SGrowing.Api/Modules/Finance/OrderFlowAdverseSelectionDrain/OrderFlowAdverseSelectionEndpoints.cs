using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Finance.OrderFlowAdverseSelectionDrain;

public static class OrderFlowAdverseSelectionEndpoints
{
    public static IServiceCollection AddOrderFlowAdverseSelectionDrainServices(this IServiceCollection services)
    {
        services.AddScoped<IOrderFlowAdverseSelectionDrainEngine, OrderFlowAdverseSelectionDrainEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapOrderFlowAdverseSelectionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/finance/order-flow-adverse-selection-drain")
            .WithTags("Finance Order Flow Toxicity Adverse Selection");

        group.MapPost("/analyze", (OrderFlowAdverseSelectionTelemetry telemetry, IOrderFlowAdverseSelectionDrainEngine engine) =>
        {
            var result = engine.AnalyzeAdverseSelectionDrain(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeOrderFlowAdverseSelectionDrain")
        .WithSummary("Analyzes high-frequency order flow toxicity, Kyle's price impact lambda, depth replenishment drain, and adverse selection sniping risks.");

        return endpoints;
    }
}
