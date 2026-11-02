using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Conversations.Unified;

public static class UnifiedInboxEndpoints
{
    public static IServiceCollection AddUnifiedInboxServices(this IServiceCollection services)
    {
        services.AddSingleton<IUnifiedInboxEngine, UnifiedInboxEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapUnifiedInboxEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/conversations/unified")
            .WithTags("Unified Omnichannel Customer Inbox & AI Autonomous Support Escalation");

        group.MapPost("/messages/ingest", (IngestMessageRequest request, IUnifiedInboxEngine engine) =>
        {
            var thread = engine.IngestMessage(request);
            return Results.Ok(thread);
        }).WithName("IngestUnifiedMessage");

        group.MapGet("/threads/{tenantId}", (string tenantId, [FromQuery] TicketStatus? status, IUnifiedInboxEngine engine) =>
        {
            var threads = engine.GetThreads(tenantId, status);
            return Results.Ok(threads);
        }).WithName("GetUnifiedThreads");

        group.MapGet("/threads/{tenantId}/{threadId:guid}", (string tenantId, Guid threadId, IUnifiedInboxEngine engine) =>
        {
            var thread = engine.GetThread(tenantId, threadId);
            return thread is not null ? Results.Ok(thread) : Results.NotFound(new { message = $"Thread '{threadId}' not found for tenant '{tenantId}'." });
        }).WithName("GetUnifiedThreadById");

        group.MapPost("/threads/{threadId:guid}/deflect", (Guid threadId, [FromQuery] string? tenantId, [FromHeader(Name = "X-Tenant-Id")] string? headerTenantId, IUnifiedInboxEngine engine) =>
        {
            var effectiveTenant = !string.IsNullOrWhiteSpace(tenantId) ? tenantId : (!string.IsNullOrWhiteSpace(headerTenantId) ? headerTenantId : string.Empty);
            try
            {
                var result = engine.EvaluateAiDeflection(effectiveTenant, threadId);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { message = ex.Message });
            }
        }).WithName("EvaluateAiDeflection");

        group.MapPost("/threads/{threadId:guid}/escalate", (Guid threadId, EscalateTicketRequest request, IUnifiedInboxEngine engine) =>
        {
            var effective = request.ThreadId == Guid.Empty ? request with { ThreadId = threadId } : request;
            try
            {
                var thread = engine.EscalateTicket(effective);
                return Results.Ok(thread);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { message = ex.Message });
            }
        }).WithName("EscalateUnifiedTicket");

        group.MapPost("/threads/{threadId:guid}/reply", (Guid threadId, AppendAgentReplyRequest request, IUnifiedInboxEngine engine) =>
        {
            try
            {
                var thread = engine.AppendAgentReply(request.TenantId, threadId, request.AgentId, request.AgentName, request.Content, request.Channel);
                return Results.Ok(thread);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { message = ex.Message });
            }
        }).WithName("AppendAgentReply");

        return endpoints;
    }
}
