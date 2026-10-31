using Microsoft.AspNetCore.Mvc;

namespace SGrowing.Api.Modules.CRM.Pipeline;

public static class DealSlippageRadarEndpoints
{
    public static IEndpointRouteBuilder MapDealSlippageRadarEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/crm/pipeline-governance")
            .WithTags("PipelineGovernance");

        group.MapPost("/deals", async (
            [FromBody] CreateDealRequest request,
            [FromServices] IDealSlippageRadarService service,
            CancellationToken ct) =>
        {
            try
            {
                var deal = await service.CreateDealAsync(request, ct);
                return Results.Created($"/api/v1/crm/pipeline-governance/deals/{deal.DealId}", deal);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapGet("/deals", async (
            [FromQuery] Guid tenantId,
            [FromQuery] OpportunityStage? stage,
            [FromServices] IDealSlippageRadarService service,
            CancellationToken ct) =>
        {
            var deals = await service.ListDealsAsync(tenantId, stage, ct);
            return Results.Ok(deals);
        });

        group.MapGet("/deals/{id:guid}/health", async (
            Guid id,
            [FromQuery] Guid tenantId,
            [FromServices] IDealSlippageRadarService service,
            CancellationToken ct) =>
        {
            try
            {
                var health = await service.AnalyzeDealHealthAsync(tenantId, id, ct);
                return Results.Ok(health);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        });

        group.MapPost("/deals/{id:guid}/advance", async (
            Guid id,
            [FromBody] UpdateDealStageApiRequest request,
            [FromServices] IDealSlippageRadarService service,
            CancellationToken ct) =>
        {
            try
            {
                var req = new UpdateDealStageRequest(request.TenantId, id, request.TargetStage);
                var updated = await service.AdvanceDealStageAsync(req, ct);
                return Results.Ok(updated);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapPost("/deals/{id:guid}/push-date", async (
            Guid id,
            [FromBody] PushCloseDateApiRequest request,
            [FromServices] IDealSlippageRadarService service,
            CancellationToken ct) =>
        {
            try
            {
                var req = new PushCloseDateRequest(request.TenantId, id, request.NewCloseDateUtc, request.Reason);
                var updated = await service.PushCloseDateAsync(req, ct);
                return Results.Ok(updated);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        });

        group.MapPost("/rules", async (
            [FromBody] DefineStageGateRuleRequest request,
            [FromServices] IDealSlippageRadarService service,
            CancellationToken ct) =>
        {
            try
            {
                var rule = await service.DefineStageGateRuleAsync(request, ct);
                return Results.Created($"/api/v1/crm/pipeline-governance/rules/{rule.RuleId}", rule);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapGet("/rules", async (
            [FromQuery] Guid tenantId,
            [FromServices] IDealSlippageRadarService service,
            CancellationToken ct) =>
        {
            var rules = await service.GetStageGateRulesAsync(tenantId, ct);
            return Results.Ok(rules);
        });

        group.MapGet("/summary", async (
            [FromQuery] Guid tenantId,
            [FromServices] IDealSlippageRadarService service,
            CancellationToken ct) =>
        {
            var summary = await service.GetPipelineSlippageSummaryAsync(tenantId, ct);
            return Results.Ok(summary);
        });

        return app;
    }
}

public record UpdateDealStageApiRequest(Guid TenantId, OpportunityStage TargetStage);
public record PushCloseDateApiRequest(Guid TenantId, DateTime NewCloseDateUtc, string Reason);
