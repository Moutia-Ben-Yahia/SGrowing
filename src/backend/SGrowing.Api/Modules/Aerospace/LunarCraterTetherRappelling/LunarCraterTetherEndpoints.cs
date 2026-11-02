using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Aerospace.LunarCraterTetherRappelling;

public static class LunarCraterTetherEndpoints
{
    public static IServiceCollection AddLunarCraterTetherRappellingServices(this IServiceCollection services)
    {
        services.AddScoped<ILunarCraterTetherRappellingEngine, LunarCraterTetherRappellingEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapLunarCraterTetherEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/aerospace/lunar-crater-tether-rappelling")
            .WithTags("Aerospace Lunar Crater Tether Rappelling");

        group.MapPost("/analyze", (LunarCraterTetherTelemetry telemetry, ILunarCraterTetherRappellingEngine engine) =>
        {
            var result = engine.AnalyzeRappellingDynamics(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeLunarCraterTetherRappelling")
        .WithSummary("Analyzes extreme terrain lunar rover crater wall rappelling tether tension dynamics, regolith anchor pull-out margins, and rollover stability.");

        return endpoints;
    }
}
