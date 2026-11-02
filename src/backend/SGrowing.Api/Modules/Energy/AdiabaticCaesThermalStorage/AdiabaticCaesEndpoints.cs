using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Energy.AdiabaticCaesThermalStorage;

public static class AdiabaticCaesEndpoints
{
    public static IServiceCollection AddAdiabaticCaesServices(this IServiceCollection services)
    {
        services.AddScoped<IAdiabaticCaesThermalStorageEngine, AdiabaticCaesThermalStorageEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapAdiabaticCaesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/energy/adiabatic-caes")
            .WithTags("Energy - Adiabatic Compressed Air Energy Storage (A-CAES)");

        group.MapPost("/analyze-cavern", (
            AdiabaticCaesTelemetry telemetry,
            IAdiabaticCaesThermalStorageEngine engine) =>
        {
            var result = engine.AnalyzeAdiabaticCaes(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeAdiabaticCaesStorage")
        .WithSummary("Analyzes subterranean salt cavern adiabatic compressed air energy storage (A-CAES), thermal energy storage (TES) exergy, and geomechanical stress.");

        return app;
    }
}
