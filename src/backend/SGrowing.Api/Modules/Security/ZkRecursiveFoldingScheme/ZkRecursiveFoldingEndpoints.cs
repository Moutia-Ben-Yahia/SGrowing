using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Security.ZkRecursiveFoldingScheme;

public static class ZkRecursiveFoldingEndpoints
{
    public static IServiceCollection AddZkRecursiveFoldingServices(this IServiceCollection services)
    {
        services.AddScoped<IZkRecursiveFoldingSchemeEngine, ZkRecursiveFoldingSchemeEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapZkRecursiveFoldingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/security/zk-folding")
            .WithTags("Security - Zero-Knowledge Recursive Folding Schemes");

        group.MapPost("/analyze-session", (
            ZkFoldingTelemetry telemetry,
            IZkRecursiveFoldingSchemeEngine engine) =>
        {
            var result = engine.AnalyzeFoldingSession(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeZkFoldingSession")
        .WithSummary("Analyzes Nova-style relaxed R1CS folding IVC, cross-term commitments, Fiat-Shamir transcript entropy, and decider wrap triggers.");

        return app;
    }
}
