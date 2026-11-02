using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Infrastructure.FeatureFlags;

public static class FeatureFlagEndpoints
{
    public static IServiceCollection AddFeatureFlagServices(this IServiceCollection services)
    {
        services.AddSingleton<IFeatureFlagEngine, FeatureFlagEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapFeatureFlagEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/infrastructure/feature-flags")
            .WithTags("Infrastructure - Multi-Tenant Feature Flags & Canary Mesh");

        group.MapPost("/evaluate", (EvaluateFlagRequest request, IFeatureFlagEngine engine) =>
        {
            var result = engine.EvaluateFlag(request);
            return Results.Ok(result);
        })
        .WithName("EvaluateFeatureFlag")
        .WithSummary("Evaluates feature flag status for a tenant using consistent SHA-256 hash bucketing and whitelist rules");

        group.MapGet("/", (IFeatureFlagEngine engine) =>
        {
            var flags = engine.GetAllFlags();
            return Results.Ok(flags);
        })
        .WithName("GetAllFeatureFlags")
        .WithSummary("Retrieves all defined feature flags, active canary rings, and circuit-breaker telemetry");

        group.MapPost("/canary-advance", (CanaryAdvanceRequest request, IFeatureFlagEngine engine) =>
        {
            var flag = engine.AdvanceCanaryRing(request);
            return Results.Ok(flag);
        })
        .WithName("AdvanceCanaryRing")
        .WithSummary("Advances a feature flag to a higher canary ring stage (Internal -> Ring 0 -> Ring 1 -> Ring 2 -> GA)");

        group.MapPost("/emergency-kill", (EmergencyKillRequest request, IFeatureFlagEngine engine) =>
        {
            var flag = engine.EmergencyKill(request);
            return Results.Ok(flag);
        })
        .WithName("EmergencyKillFeatureFlag")
        .WithSummary("Instantly trips circuit-breaker killswitch, disabling the flag across all tenants");

        return endpoints;
    }
}
