using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Dev.Gateway;

public static class DevGatewayEndpoints
{
    public static IServiceCollection AddDevGatewayServices(this IServiceCollection services)
    {
        services.AddSingleton<IDevGatewayEngine, DevGatewayEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapDevGatewayEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/developer/gateway")
            .WithTags("Public Developer Gateway & Sandbox");

        group.MapPost("/rate-limit/check", (RateLimitCheckRequest request, IDevGatewayEngine engine) =>
        {
            var result = engine.EvaluateRateLimit(request);
            return result.IsAllowed
                ? Results.Ok(result)
                : Results.Json(result, statusCode: StatusCodes.Status429TooManyRequests);
        }).WithName("CheckRateLimitQuota");

        group.MapGet("/tier-quotas/{tier}", (DeveloperTier tier, IDevGatewayEngine engine) =>
        {
            var quota = engine.GetTierQuota(tier);
            return Results.Ok(quota);
        }).WithName("GetTierQuota");

        group.MapPost("/sandbox/configure", (SandboxSessionConfig config, IDevGatewayEngine engine) =>
        {
            var configured = engine.ConfigureSandboxSession(config);
            return Results.Ok(configured);
        }).WithName("ConfigureSandboxSession");

        group.MapGet("/sandbox/{tenantId}", (string tenantId, IDevGatewayEngine engine) =>
        {
            var session = engine.GetSandboxSession(tenantId);
            return session is not null ? Results.Ok(session) : Results.NotFound();
        }).WithName("GetSandboxSession");

        group.MapPost("/sandbox/simulate", (SandboxSimulationRequest request, IDevGatewayEngine engine) =>
        {
            var response = engine.SimulateRequest(request);
            return Results.Json(response, statusCode: response.StatusCode);
        }).WithName("SimulateSandboxRequest");

        group.MapGet("/openapi-spec", (string? role, bool? includeSchemas, IDevGatewayEngine engine) =>
        {
            var query = new OpenApiExportQuery(role ?? "public", includeSchemas ?? true);
            var result = engine.ExportOpenApiSpec(query);
            return Results.Ok(result);
        }).WithName("ExportOpenApiSpec");

        return endpoints;
    }
}
