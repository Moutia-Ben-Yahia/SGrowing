using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace SGrowing.Api.Modules.Social;

public static class SocialPublishingEndpoints
{
    public static IEndpointRouteBuilder MapSocialPublishingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/social/publishing")
            .WithTags("Horizon 49: Multi-Platform Social Media Content Calendar & Automated Publishing Dispatcher");

        // 1. POST /compose: Compose a new multi-platform post
        group.MapPost("/compose", async (
            [FromBody] ComposePostRequest request,
            ISocialPublishingService service) =>
        {
            try
            {
                var post = await service.ComposePostAsync(request);
                return Results.Ok(post);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("ComposeSocialPost")
        .WithSummary("Compose a unified multi-platform social post with platform-specific overrides and media attachments.");

        // 2. POST /validate: Validate post content and platform constraints
        group.MapPost("/validate", (
            [FromBody] ComposePostRequest request,
            ISocialPublishingService service) =>
        {
            var report = service.ValidateRequest(request);
            return Results.Ok(report);
        })
        .WithName("ValidateSocialPost")
        .WithSummary("Validate post length, media count, aspect ratios, and hashtag rules across selected platforms.");

        // 3. POST /schedule: Schedule a post for future automated publishing
        group.MapPost("/schedule", async (
            [FromBody] SchedulePostRequest request,
            ISocialPublishingService service) =>
        {
            try
            {
                var post = await service.SchedulePostAsync(request.PostId, request.ScheduledAtUtc);
                return Results.Ok(post);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        })
        .WithName("ScheduleSocialPost")
        .WithSummary("Lock and transition a post to Scheduled status with target publication timestamp.");

        // 4. POST /publish-now/{id:guid}: Immediately dispatch post to target platforms
        group.MapPost("/publish-now/{id:guid}", async (
            Guid id,
            ISocialPublishingService service) =>
        {
            try
            {
                var post = await service.PublishNowAsync(id);
                return Results.Ok(post);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        })
        .WithName("PublishSocialPostNow")
        .WithSummary("Immediately dispatch post to connected platform APIs with latency measurement and external post ID tracking.");

        // 5. GET /calendar: Query content calendar posts with flexible filters
        group.MapGet("/calendar", async (
            [FromQuery] Guid? tenantId,
            [FromQuery] int? year,
            [FromQuery] int? month,
            [FromQuery] DateTime? start,
            [FromQuery] DateTime? end,
            [FromQuery] PublishingStatus? status,
            [FromQuery] string? platform,
            ISocialPublishingService service) =>
        {
            var posts = await service.GetCalendarPostsAsync(
                tenantId: tenantId,
                year: year,
                month: month,
                startDate: start,
                endDate: end,
                status: status,
                platform: platform);
            return Results.Ok(posts);
        })
        .WithName("GetSocialCalendarPosts")
        .WithSummary("Retrieve content calendar posts filtered by month, date window, publishing status, or social platform.");

        // 6. GET /accounts: List connected social profiles and OAuth 2.0 token health
        group.MapGet("/accounts", async (
            [FromQuery] Guid? tenantId,
            ISocialPublishingService service) =>
        {
            var accounts = await service.GetConnectedAccountsAsync(tenantId);
            return Results.Ok(accounts);
        })
        .WithName("GetConnectedSocialAccounts")
        .WithSummary("Retrieve all connected social profiles (Meta, X, LinkedIn, TikTok) with token expiration health status.");

        // 7. POST /refresh-token/{accountId:guid}: Refresh OAuth 2.0 access token
        group.MapPost("/refresh-token/{accountId:guid}", async (
            Guid accountId,
            ISocialPublishingService service) =>
        {
            try
            {
                var account = await service.RefreshTokenAsync(accountId);
                return Results.Ok(account);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .WithName("RefreshSocialAccountToken")
        .WithSummary("Trigger proactive OAuth 2.0 token exchange, extending access validity by 60 days.");

        return app;
    }
}
