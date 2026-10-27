using Microsoft.AspNetCore.Mvc;

namespace SGrowing.Api.Modules.Funnels;

public static class ChatbotFlowEndpoints
{
    public static IEndpointRouteBuilder MapChatbotFlowEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/funnels/chatbot")
            .WithTags("Chatbot Flows & Comment Marketing");

        group.MapPost("/flows", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid tenantId,
            [FromBody] CreateFlowRequest request,
            IChatbotFlowEngineService service) =>
        {
            var result = await service.RegisterFlowAsync(
                tenantId,
                request.Name,
                request.Channel,
                request.Nodes,
                request.EntryNodeId
            );
            return Results.Ok(result);
        });

        group.MapPost("/flows/{id:guid}/execute", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid tenantId,
            Guid id,
            [FromBody] ExecuteFlowRequest request,
            IChatbotFlowEngineService service) =>
        {
            var result = await service.ExecuteFlowStepAsync(
                tenantId,
                id,
                request.CurrentNodeId,
                request.Variables ?? new Dictionary<string, object>(),
                request.UserResponse
            );
            return Results.Ok(result);
        });

        group.MapPost("/comments/webhook", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid tenantId,
            [FromBody] SocialCommentEventDto request,
            IChatbotFlowEngineService service) =>
        {
            var result = await service.ProcessSocialCommentTriggerAsync(tenantId, request);
            return Results.Ok(result);
        });

        return app;
    }
}

public record CreateFlowRequest(
    string Name,
    string Channel,
    List<ChatbotFlowNodeDto> Nodes,
    string EntryNodeId
);

public record ExecuteFlowRequest(
    string? CurrentNodeId,
    Dictionary<string, object>? Variables,
    string? UserResponse
);
