using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Quantum.CavityOptomechanicsPhononSqueezing;

public static class CavityOptomechanicsEndpoints
{
    public static IServiceCollection AddCavityOptomechanicsPhononSqueezingServices(this IServiceCollection services)
    {
        services.AddScoped<ICavityOptomechanicsPhononSqueezingEngine, CavityOptomechanicsPhononSqueezingEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapCavityOptomechanicsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/quantum/cavity-optomechanics-phonon-squeezing")
            .WithTags("Quantum Cavity Optomechanics Phonon Squeezing");

        group.MapPost("/analyze", (CavityOptomechanicsTelemetry telemetry, ICavityOptomechanicsPhononSqueezingEngine engine) =>
        {
            var result = engine.AnalyzePhononSqueezing(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeCavityOptomechanicsPhononSqueezing")
        .WithSummary("Analyzes cavity optomechanics dynamical backaction laser cooling and sub-SQL mechanical phonon quadrature squeezing.");

        return endpoints;
    }
}
