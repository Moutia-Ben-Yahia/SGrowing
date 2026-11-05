using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.AI.Governance;

public record EvaluateActionApprovalRequest(
    Guid TenantId,
    string OperationType,
    string Description,
    decimal FinancialImpactUsd,
    string PayloadJson
);

public record ApproveActionRequest(
    Guid RequestId,
    string Token,
    string ApprovedByUserId
);

public record RejectActionRequest(
    Guid RequestId,
    string RejectedByUserId,
    string Reason
);

public static class AiGovernanceEndpoints
{
    public static IServiceCollection AddAiGovernanceServices(this IServiceCollection services)
    {
        services.AddSingleton<IAiGovernanceEngine, AiGovernanceEngine>();
        services.AddScoped<IApprovalGatekeeperService, ApprovalGatekeeperService>();
        return services;
    }

    public static IEndpointRouteBuilder MapAiGovernanceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/ai/governance")
            .WithTags("AI - Governance, Smart Routing & Prompt Injection Firewall");

        group.MapPost("/prompt/inspect", (
            InspectPromptRequest request,
            IAiGovernanceEngine engine) =>
        {
            var result = engine.InspectPrompt(request);
            return Results.Ok(result);
        })
        .WithName("InspectPromptForThreatsAndPii")
        .WithSummary("Inspects prompt for adversarial jailbreak injections and redacts sensitive PII.");

        group.MapPost("/model/route", (
            RouteModelRequest request,
            IAiGovernanceEngine engine) =>
        {
            var decision = engine.RouteModel(request);
            return Results.Ok(decision);
        })
        .WithName("RouteModelByComplexity")
        .WithSummary("Calculates optimal Multi-LLM routing with latency and cost estimation based on task complexity.");

        group.MapPost("/output/audit", (
            AuditOutputRequest request,
            IAiGovernanceEngine engine) =>
        {
            var audit = engine.AuditOutput(request);
            return Results.Ok(audit);
        })
        .WithName("AuditModelOutputSafety")
        .WithSummary("Audits generated AI output for toxicity, unauthorized pricing commitments, and hallucination indicators.");

        group.MapGet("/threat-log/{tenantId}", (
            string tenantId,
            IAiGovernanceEngine engine) =>
        {
            var logs = engine.GetThreatAuditLog(tenantId);
            return Results.Ok(logs);
        })
        .WithName("GetTenantThreatAuditLog")
        .WithSummary("Retrieves the security threat and prompt injection audit log for a given tenant.");

        // HITL Approval Mesh Endpoints (PRD Section 5.8 & 7.4)
        group.MapPost("/approval/evaluate", async (
            EvaluateActionApprovalRequest req,
            IApprovalGatekeeperService gatekeeper) =>
        {
            var result = await gatekeeper.EvaluateAndRegisterActionAsync(
                req.TenantId, req.OperationType, req.Description, req.FinancialImpactUsd, req.PayloadJson);
            return Results.Ok(result);
        })
        .WithName("EvaluateAndRegisterApprovalAction")
        .WithSummary("Evaluates autonomous action risk and registers cryptographic approval request if thresholds exceeded.");

        group.MapPost("/approval/approve", async (
            ApproveActionRequest req,
            IApprovalGatekeeperService gatekeeper) =>
        {
            var result = await gatekeeper.ApproveWithTokenAsync(req.RequestId, req.Token, req.ApprovedByUserId);
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        })
        .WithName("ApproveActionWithCryptographicToken")
        .WithSummary("Verifies one-time token and grants human-in-the-loop approval.");

        group.MapPost("/approval/reject", async (
            RejectActionRequest req,
            IApprovalGatekeeperService gatekeeper) =>
        {
            var result = await gatekeeper.RejectActionAsync(req.RequestId, req.RejectedByUserId, req.Reason);
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        })
        .WithName("RejectActionWithReason")
        .WithSummary("Rejects a pending autonomous operation with justification.");

        group.MapGet("/approval/pending/{tenantId:guid}", async (
            Guid tenantId,
            IApprovalGatekeeperService gatekeeper) =>
        {
            var pending = await gatekeeper.GetPendingApprovalsAsync(tenantId);
            return Results.Ok(pending);
        })
        .WithName("GetTenantPendingApprovals")
        .WithSummary("Fetches all pending actions awaiting human approval for a given tenant.");

        return endpoints;
    }
}
