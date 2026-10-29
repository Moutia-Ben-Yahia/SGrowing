using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace SGrowing.Api.Modules.Outbound.Enrichment;

public record CreateCascadeRequest(
    string Name,
    List<string> Providers,
    string? TargetDataType = "EmailAndPhone",
    decimal? MinConfidence = 0.80m
);

public record ExecuteWaterfallRequest(
    string ProspectQuery,
    string? CompanyDomain = null,
    Guid? CascadeId = null,
    Guid? ContactId = null
);

public static class WaterfallEnrichmentEndpoints
{
    public static IEndpointRouteBuilder MapWaterfallEnrichmentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/outbound/enrichment/waterfall")
            .WithTags("WaterfallEnrichment");

        group.MapPost("/cascades", async (
            Guid tenantId,
            CreateCascadeRequest req,
            IWaterfallEnrichmentService service) =>
        {
            var cascade = await service.CreateCascadeAsync(
                tenantId,
                req.Name,
                req.Providers,
                req.TargetDataType ?? "EmailAndPhone",
                req.MinConfidence ?? 0.80m);

            return Results.Ok(cascade);
        });

        group.MapPost("/execute", async (
            Guid tenantId,
            ExecuteWaterfallRequest req,
            IWaterfallEnrichmentService service) =>
        {
            var result = await service.ExecuteWaterfallEnrichmentAsync(
                tenantId,
                req.ProspectQuery,
                req.CompanyDomain,
                req.CascadeId,
                req.ContactId);

            return Results.Ok(result);
        });

        group.MapGet("/logs", async (
            Guid tenantId,
            int? limit,
            IWaterfallEnrichmentService service) =>
        {
            var logs = await service.GetExecutionLogsAsync(tenantId, limit ?? 50);
            return Results.Ok(logs);
        });

        return app;
    }
}
