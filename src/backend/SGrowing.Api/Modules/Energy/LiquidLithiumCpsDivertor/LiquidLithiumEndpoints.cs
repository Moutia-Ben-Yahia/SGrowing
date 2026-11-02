using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Energy.LiquidLithiumCpsDivertor;

public static class LiquidLithiumEndpoints
{
    public static IServiceCollection AddLiquidLithiumDivertorServices(this IServiceCollection services)
    {
        services.AddScoped<ILiquidLithiumCpsDivertorEngine, LiquidLithiumCpsDivertorEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapLiquidLithiumDivertorEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/energy/liquid-lithium-divertor")
            .WithTags("Energy - Liquid Lithium Divertor CPS Vapor Shielding");

        group.MapPost("/analyze-divertor", (
            LiquidLithiumDivertorTelemetry telemetry,
            ILiquidLithiumCpsDivertorEngine engine) =>
        {
            var result = engine.AnalyzeLiquidMetalDivertor(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeLiquidLithiumDivertor")
        .WithSummary("Analyzes tokamak liquid lithium capillary porous system (CPS) divertor, self-regulating vapor shielding, and plasma Zeff.");

        return app;
    }
}
