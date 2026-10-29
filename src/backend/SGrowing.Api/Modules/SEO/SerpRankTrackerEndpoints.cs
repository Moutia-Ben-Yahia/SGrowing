using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace SGrowing.Api.Modules.SEO;

public record RecordSerpRankRequest(
    string Keyword,
    string TargetUrl,
    int Position,
    string? DeviceType = "Desktop",
    string? CountryCode = "TN",
    List<string>? SerpFeatures = null,
    Guid? KeywordId = null
);

public static class SerpRankTrackerEndpoints
{
    public static IEndpointRouteBuilder MapSerpRankTrackerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/seo/serp/rankings")
            .WithTags("SerpRankings");

        group.MapPost("/", async (
            Guid tenantId,
            RecordSerpRankRequest req,
            ISerpRankTrackerService service) =>
        {
            var record = await service.RecordDailyRankAsync(
                tenantId,
                req.Keyword,
                req.TargetUrl,
                req.Position,
                req.DeviceType ?? "Desktop",
                req.CountryCode ?? "TN",
                req.SerpFeatures,
                req.KeywordId);

            return Results.Ok(record);
        });

        group.MapGet("/keywords/{keyword}/history", async (
            Guid tenantId,
            string keyword,
            int? days,
            ISerpRankTrackerService service) =>
        {
            var history = await service.GetKeywordRankHistoryAsync(tenantId, keyword, days ?? 30);
            return Results.Ok(history);
        });

        group.MapGet("/overview", async (
            Guid tenantId,
            ISerpRankTrackerService service) =>
        {
            var overview = await service.GetDailyRankOverviewAsync(tenantId);
            return Results.Ok(overview);
        });

        return app;
    }
}
