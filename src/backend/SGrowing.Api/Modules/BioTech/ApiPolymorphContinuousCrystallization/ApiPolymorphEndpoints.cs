using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.BioTech.ApiPolymorphContinuousCrystallization;

public static class ApiPolymorphEndpoints
{
    public static IServiceCollection AddApiPolymorphServices(this IServiceCollection services)
    {
        services.AddScoped<IApiPolymorphContinuousCrystallizationEngine, ApiPolymorphContinuousCrystallizationEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapApiPolymorphEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/biotech/api-polymorph")
            .WithTags("BioTech - Continuous API Polymorph Crystallization");

        group.MapPost("/analyze-crystallizer", (
            ApiCrystallizerTelemetry telemetry,
            IApiPolymorphContinuousCrystallizationEngine engine) =>
        {
            var result = engine.AnalyzeCrystallizationState(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeApiCrystallizer")
        .WithSummary("Analyzes pharmaceutical continuous MSMPR crystallization, polymorph phase transformation, and fouling.");

        return app;
    }
}
