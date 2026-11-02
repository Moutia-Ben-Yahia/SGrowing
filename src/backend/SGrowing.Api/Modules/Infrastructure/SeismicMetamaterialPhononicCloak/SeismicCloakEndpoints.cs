using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Infrastructure.SeismicMetamaterialPhononicCloak;

public static class SeismicCloakEndpoints
{
    public static IServiceCollection AddSeismicMetamaterialCloakServices(this IServiceCollection services)
    {
        services.AddScoped<ISeismicMetamaterialPhononicCloakEngine, SeismicMetamaterialPhononicCloakEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapSeismicCloakEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/infrastructure/seismic-cloak")
            .WithTags("Infrastructure - Seismic Metamaterial Ground-Wave Phononic Cloak");

        group.MapPost("/analyze-cloaking", (
            SeismicCloakTelemetry telemetry,
            ISeismicMetamaterialPhononicCloakEngine engine) =>
        {
            var result = engine.AnalyzeSeismicCloaking(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeSeismicPhononicCloaking")
        .WithSummary("Analyzes subterranean phononic crystal metamaterial bandgap attenuation, Rayleigh surface wave diversion, and earthquake energy shielding.");

        return app;
    }
}
