using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Energy.AlkalineWaterElectrolysisDynamic;

public static class AlkalineWaterElectrolysisEndpoints
{
    public static IServiceCollection AddAlkalineWaterElectrolysisDynamicServices(this IServiceCollection services)
    {
        services.AddScoped<IAlkalineWaterElectrolysisDynamicEngine, AlkalineWaterElectrolysisDynamicEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapAlkalineWaterElectrolysisEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/energy/alkaline-water-electrolysis-dynamic")
            .WithTags("Energy Alkaline Water Electrolysis Dynamic Intermittent");

        group.MapPost("/analyze", (AlkalineWaterElectrolysisTelemetry telemetry, IAlkalineWaterElectrolysisDynamicEngine engine) =>
        {
            var result = engine.AnalyzeElectrolysisStack(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeAlkalineWaterElectrolysisStack")
        .WithSummary("Analyzes zero-gap alkaline water electrolysis (AWE) stack cell overpotentials, renewable intermittency ramp rates, and diaphragm H2-in-O2 gas crossover safety.");

        return endpoints;
    }
}
