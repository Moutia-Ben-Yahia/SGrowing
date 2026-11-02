using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Security.ZtnaContinuousPosture;

public static class ZtnaEndpoints
{
    public static IServiceCollection AddZtnaServices(this IServiceCollection services)
    {
        services.AddSingleton<GeovelocityAnomalyDetectorService>();
        services.AddSingleton<ContinuousTrustScoringEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapZtnaEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/security/ztna")
            .WithTags("Sovereign Zero-Trust Network Access (ZTNA) & Continuous Behavioral Posture Attestation");

        group.MapPost("/attest", (
            SessionAccessTelemetry telemetry,
            ContinuousTrustScoringEngine engine) =>
        {
            var result = engine.EvaluateSessionAccess(telemetry);
            return Results.Ok(result);
        });

        group.MapGet("/sessions/{sessionId}/revoked", (
            string sessionId,
            ContinuousTrustScoringEngine engine) =>
        {
            var isRevoked = engine.IsSessionRevoked(sessionId);
            return Results.Ok(new { SessionId = sessionId, IsRevoked = isRevoked });
        });

        return app;
    }
}
