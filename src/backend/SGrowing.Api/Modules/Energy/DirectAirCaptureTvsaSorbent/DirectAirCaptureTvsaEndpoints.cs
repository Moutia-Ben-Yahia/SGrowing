using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Energy.DirectAirCaptureTvsaSorbent;

public static class DirectAirCaptureTvsaEndpoints
{
    public static IServiceCollection AddDirectAirCaptureTvsaServices(this IServiceCollection services)
    {
        services.AddScoped<IDirectAirCaptureTvsaEngine, DirectAirCaptureTvsaEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapDirectAirCaptureTvsaEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/energy/direct-air-capture-tvsa")
            .WithTags("Energy Direct Air Capture (DAC) Solid Sorbent TVSA Decarbonization");

        group.MapPost("/analyze", (DirectAirCaptureTvsaTelemetry telemetry, IDirectAirCaptureTvsaEngine engine) =>
        {
            var evaluation = engine.AnalyzeDacTrain(telemetry);
            return Results.Ok(evaluation);
        })
        .WithName("AnalyzeDirectAirCaptureTvsa")
        .WithSummary("Analyzes solid sorbent direct air capture (DAC), temperature-vacuum swing adsorption (TVSA) cycles, and thermodynamic energy metrics");

        return endpoints;
    }
}
