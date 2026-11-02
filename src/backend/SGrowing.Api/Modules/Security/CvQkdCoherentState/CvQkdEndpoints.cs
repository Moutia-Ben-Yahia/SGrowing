using Microsoft.AspNetCore.Mvc;

namespace SGrowing.Api.Modules.Security.CvQkdCoherentState;

public static class CvQkdEndpoints
{
    public static IServiceCollection AddCvQkdCoherentStateServices(this IServiceCollection services)
    {
        services.AddScoped<ICvQkdCoherentStateEngine, CvQkdCoherentStateEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapCvQkdEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/security/cv-qkd")
            .WithTags("Continuous-Variable QKD Coherent State Security");

        group.MapPost("/evaluate", (
            [FromBody] CvQkdEvaluationRequest request,
            [FromServices] ICvQkdCoherentStateEngine engine) =>
        {
            var assessment = engine.AssessQuantumSecurity(request);
            return Results.Ok(assessment);
        })
        .WithName("EvaluateCvQkdSecurity")
        .WithSummary("Calculates channel transmission, mutual information, Holevo bound, and asymptotic secret key rate for CV-QKD.");

        group.MapGet("/telemetry/{linkId}", (string linkId) =>
        {
            var nominal = new QuantumChannelParameters(linkId);
            return Results.Ok(new
            {
                LinkId = linkId,
                FiberLengthKm = nominal.FiberLengthKm,
                AttenuationDbPerKm = nominal.FiberAttenuationDbPerKm,
                QuantumEfficiency = nominal.DetectionQuantumEfficiency,
                Status = "COHERENT_LOCAL_OSCILLATOR_LOCKED"
            });
        })
        .WithName("GetCvQkdLinkTelemetry")
        .WithSummary("Returns physical optical parameters and detector efficiency for a continuous-variable quantum link.");

        return app;
    }
}
