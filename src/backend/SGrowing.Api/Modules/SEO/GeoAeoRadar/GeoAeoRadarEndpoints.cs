using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using SGrowing.Api.Common;
using SGrowing.Core.Tenancy;

namespace SGrowing.Api.Modules.SEO.GeoAeoRadar;

public class GeoAeoRadarEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/seo/geo-radar")
            .WithTags("GEO/AEO Radar & AI Citability Optimization");

        group.MapPost("/queries", (
            [FromBody] AddCitationQueryRequest request,
            [FromQuery] Guid? tenantId,
            IGeoAeoRadarService service,
            ITenantContext? tenantContext) =>
        {
            if (string.IsNullOrWhiteSpace(request.Query))
            {
                return Results.BadRequest(new { error = "Search query is required to track GEO citations." });
            }

            var resolvedTenant = ResolveTenantId(tenantId, tenantContext);
            var record = service.TrackQuery(resolvedTenant, request);
            return Results.Created($"/api/v1/seo/geo-radar/queries/{record.Id}", record);
        })
        .WithName("TrackGeoCitationQuery")
        .WithSummary("Tracks target prompt or keyword visibility across Perplexity, ChatGPT Search, Gemini, and Claude");

        group.MapGet("/queries", (
            [FromQuery] Guid? tenantId,
            IGeoAeoRadarService service,
            ITenantContext? tenantContext) =>
        {
            var resolvedTenant = ResolveTenantId(tenantId, tenantContext);
            var queries = service.GetQueries(resolvedTenant);
            return Results.Ok(queries);
        })
        .WithName("GetGeoCitationQueries")
        .WithSummary("Retrieves all monitored GEO/AEO query citations for the tenant");

        group.MapPost("/audit-page", (
            [FromBody] AuditPageCitabilityRequest request,
            [FromQuery] Guid? tenantId,
            IGeoAeoRadarService service,
            ITenantContext? tenantContext) =>
        {
            if (string.IsNullOrWhiteSpace(request.Url))
            {
                return Results.BadRequest(new { error = "Page URL is required for citability audit." });
            }

            if (string.IsNullOrWhiteSpace(request.Content))
            {
                return Results.BadRequest(new { error = "Content is required for citability audit." });
            }

            var resolvedTenant = ResolveTenantId(tenantId, tenantContext);
            var audit = service.AuditPageCitability(resolvedTenant, request);
            return Results.Ok(audit);
        })
        .WithName("AuditPageCitability")
        .WithSummary("Audits text and markdown for AI engine citability metrics, conciseness, and pronoun clarity");

        group.MapGet("/crawler-manifest", (
            [FromQuery] Guid? tenantId,
            IGeoAeoRadarService service,
            ITenantContext? tenantContext) =>
        {
            var resolvedTenant = ResolveTenantId(tenantId, tenantContext);
            var manifest = service.GetCrawlerManifest(resolvedTenant);
            return Results.Ok(manifest);
        })
        .WithName("GetAgenticCrawlerManifest")
        .WithSummary("Audits AI crawler accessibility, robots.txt directives, and /llms.txt manifest readiness");

        group.MapGet("/overview", (
            [FromQuery] Guid? tenantId,
            IGeoAeoRadarService service,
            ITenantContext? tenantContext) =>
        {
            var resolvedTenant = ResolveTenantId(tenantId, tenantContext);
            var report = service.GetOverviewReport(resolvedTenant);
            return Results.Ok(report);
        })
        .WithName("GetGeoRadarOverview")
        .WithSummary("Generates an aggregated executive GEO/AEO radar overview with engine breakdown and SoMV");
    }

    private static Guid ResolveTenantId(Guid? tenantId, ITenantContext? tenantContext)
    {
        if (tenantId.HasValue && tenantId.Value != Guid.Empty)
        {
            return tenantId.Value;
        }

        if (tenantContext != null && tenantContext.HasTenant)
        {
            return tenantContext.TenantId;
        }

        return GeoAeoRadarService.DefaultTenantId;
    }
}
