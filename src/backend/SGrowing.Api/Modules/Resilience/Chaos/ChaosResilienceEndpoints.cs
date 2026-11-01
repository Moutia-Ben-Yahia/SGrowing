using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Resilience.Chaos;

/// <summary>
/// Minimal API endpoints and service registration extensions for Phase 35:
/// Autonomous Chaos Engineering, Fault Injection Mesh &amp; Resilience Autopilot.
/// </summary>
public static class ChaosResilienceEndpoints
{
    /// <summary>
    /// Registers all Phase 35 Chaos Mesh, Circuit Breaker Auditor, and Resilience Autopilot services.
    /// </summary>
    public static IServiceCollection AddChaosResilienceServices(this IServiceCollection services)
    {
        services.AddSingleton<IChaosFaultInjectionMesh, ChaosFaultInjectionMesh>();
        services.AddSingleton<ICircuitBreakerResilienceAuditor, CircuitBreakerResilienceAuditor>();
        services.AddSingleton<IResilienceAutopilotCoordinator, ResilienceAutopilotCoordinator>();

        return services;
    }

    /// <summary>
    /// Maps the Chaos Resilience Minimal API endpoints under /api/v1/resilience/chaos.
    /// </summary>
    public static IEndpointRouteBuilder MapChaosResilienceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/resilience/chaos")
            .WithTags("Autonomous Chaos Engineering & Resilience Autopilot");

        // 1. POST /experiments/start
        group.MapPost("/experiments/start", (
            [FromBody] StartExperimentRequest request,
            [FromServices] IResilienceAutopilotCoordinator coordinator) =>
        {
            if (string.IsNullOrWhiteSpace(request?.ExperimentName))
            {
                return Results.BadRequest(new { error = "ExperimentName is required." });
            }

            if (request.CanaryTrafficPercentage <= 0.0 || request.CanaryTrafficPercentage > 1.0)
            {
                return Results.BadRequest(new { error = "Canary traffic percentage cannot exceed 1.0% blast-radius limit." });
            }

            if (request.DurationSeconds <= 0)
            {
                return Results.BadRequest(new { error = "DurationSeconds must be greater than zero." });
            }

            try
            {
                var report = coordinator.StartExperiment(request);
                return Results.Ok(report);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("StartChaosExperiment")
        .WithSummary("Launches an automated chaos experiment with canary blast-radius containment (<= 1.0%).");

        // 2. POST /experiments/{experimentId}/abort
        group.MapPost("/experiments/{experimentId}/abort", (
            [FromRoute] string experimentId,
            [FromBody] AbortExperimentRequest? req,
            [FromServices] IResilienceAutopilotCoordinator coordinator) =>
        {
            try
            {
                var report = coordinator.AbortExperiment(experimentId, req?.Reason ?? "Manual abort requested via API.");
                return Results.Ok(report);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .WithName("AbortChaosExperiment")
        .WithSummary("Aborts an active chaos experiment, rolling back rules and setting Grade F.");

        // 3. GET /experiments/{experimentId}
        group.MapGet("/experiments/{experimentId}", (
            [FromRoute] string experimentId,
            [FromServices] IResilienceAutopilotCoordinator coordinator) =>
        {
            var report = coordinator.GetExperimentReport(experimentId);
            return report is not null
                ? Results.Ok(report)
                : Results.NotFound(new { error = $"Experiment '{experimentId}' not found." });
        })
        .WithName("GetChaosExperimentReport")
        .WithSummary("Retrieves the live or finalized report for a chaos experiment.");

        // 4. GET /rules
        group.MapGet("/rules", (
            [FromServices] IChaosFaultInjectionMesh mesh) =>
        {
            var rules = mesh.GetActiveRules();
            return Results.Ok(rules);
        })
        .WithName("GetActiveFaultRules")
        .WithSummary("Lists all active, unexpired chaos fault injection rules in the mesh.");

        // 5. POST /rules
        group.MapPost("/rules", (
            [FromBody] AddFaultRuleRequest request,
            [FromServices] IChaosFaultInjectionMesh mesh) =>
        {
            var ruleId = $"rule-{Guid.NewGuid():N}"[..12];
            var rule = new ChaosFaultRule(
                RuleId: ruleId,
                Target: request.Target,
                FaultType: request.FaultType,
                Probability: Math.Clamp(request.Probability, 0.0, 1.0),
                LatencyDelayMs: Math.Max(0, request.LatencyDelayMs),
                ErrorStatusCode: request.ErrorStatusCode,
                ErrorMessage: request.ErrorMessage,
                Enabled: request.Enabled,
                CreatedAt: DateTimeOffset.UtcNow,
                ExpiresAt: request.TtlSeconds.HasValue ? DateTimeOffset.UtcNow.AddSeconds(request.TtlSeconds.Value) : null);

            mesh.AddRule(rule);
            return Results.Created($"/api/v1/resilience/chaos/rules/{ruleId}", rule);
        })
        .WithName("AddChaosFaultRule")
        .WithSummary("Registers a new synthetic fault rule into the in-memory mesh.");

        // 6. DELETE /rules/{ruleId}
        group.MapDelete("/rules/{ruleId}", (
            [FromRoute] string ruleId,
            [FromServices] IChaosFaultInjectionMesh mesh) =>
        {
            var removed = mesh.RemoveRule(ruleId);
            return removed
                ? Results.Ok(new { success = true, ruleId })
                : Results.NotFound(new { success = false, message = $"Rule '{ruleId}' not found." });
        })
        .WithName("RemoveChaosFaultRule")
        .WithSummary("Removes a fault rule from the active mesh.");

        // 7. GET /audit
        group.MapGet("/audit", (
            [FromQuery] ChaosTarget? target,
            [FromServices] ICircuitBreakerResilienceAuditor auditor) =>
        {
            if (target.HasValue)
            {
                var status = auditor.GetAuditStatus(target.Value);
                return Results.Ok(status);
            }

            var audits = auditor.GetAllAudits();
            return Results.Ok(audits);
        })
        .WithName("GetCircuitBreakerAudit")
        .WithSummary("Returns circuit breaker graceful degradation audit status for targets.");

        // 8. GET /metrics
        group.MapGet("/metrics", (
            [FromServices] IChaosFaultInjectionMesh mesh) =>
        {
            var metrics = mesh.GetMetrics();
            return Results.Ok(metrics);
        })
        .WithName("GetChaosMetrics")
        .WithSummary("Retrieves aggregated counters for all faults injected by category.");

        // 9. GET /status
        group.MapGet("/status", (
            [FromServices] IResilienceAutopilotCoordinator coordinator) =>
        {
            var status = coordinator.GetAutopilotStatus();
            return Results.Ok(status);
        })
        .WithName("GetAutopilotStatus")
        .WithSummary("Retrieves real-time autopilot health, error budget, and running experiment status.");

        return app;
    }
}
