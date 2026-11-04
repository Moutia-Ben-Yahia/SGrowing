using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
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

        eventType.Should().NotBeNull();
        eventType.Slug.Should().Be("strategy-demo-call");
        eventType.DurationMinutes.Should().Be(30);
        eventType.IsRoundRobin.Should().BeTrue();

        var list = await _service.GetEventTypesAsync(tenantId);
        list.Should().ContainSingle();

        var retrieved = await _service.GetEventTypeBySlugAsync(tenantId, "strategy-demo-call");
        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be(eventType.Id);
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

        slots.Should().NotBeEmpty();
        slots.Should().AllSatisfy(s =>
        {
            s.HostUserId.Should().Be(hostId);
            s.StartTimeUtc.DayOfWeek.Should().Be(DayOfWeek.Tuesday);
        });
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

        booking.Should().NotBeNull();
        booking.AttendeeName.Should().Be("John Doe");
        booking.AttendeeEmail.Should().Be("john@enterprise.com");
        booking.Status.Should().Be(CalendarBookingStatus.Scheduled);
        booking.MeetingUrl.Should().NotBeNullOrWhiteSpace();

        // Available slots should no longer include 14:00
        var remainingSlots = await _service.ComputeAvailableSlotsAsync(tenantId, eventType.Id, bookingTime.Date);
        remainingSlots.Should().NotContain(s => s.StartTimeUtc == bookingTime);
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

        ics.Should().Contain("BEGIN:VCALENDAR");
        ics.Should().Contain("VERSION:2.0");
        ics.Should().Contain("BEGIN:VEVENT");
        ics.Should().Contain("SUMMARY:Product Walkthrough");
        ics.Should().Contain("jane@logistics.tn");
        ics.Should().Contain("END:VEVENT");
        ics.Should().Contain("END:VCALENDAR");
    }

    [Fact]
    public async Task CancelBooking_ShouldUpdateStatusToCancelledAndRecordReason()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var hostId = Guid.NewGuid();

        var eventType = await _service.CreateEventTypeAsync(
            tenantId, "Quarterly QBR", "quarterly-qbr", "Review", 60, false, "#10b981", new List<Guid> { hostId });

        var bookingTime = DateTime.UtcNow.AddDays(2);
        var booking = await _service.BookAppointmentAsync(
            tenantId, eventType.Id, "Taylor Swift", "taylor@music.com", "+1234567890", bookingTime, "UTC", "QBR Review");

        // Act
        var cancelled = await _service.CancelBookingAsync(tenantId, booking.Id, "Scheduling conflict with board meeting");

        // Assert
        cancelled.Status.Should().Be(CalendarBookingStatus.Cancelled);
        cancelled.Notes.Should().Contain("Cancelled: Scheduling conflict with board meeting");
    }

    [Fact]
    public async Task RescheduleBooking_ShouldUpdateBookingTimeslot()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var hostId = Guid.NewGuid();

        var eventType = await _service.CreateEventTypeAsync(
            tenantId, "Architecture Sync", "arch-sync", "Tech", 45, false, "#f59e0b", new List<Guid> { hostId });

        var originalTime = DateTime.UtcNow.AddDays(3);
        var newTime = DateTime.UtcNow.AddDays(4);

        var booking = await _service.BookAppointmentAsync(
            tenantId, eventType.Id, "Bob Engineer", "bob@infra.com", "+1234567891", originalTime, "UTC", "Initial time");

        // Act
        var rescheduled = await _service.RescheduleBookingAsync(tenantId, booking.Id, newTime);

        // Assert
        rescheduled.StartTimeUtc.Should().Be(newTime);
        rescheduled.EndTimeUtc.Should().Be(newTime.AddMinutes(45));
        rescheduled.Status.Should().Be(CalendarBookingStatus.Scheduled);
    }

    [Fact]
    public async Task SetHostWeights_And_GetEventHostsWithStats_ShouldPersistAndReturnWeights()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var host1 = Guid.NewGuid();
        var host2 = Guid.NewGuid();

        var eventType = await _service.CreateEventTypeAsync(
            tenantId, "Lead Routing", "lead-routing", "Routing", 30, true, "#ef4444", new List<Guid> { host1, host2 });

        await _service.SetHostAvailabilityAsync(tenantId, host1, "Host One", "UTC", new List<DayAvailability>(), 1.0);
        await _service.SetHostAvailabilityAsync(tenantId, host2, "Host Two", "UTC", new List<DayAvailability>(), 1.0);

        // Act
        var customWeights = new Dictionary<Guid, double>
        {
            [host1] = 0.75,
            [host2] = 0.25
        };
        await _service.SetHostWeightsAsync(tenantId, eventType.Id, customWeights);

        var stats = await _service.GetEventHostsWithStatsAsync(tenantId, eventType.Id);

        // Assert
        stats.Should().HaveCount(2);
        var s1 = stats.Find(s => s.HostUserId == host1);
        var s2 = stats.Find(s => s.HostUserId == host2);

        s1.Should().NotBeNull();
        s1!.Weight.Should().Be(0.75);

        s2.Should().NotBeNull();
        s2!.Weight.Should().Be(0.25);
    }

    [Fact]
    public async Task ComputeAvailableSlots_WhenEventTypeNotFound_ReturnsEmptySlots()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var nonExistentEventTypeId = Guid.NewGuid();
        var targetDate = new DateTime(2026, 11, 4, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var slots = await _service.ComputeAvailableSlotsAsync(tenantId, nonExistentEventTypeId, targetDate);

        // Assert
        slots.Should().BeEmpty();
    }
}
