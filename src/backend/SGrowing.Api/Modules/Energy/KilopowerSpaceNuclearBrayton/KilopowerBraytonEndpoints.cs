using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Energy.KilopowerSpaceNuclearBrayton;

public static class KilopowerBraytonEndpoints
{
    public static IServiceCollection AddKilopowerBraytonServices(this IServiceCollection services)
    {
        services.AddScoped<IKilopowerSpaceNuclearBraytonEngine, KilopowerSpaceNuclearBraytonEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapKilopowerBraytonEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/energy/kilopower-brayton")
            .WithTags("Energy - Space Nuclear Kilopower Brayton Cycle");

        group.MapPost("/analyze", (
            KilopowerTelemetry telemetry,
            IKilopowerSpaceNuclearBraytonEngine engine) =>
        {
            var result = engine.AnalyzeSurfacePowerCycle(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeKilopowerCycle")
        .WithSummary("Analyzes space nuclear Kilopower closed Brayton cycle, sodium heat pipe core, and radiator freeze-out margins.");

        return app;
    }
}
