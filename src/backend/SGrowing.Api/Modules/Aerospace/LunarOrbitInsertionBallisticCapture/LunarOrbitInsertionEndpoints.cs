using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Aerospace.LunarOrbitInsertionBallisticCapture;

public static class LunarOrbitInsertionEndpoints
{
    public static IServiceCollection AddLunarOrbitInsertionServices(this IServiceCollection services)
    {
        services.AddScoped<ILunarOrbitInsertionEngine, LunarOrbitInsertionEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapLunarOrbitInsertionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/aerospace/lunar-orbit-insertion-ballistic")
            .WithTags("Aerospace Lunar Orbit Insertion Ballistic Capture");

        group.MapPost("/analyze", (LunarOrbitInsertionTelemetry telemetry, ILunarOrbitInsertionEngine engine) =>
        {
            var evaluation = engine.AnalyzeInsertion(telemetry);
            return Results.Ok(evaluation);
        })
        .WithName("AnalyzeLunarOrbitInsertion")
        .WithSummary("Analyzes low-energy ballistic lunar transfer (BLT), three-body gravity capture, and LOI insertion burns");

        return endpoints;
    }
}
