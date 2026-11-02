using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Social.AutoTriage;

public static class SocialTriageEndpoints
{
    public static IServiceCollection AddSocialTriageServices(this IServiceCollection services)
    {
        services.AddSingleton<SocialTriageEngine>();
        return services;
    }

    public static RouteGroupBuilder MapSocialTriageEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/social/triage")
            .WithTags("SocialSentimentAutoTriage");

        group.MapPost("/inbound", async Task<Results<Ok<TriageQueueItem>, BadRequest<string>>> (
            [FromBody] IngestSocialMessageRequest request,
            [FromServices] SocialTriageEngine engine,
            CancellationToken ct) =>
        {
            try
            {
                var item = await engine.IngestAndTriageMessageAsync(request, ct);
                return TypedResults.Ok(item);
            }
            catch (Exception ex)
            {
                return TypedResults.BadRequest(ex.Message);
            }
        })
        .WithName("IngestAndTriageSocialMessage")
        .WithSummary("Ingest cross-channel social DM and evaluate real-time sentiment/hazard");

        group.MapGet("/queue", async Task<Ok<IReadOnlyList<TriageQueueItem>>> (
            [FromQuery] string? channel,
            [FromQuery] string? status,
            [FromServices] SocialTriageEngine engine,
            CancellationToken ct) =>
        {
            var items = await engine.GetQueueItemsAsync(channel, status, ct);
            return TypedResults.Ok(items);
        })
        .WithName("GetSocialTriageQueue")
        .WithSummary("Retrieve prioritized triage queue items with SLA countdowns");

        group.MapPost("/queue/{id}/approve-reply", async Task<Results<Ok<TriageQueueItem>, NotFound<string>>> (
            [FromRoute] string id,
            [FromBody] ApproveReplyRequest request,
            [FromServices] SocialTriageEngine engine,
            CancellationToken ct) =>
        {
            try
            {
                var item = await engine.ApproveAndSendReplyAsync(id, request, ct);
                return TypedResults.Ok(item);
            }
            catch (KeyNotFoundException)
            {
                return TypedResults.NotFound($"Queue item '{id}' not found.");
            }
        })
        .WithName("ApproveAndSendTriageReply")
        .WithSummary("Authorize AI draft reply and dispatch to destination channel");

        group.MapGet("/analytics", async Task<Ok<TriageAnalyticsSummary>> (
            [FromServices] SocialTriageEngine engine,
            CancellationToken ct) =>
        {
            var analytics = await engine.GetAnalyticsAsync(ct);
            return TypedResults.Ok(analytics);
        })
        .WithName("GetSocialTriageAnalytics")
        .WithSummary("Fetch sentiment distribution, hazard volume, and SLA compliance analytics");

        return group;
    }
}
