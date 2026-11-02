using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Finance.VpinMarketMakerToxicFlow;

public static class VpinEndpoints
{
    public static IServiceCollection AddVpinToxicFlowServices(this IServiceCollection services)
    {
        services.AddScoped<IVpinMarketMakerToxicFlowEngine, VpinMarketMakerToxicFlowEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapVpinEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/finance/vpin-toxic-flow")
            .WithTags("Finance - VPIN Market Maker Informed Toxic Flow");

        group.MapPost("/analyze-toxicity", (
            VpinTelemetry telemetry,
            IVpinMarketMakerToxicFlowEngine engine) =>
        {
            var result = engine.AnalyzeOrderFlowToxicity(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeVpinOrderFlowToxicity")
        .WithSummary("Analyzes Volume-Synchronized Probability of Toxicity (VPIN), order flow imbalance, informed trading avalanche, and quote spread widening.");

        return app;
    }
}
