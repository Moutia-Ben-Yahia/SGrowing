using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Calendar;

public record CreateEventTypeDto(Guid TenantId, string Title, string Slug, string Description = "", int DurationMinutes = 30, bool IsRoundRobin = false, string ColorHex = "#6366f1", List<Guid>? HostIds = null);
public record SetHostAvailabilityDto(Guid TenantId, Guid HostUserId, string HostName, string TimeZone, List<DayAvailability> Hours);
public record BookAppointmentDto(Guid TenantId, Guid EventTypeId, string AttendeeName, string AttendeeEmail, string AttendeePhone, DateTime StartTimeUtc, string TimeZone = "Africa/Tunis", string Notes = "");

public static class CalendarEndpoints
{
    public static IEndpointRouteBuilder MapCalendarEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/calendar").WithTags("Appointment Scheduling & Calendar Engine");

        group.MapPost("/event-types", async ([FromBody] CreateEventTypeDto req, ICalendarSchedulingService service) =>
        {
            var res = await service.CreateEventTypeAsync(req.TenantId, req.Title, req.Slug, req.Description, req.DurationMinutes, req.IsRoundRobin, req.ColorHex, req.HostIds);
            return Results.Ok(res);
        });

        group.MapGet("/event-types", async ([FromQuery] Guid tenantId, ICalendarSchedulingService service) =>
        {
            var types = await service.GetEventTypesAsync(tenantId);
            return Results.Ok(types);
        });

        group.MapPost("/availability", async ([FromBody] SetHostAvailabilityDto req, ICalendarSchedulingService service) =>
        {
            var res = await service.SetHostAvailabilityAsync(req.TenantId, req.HostUserId, req.HostName, req.TimeZone, req.Hours);
            return Results.Ok(res);
        });

        group.MapGet("/availability/slots", async ([FromQuery] Guid tenantId, [FromQuery] Guid eventTypeId, [FromQuery] DateTime date, ICalendarSchedulingService service) =>
        {
            var slots = await service.ComputeAvailableSlotsAsync(tenantId, eventTypeId, date);
            return Results.Ok(slots);
        });

        group.MapPost("/bookings", async ([FromBody] BookAppointmentDto req, ICalendarSchedulingService service) =>
        {
            var booking = await service.BookAppointmentAsync(req.TenantId, req.EventTypeId, req.AttendeeName, req.AttendeeEmail, req.AttendeePhone, req.StartTimeUtc, req.TimeZone, req.Notes);
            return Results.Ok(booking);
        });

        group.MapGet("/bookings", async ([FromQuery] Guid tenantId, [FromQuery] DateTime? from, [FromQuery] DateTime? to, ICalendarSchedulingService service) =>
        {
            var bookings = await service.GetBookingsAsync(tenantId, from, to);
            return Results.Ok(bookings);
        });

        group.MapGet("/bookings/{id:guid}/ics", async (Guid id, [FromQuery] Guid tenantId, ICalendarSchedulingService service) =>
        {
            var ics = await service.GenerateIcsContentAsync(tenantId, id);
            return Results.Text(ics, "text/calendar", System.Text.Encoding.UTF8);
        });

        return endpoints;
    }
}
