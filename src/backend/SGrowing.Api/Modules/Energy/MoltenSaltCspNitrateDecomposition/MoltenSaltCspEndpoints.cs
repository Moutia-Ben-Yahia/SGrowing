using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Energy.MoltenSaltCspNitrateDecomposition;

public static class MoltenSaltNitrateDecompositionEndpoints
{
    public static IServiceCollection AddMoltenSaltNitrateDecompositionServices(this IServiceCollection services)
    {
        services.AddScoped<IMoltenSaltCspDecompositionEngine, MoltenSaltCspDecompositionEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapMoltenSaltNitrateDecompositionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/energy/molten-salt-csp-nitrate-decomposition")
            .WithTags("Energy Molten Salt CSP Nitrate Decomposition");

        group.MapPost("/analyze", (MoltenSaltCspTelemetry telemetry, IMoltenSaltCspDecompositionEngine engine) =>
        {
            var result = engine.AnalyzeMoltenSaltStorage(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeMoltenSaltCspStorage")
        .WithSummary("Analyzes high-temperature Concentrated Solar Power (CSP) molten solar salt thermal decomposition, cold tank freeze margins, and oxide-accelerated containment corrosion.");

        return endpoints;
    }
}
