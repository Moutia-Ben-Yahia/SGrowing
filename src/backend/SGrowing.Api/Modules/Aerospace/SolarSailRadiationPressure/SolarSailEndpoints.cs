using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Aerospace.SolarSailRadiationPressure;

public static class SolarSailEndpoints
{
    public static IServiceCollection AddSolarSailRadiationPressureServices(this IServiceCollection services)
    {
        services.AddScoped<ISolarSailRadiationPressureEngine, SolarSailRadiationPressureEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapSolarSailEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/aerospace/solar-sail-radiation-pressure")
            .WithTags("Aerospace Solar Sail Radiation Pressure");

        group.MapPost("/analyze", (SolarSailTelemetry telemetry, ISolarSailRadiationPressureEngine engine) =>
        {
            var result = engine.AnalyzeSolarSail(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeSolarSailRadiationPressure")
        .WithSummary("Analyzes solar sail photon momentum transfer, radiation pressure, thrust force, and heliocentric lightness factor.");

        return endpoints;
    }
}
