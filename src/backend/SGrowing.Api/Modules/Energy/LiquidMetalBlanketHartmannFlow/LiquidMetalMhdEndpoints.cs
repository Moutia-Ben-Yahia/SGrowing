namespace SGrowing.Api.Modules.Energy.LiquidMetalBlanketHartmannFlow;

public static class LiquidMetalMhdEndpoints
{
    public static IServiceCollection AddLiquidMetalMhdServices(this IServiceCollection services)
    {
        services.AddScoped<LiquidMetalBlanketHartmannFlowEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapLiquidMetalMhdEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/energy/liquid-metal-mhd")
            .WithTags("Liquid Metal Blanket MHD Hartmann Flow Arbiter");

        group.MapPost("/analyze", (LiquidMetalMhdTelemetry telemetry, LiquidMetalBlanketHartmannFlowEngine engine) =>
        {
            var result = engine.AnalyzeBlanketChannel(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeLiquidMetalBlanketMhd")
        .WithSummary("Analyzes tokamak Pb-17Li liquid metal breeding blanket Hartmann flow, MHD pressure drop, and tritium permeation barriers.");

        return endpoints;
    }
}
