using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Channels.WhatsApp;

public static class WhatsAppNewsletterEndpoints
{
    private static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private static Guid ResolveTenantId(Guid? tenantId, Guid? headerTenantId)
    {
        if (tenantId.HasValue && tenantId.Value != Guid.Empty) return tenantId.Value;
        if (headerTenantId.HasValue && headerTenantId.Value != Guid.Empty) return headerTenantId.Value;
        return DefaultTenantId;
    }

    public static IEndpointRouteBuilder MapWhatsAppNewsletterEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/channels/whatsapp/newsletters")
            .WithTags("WhatsApp Channels & Newsletters (@newsletter Protocol)");

        group.MapGet("/", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IWhatsAppNewsletterService newsletterService,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var channels = await newsletterService.GetChannelsAsync(effectiveTenantId, ct);
            return Results.Ok(channels);
        })
        .WithName("GetWhatsAppNewsletterChannels")
        .WithSummary("List all public WhatsApp broadcast newsletter channels with subscriber metrics");

        group.MapPost("/", async (
            [FromBody] CreateNewsletterChannelRequest req,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IWhatsAppNewsletterService newsletterService,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var channel = await newsletterService.CreateChannelAsync(effectiveTenantId, req, ct);
            return Results.Created($"/api/v1/channels/whatsapp/newsletters/{channel.Id}", channel);
        })
        .WithName("CreateWhatsAppNewsletterChannel")
        .WithSummary("Create a new WhatsApp newsletter channel using the @newsletter JID protocol");

        group.MapGet("/{id:guid}", async (
            Guid id,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IWhatsAppNewsletterService newsletterService,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var details = await newsletterService.GetChannelDetailsAsync(effectiveTenantId, id, ct);
            if (details == null)
            {
                return Results.Problem(
                    title: "Newsletter Channel Not Found",
                    detail: $"WhatsApp newsletter channel '{id}' does not exist.",
                    statusCode: StatusCodes.Status404NotFound,
                    instance: $"/api/v1/channels/whatsapp/newsletters/{id}",
                    type: "https://sgrowing.io/errors/channel-not-found"
                );
            }
            return Results.Ok(details);
        })
        .WithName("GetWhatsAppNewsletterChannelDetails")
        .WithSummary("Get channel profile, invite link, and recent broadcast posts");

        group.MapPost("/{id:guid}/broadcast", async (
            Guid id,
            [FromBody] BroadcastNewsletterPostRequest req,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IWhatsAppNewsletterService newsletterService,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var post = await newsletterService.BroadcastPostAsync(effectiveTenantId, id, req, ct);
            if (post == null)
            {
                return Results.Problem(
                    title: "Newsletter Channel Not Found",
                    detail: $"Cannot broadcast post to non-existent newsletter channel '{id}'.",
                    statusCode: StatusCodes.Status404NotFound,
                    instance: $"/api/v1/channels/whatsapp/newsletters/{id}/broadcast",
                    type: "https://sgrowing.io/errors/channel-not-found"
                );
            }
            return Results.Ok(post);
        })
        .WithName("BroadcastWhatsAppNewsletterPost")
        .WithSummary("Broadcast a text/media post to all subscribers of a WhatsApp newsletter channel");

        group.MapPost("/posts/{postId:guid}/react", async (
            Guid postId,
            [FromBody] ReactNewsletterPostRequest req,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IWhatsAppNewsletterService newsletterService,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var updatedPost = await newsletterService.RecordReactionAsync(effectiveTenantId, postId, req, ct);
            if (updatedPost == null)
            {
                return Results.Problem(
                    title: "Newsletter Post Not Found",
                    detail: $"Cannot react to non-existent post '{postId}'.",
                    statusCode: StatusCodes.Status404NotFound,
                    instance: $"/api/v1/channels/whatsapp/newsletters/posts/{postId}/react",
                    type: "https://sgrowing.io/errors/post-not-found"
                );
            }
            return Results.Ok(updatedPost);
        })
        .WithName("ReactToWhatsAppNewsletterPost")
        .WithSummary("Record an emoji reaction on a published newsletter broadcast post");

        return app;
    }
}
