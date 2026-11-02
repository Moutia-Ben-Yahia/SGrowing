using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.BioTech.ProtacTernaryComplexKinetics;

public static class ProtacTernaryComplexEndpoints
{
    public static IServiceCollection AddProtacTernaryComplexServices(this IServiceCollection services)
    {
        services.AddScoped<IProtacTernaryComplexKineticsEngine, ProtacTernaryComplexKineticsEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapProtacTernaryComplexEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/biotech/protac-ternary-complex-kinetics")
            .WithTags("BioTech PROTAC Ternary Complex Kinetics");

        group.MapPost("/analyze", (ProtacTernaryComplexTelemetry telemetry, IProtacTernaryComplexKineticsEngine engine) =>
        {
            var result = engine.AnalyzeProtacKinetics(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeProtacTernaryComplex")
        .WithSummary("Analyzes proteolysis-targeting chimera (PROTAC) ternary complex cooperativity (alpha), Hook effect concentrations, and proteasomal degradation efficacy.");

        return endpoints;
    }
}
