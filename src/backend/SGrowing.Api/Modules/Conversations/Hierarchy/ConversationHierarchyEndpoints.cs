using Microsoft.AspNetCore.Mvc;

namespace SGrowing.Api.Modules.Conversations.Hierarchy;

public static class ConversationHierarchyEndpoints
{
    public static IEndpointRouteBuilder MapConversationHierarchyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/conversations/hierarchy")
            .WithTags("ConversationHierarchy");

        group.MapPost("/split", async (
            [FromBody] SplitConversationRequest request,
            [FromServices] IConversationHierarchyService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.SplitConversationAsync(request, ct);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapPost("/merge", async (
            [FromBody] MergeConversationsRequest request,
            [FromServices] IConversationHierarchyService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.MergeConversationsAsync(request, ct);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapPost("/link", async (
            [FromBody] LinkConversationApiRequest request,
            [FromServices] IConversationHierarchyService service,
            CancellationToken ct) =>
        {
            try
            {
                var link = await service.LinkConversationsAsync(request.TenantId, request.ParentId, request.ChildId, request.Relationship, request.IsBlocking, ct);
                return Results.Created($"/api/v1/conversations/hierarchy/links/{link.LinkId}", link);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapDelete("/link", async (
            [FromQuery] Guid tenantId,
            [FromQuery] Guid parentId,
            [FromQuery] Guid childId,
            [FromServices] IConversationHierarchyService service,
            CancellationToken ct) =>
        {
            var unlinked = await service.UnlinkConversationsAsync(tenantId, parentId, childId, ct);
            return unlinked ? Results.Ok(new { success = true }) : Results.NotFound();
        });

        group.MapGet("/tree/{conversationId:guid}", async (
            Guid conversationId,
            [FromQuery] Guid tenantId,
            [FromServices] IConversationHierarchyService service,
            CancellationToken ct) =>
        {
            var tree = await service.GetHierarchyTreeAsync(tenantId, conversationId, ct);
            return Results.Ok(tree);
        });

        group.MapGet("/can-close/{conversationId:guid}", async (
            Guid conversationId,
            [FromQuery] Guid tenantId,
            [FromServices] IConversationHierarchyService service,
            CancellationToken ct) =>
        {
            var summary = await service.CanCloseConversationAsync(tenantId, conversationId, ct);
            return Results.Ok(summary);
        });

        return app;
    }
}

public record LinkConversationApiRequest(
    Guid TenantId,
    Guid ParentId,
    Guid ChildId,
    HierarchyRelationshipType Relationship = HierarchyRelationshipType.ParentChild,
    bool IsBlocking = false
);
