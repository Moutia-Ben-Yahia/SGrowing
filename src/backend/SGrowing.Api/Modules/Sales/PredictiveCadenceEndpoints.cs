using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Sales;

public record PauseEnrollmentRequest(string? Reason);

public static class PredictiveCadenceEndpoints
{
    private static readonly Guid FallbackTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static IPredictiveCadenceService ResolveService(IServiceProvider sp) =>
        sp.GetService<IPredictiveCadenceService>() ?? PredictiveCadenceService.Instance;

    public static IEndpointRouteBuilder MapPredictiveCadenceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/sales/predictive-cadence")
            .WithTags("Predictive Sales Cadence & Optimal Contact Time Intelligence");

        // 1. GET /cadences
        group.MapGet("/cadences", async (
            [FromQuery] CadenceCategory? category,
            [FromQuery] Guid? tenantId,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            IServiceProvider sp) =>
        {
            var effectiveTenant = tenantId ?? headerTenantId ?? FallbackTenantId;
            var service = ResolveService(sp);
            var cadences = await service.GetCadencesAsync(effectiveTenant, category);
            return Results.Ok(cadences);
        })
        .WithName("ListPredictiveCadences")
        .WithSummary("List all predictive sales cadences filtered optionally by category.");

        // 2. GET /cadences/{id}
        group.MapGet("/cadences/{id:guid}", async (
            Guid id,
            [FromQuery] Guid? tenantId,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            IServiceProvider sp) =>
        {
            var effectiveTenant = tenantId ?? headerTenantId ?? FallbackTenantId;
            var service = ResolveService(sp);
            var cadence = await service.GetCadenceByIdAsync(effectiveTenant, id);
            return cadence != null ? Results.Ok(cadence) : Results.NotFound(new { error = $"Cadence {id} not found." });
        })
        .WithName("GetPredictiveCadenceById")
        .WithSummary("Get a predictive cadence definition with multi-channel steps.");

        // 3. POST /cadences
        group.MapPost("/cadences", async (
            [FromBody] CreateCadenceRequest request,
            [FromQuery] Guid? tenantId,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            IServiceProvider sp) =>
        {
            var effectiveTenant = tenantId ?? headerTenantId ?? FallbackTenantId;
            var service = ResolveService(sp);
            var cadence = await service.CreateCadenceAsync(effectiveTenant, request);
            return Results.Created($"/api/v1/sales/predictive-cadence/cadences/{cadence.Id}", cadence);
        })
        .WithName("CreatePredictiveCadence")
        .WithSummary("Create a new multi-channel predictive cadence sequence.");

        // 4. PUT /cadences/{id}
        group.MapPut("/cadences/{id:guid}", async (
            Guid id,
            [FromBody] UpdateCadenceRequest request,
            [FromQuery] Guid? tenantId,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            IServiceProvider sp) =>
        {
            var effectiveTenant = tenantId ?? headerTenantId ?? FallbackTenantId;
            var service = ResolveService(sp);
            var updated = await service.UpdateCadenceAsync(effectiveTenant, id, request);
            return updated != null ? Results.Ok(updated) : Results.NotFound(new { error = $"Cadence {id} not found." });
        })
        .WithName("UpdatePredictiveCadence")
        .WithSummary("Update cadence metadata, active state, or sequential steps.");

        // 5. DELETE /cadences/{id}
        group.MapDelete("/cadences/{id:guid}", async (
            Guid id,
            [FromQuery] Guid? tenantId,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            IServiceProvider sp) =>
        {
            var effectiveTenant = tenantId ?? headerTenantId ?? FallbackTenantId;
            var service = ResolveService(sp);
            var removed = await service.DeleteCadenceAsync(effectiveTenant, id);
            return removed ? Results.NoContent() : Results.NotFound(new { error = $"Cadence {id} not found." });
        })
        .WithName("DeletePredictiveCadence")
        .WithSummary("Delete a predictive cadence.");

        // 6. GET /enrollments
        group.MapGet("/enrollments", async (
            [FromQuery] Guid? cadenceId,
            [FromQuery] EnrollmentStatus? status,
            [FromQuery] Guid? tenantId,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            IServiceProvider sp) =>
        {
            var effectiveTenant = tenantId ?? headerTenantId ?? FallbackTenantId;
            var service = ResolveService(sp);
            var enrollments = await service.GetEnrollmentsAsync(effectiveTenant, cadenceId, status);
            return Results.Ok(enrollments);
        })
        .WithName("ListLeadEnrollments")
        .WithSummary("List lead enrollments with current progress and instantaneous connection confidence.");

        // 7. GET /enrollments/{id}
        group.MapGet("/enrollments/{id:guid}", async (
            Guid id,
            [FromQuery] Guid? tenantId,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            IServiceProvider sp) =>
        {
            var effectiveTenant = tenantId ?? headerTenantId ?? FallbackTenantId;
            var service = ResolveService(sp);
            var enrollment = await service.GetEnrollmentByIdAsync(effectiveTenant, id);
            return enrollment != null ? Results.Ok(enrollment) : Results.NotFound(new { error = $"Enrollment {id} not found." });
        })
        .WithName("GetLeadEnrollmentById")
        .WithSummary("Get a single lead enrollment detail and touch logs.");

        // 8. POST /enrollments
        group.MapPost("/enrollments", async (
            [FromBody] EnrollLeadRequest request,
            [FromQuery] Guid? tenantId,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            IServiceProvider sp) =>
        {
            var effectiveTenant = tenantId ?? headerTenantId ?? FallbackTenantId;
            var service = ResolveService(sp);
            var enrollment = await service.EnrollLeadAsync(effectiveTenant, request);
            return Results.Created($"/api/v1/sales/predictive-cadence/enrollments/{enrollment.Id}", enrollment);
        })
        .WithName("EnrollLeadInCadence")
        .WithSummary("Enroll a prospect lead into an active cadence with optimal timing scheduling.");

        // 9. POST /enrollments/{id}/pause
        group.MapPost("/enrollments/{id:guid}/pause", async (
            Guid id,
            [FromBody] PauseEnrollmentRequest? request,
            [FromQuery] Guid? tenantId,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            IServiceProvider sp) =>
        {
            var effectiveTenant = tenantId ?? headerTenantId ?? FallbackTenantId;
            var service = ResolveService(sp);
            var result = await service.PauseEnrollmentAsync(effectiveTenant, id, request?.Reason ?? "Manual operator pause.");
            return result != null ? Results.Ok(result) : Results.NotFound(new { error = $"Enrollment {id} not found." });
        })
        .WithName("PauseLeadEnrollment")
        .WithSummary("Pause outreach cadence for an enrolled lead.");

        // 10. POST /enrollments/{id}/resume
        group.MapPost("/enrollments/{id:guid}/resume", async (
            Guid id,
            [FromQuery] Guid? tenantId,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            IServiceProvider sp) =>
        {
            var effectiveTenant = tenantId ?? headerTenantId ?? FallbackTenantId;
            var service = ResolveService(sp);
            var result = await service.ResumeEnrollmentAsync(effectiveTenant, id);
            return result != null ? Results.Ok(result) : Results.NotFound(new { error = $"Enrollment {id} not found." });
        })
        .WithName("ResumeLeadEnrollment")
        .WithSummary("Resume outreach cadence for a previously paused lead.");

        // 11. POST /enrollments/{id}/advance
        group.MapPost("/enrollments/{id:guid}/advance", async (
            Guid id,
            [FromQuery] Guid? tenantId,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            IServiceProvider sp) =>
        {
            var effectiveTenant = tenantId ?? headerTenantId ?? FallbackTenantId;
            var service = ResolveService(sp);
            var result = await service.AdvanceStepAsync(effectiveTenant, id);
            return result != null ? Results.Ok(result) : Results.NotFound(new { error = $"Enrollment {id} not found." });
        })
        .WithName("AdvanceEnrollmentStep")
        .WithSummary("Trigger immediate progression to subsequent cadence step.");

        // 12. PATCH /enrollments/{id}/status
        group.MapPatch("/enrollments/{id:guid}/status", async (
            Guid id,
            [FromBody] UpdateEnrollmentStatusRequest request,
            [FromQuery] Guid? tenantId,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            IServiceProvider sp) =>
        {
            var effectiveTenant = tenantId ?? headerTenantId ?? FallbackTenantId;
            var service = ResolveService(sp);
            var result = await service.UpdateEnrollmentStatusAsync(effectiveTenant, id, request.Status, request.Reason);
            return result != null ? Results.Ok(result) : Results.NotFound(new { error = $"Enrollment {id} not found." });
        })
        .WithName("UpdateEnrollmentStatus")
        .WithSummary("Update enrollment lifecycle status (e.g. Replied, Graduated, DealWon, Bounced).");

        // 13. GET /heatmap
        group.MapGet("/heatmap", async (
            [FromQuery] string? timeZone,
            [FromQuery] PredictiveCadenceChannel? channel,
            [FromQuery] Guid? leadId,
            [FromQuery] Guid? tenantId,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            IServiceProvider sp) =>
        {
            var effectiveTenant = tenantId ?? headerTenantId ?? FallbackTenantId;
            var service = ResolveService(sp);
            var matrix = await service.Get7x24HeatmapAsync(effectiveTenant, timeZone, channel, leadId);
            return Results.Ok(matrix);
        })
        .WithName("Get7x24ContactHeatmap")
        .WithSummary("Retrieve 168-slot hourly probability heatmap translated into target timezone.");

        // 14. GET /optimal-slots
        group.MapGet("/optimal-slots", async (
            [FromQuery] Guid leadId,
            [FromQuery] PredictiveCadenceChannel? channel,
            [FromQuery] string? timeZone,
            [FromQuery] int? limit,
            [FromQuery] Guid? tenantId,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            IServiceProvider sp) =>
        {
            var effectiveTenant = tenantId ?? headerTenantId ?? FallbackTenantId;
            var service = ResolveService(sp);
            var slots = await service.GetOptimalContactSlotsAsync(effectiveTenant, leadId, channel, timeZone, limit ?? 3);
            return Results.Ok(slots);
        })
        .WithName("GetOptimalContactSlots")
        .WithSummary("Predict top optimal upcoming contact slots with confidence scores for a prospect.");

        // 15. POST /telemetry
        group.MapPost("/telemetry", async (
            [FromBody] RecordTelemetryRequest request,
            [FromQuery] Guid? tenantId,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            IServiceProvider sp) =>
        {
            var effectiveTenant = tenantId ?? headerTenantId ?? FallbackTenantId;
            var service = ResolveService(sp);
            var evt = await service.RecordTelemetryEventAsync(effectiveTenant, request);
            return Results.Created($"/api/v1/sales/predictive-cadence/telemetry/{evt.Id}", evt);
        })
        .WithName("RecordInteractionTelemetry")
        .WithSummary("Log engagement telemetry (opens, replies, calls) and trigger auto-pause on reply.");

        // 16. GET /telemetry
        group.MapGet("/telemetry", async (
            [FromQuery] Guid? leadId,
            [FromQuery] int? limit,
            [FromQuery] Guid? tenantId,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            IServiceProvider sp) =>
        {
            var effectiveTenant = tenantId ?? headerTenantId ?? FallbackTenantId;
            var service = ResolveService(sp);
            var events = await service.GetTelemetryEventsAsync(effectiveTenant, leadId, limit ?? 50);
            return Results.Ok(events);
        })
        .WithName("ListTelemetryEvents")
        .WithSummary("Retrieve raw historical interaction telemetry events.");

        // 17. GET /dialer-queue
        group.MapGet("/dialer-queue", async (
            [FromQuery] int? limit,
            [FromQuery] Guid? tenantId,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            IServiceProvider sp) =>
        {
            var effectiveTenant = tenantId ?? headerTenantId ?? FallbackTenantId;
            var service = ResolveService(sp);
            var queue = await service.GetPredictiveDialerQueueAsync(effectiveTenant, limit ?? 20);
            return Results.Ok(queue);
        })
        .WithName("GetPredictiveDialerQueue")
        .WithSummary("Get active phone queue ordered by instantaneous connection probability.");

        // 18. POST /dialer-queue/simulate-call
        group.MapPost("/dialer-queue/simulate-call", async (
            [FromBody] SimulateCallRequest request,
            [FromQuery] Guid? tenantId,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            IServiceProvider sp) =>
        {
            var effectiveTenant = tenantId ?? headerTenantId ?? FallbackTenantId;
            var service = ResolveService(sp);
            var result = await service.SimulateDialerCallAsync(effectiveTenant, request.EnrollmentId, request.Outcome);
            return Results.Ok(result);
        })
        .WithName("SimulateDialerCall")
        .WithSummary("Simulate softphone call outcome and automatically update cadence state.");

        // 19. GET /stats
        group.MapGet("/stats", async (
            [FromQuery] Guid? tenantId,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            IServiceProvider sp) =>
        {
            var effectiveTenant = tenantId ?? headerTenantId ?? FallbackTenantId;
            var service = ResolveService(sp);
            var stats = await service.GetOverviewStatsAsync(effectiveTenant);
            return Results.Ok(stats);
        })
        .WithName("GetPredictiveCadenceOverviewStats")
        .WithSummary("Get aggregate cadence reply rates, queue sizes, and peak connect window metrics.");

        return app;
    }
}
