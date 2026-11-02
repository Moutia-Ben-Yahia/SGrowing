using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Marketing.SocialPublishing;

public static class SocialPublishingEndpoints
{
    public static IServiceCollection AddSocialPublishingServices(this IServiceCollection services)
    {
        services.AddSingleton<ISocialPublishingEngine, SocialPublishingEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapSocialPublishingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/marketing/social")
            .WithTags("Social Media Publishing & Multi-Platform Execution Engine");

        // 1. POST /posts/schedule
        group.MapPost("/posts/schedule", (
            [FromBody] SchedulePostRequest request,
            ISocialPublishingEngine engine) =>
        {
            try
            {
                var post = engine.SchedulePost(request);
                return Results.Created($"/api/v1/marketing/social/posts/{post.TenantId}/{post.Id}", post);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("ScheduleSocialMediaPost")
        .WithSummary("Schedules a social media post across selected platforms with guardrail validations.");

        // 2. POST /posts/approve
        group.MapPost("/posts/approve", (
            [FromBody] ApprovePostRequest request,
            ISocialPublishingEngine engine) =>
        {
            try
            {
                var post = engine.ApprovePost(request);
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
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("ApproveSocialMediaPost")
        .WithSummary("Approves or rejects a scheduled social media post in the state machine.");

        // 3. POST /posts/publish-now
        group.MapPost("/posts/publish-now", (
            [FromBody] PublishNowRequest request,
            ISocialPublishingEngine engine) =>
        {
            try
            {
                var post = engine.PublishNow(request);
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
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("PublishSocialMediaPostNow")
        .WithSummary("Immediately dispatches and publishes an approved social media post.");

        // 4. GET /calendar/{tenantId}
        group.MapGet("/calendar/{tenantId}", (
            [FromRoute] string tenantId,
            [FromQuery] DateTime? startUtc,
            [FromQuery] DateTime? endUtc,
            ISocialPublishingEngine engine) =>
        {
            try
            {
                var start = startUtc ?? DateTime.UtcNow.AddDays(-30);
                var end = endUtc ?? DateTime.UtcNow.AddDays(90);
                var posts = engine.GetCalendar(tenantId, start, end);
                return Results.Ok(posts);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("GetSocialCalendar")
        .WithSummary("Retrieves the editorial and scheduled publishing calendar for a tenant.");

        // 5. POST /analytics/webhook
        group.MapPost("/analytics/webhook", (
            [FromBody] SocialAnalyticsWebhookPayload payload,
            ISocialPublishingEngine engine) =>
        {
            try
            {
                var post = engine.IngestAnalytics(payload);
                return Results.Ok(post);
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
        .WithName("IngestSocialAnalyticsWebhook")
        .WithSummary("Ingests multi-platform post analytics webhook payloads and aggregates performance metrics.");

        // 6. GET /posts/{tenantId}/{postId}
        group.MapGet("/posts/{tenantId}/{postId:guid}", (
            [FromRoute] string tenantId,
            [FromRoute] Guid postId,
            ISocialPublishingEngine engine) =>
        {
            try
            {
                var post = engine.GetPost(tenantId, postId);
                return post != null
                    ? Results.Ok(post)
                    : Results.NotFound(new { error = $"Post {postId} was not found for tenant {tenantId}." });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("GetSocialMediaPost")
        .WithSummary("Fetches a single social post by tenant and identifier.");

        return endpoints;
    }
}
