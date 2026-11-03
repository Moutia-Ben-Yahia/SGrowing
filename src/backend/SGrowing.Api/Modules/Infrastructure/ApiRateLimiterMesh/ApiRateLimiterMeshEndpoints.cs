using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Infrastructure.ApiRateLimiterMesh;

public static class ApiRateLimiterMeshEndpoints
{
    public static IServiceCollection AddApiRateLimiterMeshServices(this IServiceCollection services)
    {
        services.AddSingleton<ApiRateLimiterMeshEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapApiRateLimiterMeshEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/infrastructure/api-rate-limiter-mesh")
            .WithTags("Infrastructure - Zero-Trust API Rate Limiting & DDoS Defense Mesh");

        group.MapGet("/overview", (ApiRateLimiterMeshEngine engine) =>
        {
            var overview = engine.GetOverview();
            return Results.Ok(overview);
        })
        .WithName("GetApiRateLimiterMeshOverview")
        .WithSummary("Retrieve real-time token bucket capacity, blocked IP count, and tarpit metrics");

        group.MapPost("/consume-token", (ConsumeTokenRequest request, ApiRateLimiterMeshEngine engine) =>
        {
            var verdict = engine.ConsumeToken(request);
            return Results.Ok(verdict);
        })
        .WithName("ConsumeRateLimiterToken")
        .WithSummary("Evaluate incoming request token bucket and return allow, delay, or block verdict");

        group.MapPost("/quarantine-ip", (QuarantineIpRequest request, ApiRateLimiterMeshEngine engine) =>
        {
            var receipt = engine.QuarantineIp(request);
            return Results.Ok(receipt);
        })
        .WithName("QuarantineAbusiveIp")
        .WithSummary("Add high-entropy abusive IP address to distributed quarantine blacklist");

        return endpoints;
    }
}
