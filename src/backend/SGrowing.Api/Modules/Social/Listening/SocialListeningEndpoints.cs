using Microsoft.AspNetCore.Mvc;

namespace SGrowing.Api.Modules.Social.Listening;

public static class SocialListeningEndpoints
{
    public static IEndpointRouteBuilder MapSocialListeningEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/social/listening")
            .WithTags("SocialListening");

        group.MapPost("/mentions", async (
            [FromBody] IngestSocialMentionRequest request,
            [FromServices] ISocialListeningSentinelService service,
            CancellationToken ct) =>
        {
            try
            {
                var mention = await service.IngestMentionAsync(request, ct);
                return Results.Created($"/api/v1/social/listening/mentions/{mention.MentionId}", mention);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapGet("/mentions", async (
            [FromQuery] Guid tenantId,
            [FromQuery] SocialPlatform? platform,
            [FromQuery] MentionSentiment? sentiment,
            [FromServices] ISocialListeningSentinelService service,
            CancellationToken ct) =>
        {
            var mentions = await service.ListMentionsAsync(tenantId, platform, sentiment, ct);
            return Results.Ok(mentions);
        });

        group.MapPost("/rules", async (
            [FromBody] CreateCrisisRuleRequest request,
            [FromServices] ISocialListeningSentinelService service,
            CancellationToken ct) =>
        {
            try
            {
                var rule = await service.CreateCrisisRuleAsync(request, ct);
                return Results.Created($"/api/v1/social/listening/rules/{rule.RuleId}", rule);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapGet("/rules", async (
            [FromQuery] Guid tenantId,
            [FromServices] ISocialListeningSentinelService service,
            CancellationToken ct) =>
        {
            var rules = await service.GetCrisisRulesAsync(tenantId, ct);
            return Results.Ok(rules);
        });

        group.MapPost("/incidents/{id:guid}/resolve", async (
            Guid id,
            [FromBody] ResolveCrisisApiRequest request,
            [FromServices] ISocialListeningSentinelService service,
            CancellationToken ct) =>
        {
            var req = new ResolveCrisisIncidentRequest(request.TenantId, id, request.MitigationNotes);
            var resolved = await service.ResolveCrisisIncidentAsync(req, ct);
            return resolved != null ? Results.Ok(resolved) : Results.NotFound();
        });

        group.MapGet("/incidents", async (
            [FromQuery] Guid tenantId,
            [FromServices] ISocialListeningSentinelService service,
            CancellationToken ct) =>
        {
            var incidents = await service.GetActiveCrisisIncidentsAsync(tenantId, ct);
            return Results.Ok(incidents);
        });

        group.MapGet("/trends", async (
            [FromQuery] Guid tenantId,
            [FromQuery] int hours,
            [FromServices] ISocialListeningSentinelService service,
            CancellationToken ct) =>
        {
            var trends = await service.GetSentimentTrendsAsync(tenantId, hours > 0 ? hours : 24, ct);
            return Results.Ok(trends);
        });

        group.MapGet("/summary", async (
            [FromQuery] Guid tenantId,
            [FromServices] ISocialListeningSentinelService service,
            CancellationToken ct) =>
        {
            var summary = await service.GetSocialListeningSummaryAsync(tenantId, ct);
            return Results.Ok(summary);
        });

        return app;
    }
}

public record ResolveCrisisApiRequest(Guid TenantId, string MitigationNotes);
