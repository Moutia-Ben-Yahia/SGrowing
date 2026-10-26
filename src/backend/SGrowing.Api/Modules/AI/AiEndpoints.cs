using Microsoft.AspNetCore.Mvc;
using SGrowing.Core.Tenancy;

namespace SGrowing.Api.Modules.AI;

public static class AiEndpoints
{
    public static void MapAiEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/ai").RequireAuthorization();

        group.MapPost("/tools/execute", async (
            [FromServices] IAiAgentOrchestratorService service,
            [FromServices] ITenantContext tenant,
            [FromBody] ExecuteToolRequest request) =>
        {
            var result = await service.ExecuteToolAsync(
                tenant.TenantId,
                request.AgentName ?? "GeneralAgent",
                request.ToolName,
                request.ArgumentsJson ?? "{}",
                request.UserApproved
            );
            return Results.Ok(result);
        });

        group.MapGet("/budgets", async (
            [FromServices] IAiAgentOrchestratorService service,
            [FromServices] ITenantContext tenant) =>
        {
            var budget = await service.GetBudgetAsync(tenant.TenantId);
            return Results.Ok(budget);
        });

        group.MapPost("/budgets", async (
            [FromServices] IAiAgentOrchestratorService service,
            [FromServices] ITenantContext tenant,
            [FromBody] AllocateBudgetRequest request) =>
        {
            var budget = await service.AllocateBudgetAsync(
                tenant.TenantId,
                request.TokenLimit,
                request.MaxCostUsd,
                request.EnforceHardCap
            );
            return Results.Ok(budget);
        });

        group.MapGet("/audit-logs", async (
            [FromServices] IAiAgentOrchestratorService service,
            [FromServices] ITenantContext tenant,
            [FromQuery] int limit = 50) =>
        {
            var logs = await service.GetAuditLogsAsync(tenant.TenantId, limit);
            return Results.Ok(logs);
        });

        group.MapPost("/conversational-bi", async (
            [FromServices] IConversationalBiService service,
            [FromServices] ITenantContext tenant,
            [FromBody] ConversationalBiQueryRequest request) =>
        {
            var response = await service.AskBiQuestionAsync(tenant.TenantId, request.Question);
            return Results.Ok(response);
        });
    }
}

public record ExecuteToolRequest(
    string ToolName,
    string? AgentName = null,
    string? ArgumentsJson = null,
    bool UserApproved = false
);

public record AllocateBudgetRequest(
    long TokenLimit = 1_000_000,
    decimal MaxCostUsd = 50.0m,
    bool EnforceHardCap = true
);

public record ConversationalBiQueryRequest(
    string Question
);
