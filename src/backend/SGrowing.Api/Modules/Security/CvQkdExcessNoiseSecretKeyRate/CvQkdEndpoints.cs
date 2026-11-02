using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Security.CvQkdExcessNoiseSecretKeyRate;

public static class CvQkdExcessNoiseEndpoints
{
    public static IServiceCollection AddCvQkdExcessNoiseServices(this IServiceCollection services)
    {
        services.AddScoped<ICvQkdExcessNoiseSecretKeyRateEngine, CvQkdExcessNoiseSecretKeyRateEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapCvQkdExcessNoiseEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/security/cv-qkd")
            .WithTags("Security - Continuous-Variable QKD Excess Noise");

        group.MapPost("/analyze", (
            CvQkdTelemetry telemetry,
            ICvQkdExcessNoiseSecretKeyRateEngine engine) =>
        {
            var result = engine.AnalyzeQuantumChannel(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeCvQkdChannel")
        .WithSummary("Analyzes fiber CV-QKD excess noise, Holevo eavesdropping information bounds, and asymptotic secret key rate.");

        return app;
    }
}
