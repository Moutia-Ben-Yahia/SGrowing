using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Calendar;

public record CreateEventTypeDto(Guid TenantId, string Title, string Slug, string Description = "", int DurationMinutes = 30, bool IsRoundRobin = false, string ColorHex = "#6366f1", List<Guid>? HostIds = null, Dictionary<Guid, double>? HostWeights = null);
public record SetHostAvailabilityDto(Guid TenantId, Guid HostUserId, string HostName, string TimeZone, List<DayAvailability> Hours, double Weight = 1.0);
public record BookAppointmentDto(Guid TenantId, Guid EventTypeId, string AttendeeName, string AttendeeEmail, string AttendeePhone, DateTime StartTimeUtc, string TimeZone = "Africa/Tunis", string Notes = "");
public record CancelBookingDto(Guid TenantId, string Reason = "");
public record RescheduleBookingDto(Guid TenantId, DateTime NewStartTimeUtc);
public record BookPublicAppointmentDto(string AttendeeName, string AttendeeEmail, string AttendeePhone, DateTime StartTimeUtc, string TimeZone = "Africa/Tunis", string Notes = "");
public record UpdateHostWeightsDto(Guid TenantId, Dictionary<Guid, double> Weights);

public static class CalendarEndpoints
{
    public static IEndpointRouteBuilder MapCalendarEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var groups = new[]
        {
            endpoints.MapGroup("/api/calendar").WithTags("Appointment Scheduling & Calendar Engine"),
            endpoints.MapGroup("/api/v1/calendar").WithTags("Appointment Scheduling & Calendar Engine v1")
        };

        foreach (var group in groups)
        {
            // Public Booker Endpoints (Inspired by Calnode / Cal.diy)
            group.MapGet("/public/{tenantSlug}/{eventSlug}", async (string tenantSlug, string eventSlug, [FromQuery] DateTime? date, ICalendarSchedulingService service) =>
            {
                var targetDate = date ?? DateTime.UtcNow;
                var result = await service.GetPublicBookingPageAsync(tenantSlug, eventSlug, targetDate);
                return result.HasValue ? Results.Ok(new { eventType = result.Value.EventType, slots = result.Value.Slots }) : Results.NotFound(new { error = "Event or tenant not found." });
            });

            group.MapPost("/public/{tenantSlug}/{eventSlug}/book", async (string tenantSlug, string eventSlug, [FromBody] BookPublicAppointmentDto req, ICalendarSchedulingService service) =>
            {
                try
                {
                    var booking = await service.BookPublicAppointmentAsync(tenantSlug, eventSlug, req.AttendeeName, req.AttendeeEmail, req.AttendeePhone, req.StartTimeUtc, req.TimeZone, req.Notes);
                    return Results.Ok(booking);
                }
                catch (Exception ex)
                {
                    return Results.BadRequest(new { error = ex.Message });
                }
            });

            group.MapGet("/public/bookings/{id:guid}/ics", async (Guid id, ICalendarSchedulingService service) =>
            {
                try
                {
                    var ics = await service.GenerateIcsContentAsync(Guid.Empty, id);
                    return Results.Text(ics, "text/calendar", System.Text.Encoding.UTF8);
                }
                catch (Exception ex)
                {
                    return Results.NotFound(new { error = ex.Message });
                }
            });

            // Event Types Management
            group.MapPost("/event-types", async ([FromBody] CreateEventTypeDto req, ICalendarSchedulingService service) =>
            {
                var res = await service.CreateEventTypeAsync(req.TenantId, req.Title, req.Slug, req.Description, req.DurationMinutes, req.IsRoundRobin, req.ColorHex, req.HostIds, req.HostWeights);
                return Results.Ok(res);
            });

            group.MapGet("/event-types", async ([FromQuery] Guid tenantId, ICalendarSchedulingService service) =>
            {
                var types = await service.GetEventTypesAsync(tenantId);
                return Results.Ok(types);
            });

            // Round-Robin Host Weights Management
            group.MapGet("/event-types/{id:guid}/hosts", async (Guid id, [FromQuery] Guid tenantId, ICalendarSchedulingService service) =>
            {
                var hosts = await service.GetEventHostsWithStatsAsync(tenantId, id);
                return Results.Ok(hosts);
            });

            group.MapPost("/event-types/{id:guid}/hosts/weights", async (Guid id, [FromBody] UpdateHostWeightsDto req, ICalendarSchedulingService service) =>
            {
                try
                {
                    await service.SetHostWeightsAsync(req.TenantId, id, req.Weights);
                    return Results.Ok(new { success = true, message = "Host weights updated successfully." });
                }
                catch (Exception ex)
                {
                    return Results.BadRequest(new { error = ex.Message });
                }
            });

            // Host Availability & Slots
            group.MapPost("/availability", async ([FromBody] SetHostAvailabilityDto req, ICalendarSchedulingService service) =>
            {
                var res = await service.SetHostAvailabilityAsync(req.TenantId, req.HostUserId, req.HostName, req.TimeZone, req.Hours, req.Weight);
                return Results.Ok(res);
            });

            group.MapGet("/availability/slots", async ([FromQuery] Guid tenantId, [FromQuery] Guid eventTypeId, [FromQuery] DateTime date, ICalendarSchedulingService service) =>
            {
                var slots = await service.ComputeAvailableSlotsAsync(tenantId, eventTypeId, date);
                return Results.Ok(slots);
            });

            // Bookings Management
            group.MapPost("/bookings", async ([FromBody] BookAppointmentDto req, ICalendarSchedulingService service) =>
            {
                try
                {
                    var booking = await service.BookAppointmentAsync(req.TenantId, req.EventTypeId, req.AttendeeName, req.AttendeeEmail, req.AttendeePhone, req.StartTimeUtc, req.TimeZone, req.Notes);
                    return Results.Ok(booking);
                }
                catch (Exception ex)
                {
                    return Results.BadRequest(new { error = ex.Message });
                }
            });

            group.MapPost("/bookings/{id:guid}/cancel", async (Guid id, [FromBody] CancelBookingDto req, ICalendarSchedulingService service) =>
            {
                var cancelled = await service.CancelBookingAsync(req.TenantId, id, req.Reason);
                return Results.Ok(cancelled);
            });

            group.MapPost("/bookings/{id:guid}/reschedule", async (Guid id, [FromBody] RescheduleBookingDto req, ICalendarSchedulingService service) =>
            {
                try
                {
                    var rescheduled = await service.RescheduleBookingAsync(req.TenantId, id, req.NewStartTimeUtc);
                    return Results.Ok(rescheduled);
                }
                catch (Exception ex)
                {
                    return Results.BadRequest(new { error = ex.Message });
                }
            });

            group.MapGet("/bookings", async ([FromQuery] Guid tenantId, [FromQuery] DateTime? from, [FromQuery] DateTime? to, ICalendarSchedulingService service) =>
            {
                var bookings = await service.GetBookingsAsync(tenantId, from, to);
                return Results.Ok(bookings);
            });

            group.MapGet("/bookings/{id:guid}/ics", async (Guid id, [FromQuery] Guid? tenantId, ICalendarSchedulingService service) =>
            {
                var ics = await service.GenerateIcsContentAsync(tenantId ?? Guid.Empty, id);
                return Results.Text(ics, "text/calendar", System.Text.Encoding.UTF8);
            });
        }

        return endpoints;
    }
}
