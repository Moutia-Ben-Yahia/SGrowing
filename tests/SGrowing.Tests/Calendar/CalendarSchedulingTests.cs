using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Calendar;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Calendar;

public class CalendarSchedulingTests
{
    private readonly SGrowingDbContext _db;
    private readonly CalendarSchedulingService _service;

    public CalendarSchedulingTests()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new SGrowingDbContext(options);
        _service = new CalendarSchedulingService(_db);
    }

    [Fact]
    public async Task CreateEventType_ShouldPersistAndNormalizeSlug()
    {
        var tenantId = Guid.NewGuid();
        var hostId = Guid.NewGuid();

        var eventType = await _service.CreateEventTypeAsync(
            tenantId,
            "Strategy & Demo Call",
            "Strategy-Demo-Call",
            "30-minute introductory meeting",
            30,
            isRoundRobin: true,
            color: "#6366f1",
            hosts: new List<Guid> { hostId }
        );

        Assert.NotNull(eventType);
        Assert.Equal("strategy-demo-call", eventType.Slug);
        Assert.Equal(30, eventType.DurationMinutes);
        Assert.True(eventType.IsRoundRobin);

        var list = await _service.GetEventTypesAsync(tenantId);
        Assert.Single(list);

        var retrieved = await _service.GetEventTypeBySlugAsync(tenantId, "strategy-demo-call");
        Assert.NotNull(retrieved);
        Assert.Equal(eventType.Id, retrieved.Id);
    }

    [Fact]
    public async Task ComputeAvailableSlots_ShouldGenerateExpectedWorkingHoursSlots()
    {
        var tenantId = Guid.NewGuid();
        var hostId = Guid.NewGuid();

        // 1. Create event type
        var eventType = await _service.CreateEventTypeAsync(
            tenantId,
            "Quick Catchup",
            "quick-catchup",
            "15 min checkin",
            30,
            isRoundRobin: false,
            hosts: new List<Guid> { hostId }
        );

        // 2. Set host availability for Tuesday
        var tuesdayHours = new List<DayAvailability>
        {
            new("Tuesday", "09:00", "12:00")
        };
        await _service.SetHostAvailabilityAsync(tenantId, hostId, "Alex Host", "UTC", tuesdayHours);

        // 3. Compute slots on a Tuesday
        var targetTuesday = new DateTime(2026, 11, 3, 0, 0, 0, DateTimeKind.Utc); // Nov 3 2026 is a Tuesday
        var slots = await _service.ComputeAvailableSlotsAsync(tenantId, eventType.Id, targetTuesday);

        Assert.NotEmpty(slots);
        Assert.All(slots, s => Assert.Equal(hostId, s.HostUserId));
        Assert.All(slots, s => Assert.Equal(DayOfWeek.Tuesday, s.StartTimeUtc.DayOfWeek));
    }

    [Fact]
    public async Task BookAppointment_ShouldCreateBookingAndExcludeSlotFromAvailability()
    {
        var tenantId = Guid.NewGuid();
        var hostId = Guid.NewGuid();

        var eventType = await _service.CreateEventTypeAsync(
            tenantId,
            "VIP Session",
            "vip-session",
            "60 mins",
            60,
            isRoundRobin: false,
            hosts: new List<Guid> { hostId }
        );

        var mondayHours = new List<DayAvailability>
        {
            new("Monday", "14:00", "16:00")
        };
        await _service.SetHostAvailabilityAsync(tenantId, hostId, "Sam Host", "UTC", mondayHours);

        var bookingTime = new DateTime(2026, 11, 2, 14, 0, 0, DateTimeKind.Utc); // Monday

        // Book slot
        var booking = await _service.BookAppointmentAsync(
            tenantId,
            eventType.Id,
            "John Doe",
            "john@enterprise.com",
            "+21698765432",
            bookingTime,
            "Africa/Tunis",
            "Need enterprise COD integration"
        );

        Assert.NotNull(booking);
        Assert.Equal("John Doe", booking.AttendeeName);
        Assert.Equal("john@enterprise.com", booking.AttendeeEmail);
        Assert.Equal(CalendarBookingStatus.Scheduled, booking.Status);
        Assert.False(string.IsNullOrEmpty(booking.MeetingUrl));

        // Available slots should no longer include 14:00
        var remainingSlots = await _service.ComputeAvailableSlotsAsync(tenantId, eventType.Id, bookingTime.Date);
        Assert.DoesNotContain(remainingSlots, s => s.StartTimeUtc == bookingTime);
    }

    [Fact]
    public async Task GenerateIcsContent_ShouldReturnRfc5545CompliantPayload()
    {
        var tenantId = Guid.NewGuid();
        var hostId = Guid.NewGuid();

        var eventType = await _service.CreateEventTypeAsync(
            tenantId,
            "Product Walkthrough",
            "walkthrough",
            "Deep demo",
            45,
            isRoundRobin: false,
            hosts: new List<Guid> { hostId }
        );

        var startTime = DateTime.UtcNow.AddDays(1);
        var booking = await _service.BookAppointmentAsync(
            tenantId,
            eventType.Id,
            "Jane Operator",
            "jane@logistics.tn",
            "+21622334455",
            startTime,
            "UTC",
            "Review multi-carrier SLA tracking"
        );

        var ics = await _service.GenerateIcsContentAsync(tenantId, booking.Id);

        Assert.Contains("BEGIN:VCALENDAR", ics);
        Assert.Contains("VERSION:2.0", ics);
        Assert.Contains("BEGIN:VEVENT", ics);
        Assert.Contains("SUMMARY:Product Walkthrough", ics);
        Assert.Contains("jane@logistics.tn", ics);
        Assert.Contains("END:VEVENT", ics);
        Assert.Contains("END:VCALENDAR", ics);
    }
}
