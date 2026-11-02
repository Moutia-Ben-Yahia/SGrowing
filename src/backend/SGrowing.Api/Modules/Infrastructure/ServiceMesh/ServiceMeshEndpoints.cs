using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Infrastructure.ServiceMesh;

public static class ServiceMeshEndpoints
{
    public static IServiceCollection AddServiceMeshServices(this IServiceCollection services)
    {
        services.AddSingleton<AdaptiveCircuitBreakerControlPlane>();
        services.AddSingleton<CanaryTrafficSplitterService>();
        services.AddSingleton<DistributedRateLimitTokenBucket>();
        return services;
    }

    public static IEndpointRouteBuilder MapServiceMeshEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/infrastructure/service-mesh")
            .WithTags("Self-Healing Microservices Service Mesh & Adaptive Circuit Breakers");

        group.MapPost("/instances/register", (
            RegisterInstanceApiRequest request,
            AdaptiveCircuitBreakerControlPlane plane) =>
        {
            plane.RegisterInstance(request.InstanceId, request.ServiceName, request.Host, request.Port);
            return Results.Ok(new { Status = "Registered", InstanceId = request.InstanceId });
        });

        group.MapGet("/instances", (AdaptiveCircuitBreakerControlPlane plane) =>
        {
            return Results.Ok(plane.GetAllInstances());
        });

        group.MapPost("/canary/policy", (
            CanaryPolicy policy,
            CanaryTrafficSplitterService splitter) =>
        {
            splitter.SetPolicy(policy);
            return Results.Ok(new { Status = "PolicyConfigured", policy.ServiceName });
        });

        group.MapGet("/canary/evaluate/{serviceName}", (
            string serviceName,
            CanaryTrafficSplitterService splitter) =>
        {
            try
            {
                var report = splitter.EvaluateCanary(serviceName);
                return Results.Ok(report);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { Error = ex.Message });
            }
        });

        group.MapPost("/ratelimit/check", (
            RateLimitCheckApiRequest request,
            DistributedRateLimitTokenBucket bucket) =>
        {
            var result = bucket.TryAcquire(
                request.TenantId,
                request.ServiceKey,
                request.TokenCost ?? 1.0,
                request.Capacity ?? 100.0,
                request.RefillRate ?? 20.0);

            return Results.Ok(result);
        });

        return app;
    }
}

public sealed record RegisterInstanceApiRequest(
    string InstanceId,
    string ServiceName,
    string Host,
    int Port
);

public sealed record RateLimitCheckApiRequest(
    string TenantId,
    string ServiceKey,
    double? TokenCost = 1.0,
    double? Capacity = 100.0,
    double? RefillRate = 20.0
);
