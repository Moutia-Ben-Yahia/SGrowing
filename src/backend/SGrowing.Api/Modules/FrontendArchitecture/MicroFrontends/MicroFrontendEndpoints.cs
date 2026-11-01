using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.FrontendArchitecture.MicroFrontends;

public static class MicroFrontendEndpoints
{
    /// <summary>
    /// Registers the Self-Healing Micro-Frontend Architecture, Module Federation,
    /// Circuit Breaker, Cross-App Event Bus, and Edge SSR Hydration services into DI.
    /// </summary>
    public static IServiceCollection AddMicroFrontendServices(this IServiceCollection services)
    {
        services.AddSingleton<IMicroFrontendHostEngine, MicroFrontendHostEngine>();
        services.AddSingleton<IRemoteMicroAppCircuitBreaker, RemoteMicroAppCircuitBreaker>();
        services.AddSingleton<IFederatedCrossAppEventBus, FederatedCrossAppEventBus>();
        services.AddSingleton<IEdgeSsrHydrationService, EdgeSsrHydrationService>();

        return services;
    }

    /// <summary>
    /// Maps the Micro-Frontend Module Federation, Circuit Breaker, Event Bus, and Edge SSR endpoints.
    /// </summary>
    public static IEndpointRouteBuilder MapMicroFrontendEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/frontend/micro-frontends")
            .WithTags("Micro-Frontend Architecture, Module Federation & Edge SSR");

        // 1. GET /manifest - Fetch active module federation manifest and shared singleton libraries
        group.MapGet("/manifest", (
            [FromQuery] string? hostVersion,
            [FromServices] IMicroFrontendHostEngine hostEngine) =>
        {
            var manifest = hostEngine.GetFederationManifest(hostVersion);
            return Results.Ok(manifest);
        })
        .WithName("GetModuleFederationManifest")
        .WithSummary("Fetch unified Module Federation manifest and shared singleton dependencies.");

        // 2. POST /remotes/register - Dynamically register or update a federated remote module
        group.MapPost("/remotes/register", (
            [FromBody] RegisterRemoteRequest request,
            [FromServices] IMicroFrontendHostEngine hostEngine) =>
        {
            if (string.IsNullOrWhiteSpace(request.ModuleId) ||
                string.IsNullOrWhiteSpace(request.RemoteEntryUrl) ||
                string.IsNullOrWhiteSpace(request.ExposedComponent) ||
                string.IsNullOrWhiteSpace(request.Version))
            {
                return Results.BadRequest(new { error = "INVALID_REQUEST", message = "ModuleId, RemoteEntryUrl, ExposedComponent, and Version are required." });
            }

            var metadata = new RemoteModuleMetadata(
                ModuleId: request.ModuleId,
                RemoteEntryUrl: request.RemoteEntryUrl,
                ExposedComponent: request.ExposedComponent,
                Version: request.Version,
                HealthStatus: RemoteHealthStatus.Healthy,
                LatencyMs: 0.0,
                LastPingUtc: DateTimeOffset.UtcNow,
                FallbackComponentUrl: request.FallbackComponentUrl);

            hostEngine.RegisterRemote(metadata);

            return Results.Created($"/api/v1/frontend/micro-frontends/remotes/{request.ModuleId}", metadata);
        })
        .WithName("RegisterFederatedRemote")
        .WithSummary("Register or dynamically update a federated remote micro-app.");

        // 3. POST /circuit/trip - Simulate bundle failure or force-trip circuit breaker
        group.MapPost("/circuit/trip", (
            [FromBody] TripCircuitRequest request,
            [FromServices] IRemoteMicroAppCircuitBreaker circuitBreaker) =>
        {
            if (string.IsNullOrWhiteSpace(request.ModuleId))
            {
                return Results.BadRequest(new { error = "INVALID_MODULE_ID", message = "ModuleId is required." });
            }

            var metrics = circuitBreaker.ForceTrip(request.ModuleId, request.Reason);
            var fallback = circuitBreaker.GetFallbackView(request.ModuleId, request.Reason);

            return Results.Ok(new
            {
                module_id = request.ModuleId,
                status = "CIRCUIT_TRIPPED",
                metrics,
                fallback
            });
        })
        .WithName("TripRemoteCircuitBreaker")
        .WithSummary("Force trip the circuit breaker for a micro-app to enter resilient degraded fallback mode.");

        // 4. POST /circuit/reset - Reset circuit breaker back to Closed state
        group.MapPost("/circuit/reset", (
            [FromBody] ResetCircuitRequest request,
            [FromServices] IRemoteMicroAppCircuitBreaker circuitBreaker) =>
        {
            if (string.IsNullOrWhiteSpace(request.ModuleId))
            {
                return Results.BadRequest(new { error = "INVALID_MODULE_ID", message = "ModuleId is required." });
            }

            var metrics = circuitBreaker.ResetCircuit(request.ModuleId);

            return Results.Ok(new
            {
                module_id = request.ModuleId,
                status = "CIRCUIT_RESET",
                metrics
            });
        })
        .WithName("ResetRemoteCircuitBreaker")
        .WithSummary("Reset a tripped circuit breaker back to Closed state.");

        // 5. POST /events/publish - Publish a cryptographically verified cross-app event
        group.MapPost("/events/publish", async (
            [FromBody] CrossAppEvent appEvent,
            [FromServices] IFederatedCrossAppEventBus eventBus) =>
        {
            var result = await ((FederatedCrossAppEventBus)eventBus).PublishAsync(appEvent);
            if (!result.IsSuccess)
            {
                return Results.BadRequest(new
                {
                    error = result.ErrorCode,
                    message = result.ErrorMessage,
                    event_id = result.EventId
                });
            }

            return Results.Ok(result);
        })
        .WithName("PublishCrossAppEvent")
        .WithSummary("Publish an HMAC-SHA256 cryptographically verified cross-app event.");

        // 6. GET /edge/ssr-manifest - Fetch edge SSR hydration instructions
        group.MapGet("/edge/ssr-manifest", (
            [FromQuery] string? route,
            [FromQuery] HydrationStrategy? strategy,
            [FromQuery] int? viewportWidth,
            [FromServices] IEdgeSsrHydrationService ssrService) =>
        {
            var targetRoute = string.IsNullOrWhiteSpace(route) ? "/cockpit/commerce" : route;
            var manifest = ssrService.GenerateManifest(targetRoute, strategy, viewportWidth ?? 1280);

            return Results.Ok(manifest);
        })
        .WithName("GetEdgeSsrManifest")
        .WithSummary("Fetch Edge SSR pre-rendered HTML, critical CSS, and progressive hydration directives.");

        // 7. GET /health - Engine health report
        group.MapGet("/health", (
            [FromServices] IMicroFrontendHostEngine hostEngine,
            [FromServices] IRemoteMicroAppCircuitBreaker circuitBreaker,
            [FromServices] IFederatedCrossAppEventBus eventBus) =>
        {
            var remotes = hostEngine.GetAllRemotes();
            var metrics = circuitBreaker.GetAllMetrics();
            var openCircuits = metrics.Count(m => m.State == CircuitState.Open);
            var halfOpenCircuits = metrics.Count(m => m.State == CircuitState.HalfOpen);
            var busMetrics = eventBus.GetMetrics();

            var status = openCircuits > 0
                ? "Degraded"
                : "Healthy";

            var report = new EngineHealthReport(
                HostVersion: MicroFrontendHostEngine.DefaultHostVersion,
                Status: status,
                ActiveRemotesCount: remotes.Count,
                OpenCircuitsCount: openCircuits,
                HalfOpenCircuitsCount: halfOpenCircuits,
                EventBus: busMetrics,
                TimestampUtc: DateTimeOffset.UtcNow);

            return Results.Ok(report);
        })
        .WithName("GetMicroFrontendHealth")
        .WithSummary("Get micro-frontend architecture operational health and telemetry.");

        return app;
    }
}
