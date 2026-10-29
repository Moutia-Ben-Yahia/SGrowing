using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace SGrowing.Api.Modules.Conversations.PresenceAndCsat;

public record PresenceHeartbeatRequest(
    Guid ConversationId,
    Guid AgentUserId,
    string AgentName,
    bool IsTyping
);

public record PresenceLeaveRequest(
    Guid ConversationId,
    Guid AgentUserId
);

public static class ConversationPresenceEndpoints
{
    public static IEndpointRouteBuilder MapConversationPresenceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/conversations/presence")
            .WithTags("ConversationPresence");

        group.MapPost("/heartbeat", async (
            Guid tenantId,
            PresenceHeartbeatRequest req,
            IConversationPresenceService service) =>
        {
            var report = await service.HeartbeatPresenceAsync(
                tenantId,
                req.ConversationId,
                req.AgentUserId,
                req.AgentName,
                req.IsTyping);

            return Results.Ok(report);
        });

        group.MapPost("/leave", async (
            Guid tenantId,
            PresenceLeaveRequest req,
            IConversationPresenceService service) =>
        {
            var left = await service.LeavePresenceAsync(tenantId, req.ConversationId, req.AgentUserId);
            return Results.Ok(new { Success = left });
        });

        group.MapGet("/{conversationId:guid}", async (
            Guid tenantId,
            Guid conversationId,
            IConversationPresenceService service) =>
        {
            var report = await service.GetActivePresenceAsync(tenantId, conversationId);
            return Results.Ok(report);
        });

        return app;
    }
}
