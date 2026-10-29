using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace SGrowing.Api.Modules.Marketing;

public static class InAppTourEndpoints
{
    private static readonly Guid FallbackTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static IEndpointRouteBuilder MapInAppTourEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/onboarding")
            .WithTags("In-App Guided Tours & Behavioral Announcements");

        // GET /api/v1/onboarding/tours
        // Returns eligible tours for user by route and role, or all tours if no user/route provided
        group.MapGet("/tours", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            [FromQuery] string? userId,
            [FromQuery] string? route,
            [FromQuery] string? role,
            IInAppTourService service) =>
        {
            var effectiveTenantId = headerTenantId ?? tenantId ?? FallbackTenantId;

            if (!string.IsNullOrWhiteSpace(userId) && !string.IsNullOrWhiteSpace(route))
            {
                var eligible = await service.GetEligibleToursAsync(
                    effectiveTenantId,
                    userId,
                    route,
                    role ?? "All"
                );
                return Results.Ok(eligible);
            }

            var allTours = await service.GetAllToursAsync(effectiveTenantId);
            return Results.Ok(allTours);
        });

        // POST /api/v1/onboarding/tours
        // Create tour definition
        group.MapPost("/tours", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            [FromBody] CreateProductTourRequest request,
            IInAppTourService service) =>
        {
            var effectiveTenantId = headerTenantId ?? tenantId ?? FallbackTenantId;
            var tour = await service.CreateTourAsync(effectiveTenantId, request);
            return Results.Created($"/api/v1/onboarding/tours/{tour.Id}", tour);
        });

        // GET /api/v1/onboarding/tours/{id}
        group.MapGet("/tours/{id:guid}", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            Guid id,
            IInAppTourService service) =>
        {
            var effectiveTenantId = headerTenantId ?? tenantId ?? FallbackTenantId;
            var tour = await service.GetTourByIdAsync(effectiveTenantId, id);
            return tour != null ? Results.Ok(tour) : Results.NotFound(new { Message = $"Tour {id} not found." });
        });

        // POST /api/v1/onboarding/tours/{id}/steps
        // Append / replace steps for a tour
        group.MapPost("/tours/{id:guid}/steps", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            Guid id,
            [FromBody] List<CreateTourStepRequest> steps,
            IInAppTourService service) =>
        {
            var effectiveTenantId = headerTenantId ?? tenantId ?? FallbackTenantId;
            try
            {
                var updatedSteps = await service.SetTourStepsAsync(effectiveTenantId, id, steps);
                return Results.Ok(new { TourId = id, StepsCount = updatedSteps.Count, Steps = updatedSteps });
            }
            catch (KeyNotFoundException knf)
            {
                return Results.NotFound(new { Message = knf.Message });
            }
        });

        // POST /api/v1/onboarding/tours/{id}/telemetry
        // Record tour step viewed, completed, or dismissed
        group.MapPost("/tours/{id:guid}/telemetry", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            Guid id,
            [FromBody] RecordTourTelemetryRequest request,
            IInAppTourService service) =>
        {
            var effectiveTenantId = headerTenantId ?? tenantId ?? FallbackTenantId;
            var progress = await service.RecordTourTelemetryAsync(
                effectiveTenantId,
                id,
                request.UserId,
                request.Action,
                request.StepOrder
            );
            return Results.Ok(progress);
        });

        // GET /api/v1/onboarding/announcements
        // List active announcements matching user role, session count, and route
        group.MapGet("/announcements", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            [FromQuery] string? userId,
            [FromQuery] string? route,
            [FromQuery] string? role,
            [FromQuery] int? sessionCount,
            IInAppTourService service) =>
        {
            var effectiveTenantId = headerTenantId ?? tenantId ?? FallbackTenantId;

            if (!string.IsNullOrWhiteSpace(userId))
            {
                var eligible = await service.GetEligibleAnnouncementsAsync(
                    effectiveTenantId,
                    userId,
                    route ?? "/",
                    role ?? "All",
                    sessionCount ?? 1
                );
                return Results.Ok(eligible);
            }

            var all = await service.GetAllAnnouncementsAsync(effectiveTenantId);
            return Results.Ok(all);
        });

        // POST /api/v1/onboarding/announcements
        // Create announcement
        group.MapPost("/announcements", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            [FromBody] CreateAnnouncementRequest request,
            IInAppTourService service) =>
        {
            var effectiveTenantId = headerTenantId ?? tenantId ?? FallbackTenantId;
            var announcement = await service.CreateAnnouncementAsync(effectiveTenantId, request);
            return Results.Created($"/api/v1/onboarding/announcements/{announcement.Id}", announcement);
        });

        // POST /api/v1/onboarding/announcements/{id}/dismiss
        // Record user dismissal
        group.MapPost("/announcements/{id:guid}/dismiss", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            Guid id,
            [FromBody] DismissAnnouncementRequest request,
            IInAppTourService service) =>
        {
            var effectiveTenantId = headerTenantId ?? tenantId ?? FallbackTenantId;
            var dismissal = await service.DismissAnnouncementAsync(
                effectiveTenantId,
                id,
                request.UserId,
                request.CtaClicked
            );
            return Results.Ok(dismissal);
        });

        // GET /api/v1/onboarding/metrics
        // Tour completion rates, step drop-off %, announcement impressions/clicks
        group.MapGet("/metrics", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IInAppTourService service) =>
        {
            var effectiveTenantId = headerTenantId ?? tenantId ?? FallbackTenantId;
            var metrics = await service.GetMetricsAsync(effectiveTenantId);
            return Results.Ok(metrics);
        });

        return app;
    }
}
