using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace SGrowing.Api.Modules.Feedback.Community;

public record SubmitFeedbackRequest(
    string Title,
    string Description,
    string AuthorEmail
);

public record VoteFeedbackRequest(
    string VoterEmail
);

public record UpdateStatusRequest(
    string NewStatus,
    string? OfficialResponse = ""
);

public static class CommunityFeedbackEndpoints
{
    public static IEndpointRouteBuilder MapCommunityFeedbackEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/feedback/community")
            .WithTags("CommunityFeedback");

        group.MapPost("/posts", async (
            Guid tenantId,
            SubmitFeedbackRequest req,
            ICommunityFeedbackService service) =>
        {
            var post = await service.SubmitPostAsync(
                tenantId,
                req.Title,
                req.Description,
                req.AuthorEmail);

            return Results.Ok(post);
        });

        group.MapPost("/posts/{postId:guid}/vote", async (
            Guid tenantId,
            Guid postId,
            VoteFeedbackRequest req,
            ICommunityFeedbackService service) =>
        {
            var voted = await service.VotePostAsync(tenantId, postId, req.VoterEmail);
            return voted ? Results.Ok(new { Success = true }) : Results.Conflict(new { Success = false, Message = "Already voted" });
        });

        group.MapPost("/posts/{postId:guid}/status", async (
            Guid tenantId,
            Guid postId,
            UpdateStatusRequest req,
            ICommunityFeedbackService service) =>
        {
            var post = await service.UpdateRoadmapStatusAsync(
                tenantId,
                postId,
                req.NewStatus,
                req.OfficialResponse ?? "");

            return Results.Ok(post);
        });

        group.MapGet("/roadmap", async (
            Guid tenantId,
            ICommunityFeedbackService service) =>
        {
            var roadmap = await service.GetPublicRoadmapAsync(tenantId);
            return Results.Ok(roadmap);
        });

        group.MapGet("/search", async (
            Guid tenantId,
            string query,
            ICommunityFeedbackService service) =>
        {
            var results = await service.SearchPostsAsync(tenantId, query);
            return Results.Ok(results);
        });

        return app;
    }
}
