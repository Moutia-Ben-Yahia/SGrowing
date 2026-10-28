using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using SGrowing.Api.Modules.Conversations;

namespace SGrowing.Api.Modules.AI;

public record GenerateCopilotDraftRequest(string Content);
public record AcceptCopilotDraftRequest(string? EditedReply = null);
public record RejectCopilotDraftRequest(string? Reason = null);

public static class ConversationCopilotEndpoints
{
    private static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private static Guid ResolveTenantId(Guid? tenantId, Guid? headerTenantId)
    {
        if (tenantId.HasValue && tenantId.Value != Guid.Empty) return tenantId.Value;
        if (headerTenantId.HasValue && headerTenantId.Value != Guid.Empty) return headerTenantId.Value;
        return DefaultTenantId;
    }

    public static IEndpointRouteBuilder MapConversationCopilotEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/conversations/{id:guid}/copilot")
            .WithTags("In-Thread AI Copilot & pgvector RAG Suggestions");

        group.MapPost("/suggest", async (
            Guid id,
            [FromBody] GenerateCopilotDraftRequest req,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IAiCopilotService copilot,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var draft = await copilot.GenerateDraftAsync(effectiveTenantId, id, req.Content, ct);
            return Results.Ok(draft);
        })
        .WithName("GenerateConversationCopilotSuggestion")
        .WithSummary("Generate sub-second AI suggested response grounded in pgvector FAQ articles");

        group.MapPost("/drafts/{draftId:guid}/accept", async (
            Guid id,
            Guid draftId,
            [FromBody] AcceptCopilotDraftRequest req,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IAiCopilotService copilot,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var message = await copilot.AcceptDraftAsync(effectiveTenantId, draftId, req.EditedReply, ct);
            if (message == null) return Results.NotFound(new { error = "Draft or conversation not found or already accepted" });
            return Results.Ok(message);
        })
        .WithName("AcceptConversationCopilotDraft")
        .WithSummary("1-click Accept AI draft, converting it into an outbound agent message");

        group.MapPost("/drafts/{draftId:guid}/reject", async (
            Guid id,
            Guid draftId,
            [FromBody] RejectCopilotDraftRequest req,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IAiCopilotService copilot,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var success = await copilot.RejectDraftAsync(effectiveTenantId, draftId, req.Reason, ct);
            if (!success) return Results.NotFound(new { error = "Draft not found" });
            return Results.Ok(new { status = "Rejected", draftId });
        })
        .WithName("RejectConversationCopilotDraft")
        .WithSummary("Dismiss AI suggested response with optional feedback loop reason");

        group.MapGet("/drafts", async (
            Guid id,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IAiCopilotService copilot,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var drafts = await copilot.GetDraftsAsync(effectiveTenantId, id, ct);
            return Results.Ok(drafts);
        })
        .WithName("GetConversationCopilotDrafts")
        .WithSummary("Fetch all AI suggested reply drafts generated for this thread");

        return app;
    }
}
