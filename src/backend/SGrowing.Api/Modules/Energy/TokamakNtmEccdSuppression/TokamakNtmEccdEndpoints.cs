using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Energy.TokamakNtmEccdSuppression;

public static class TokamakNtmEccdEndpoints
{
    public static IServiceCollection AddTokamakNtmEccdServices(this IServiceCollection services)
    {
        services.AddScoped<ITokamakNtmEccdEngine, TokamakNtmEccdEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapTokamakNtmEccdEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/energy/tokamak-ntm-eccd-suppression")
            .WithTags("Energy Tokamak Neoclassical Tearing Mode ECCD Suppression");

        group.MapPost("/analyze", (TokamakNtmEccdTelemetry telemetry, ITokamakNtmEccdEngine engine) =>
        {
            var evaluation = engine.AnalyzeDischarge(telemetry);
            return Results.Ok(evaluation);
        })
        .WithName("AnalyzeTokamakNtmEccd")
        .WithSummary("Analyzes high-beta tokamak neoclassical tearing modes (NTM), island dynamics, and real-time ECCD stabilization");

        return endpoints;
    }
}
