namespace SGrowing.Api.Modules.SEO.GeoVisibility;

public static class GeoVisibilityEndpoints
{
    public static IServiceCollection AddGeoVisibilityServices(this IServiceCollection services)
    {
        services.AddSingleton<IGeoVisibilityEngine, GeoVisibilityEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapGeoVisibilityEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/seo/geo")
            .WithTags("GEO/AEO AI Search Visibility Tracker");

        group.MapGet("/summary", (IGeoVisibilityEngine engine, string? tenantId, string? brandName) =>
        {
            var targetTenant = tenantId ?? "tenant_default";
            var targetBrand = brandName ?? "SGrowing";
            var summary = engine.GetSummary(targetTenant, targetBrand);
            return Results.Ok(summary);
        })
        .WithName("GetGeoVisibilitySummary")
        .WithSummary("Retrieve overall AI Answer Engine Optimization (AEO/GEO) visibility and Share of Model Voice");

        group.MapGet("/citations", (IGeoVisibilityEngine engine, string? tenantId, string? brandName) =>
        {
            var targetTenant = tenantId ?? "tenant_default";
            var citations = engine.ListCitations(targetTenant, brandName);
            return Results.Ok(citations);
        })
        .WithName("ListGeoCitations")
        .WithSummary("List all verified citations across Perplexity, ChatGPT Search, Claude, and Google AI Overviews");

        group.MapPost("/audit-query", (IGeoVisibilityEngine engine, AuditQueryRequest request) =>
        {
            var citations = engine.AuditQuery(request);
            return Results.Ok(citations);
        })
        .WithName("AuditQueryGeoVisibility")
        .WithSummary("Execute a live synthetic audit of target search queries across multiple AI answer engines");

        return app;
    }
}
