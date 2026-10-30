using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SGrowing.Api.Data;

namespace SGrowing.Api.Modules.Feedback;

public static class FeedbackBoardEndpoints
{
    public static IEndpointRouteBuilder MapFeedbackBoardEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/feedback/board")
            .WithTags("Public Community Feedback & ARR-Weighted Roadmap Board");

        static IFeedbackBoardService ResolveService(HttpContext httpContext)
        {
            var svc = httpContext.RequestServices.GetService<IFeedbackBoardService>();
            if (svc != null) return svc;
            var db = httpContext.RequestServices.GetService<SGrowingDbContext>();
            return new FeedbackBoardService(db);
        }

        // 1. GET /posts: List suggestions with filters and sorting
        group.MapGet("/posts", async (
            HttpContext context,
            [FromQuery] Guid? tenantId,
            [FromQuery] string? category,
            [FromQuery] string? status,
            [FromQuery] string? search,
            [FromQuery] string? sortBy) =>
        {
            var tid = tenantId ?? Guid.Empty;
            var service = ResolveService(context);
            var posts = await service.GetPostsAsync(tid, category, status, search, sortBy, context.RequestAborted);
            return Results.Ok(posts);
        })
        .WithName("GetFeedbackBoardPosts")
        .WithSummary("List all feedback ideas with category, status, search, and sorting options");

        // 2. GET /posts/{id}: Detailed post with comments & vote status
        group.MapGet("/posts/{id:guid}", async (
            HttpContext context,
            Guid id,
            [FromQuery] Guid? tenantId,
            [FromQuery] string? voterEmail) =>
        {
            var tid = tenantId ?? Guid.Empty;
            var service = ResolveService(context);
            try
            {
                var detail = await service.GetPostByIdAsync(tid, id, voterEmail, context.RequestAborted);
                return Results.Ok(detail);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .WithName("GetFeedbackBoardPostById")
        .WithSummary("Get a specific feedback post with full comment thread and vote status");

        // 3. POST /posts: Create feedback suggestion
        group.MapPost("/posts", async (
            HttpContext context,
            [FromBody] CreateBoardPostRequest request,
            [FromQuery] Guid? tenantId) =>
        {
            var effectiveTenantId = request.TenantId != Guid.Empty ? request.TenantId : (tenantId ?? Guid.Empty);
            var effectiveRequest = request with { TenantId = effectiveTenantId };
            var service = ResolveService(context);

            try
            {
                var post = await service.CreatePostAsync(effectiveRequest, context.RequestAborted);
                return Results.Created($"/api/v1/feedback/board/posts/{post.Id}", post);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("CreateFeedbackBoardPost")
        .WithSummary("Submit a new community feedback feature request or suggestion");

        // 4. POST /posts/{id}/vote: Toggle upvote with anti-gaming
        group.MapPost("/posts/{id:guid}/vote", async (
            HttpContext context,
            Guid id,
            [FromBody] ToggleBoardVoteRequest request,
            [FromQuery] Guid? tenantId) =>
        {
            var tid = request.TenantId != Guid.Empty ? request.TenantId : (tenantId ?? Guid.Empty);
            var service = ResolveService(context);

            var ip = !string.IsNullOrWhiteSpace(request.VoterIp)
                ? request.VoterIp
                : context.Connection.RemoteIpAddress?.ToString();

            try
            {
                var result = await service.ToggleVoteAsync(tid, id, request.VoterEmail, ip, context.RequestAborted);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("ToggleFeedbackBoardVote")
        .WithSummary("Toggle upvote on a post with ARR-impact calculation and anti-gaming protection");

        // 5. POST /posts/{id}/comments: Add discussion or official team response
        group.MapPost("/posts/{id:guid}/comments", async (
            HttpContext context,
            Guid id,
            [FromBody] AddFeedbackCommentRequest request,
            [FromQuery] Guid? tenantId) =>
        {
            var tid = request.TenantId != Guid.Empty ? request.TenantId : (tenantId ?? Guid.Empty);
            var service = ResolveService(context);

            try
            {
                var comment = await service.AddCommentAsync(tid, id, request with { TenantId = tid }, context.RequestAborted);
                return Results.Created($"/api/v1/feedback/board/posts/{id}/comments/{comment.Id}", comment);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("AddFeedbackBoardComment")
        .WithSummary("Post a public discussion comment or official team response to an idea");

        // 6. PATCH /posts/{id}/status: Admin status transition & release notes
        group.MapPatch("/posts/{id:guid}/status", async (
            HttpContext context,
            Guid id,
            [FromBody] UpdateBoardPostStatusRequest request,
            [FromQuery] Guid? tenantId) =>
        {
            var tid = request.TenantId != Guid.Empty ? request.TenantId : (tenantId ?? Guid.Empty);
            var service = ResolveService(context);

            try
            {
                var updated = await service.UpdatePostStatusAsync(tid, id, request with { TenantId = tid }, context.RequestAborted);
                return Results.Ok(updated);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("UpdateFeedbackBoardStatus")
        .WithSummary("Update post roadmap status (Planned, InProgress, Completed) with response notes");

        // 7. GET /roadmap: Kanban grouping
        group.MapGet("/roadmap", async (
            HttpContext context,
            [FromQuery] Guid? tenantId) =>
        {
            var tid = tenantId ?? Guid.Empty;
            var service = ResolveService(context);
            var roadmap = await service.GetRoadmapAsync(tid, context.RequestAborted);
            return Results.Ok(roadmap);
        })
        .WithName("GetFeedbackBoardRoadmap")
        .WithSummary("Retrieve roadmap grouped into Planned, InProgress, and Completed categories");

        // 8. GET /summary: Summary KPIs
        group.MapGet("/summary", async (
            HttpContext context,
            [FromQuery] Guid? tenantId) =>
        {
            var tid = tenantId ?? Guid.Empty;
            var service = ResolveService(context);
            var summary = await service.GetSummaryAsync(tid, context.RequestAborted);
            return Results.Ok(summary);
        })
        .WithName("GetFeedbackBoardSummary")
        .WithSummary("Retrieve high-level feedback board metrics, community votes, and ARR demand");

        return app;
    }
}
