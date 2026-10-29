using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Marketing;

public record CreateSocialPostDto(
    Guid TenantId,
    string Content,
    List<SocialPlatform> TargetPlatforms,
    DateTime? ScheduledAtUtc = null,
    List<string>? MediaUrls = null
);

public record ConnectSocialAccountDto(
    Guid TenantId,
    SocialPlatform Platform,
    string AccountName,
    string ExternalAccountId,
    string? AccessToken = null,
    DateTime? TokenExpiresAtUtc = null,
    string? RefreshToken = null
);

public record DeflectCommentDto(
    Guid TenantId,
    SocialPlatform Platform,
    string PostExternalId,
    string CommenterUsername,
    string CommentText
);

public static class SocialEndpoints
{
    public static IEndpointRouteBuilder MapSocialEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/social").WithTags("Social Media Multi-Publishing & AI Deflector");

        // Supported channels metadata with limits
        group.MapGet("/channels", () => Results.Ok(new[]
        {
            new { platform = "X", id = (int)SocialPlatform.X, charLimit = 280, supportsMedia = true, icon = "🐦" },
            new { platform = "LinkedIn", id = (int)SocialPlatform.LinkedIn, charLimit = 3000, supportsMedia = true, icon = "💼" },
            new { platform = "Instagram", id = (int)SocialPlatform.Instagram, charLimit = 2200, supportsMedia = true, icon = "📸" },
            new { platform = "Facebook", id = (int)SocialPlatform.Facebook, charLimit = 2200, supportsMedia = true, icon = "📘" },
            new { platform = "TikTok", id = (int)SocialPlatform.TikTok, charLimit = 2200, supportsMedia = true, icon = "🎵" },
            new { platform = "Threads", id = (int)SocialPlatform.Threads, charLimit = 500, supportsMedia = true, icon = "🧵" },
            new { platform = "Telegram", id = (int)SocialPlatform.Telegram, charLimit = 4096, supportsMedia = true, icon = "✈️" }
        }));

        // Posts
        group.MapPost("/posts", async ([FromBody] CreateSocialPostDto req, SocialPublishingService service) =>
        {
            if (req.TargetPlatforms == null || req.TargetPlatforms.Count == 0)
            {
                return Results.BadRequest(new { error = "At least one target platform is required." });
            }

            var post = await service.CreatePostAsync(req.TenantId, req.Content, req.TargetPlatforms, req.ScheduledAtUtc, req.MediaUrls);
            return Results.Ok(post);
        });

        group.MapGet("/posts", async ([FromQuery] Guid tenantId, SocialPublishingService service) =>
        {
            var posts = await service.Repository.GetAllAsync(tenantId);
            return Results.Ok(posts.OrderByDescending(p => p.CreatedAtUtc));
        });

        group.MapPost("/posts/publish-due", async ([FromQuery] Guid tenantId, SocialPublishingService service) =>
        {
            var published = await service.PublishDuePostsAsync(tenantId);
            return Results.Ok(new { count = published.Count, publishedPosts = published });
        });

        group.MapPost("/posts/{id:guid}/publish-now", async (Guid id, [FromQuery] Guid tenantId, SocialPublishingService service) =>
        {
            var post = await service.Repository.GetByIdAsync(id);
            if (post == null || post.TenantId != tenantId)
            {
                return Results.NotFound(new { error = "Post not found." });
            }

            post.ScheduledAtUtc = DateTime.UtcNow.AddSeconds(-5);
            await service.Repository.UpdateAsync(post);
            var published = await service.PublishDuePostsAsync(tenantId);
            return Results.Ok(new { success = true, post });
        });

        // Accounts
        group.MapGet("/accounts", async ([FromQuery] Guid tenantId, SGrowingDbContext db) =>
        {
            var accounts = await db.SocialAccounts
                .Where(a => a.TenantId == tenantId && a.IsActive)
                .ToListAsync();

            if (accounts.Count == 0)
            {
                // Return default demo accounts if none connected yet
                return Results.Ok(new object[]
                {
                    new { id = Guid.NewGuid(), tenantId, platform = SocialPlatform.X, accountName = "@sgrowing", externalAccountId = "x_101", isActive = true },
                    new { id = Guid.NewGuid(), tenantId, platform = SocialPlatform.LinkedIn, accountName = "SGrowing Global", externalAccountId = "li_202", isActive = true },
                    new { id = Guid.NewGuid(), tenantId, platform = SocialPlatform.Instagram, accountName = "@sgrowing.official", externalAccountId = "ig_303", isActive = true },
                    new { id = Guid.NewGuid(), tenantId, platform = SocialPlatform.Facebook, accountName = "SGrowing Enterprise", externalAccountId = "fb_404", isActive = true },
                    new { id = Guid.NewGuid(), tenantId, platform = SocialPlatform.TikTok, accountName = "@sgrowing_tok", externalAccountId = "tt_707", isActive = true },
                    new { id = Guid.NewGuid(), tenantId, platform = SocialPlatform.Threads, accountName = "@sgrowing", externalAccountId = "th_505", isActive = true },
                    new { id = Guid.NewGuid(), tenantId, platform = SocialPlatform.Telegram, accountName = "SGrowing VIP Broadcast", externalAccountId = "tg_606", isActive = true }
                });
            }

            return Results.Ok(accounts);
        });

        group.MapPost("/accounts", async ([FromBody] ConnectSocialAccountDto req, SGrowingDbContext db) =>
        {
            var account = SocialAccount.Create(
                req.TenantId,
                req.Platform,
                req.AccountName,
                req.ExternalAccountId,
                req.AccessToken,
                true,
                req.TokenExpiresAtUtc,
                req.RefreshToken);
            db.SocialAccounts.Add(account);
            await db.SaveChangesAsync();
            return Results.Ok(account);
        });

        // Comment to DM Deflector
        group.MapPost("/deflect", async ([FromBody] DeflectCommentDto req, SocialCommentDeflectionService service) =>
        {
            var res = await service.HandlePublicCommentAsync(req.TenantId, req.Platform, req.PostExternalId, req.CommenterUsername, req.CommentText);
            return Results.Ok(res);
        });

        return endpoints;
    }
}
