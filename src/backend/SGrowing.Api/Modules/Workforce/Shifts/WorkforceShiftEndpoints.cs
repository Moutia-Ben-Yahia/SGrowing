using Microsoft.AspNetCore.Mvc;

namespace SGrowing.Api.Modules.Workforce.Shifts;

public static class WorkforceShiftEndpoints
{
    public static IEndpointRouteBuilder MapWorkforceShiftEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/workforce/shifts")
            .WithTags("WorkforceShifts");

        group.MapPost("/templates", async (
            [FromBody] CreateShiftTemplateRequest request,
            [FromServices] IWorkforceShiftService service,
            CancellationToken ct) =>
        {
            try
            {
                var template = await service.CreateShiftTemplateAsync(request, ct);
                return Results.Created($"/api/v1/workforce/shifts/templates/{template.TemplateId}", template);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapGet("/templates", async (
            [FromQuery] Guid tenantId,
            [FromServices] IWorkforceShiftService service,
            CancellationToken ct) =>
        {
            var list = await service.GetShiftTemplatesAsync(tenantId, ct);
            return Results.Ok(list);
        });

        group.MapPost("/schedule", async (
            [FromBody] ScheduleShiftRequest request,
            [FromServices] IWorkforceShiftService service,
            CancellationToken ct) =>
        {
            try
            {
                var shift = await service.ScheduleShiftAsync(request, ct);
                return Results.Created($"/api/v1/workforce/shifts/schedules/{shift.ShiftId}", shift);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapGet("/schedules", async (
            [FromQuery] Guid tenantId,
            [FromQuery] DateTime? fromUtc,
            [FromQuery] DateTime? toUtc,
            [FromQuery] Guid? agentId,
            [FromServices] IWorkforceShiftService service,
            CancellationToken ct) =>
        {
            var schedules = await service.GetSchedulesAsync(tenantId, fromUtc, toUtc, agentId, ct);
            return Results.Ok(schedules);
        });

        group.MapPost("/{id:guid}/clock-in", async (
            Guid id,
            [FromBody] ClockInApiRequest request,
            [FromServices] IWorkforceShiftService service,
            CancellationToken ct) =>
        {
            try
            {
                var req = new ClockInShiftRequest(request.TenantId, id, request.AgentId, request.ClockInTimeUtc, request.IpAddress);
                var shift = await service.ClockInShiftAsync(req, ct);
                return Results.Ok(shift);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Forbid();
            }
        });

        group.MapPost("/{id:guid}/clock-out", async (
            Guid id,
            [FromBody] ClockOutApiRequest request,
            [FromServices] IWorkforceShiftService service,
            CancellationToken ct) =>
        {
            try
            {
                var req = new ClockOutShiftRequest(request.TenantId, id, request.AgentId, request.ClockOutTimeUtc);
                var shift = await service.ClockOutShiftAsync(req, ct);
                return Results.Ok(shift);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Forbid();
            }
        });

        group.MapPost("/swaps", async (
            [FromBody] RequestShiftSwapRequest request,
            [FromServices] IWorkforceShiftService service,
            CancellationToken ct) =>
        {
            try
            {
                var swap = await service.RequestShiftSwapAsync(request, ct);
                return Results.Created($"/api/v1/workforce/shifts/swaps/{swap.SwapId}", swap);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapPost("/swaps/{id:guid}/approve", async (
            Guid id,
            [FromBody] ReviewSwapApiRequest request,
            [FromServices] IWorkforceShiftService service,
            CancellationToken ct) =>
        {
            try
            {
                var approved = await service.ApproveShiftSwapAsync(request.TenantId, id, request.ManagerId, ct);
                return Results.Ok(approved);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapPost("/swaps/{id:guid}/reject", async (
            Guid id,
            [FromBody] ReviewSwapApiRequest request,
            [FromServices] IWorkforceShiftService service,
            CancellationToken ct) =>
        {
            try
            {
                var rejected = await service.RejectShiftSwapAsync(request.TenantId, id, request.ManagerId, ct);
                return Results.Ok(rejected);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        });

        group.MapGet("/swaps/pending", async (
            [FromQuery] Guid tenantId,
            [FromServices] IWorkforceShiftService service,
            CancellationToken ct) =>
        {
            var pending = await service.GetPendingSwapRequestsAsync(tenantId, ct);
            return Results.Ok(pending);
        });

        group.MapGet("/capacity", async (
            [FromQuery] Guid tenantId,
            [FromQuery] DateTime dateUtc,
            [FromQuery] int forecastedVolume,
            [FromQuery] ChannelSpecialty channel,
            [FromServices] IWorkforceShiftService service,
            CancellationToken ct) =>
        {
            var capacity = await service.EvaluateCapacityAndGapsAsync(tenantId, dateUtc, forecastedVolume, channel, ct);
            return Results.Ok(capacity);
        });

        group.MapGet("/adherence", async (
            [FromQuery] Guid tenantId,
            [FromQuery] DateTime fromUtc,
            [FromQuery] DateTime toUtc,
            [FromServices] IWorkforceShiftService service,
            CancellationToken ct) =>
        {
            var report = await service.GetAdherenceReportAsync(tenantId, fromUtc, toUtc, ct);
            return Results.Ok(report);
        });

        return app;
    }
}

public record ClockInApiRequest(Guid TenantId, Guid AgentId, DateTime? ClockInTimeUtc = null, string? IpAddress = null);
public record ClockOutApiRequest(Guid TenantId, Guid AgentId, DateTime? ClockOutTimeUtc = null);
public record ReviewSwapApiRequest(Guid TenantId, Guid ManagerId);
