using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Quantum.BosonicCatQubitProtection;

public static class BosonicCatQubitEndpoints
{
    public static IServiceCollection AddBosonicCatQubitProtectionServices(this IServiceCollection services)
    {
        services.AddScoped<IBosonicCatQubitProtectionEngine, BosonicCatQubitProtectionEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapBosonicCatQubitEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/quantum/bosonic-cat-qubit-protection")
            .WithTags("Quantum Bosonic Cat Qubit Error Correction");

        group.MapPost("/analyze", (BosonicCatQubitTelemetry telemetry, IBosonicCatQubitProtectionEngine engine) =>
        {
            var result = engine.AnalyzeCatQubitProtection(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeBosonicCatQubit")
        .WithSummary("Analyzes superconducting microwave cavity bosonic cat qubit autonomous stabilization, exponential bit-flip suppression, and noise bias preservation.");

        return endpoints;
    }
}
