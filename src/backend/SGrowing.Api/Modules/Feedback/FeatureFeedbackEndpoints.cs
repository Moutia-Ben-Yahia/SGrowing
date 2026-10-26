using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Feedback;

public record CreateFeedbackPostRequest(
    Guid TenantId,
    string Title,
    string Description,
    string Category = "Feature",
    string AuthorEmail = "",
    string AuthorName = ""
);

public record ToggleVoteRequest(
    Guid TenantId,
    string VoterIdentifier
);

public record UpdatePostStatusRequest(
    Guid TenantId,
    FeedbackStatus NewStatus
);

public static class FeatureFeedbackEndpoints
{
    public static IEndpointRouteBuilder MapFeatureFeedbackEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/feedback")
            .WithTags("Public Roadmap & Feature Voting Board (fider & formbricks)");

        group.MapPost("/posts", async ([FromBody] CreateFeedbackPostRequest req, IFeatureFeedbackService service) =>
        {
            var post = await service.CreatePostAsync(req.TenantId, req.Title, req.Description, req.Category, req.AuthorEmail, req.AuthorName);
            return Results.Ok(post);
        });

        group.MapGet("/posts", async ([FromQuery] Guid tenantId, [FromQuery] FeedbackStatus? status, [FromQuery] string? category, IFeatureFeedbackService service) =>
        {
            var posts = await service.GetPostsAsync(tenantId, status, category);
            return Results.Ok(posts);
        });

        group.MapPost("/posts/{id:guid}/vote", async (Guid id, [FromBody] ToggleVoteRequest req, IFeatureFeedbackService service) =>
        {
            var result = await service.ToggleVoteAsync(req.TenantId, id, req.VoterIdentifier);
            return Results.Ok(result);
        });

        group.MapPatch("/posts/{id:guid}/status", async (Guid id, [FromBody] UpdatePostStatusRequest req, IFeatureFeedbackService service) =>
        {
            var post = await service.UpdatePostStatusAsync(req.TenantId, id, req.NewStatus);
            return Results.Ok(post);
        });

        group.MapGet("/roadmap", async ([FromQuery] Guid tenantId, IFeatureFeedbackService service) =>
        {
            var roadmap = await service.GetRoadmapBoardAsync(tenantId);
            return Results.Ok(roadmap);
        });

        return routes;
    }
}
