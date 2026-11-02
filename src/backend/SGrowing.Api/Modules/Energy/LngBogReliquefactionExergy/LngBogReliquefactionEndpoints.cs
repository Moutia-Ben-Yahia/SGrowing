using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Energy.LngBogReliquefactionExergy;

public static class LngBogReliquefactionEndpoints
{
    public static IServiceCollection AddLngBogReliquefactionServices(this IServiceCollection services)
    {
        services.AddScoped<ILngBogReliquefactionExergyEngine, LngBogReliquefactionExergyEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapLngBogReliquefactionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/energy/lng-bog-reliquefaction")
            .WithTags("Energy - LNG BOG Re-Liquefaction & Thermodynamic Exergy");

        group.MapPost("/analyze", (
            LngBogReliquefactionTelemetry telemetry,
            ILngBogReliquefactionExergyEngine engine) =>
        {
            var result = engine.AnalyzeReliquefactionEfficiency(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeLngBogReliquefaction")
        .WithSummary("Analyzes LNG cryogenic boil-off gas re-liquefaction, nitrogen reverse-Brayton power, and Second-Law exergy efficiency.");

        return app;
    }
}
