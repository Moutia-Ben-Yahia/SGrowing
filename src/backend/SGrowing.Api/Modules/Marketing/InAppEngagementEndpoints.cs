using Microsoft.AspNetCore.Mvc;

namespace SGrowing.Api.Modules.Marketing;

public static class InAppEngagementEndpoints
{
    public static IEndpointRouteBuilder MapInAppEngagementEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/engagement")
            .WithTags("In-App Guided Tours & Targeted Banners");

        group.MapPost("/tours", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid tenantId,
            [FromBody] CreateTourRequest request,
            IInAppEngagementService service) =>
        {
            var result = await service.CreateTourAsync(tenantId, request.Name, request.PathPattern, request.Steps);
            return Results.Ok(result);
        });

        group.MapGet("/tours/active", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid tenantId,
            [FromQuery] string? path,
            IInAppEngagementService service) =>
        {
            var result = await service.GetActiveToursAsync(tenantId, path ?? "/");
            return Results.Ok(result);
        });

        group.MapPost("/tours/{id:guid}/complete", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid tenantId,
            Guid id,
            [FromQuery] string user,
            IInAppEngagementService service) =>
        {
            var result = await service.RecordTourCompletionAsync(tenantId, id, user);
            return Results.Ok(new { TourId = id, Completed = result });
        });

        group.MapPost("/banners", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid tenantId,
            [FromBody] CreateBannerRequest request,
            IInAppEngagementService service) =>
        {
            var result = await service.CreateBannerAsync(
                tenantId,
                request.Title,
                request.BodyText,
                request.BannerType,
                request.CtaButtonText,
                request.CtaUrl,
                request.AudienceSegment
            );
            return Results.Ok(result);
        });

        group.MapGet("/banners/evaluate", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid tenantId,
            [FromQuery] string? path,
            [FromQuery] string? segment,
            IInAppEngagementService service) =>
        {
            var result = await service.EvaluateActiveBannersAsync(tenantId, path ?? "/", segment ?? "ALL");
            return Results.Ok(result);
        });

        return app;
    }
}

public record CreateTourRequest(
    string Name,
    string PathPattern,
    List<TourStepDto> Steps
);

public record CreateBannerRequest(
    string Title,
    string BodyText,
    string BannerType,
    string CtaButtonText,
    string CtaUrl,
    string AudienceSegment
);
