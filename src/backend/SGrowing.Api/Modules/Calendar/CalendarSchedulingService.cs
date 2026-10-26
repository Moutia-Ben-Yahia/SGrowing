using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Calendar;

public record TimeSlotDto(DateTime StartTimeUtc, DateTime EndTimeUtc, Guid HostUserId, string HostName);

public interface ICalendarSchedulingService
{
    Task<BookingEventType> CreateEventTypeAsync(Guid tenantId, string title, string slug, string description = "", int duration = 30, bool isRoundRobin = false, string color = "#6366f1", List<Guid>? hosts = null);
    Task<List<BookingEventType>> GetEventTypesAsync(Guid tenantId);
    Task<BookingEventType?> GetEventTypeBySlugAsync(Guid tenantId, string slug);
    Task<HostAvailabilitySchedule> SetHostAvailabilityAsync(Guid tenantId, Guid hostUserId, string hostName, string timeZone, List<DayAvailability> hours);
    Task<HostAvailabilitySchedule?> GetHostAvailabilityAsync(Guid tenantId, Guid hostUserId);
    Task<List<TimeSlotDto>> ComputeAvailableSlotsAsync(Guid tenantId, Guid eventTypeId, DateTime targetDateUtc);
    Task<CalendarBookingRecord> BookAppointmentAsync(Guid tenantId, Guid eventTypeId, string attendeeName, string attendeeEmail, string attendeePhone, DateTime startTimeUtc, string timeZone = "Africa/Tunis", string notes = "");
    Task<List<CalendarBookingRecord>> GetBookingsAsync(Guid tenantId, DateTime? fromUtc = null, DateTime? toUtc = null);
    Task<string> GenerateIcsContentAsync(Guid tenantId, Guid bookingId);
}

public class CalendarSchedulingService : ICalendarSchedulingService
{
    private readonly SGrowingDbContext _db;

    public CalendarSchedulingService(SGrowingDbContext db)
    {
        _db = db;
    }

    public async Task<BookingEventType> CreateEventTypeAsync(Guid tenantId, string title, string slug, string description = "", int duration = 30, bool isRoundRobin = false, string color = "#6366f1", List<Guid>? hosts = null)
    {
        var existing = await _db.BookingEventTypes
            .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Slug == slug.ToLower().Trim());

        if (existing != null)
        {
            return existing;
        }

        var eventType = BookingEventType.Create(tenantId, title, slug, description, duration, isRoundRobin, color, hosts);
        _db.BookingEventTypes.Add(eventType);
        await _db.SaveChangesAsync();
        return eventType;
    }

    public async Task<List<BookingEventType>> GetEventTypesAsync(Guid tenantId)
    {
        return await _db.BookingEventTypes
            .Where(e => e.TenantId == tenantId && e.IsActive)
            .OrderBy(e => e.DurationMinutes)
            .ToListAsync();
    }

    public async Task<BookingEventType?> GetEventTypeBySlugAsync(Guid tenantId, string slug)
    {
        return await _db.BookingEventTypes
            .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Slug == slug.ToLower().Trim() && e.IsActive);
    }

    public async Task<HostAvailabilitySchedule> SetHostAvailabilityAsync(Guid tenantId, Guid hostUserId, string hostName, string timeZone, List<DayAvailability> hours)
    {
        var existing = await _db.HostAvailabilitySchedules
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.HostUserId == hostUserId);

        var hoursJson = JsonSerializer.Serialize(hours);

        if (existing != null)
        {
            existing.HostName = hostName.Trim();
            existing.TimeZone = timeZone;
            existing.WeeklyHoursJson = hoursJson;
            await _db.SaveChangesAsync();
            return existing;
        }

        var schedule = HostAvailabilitySchedule.Create(tenantId, hostUserId, hostName, timeZone, hoursJson);
        _db.HostAvailabilitySchedules.Add(schedule);
        await _db.SaveChangesAsync();
        return schedule;
    }

    public async Task<HostAvailabilitySchedule?> GetHostAvailabilityAsync(Guid tenantId, Guid hostUserId)
    {
        return await _db.HostAvailabilitySchedules
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.HostUserId == hostUserId);
    }

    public async Task<List<TimeSlotDto>> ComputeAvailableSlotsAsync(Guid tenantId, Guid eventTypeId, DateTime targetDateUtc)
    {
        var eventType = await _db.BookingEventTypes.FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Id == eventTypeId);
        if (eventType == null) return new List<TimeSlotDto>();

        // Parse assigned hosts
        List<Guid> hostIds = new();
        if (!string.IsNullOrWhiteSpace(eventType.AssignedHostIdsJson) && eventType.AssignedHostIdsJson != "[]")
        {
            try { hostIds = JsonSerializer.Deserialize<List<Guid>>(eventType.AssignedHostIdsJson) ?? new(); } catch { }
        }

        if (hostIds.Count == 0)
        {
            // Fallback to demo default host
            hostIds.Add(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        }

        var availableSlots = new List<TimeSlotDto>();
        var dayDate = targetDateUtc.Date;

        // Candidate working hours: 09:00 to 17:00 in increments of duration
        var startHour = 9;
        var endHour = 17;
        var duration = eventType.DurationMinutes;

        // Existing bookings for tenant on target date
        var existingBookings = await _db.CalendarBookingRecords
            .Where(b => b.TenantId == tenantId && b.StartTimeUtc.Date == dayDate && b.Status == CalendarBookingStatus.Scheduled)
            .ToListAsync();

        foreach (var hostId in hostIds)
        {
            var hostBookings = existingBookings.Where(b => b.HostUserId == hostId).ToList();

            for (int h = startHour; h < endHour; h++)
            {
                for (int m = 0; m < 60; m += duration)
                {
                    if (h * 60 + m + duration > endHour * 60) break;

                    var slotStart = dayDate.AddHours(h).AddMinutes(m);
                    var slotEnd = slotStart.AddMinutes(duration);

                    // Skip if slot has conflict with existing booking
                    bool hasConflict = hostBookings.Any(b =>
                        (slotStart >= b.StartTimeUtc && slotStart < b.EndTimeUtc) ||
                        (slotEnd > b.StartTimeUtc && slotEnd <= b.EndTimeUtc) ||
                        (slotStart <= b.StartTimeUtc && slotEnd >= b.EndTimeUtc));

                    if (!hasConflict)
                    {
                        availableSlots.Add(new TimeSlotDto(slotStart, slotEnd, hostId, "Sales Specialist"));
                    }
                }
            }
        }

        return availableSlots;
    }

    public async Task<CalendarBookingRecord> BookAppointmentAsync(Guid tenantId, Guid eventTypeId, string attendeeName, string attendeeEmail, string attendeePhone, DateTime startTimeUtc, string timeZone = "Africa/Tunis", string notes = "")
    {
        var eventType = await _db.BookingEventTypes.FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Id == eventTypeId);
        if (eventType == null)
        {
            throw new InvalidOperationException($"Event type {eventTypeId} not found.");
        }

        List<Guid> hostIds = new();
        if (!string.IsNullOrWhiteSpace(eventType.AssignedHostIdsJson) && eventType.AssignedHostIdsJson != "[]")
        {
            try { hostIds = JsonSerializer.Deserialize<List<Guid>>(eventType.AssignedHostIdsJson) ?? new(); } catch { }
        }

        Guid selectedHostId;
        string selectedHostName = "Sales Specialist";

        if (eventType.IsRoundRobin && hostIds.Count > 1)
        {
            // Pick host with lowest bookings
            var bookingCounts = await _db.CalendarBookingRecords
                .Where(b => b.TenantId == tenantId && hostIds.Contains(b.HostUserId))
                .GroupBy(b => b.HostUserId)
                .Select(g => new { HostId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.HostId, x => x.Count);

            selectedHostId = hostIds.OrderBy(h => bookingCounts.ContainsKey(h) ? bookingCounts[h] : 0).First();
        }
        else
        {
            selectedHostId = hostIds.FirstOrDefault();
            if (selectedHostId == Guid.Empty) selectedHostId = Guid.NewGuid();
        }

        var endTimeUtc = startTimeUtc.AddMinutes(eventType.DurationMinutes);
        var booking = CalendarBookingRecord.Create(
            tenantId,
            eventTypeId,
            selectedHostId,
            selectedHostName,
            attendeeName,
            attendeeEmail,
            attendeePhone,
            startTimeUtc,
            endTimeUtc,
            timeZone,
            notes
        );

        _db.CalendarBookingRecords.Add(booking);
        await _db.SaveChangesAsync();
        return booking;
    }

    public async Task<List<CalendarBookingRecord>> GetBookingsAsync(Guid tenantId, DateTime? fromUtc = null, DateTime? toUtc = null)
    {
        var query = _db.CalendarBookingRecords.Where(b => b.TenantId == tenantId);

        if (fromUtc.HasValue) query = query.Where(b => b.StartTimeUtc >= fromUtc.Value);
        if (toUtc.HasValue) query = query.Where(b => b.StartTimeUtc <= toUtc.Value);

        return await query.OrderBy(b => b.StartTimeUtc).ToListAsync();
    }

    public async Task<string> GenerateIcsContentAsync(Guid tenantId, Guid bookingId)
    {
        var booking = await _db.CalendarBookingRecords.FirstOrDefaultAsync(b => b.TenantId == tenantId && b.Id == bookingId);
        if (booking == null) throw new InvalidOperationException("Booking not found");

        var eventType = await _db.BookingEventTypes.FirstOrDefaultAsync(e => e.Id == booking.EventTypeId);
        var title = eventType?.Title ?? "Client Strategy Session";

        var sb = new StringBuilder();
        sb.AppendLine("BEGIN:VCALENDAR");
        sb.AppendLine("VERSION:2.0");
        sb.AppendLine("PRODID:-//SGrowing Enterprise//Calendar Engine//EN");
        sb.AppendLine("CALSCALE:GREGORIAN");
        sb.AppendLine("METHOD:REQUEST");
        sb.AppendLine("BEGIN:VEVENT");
        sb.AppendLine($"UID:{booking.IcsUid}");
        sb.AppendLine($"DTSTAMP:{DateTime.UtcNow:yyyyMMddTHHmmssZ}");
        sb.AppendLine($"DTSTART:{booking.StartTimeUtc:yyyyMMddTHHmmssZ}");
        sb.AppendLine($"DTEND:{booking.EndTimeUtc:yyyyMMddTHHmmssZ}");
        sb.AppendLine($"SUMMARY:{title}");
        sb.AppendLine($"ATTENDEE;CN={booking.AttendeeName};RSVP=TRUE:mailto:{booking.AttendeeEmail}");
        sb.AppendLine($"ORGANIZER;CN={booking.HostName}:mailto:host@sgrowing.io");
        sb.AppendLine($"DESCRIPTION:Meeting scheduled via SGrowing Omnicommunication.\\nAttendee: {booking.AttendeeName} ({booking.AttendeeEmail})\\nMeeting URL: {booking.MeetingUrl}");
        sb.AppendLine($"LOCATION:{booking.MeetingUrl}");
        sb.AppendLine("STATUS:CONFIRMED");
        sb.AppendLine("END:VEVENT");
        sb.AppendLine("END:VCALENDAR");

        return sb.ToString();
    }
}
