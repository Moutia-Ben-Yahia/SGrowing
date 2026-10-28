using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
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

    public static IEndpointRouteBuilder MapAutonomousFunnelEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/funnels/autonomous")
            .WithTags("15-Step Idea-to-Funnel Autonomous Workshop");

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

        return app;
    }
}
