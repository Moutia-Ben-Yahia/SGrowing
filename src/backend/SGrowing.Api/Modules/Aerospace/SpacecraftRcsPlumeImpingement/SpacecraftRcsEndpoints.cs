using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Aerospace.SpacecraftRcsPlumeImpingement;

public static class SpacecraftRcsEndpoints
{
    public static IServiceCollection AddSpacecraftRcsServices(this IServiceCollection services)
    {
        services.AddScoped<ISpacecraftRcsEngine, SpacecraftRcsEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapSpacecraftRcsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/aerospace/spacecraft-rcs-plume-impingement")
            .WithTags("Aerospace Spacecraft RCS Pulse-Width Modulation & Plume Impingement");

        group.MapPost("/analyze", (SpacecraftRcsTelemetry telemetry, ISpacecraftRcsEngine engine) =>
        {
            var evaluation = engine.AnalyzeRcsFiring(telemetry);
            return Results.Ok(evaluation);
        })
        .WithName("AnalyzeSpacecraftRcs")
        .WithSummary("Analyzes reaction control system (RCS) pulse-width modulation, plume impingement pressure/heat flux, and valve thermal soakback");

        return endpoints;
    }
}
