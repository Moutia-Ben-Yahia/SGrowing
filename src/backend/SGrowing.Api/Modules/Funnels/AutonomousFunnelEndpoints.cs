using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Agents.Git;
using SGrowing.Api.Modules.Funnels.Autonomous;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Funnels.Autonomous;

public static class AutonomousFunnelEndpoints
{
    private static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private static Guid ResolveTenantId(Guid? tenantId, Guid? headerTenantId)
    {
        if (tenantId.HasValue && tenantId.Value != Guid.Empty) return tenantId.Value;
        if (headerTenantId.HasValue && headerTenantId.Value != Guid.Empty) return headerTenantId.Value;
        return DefaultTenantId;
    }

    private static IAutonomousFunnelBuilderService ResolveBuilderService(IServiceProvider sp)
    {
        return sp.GetService<IAutonomousFunnelBuilderService>()
            ?? new AutonomousFunnelBuilderService(sp.GetRequiredService<SGrowingDbContext>());
    }

    public static IEndpointRouteBuilder MapAutonomousFunnelEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/funnels/autonomous")
            .WithTags("15-Step Idea-to-Funnel Autonomous Workshop");

        // =====================================================================
        // 1. Horizon 11 Idea-to-Funnel 15-Step Pipeline & Liquid Section Engine
        // =====================================================================

        group.MapPost("/generate", async (
            [FromBody] AutonomousFunnelGenerateRequest req,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IServiceProvider sp,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var builder = ResolveBuilderService(sp);
            try
            {
                var spec = await builder.GenerateFunnelAsync(effectiveTenantId, req, ct);
                return Results.Ok(spec);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("GenerateAutonomousFunnel")
        .WithSummary("Execute the 15-step deterministic idea-to-funnel generation pipeline");

        group.MapGet("/status/{jobId}", async (
            string jobId,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IServiceProvider sp,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var builder = ResolveBuilderService(sp);
            var status = await builder.GetJobStatusAsync(effectiveTenantId, jobId, ct);
            if (status == null) return Results.NotFound(new { error = $"Job '{jobId}' not found" });
            return Results.Ok(status);
        })
        .WithName("GetAutonomousFunnelJobStatus")
        .WithSummary("Get step-by-step progress (1 to 15) with step names, durations and artifacts");

        group.MapGet("/{funnelId:guid}", async (
            Guid funnelId,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IServiceProvider sp,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var builder = ResolveBuilderService(sp);
            var spec = await builder.GetFunnelByIdAsync(effectiveTenantId, funnelId, ct);
            if (spec == null) return Results.NotFound(new { error = $"Funnel with ID '{funnelId}' not found" });
            return Results.Ok(spec);
        })
        .WithName("GetAutonomousFunnelSpecification")
        .WithSummary("Retrieve full compiled funnel specification, Liquid sections, and tokens");

        group.MapPost("/{funnelId:guid}/publish", async (
            Guid funnelId,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IServiceProvider sp,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var builder = ResolveBuilderService(sp);
            try
            {
                var result = await builder.PublishFunnelAsync(effectiveTenantId, funnelId, ct);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new { error = $"Funnel with ID '{funnelId}' not found" });
            }
        })
        .WithName("PublishAutonomousFunnel")
        .WithSummary("Publish funnel to live public slug URL");

        group.MapPost("/order-bump-roi", async (
            [FromBody] EvaluateOrderBumpRoiRequest req,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IServiceProvider sp) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var builder = ResolveBuilderService(sp);
            var roi = await builder.EvaluateOrderBumpRoiAsync(
                effectiveTenantId,
                req.BasePrice,
                req.BumpPrice,
                req.BumpMarginPct,
                req.EstimatedTakeRate
            );
            return Results.Ok(roi);
        })
        .WithName("EvaluateAutonomousFunnelOrderBumpRoi")
        .WithSummary("Evaluate expected value for an order bump proposal (EV_bump = P_bump * Price_bump * Margin - Friction)");

        // =====================================================================
        // 2. Legacy Runs, GitHub Drift & Split-Testing Endpoints
        // =====================================================================

        group.MapPost("/execute", async (
            [FromBody] ExecuteAutonomousFunnelRequest req,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IAutonomousFunnelPipelineService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var result = await service.ExecutePipelineAsync(effectiveTenantId, req, ct);
            return Results.Ok(result);
        })
        .WithName("ExecuteAutonomousFunnelPipeline")
        .WithSummary("Trigger 15-step autonomous idea-to-deployed business funnel pipeline");

        group.MapGet("/runs", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IAutonomousFunnelPipelineService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var runs = await service.GetRunsAsync(effectiveTenantId, ct);
            return Results.Ok(runs);
        })
        .WithName("GetAutonomousFunnelRuns")
        .WithSummary("List past autonomous funnel runs and status");

        group.MapGet("/runs/{id:guid}", async (
            Guid id,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IAutonomousFunnelPipelineService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var run = await service.GetRunByIdAsync(effectiveTenantId, id, ct);
            if (run == null) return Results.NotFound(new { error = "Autonomous funnel run not found" });
            return Results.Ok(run);
        })
        .WithName("GetAutonomousFunnelRunDetail")
        .WithSummary("Get full step-by-step audit trail and preview for a specific run");

        group.MapPost("/runs/{id:guid}/git/detect-drift", async (
            Guid id,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IGitHubAutomationService gitService,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var result = await gitService.DetectFunnelDriftAsync(effectiveTenantId, id, ct);
            if (result == null) return Results.NotFound(new { error = "Autonomous funnel run not found" });
            return Results.Ok(result);
        })
        .WithName("DetectAutonomousFunnelDrift")
        .WithSummary("Analyze copy and pricing drift between repository template and autonomous funnel run");

        group.MapPost("/runs/{id:guid}/git/create-pr", async (
            Guid id,
            [FromBody] CreateFunnelPrRequest req,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IGitHubAutomationService gitService,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var result = await gitService.CreatePullRequestForFunnelDriftAsync(effectiveTenantId, id, req, ct);
            if (result == null) return Results.NotFound(new { error = "Autonomous funnel run not found" });
            return Results.Ok(result);
        })
        .WithName("CreateFunnelDriftPullRequest")
        .WithSummary("Auto-open GitHub PR with commit diffs and automated persona reviews to sync funnel drift");

        group.MapGet("/runs/{id:guid}/split-test", async (
            Guid id,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IFunnelSplitTestService splitService,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var test = await splitService.GetSplitTestAsync(effectiveTenantId, id, ct);
            if (test == null) return Results.NotFound(new { error = "No active split test found for this funnel run" });
            return Results.Ok(test);
        })
        .WithName("GetFunnelSplitTest")
        .WithSummary("Get A/B split-testing metrics, Bayesian conversion probabilities, and winning variant");

        group.MapPost("/runs/{id:guid}/split-test", async (
            Guid id,
            [FromBody] CreateFunnelSplitTestRequest req,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IFunnelSplitTestService splitService,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var test = await splitService.CreateOrGetSplitTestAsync(effectiveTenantId, id, req, ct);
            return Results.Ok(test);
        })
        .WithName("CreateOrConfigureFunnelSplitTest")
        .WithSummary("Create or configure an A/B split-testing campaign with traffic allocation ratios");

        group.MapPost("/runs/{id:guid}/split-test/event", async (
            Guid id,
            [FromBody] RecordSplitTestEventRequest req,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IFunnelSplitTestService splitService,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var test = await splitService.RecordEventAsync(effectiveTenantId, id, req, ct);
            if (test == null) return Results.NotFound(new { error = "No active split test found for this funnel run" });
            return Results.Ok(test);
        })
        .WithName("RecordFunnelSplitTestEvent")
        .WithSummary("Record view impression or successful checkout conversion for A/B variant");

        group.MapPost("/runs/{id:guid}/split-test/conclude", async (
            Guid id,
            [FromQuery] string winningVariant,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IFunnelSplitTestService splitService,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            bool ok = await splitService.ConcludeTestAsync(effectiveTenantId, id, winningVariant, ct);
            return ok ? Results.Ok(new { success = true, winningVariant }) : Results.NotFound(new { error = "Split test not found" });
        })
        .WithName("ConcludeFunnelSplitTest")
        .WithSummary("Conclude split test and promote chosen variant to 100% traffic");

        return app;
    }
}
