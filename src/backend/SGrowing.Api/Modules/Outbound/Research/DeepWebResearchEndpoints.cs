using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Outbound.Research;

public static class DeepWebResearchEndpoints
{
    private static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private static Guid ResolveTenantId(Guid? tenantId, Guid? headerTenantId)
    {
        return tenantId ?? headerTenantId ?? DefaultTenantId;
    }

    public static IEndpointRouteBuilder MapDeepWebResearchEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/outbound/research")
            .WithTags("Deep Web Research & Prospect Intelligence");

        group.MapPost("/crawl", async (
            [FromBody] WebResearchCrawlRequest req,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IDeepWebResearchService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var result = await service.CrawlAndExtractAsync(effectiveTenantId, req, ct);
            return Results.Ok(result);
        })
        .WithName("CrawlAndExtractWebResearch")
        .WithSummary("Trigger multi-engine web crawl to extract company summaries, executive bios, tech tags, and 3-bullet sales hooks");

        group.MapGet("/brief", async (
            [FromQuery] string? domain,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IDeepWebResearchService service,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(domain))
            {
                return Results.BadRequest(new { error = "Target domain query parameter is required." });
            }

            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var brief = await service.GetBriefAsync(effectiveTenantId, domain, ct);
            return brief != null ? Results.Ok(brief) : Results.NotFound(new { error = $"No research brief found for domain {domain}" });
        })
        .WithName("GetProspectResearchBrief")
        .WithSummary("Retrieve cached prospect intelligence research brief");

        group.MapGet("/briefs", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IDeepWebResearchService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var briefs = await service.ListBriefsAsync(effectiveTenantId, ct);
            return Results.Ok(briefs);
        })
        .WithName("ListProspectResearchBriefs")
        .WithSummary("List recent prospect intelligence briefs");

        group.MapGet("/crawler-secrets", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IScraperVaultService vault,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var overview = await vault.GetCrawlerSecretsOverviewAsync(effectiveTenantId, ct);
            return Results.Ok(overview);
        })
        .WithName("GetCrawlerSecretsOverview")
        .WithSummary("Get configured scraper engine credentials status and health");

        group.MapPost("/crawler-secrets", async (
            [FromBody] SaveCrawlerSecretRequest req,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IScraperVaultService vault,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var result = await vault.SaveCrawlerSecretAsync(effectiveTenantId, req, ct);
            return Results.Ok(result);
        })
        .WithName("SaveCrawlerSecret")
        .WithSummary("Encrypt and store crawler engine API secret in tenant vault");

        group.MapGet("/crawler-health", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IScraperVaultService vault,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var health = await vault.CheckAllEnginesHealthAsync(effectiveTenantId, ct);
            return Results.Ok(health);
        })
        .WithName("CheckCrawlerHealth")
        .WithSummary("Execute connectivity health check across web crawler engines");

        return app;
    }
}
