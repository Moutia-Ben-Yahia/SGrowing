using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Energy.SolidOxideCoElectrolysisSyngas;

public static class SolidOxideCoElectrolysisEndpoints
{
    public static IServiceCollection AddSolidOxideCoElectrolysisServices(this IServiceCollection services)
    {
        services.AddScoped<ISolidOxideCoElectrolysisSyngasEngine, SolidOxideCoElectrolysisSyngasEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapSolidOxideCoElectrolysisEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/energy/soec-co-electrolysis-syngas")
            .WithTags("Energy Solid Oxide High-Temperature Co-Electrolysis");

        group.MapPost("/analyze", (SolidOxideCoElectrolysisTelemetry telemetry, ISolidOxideCoElectrolysisSyngasEngine engine) =>
        {
            var result = engine.AnalyzeCoElectrolysisStack(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeSolidOxideCoElectrolysis")
        .WithSummary("Analyzes high-temperature solid oxide co-electrolysis (SOEC) of steam and CO2, thermoneutral voltage balance, and Fischer-Tropsch syngas ratios.");

        return endpoints;
    }
}
