using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Transportation.MaglevInductrackNullFlux;

public static class MaglevInductrackEndpoints
{
    public static IServiceCollection AddMaglevInductrackServices(this IServiceCollection services)
    {
        services.AddScoped<IMaglevInductrackNullFluxEngine, MaglevInductrackNullFluxEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapMaglevInductrackEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/transportation/maglev-inductrack")
            .WithTags("Transportation - Maglev Inductrack Null-Flux EDS");

        group.MapPost("/analyze-levitation", (
            MaglevInductrackTelemetry telemetry,
            IMaglevInductrackNullFluxEngine engine) =>
        {
            var result = engine.AnalyzeLevitationState(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeMaglevLevitation")
        .WithSummary("Analyzes Inductrack electrodynamic suspension (EDS), null-flux lift-to-drag ratio, transition speed, and airgap stability.");

        return app;
    }
}
