using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Advertising.GeoIncrementality;

public static class GeoIncrementalityEndpoints
{
    public static IServiceCollection AddGeoIncrementalityServices(this IServiceCollection services)
    {
        services.AddSingleton<GeoIncrementalityEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapGeoIncrementalityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/advertising/geo-incrementality")
            .WithTags("Advertising - Synthetic Control & Geo Incrementality Mesh");

        group.MapGet("/overview", (GeoIncrementalityEngine engine) =>
        {
            var overview = engine.GetOverview();
            return Results.Ok(overview);
        })
        .WithName("GetGeoIncrementalityOverview")
        .WithSummary("Retrieve active geo-matched market experiments, synthetic controls, and true incremental ROAS");

        group.MapPost("/launch-experiment", (LaunchGeoExperimentRequest request, GeoIncrementalityEngine engine) =>
        {
            var experiment = engine.LaunchExperiment(request);
            return Results.Ok(experiment);
        })
        .WithName("LaunchGeoExperiment")
        .WithSummary("Deploys matched-market test/control holdouts with algorithmic synthetic control weighting");

        group.MapPost("/evaluate-lift", (EvaluateLiftRequest request, GeoIncrementalityEngine engine) =>
        {
            var experiment = engine.EvaluateLift(request);
            return Results.Ok(experiment);
        })
        .WithName("EvaluateGeoLift")
        .WithSummary("Runs causal ridge regression to measure pure incremental lift un-skewed by correlation");

        return endpoints;
    }
}
