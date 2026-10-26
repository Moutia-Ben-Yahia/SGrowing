using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Marketing;

public record CreateArticleRequest(
    Guid TenantId,
    string Title,
    string Slug,
    string Excerpt,
    string HtmlContent,
    string AuthorName = "Editorial Team",
    PublicationVisibility Visibility = PublicationVisibility.Public,
    string LexicalAstJson = "{}",
    string FeaturedImageUrl = ""
);

public record BroadcastNewsletterRequest(
    Guid TenantId,
    string Subject,
    string RecipientSegment = "AllSubscribers",
    int EstimatedAudience = 1500
);

public static class PublicationCmsEndpoints
{
    public static IEndpointRouteBuilder MapPublicationCmsEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/cms")
            .WithTags("Headless Publication CMS & MEGA Newsletter Engine (Ghost & Beehiiv)");

        group.MapPost("/articles", async ([FromBody] CreateArticleRequest req, IPublicationCmsService service) =>
        {
            var article = await service.CreateArticleAsync(
                req.TenantId, req.Title, req.Slug, req.Excerpt, req.HtmlContent, req.AuthorName, req.Visibility, req.LexicalAstJson, req.FeaturedImageUrl);
            return Results.Ok(article);
        });

        group.MapGet("/articles", async ([FromQuery] Guid tenantId, [FromQuery] PublicationArticleStatus? status, IPublicationCmsService service) =>
        {
            var articles = await service.GetArticlesAsync(tenantId, status);
            return Results.Ok(articles);
        });

        group.MapPost("/articles/{id:guid}/publish", async (Guid id, [FromQuery] Guid tenantId, IPublicationCmsService service) =>
        {
            var article = await service.PublishArticleAsync(tenantId, id);
            return Results.Ok(article);
        });

        group.MapGet("/articles/{slug}/render", async (string slug, [FromQuery] Guid tenantId, [FromQuery] bool isMember, [FromQuery] bool isPaid, IPublicationCmsService service) =>
        {
            var rendered = await service.RenderArticleForReaderAsync(tenantId, slug, isMember, isPaid);
            return rendered != null ? Results.Ok(rendered) : Results.NotFound(new { error = "Article not found." });
        });

        group.MapPost("/articles/{id:guid}/broadcast-newsletter", async (Guid id, [FromBody] BroadcastNewsletterRequest req, IPublicationCmsService service) =>
        {
            var broadcast = await service.BroadcastArticleNewsletterAsync(req.TenantId, id, req.Subject, req.RecipientSegment, req.EstimatedAudience);
            return Results.Ok(broadcast);
        });

        return routes;
    }
}
