using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SGrowing.Api.Common;

namespace SGrowing.Api.Modules.AI.Evaluation;

/// <summary>
/// Minimal API endpoints for autonomous agent evaluation, quality benchmark radar, and supervised autonomy gating.
/// </summary>
public class AiAgentEvaluationEndpoints : IEndpointModule
{
    private static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    /// <summary>
    /// Initializes a new instance of <see cref="AiAgentEvaluationEndpoints"/>.
    /// Parameterless constructor required for module auto-discovery.
    /// </summary>
    public AiAgentEvaluationEndpoints()
    {
    }

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/ai/evaluations")
            .WithTags("AI Autonomous Agent Evaluation & Quality Benchmark Radar");

        group.MapGet("/radar", (
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IAiAgentEvaluationService service) =>
        {
            var effectiveTenantId = headerTenantId ?? tenantId ?? DefaultTenantId;
            var report = service.GetGlobalRadarReport(effectiveTenantId);
            return Results.Ok(report);
        })
        .WithName("GetGlobalEvaluationRadarReport")
        .WithSummary("Generates panoramic AI agent quality scorecard and supervised autonomy benchmark metrics");

        group.MapGet("/agents/{agentRole}", (
            string agentRole,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IAiAgentEvaluationService service) =>
        {
            var effectiveTenantId = headerTenantId ?? tenantId ?? DefaultTenantId;
            var scorecard = service.GetAgentScorecard(effectiveTenantId, agentRole);
            return scorecard is not null
                ? Results.Ok(scorecard)
                : Results.NotFound(new { message = $"Scorecard for agent role '{agentRole}' was not found." });
        })
        .WithName("GetAgentRadarScorecard")
        .WithSummary("Retrieves detailed quality benchmark scorecard for a specific AI agent persona");

        group.MapPost("/record", (
            RecordEvaluationRequest request,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            IAiAgentEvaluationService service) =>
        {
            var effectiveTenantId = request.TenantId != Guid.Empty
                ? request.TenantId
                : (headerTenantId ?? DefaultTenantId);

            var normalizedRequest = request with { TenantId = effectiveTenantId };
            var updated = service.RecordEvaluation(normalizedRequest);
            return Results.Ok(updated);
        })
        .WithName("RecordAgentEvaluation")
        .WithSummary("Records execution telemetry or human evaluation review to update the quality benchmark radar");

        group.MapGet("/agents/{agentRole}/autonomy-check", (
            string agentRole,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IAiAgentEvaluationService service) =>
        {
            var effectiveTenantId = headerTenantId ?? tenantId ?? DefaultTenantId;
            try
            {
                var result = service.EvaluateAutonomyPromotion(effectiveTenantId, agentRole);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { message = ex.Message });
            }
        })
        .WithName("CheckAgentAutonomyPromotion")
        .WithSummary("Evaluates autonomy promotion or demotion status against safety and accuracy baselines");
    }
}

/// <summary>
/// Service collection extensions for the AI autonomous agent evaluation benchmark engine.
/// </summary>
public static class AiAgentEvaluationServiceExtensions
{
    /// <summary>
    /// Registers the singleton AI agent evaluation and quality benchmark service.
    /// </summary>
    public static IServiceCollection AddAiAgentEvaluationServices(this IServiceCollection services)
    {
        services.AddSingleton<IAiAgentEvaluationService, AiAgentEvaluationService>();
        return services;
    }
}
