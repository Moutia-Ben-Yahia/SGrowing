using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Infrastructure.Autopilot;

/// <summary>
/// Minimal API endpoints and dependency injection extensions for Phase 40:
/// Autonomous Self-Healing Infrastructure, Automated Database Index Tuning &amp; Memory Autopilot.
/// </summary>
public static class AutopilotEndpoints
{
    /// <summary>
    /// Registers Phase 40 Autonomous Infrastructure, Index Synthesizer, Bloat Optimizer, and Memory Autopilot services.
    /// </summary>
    public static IServiceCollection AddAutopilotServices(this IServiceCollection services)
    {
        services.AddSingleton<IAutonomousIndexSynthesizerEngine, AutonomousIndexSynthesizerEngine>();
        services.AddSingleton<ITableBloatAndVacuumOptimizerService, TableBloatAndVacuumOptimizerService>();
        services.AddSingleton<IMemoryAllocationAutopilotService, MemoryAllocationAutopilotService>();

        return services;
    }

    /// <summary>
    /// Maps the Autonomous Infrastructure and Database Autopilot endpoints under /api/v1/infrastructure/autopilot.
    /// </summary>
    public static IEndpointRouteBuilder MapAutopilotEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/infrastructure/autopilot")
            .WithTags("Autonomous Self-Healing Infrastructure & Autopilot");

        // 1. GET /slow-queries
        group.MapGet("/slow-queries", (
            [FromServices] IAutonomousIndexSynthesizerEngine engine) =>
        {
            var recommendations = engine.GetActiveRecommendations();
            return Results.Ok(recommendations);
        })
        .WithName("GetSlowQueries")
        .WithSummary("Retrieves active slow query analysis and synthesized index recommendations.");

        // 2. POST /indexes/synthesize
        group.MapPost("/indexes/synthesize", (
            [FromBody] SynthesizeIndexRequest request,
            [FromServices] IAutonomousIndexSynthesizerEngine engine) =>
        {
            if (request?.Query is null)
            {
                return Results.BadRequest(new { error = "Query metric payload is required." });
            }

            var recommendation = engine.SynthesizeIndex(request.Query);
            return recommendation is not null
                ? Results.Ok(recommendation)
                : Results.UnprocessableEntity(new
                {
                    message = "Query does not satisfy criteria for index synthesis (must be SequentialScan, Rows > 5,000, MeanTime > 50ms)."
                });
        })
        .WithName("SynthesizeIndex")
        .WithSummary("Synthesizes a non-blocking PostgreSQL index recommendation for a slow query.");

        // 3. GET /indexes/recommendations
        group.MapGet("/indexes/recommendations", (
            [FromServices] IAutonomousIndexSynthesizerEngine engine) =>
        {
            return Results.Ok(engine.GetActiveRecommendations());
        })
        .WithName("GetIndexRecommendations")
        .WithSummary("Retrieves all active database index recommendations.");

        // 4. GET /bloat-audit
        group.MapGet("/bloat-audit", (
            [FromQuery] string? table,
            [FromQuery] long? liveTuples,
            [FromQuery] long? deadTuples,
            [FromQuery] long? totalBytes,
            [FromServices] ITableBloatAndVacuumOptimizerService bloatService) =>
        {
            if (string.IsNullOrWhiteSpace(table))
            {
                return Results.BadRequest(new { error = "Query parameter 'table' is required." });
            }

            var live = liveTuples ?? 100_000L;
            var dead = deadTuples ?? 0L;
            var bytes = totalBytes ?? 100_000_000L;

            try
            {
                var metric = bloatService.AuditTableBloat(table, live, dead, bytes);
                return Results.Ok(metric);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("AuditTableBloat")
        .WithSummary("Evaluates PostgreSQL table MVCC bloat and dead-tuple ratio.");

        // 5. POST /vacuum/schedule
        group.MapPost("/vacuum/schedule", (
            [FromBody] ScheduleVacuumRequest request,
            [FromServices] ITableBloatAndVacuumOptimizerService bloatService) =>
        {
            if (request is null || string.IsNullOrWhiteSpace(request.TableName))
            {
                return Results.BadRequest(new { error = "TableName is required." });
            }

            var task = bloatService.ScheduleVacuumTask(request);
            return Results.Ok(task);
        })
        .WithName("ScheduleVacuum")
        .WithSummary("Schedules a vacuum or concurrent reindex task.");

        // 6. GET /vacuum/pending
        group.MapGet("/vacuum/pending", (
            [FromServices] ITableBloatAndVacuumOptimizerService bloatService) =>
        {
            return Results.Ok(bloatService.GetPendingVacuumTasks());
        })
        .WithName("GetPendingVacuumTasks")
        .WithSummary("Lists all pending vacuum and reindex tasks.");

        // 7. GET /memory/status
        group.MapGet("/memory/status", (
            [FromServices] IMemoryAllocationAutopilotService memoryService) =>
        {
            var health = memoryService.EvaluateMemoryHealth();
            return Results.Ok(health);
        })
        .WithName("GetMemoryStatus")
        .WithSummary("Evaluates live .NET GC memory pressure and cache throttle posture.");

        // 8. POST /memory/tune
        group.MapPost("/memory/tune", (
            [FromBody] TuneMemoryRequest request,
            [FromServices] IMemoryAllocationAutopilotService memoryService) =>
        {
            if (request is null || string.IsNullOrWhiteSpace(request.TargetCache))
            {
                return Results.BadRequest(new { error = "TargetCache is required." });
            }

            var action = memoryService.ApplyCacheBudgetTuning(request.TargetCache, request.TargetReductionPct);
            return Results.Ok(action);
        })
        .WithName("TuneMemoryCache")
        .WithSummary("Manually adjusts cache budget allocation.");

        return app;
    }
}
