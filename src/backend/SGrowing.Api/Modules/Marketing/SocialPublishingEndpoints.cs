using Microsoft.AspNetCore.Mvc;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Marketing;

public static class SocialPublishingEndpoints
{
    public static IEndpointRouteBuilder MapSocialPublishingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/social")
            .WithTags("Social Publishing & AI Copilot");

        group.MapPost("/posts/publish-durable", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid tenantId,
            [FromBody] DurablePublishRequest request,
            ISocialPublishingEngineService service) =>
        {
            var result = await service.PublishPostDurablyAsync(tenantId, request.PostId, request.Platforms);
            return Results.Ok(result);
        });

        group.MapPost("/ai/viral-hooks", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid tenantId,
            [FromQuery] string topic,
            ISocialPublishingEngineService service) =>
        {
            var result = await service.GenerateViralHooksAsync(tenantId, topic ?? "B2B SaaS Growth");
            return Results.Ok(result);
        });

        group.MapPost("/ai/rewrite", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid tenantId,
            [FromBody] RewritePostRequest request,
            ISocialPublishingEngineService service) =>
        {
            var result = await service.RewriteForPlatformsAsync(tenantId, request.MasterDraft, request.Platforms);
            return Results.Ok(result);
        });

        return app;
    }
}

public record DurablePublishRequest(
    Guid PostId,
    List<SocialPlatform> Platforms
);

public record RewritePostRequest(
    string MasterDraft,
    List<SocialPlatform> Platforms
);
