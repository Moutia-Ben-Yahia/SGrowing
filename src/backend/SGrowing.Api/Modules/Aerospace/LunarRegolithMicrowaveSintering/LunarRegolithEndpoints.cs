using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Aerospace.LunarRegolithMicrowaveSintering;

public static class LunarRegolithEndpoints
{
    public static IServiceCollection AddLunarRegolithServices(this IServiceCollection services)
    {
        services.AddScoped<ILunarRegolithMicrowaveSinteringEngine, LunarRegolithMicrowaveSinteringEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapLunarRegolithEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/aerospace/lunar-regolith-sintering")
            .WithTags("Aerospace - Lunar Regolith In-Situ Microwave Sintering");

        group.MapPost("/analyze", (
            LunarRegolithSinteringTelemetry telemetry,
            ILunarRegolithMicrowaveSinteringEngine engine) =>
        {
            var result = engine.AnalyzeSinteringProcess(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeLunarRegolithSintering")
        .WithSummary("Analyzes lunar regolith microwave heating, densification kinetics, and compressive yield strength.");

        return app;
    }
}
