using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Quantum.GaussianBosonSampling;

public static class GaussianBosonSamplingEndpoints
{
    public static IServiceCollection AddGaussianBosonSamplingServices(this IServiceCollection services)
    {
        services.AddScoped<IGaussianBosonSamplingEngine, GaussianBosonSamplingEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapGaussianBosonSamplingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/quantum/gaussian-boson-sampling")
            .WithTags("Quantum - Photonic Gaussian Boson Sampling (GBS)");

        group.MapPost("/analyze-sampling", (
            GbsTelemetry telemetry,
            IGaussianBosonSamplingEngine engine) =>
        {
            var result = engine.AnalyzeGaussianBosonSampling(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeGaussianBosonSampling")
        .WithSummary("Analyzes photonic Gaussian Boson Sampling (GBS), Torontonian/Hafnian simulation complexity, MCMC mock validation, and quantum supremacy.");

        return app;
    }
}
