using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.AI.Governance;

public static class AiGovernanceEndpoints
{
    public static IServiceCollection AddAiGovernanceServices(this IServiceCollection services)
    {
        services.AddSingleton<IAiGovernanceEngine, AiGovernanceEngine>();
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

        return endpoints;
    }
}
