using Microsoft.AspNetCore.Mvc;

namespace SGrowing.Api.Modules.Scheduling;

public static class AppointmentSchedulingEndpoints
{
    public static IEndpointRouteBuilder MapAppointmentSchedulingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/scheduling/appointments")
            .WithTags("High-Speed Temporal Appointment Scheduling & Round-Robin Calendar Engine");

        // 1. Get all scheduled appointments (Ledger)
        group.MapGet("/", async (IAppointmentSchedulingService service, CancellationToken ct) =>
        {
            var appointments = await service.GetAllAppointmentsAsync(ct);
            return Results.Ok(appointments);
        })
        .WithName("GetAllAppointments")
        .WithSummary("List all scheduled, rescheduled, completed, and cancelled appointments");

        // 2. Get specific appointment
        group.MapGet("/{id:guid}", async (Guid id, IAppointmentSchedulingService service, CancellationToken ct) =>
        {
            var appointment = await service.GetAppointmentByIdAsync(id, ct);
            if (appointment == null)
            {
                return Results.NotFound(new { error = $"Appointment with id '{id}' was not found." });
            }
            return Results.Ok(appointment);
        })
        .WithName("GetAppointmentById")
        .WithSummary("Retrieve details of a single appointment");

        // 3. Get all team hosts and their schedules
        group.MapGet("/hosts", async (IAppointmentSchedulingService service, CancellationToken ct) =>
        {
            var hosts = await service.GetHostsAsync(ct);
            return Results.Ok(hosts);
        })
        .WithName("GetHosts")
        .WithSummary("Retrieve team hosts, calendar capacities, working hours, and weights");

        // 4. Query available temporal slots
        group.MapGet("/slots", async (
            [FromQuery] Guid? hostId,
            [FromQuery] string? collectiveHostIds,
            [FromQuery] DateTimeOffset? rangeStart,
            [FromQuery] DateTimeOffset? rangeEnd,
            [FromQuery] int? duration,
            [FromQuery] string? timeZoneId,
            [FromQuery] RoutingTopology? topology,
            IAppointmentSchedulingService service,
            CancellationToken ct) =>
        {
            List<Guid>? parsedCollectiveIds = null;
            if (!string.IsNullOrWhiteSpace(collectiveHostIds))
            {
                parsedCollectiveIds = collectiveHostIds
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(Guid.Parse)
                    .ToList();
            }

            var query = new SlotQuery(
                HostId: hostId,
                CollectiveHostIds: parsedCollectiveIds,
                RangeStartUtc: rangeStart,
                RangeEndUtc: rangeEnd,
                DurationMinutes: duration ?? 30,
                TargetTimeZoneId: string.IsNullOrWhiteSpace(timeZoneId) ? "UTC" : timeZoneId,
                Topology: topology ?? RoutingTopology.WeightedRoundRobin
            );

            try
            {
                var slots = await service.GetAvailableSlotsAsync(query, ct);
                return Results.Ok(slots);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("GetAvailableSlots")
        .WithSummary("Calculate conflict-free available meeting slots intersecting buffers and working hours");

        // 5. Book an appointment
        group.MapPost("/book", async (
            [FromBody] BookAppointmentRequest request,
            IAppointmentSchedulingService service,
            CancellationToken ct) =>
        {
            try
            {
                var booked = await service.BookAppointmentAsync(request, ct);
                return Results.Created($"/api/v1/scheduling/appointments/{booked.Id}", booked);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        })
        .WithName("BookAppointment")
        .WithSummary("Book a confirmed appointment via direct selection, weighted round-robin, or collective routing");

        // 6. Reschedule an appointment
        group.MapPost("/reschedule/{id:guid}", async (
            Guid id,
            [FromBody] RescheduleAppointmentRequest request,
            IAppointmentSchedulingService service,
            CancellationToken ct) =>
        {
            try
            {
                var rescheduled = await service.RescheduleAppointmentAsync(id, request, ct);
                return Results.Ok(rescheduled);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        })
        .WithName("RescheduleAppointment")
        .WithSummary("Reschedule an existing active appointment to a new temporal slot");

        // 7. Cancel an appointment
        group.MapPost("/cancel/{id:guid}", async (
            Guid id,
            [FromBody] CancelAppointmentRequest request,
            IAppointmentSchedulingService service,
            CancellationToken ct) =>
        {
            try
            {
                var cancelled = await service.CancelAppointmentAsync(id, request, ct);
                return Results.Ok(cancelled);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .WithName("CancelAppointment")
        .WithSummary("Cancel an appointment and free its temporal window for subsequent bookings");

        // 8. Download RFC 5545 .ics Calendar File
        group.MapGet("/{id:guid}/ics", async (
            Guid id,
            IAppointmentSchedulingService service,
            CancellationToken ct) =>
        {
            try
            {
                var icsContent = await service.GenerateIcsCalendarAsync(id, ct);
                var bytes = System.Text.Encoding.UTF8.GetBytes(icsContent);
                return Results.File(bytes, "text/calendar; charset=utf-8", $"appointment-{id}.ics");
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .WithName("DownloadIcsCalendar")
        .WithSummary("Export standard RFC 5545 iCalendar (.ics) file with UID, DTSTART, DTEND, and video link");

        // 9. Team routing metrics and statistics
        group.MapGet("/routing-stats", async (
            IAppointmentSchedulingService service,
            CancellationToken ct) =>
        {
            var stats = await service.GetRoutingStatsAsync(ct);
            return Results.Ok(stats);
        })
        .WithName("GetRoutingStats")
        .WithSummary("Retrieve team allocation weights, active booking distribution, and utilization ratios");

        return group;
    }
}
