using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Quantum.NvCenterNanoscaleMagnetometry;

public static class NvCenterMagnetometryEndpoints
{
    public static IServiceCollection AddNvCenterMagnetometryServices(this IServiceCollection services)
    {
        services.AddScoped<INvCenterNanoscaleMagnetometryEngine, NvCenterNanoscaleMagnetometryEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapNvCenterMagnetometryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/quantum/nv-center-magnetometry")
            .WithTags("Quantum NV Center Nanoscale Magnetometry");

        group.MapPost("/analyze", (NvCenterMagnetometryTelemetry telemetry, INvCenterNanoscaleMagnetometryEngine engine) =>
        {
            var result = engine.AnalyzeMagnetometry(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeNvCenterMagnetometry")
        .WithSummary("Analyzes diamond NV- center ODMR Zeeman splitting, magnetic field vector, and shot-noise limited sensitivity.");

        return endpoints;
    }
}
