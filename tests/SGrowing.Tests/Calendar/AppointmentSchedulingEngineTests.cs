using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Calendar;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Calendar;

public class AppointmentSchedulingEngineTests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public async Task DoubleBooking_ConflictGuard_ThrowsInvalidOperationException()
    {
        // Arrange
        using var db = CreateDbContext();
        var service = new CalendarSchedulingService(db);
        var tenantId = Guid.NewGuid();
        var hostId = Guid.NewGuid();

        var eventType = await service.CreateEventTypeAsync(
            tenantId,
            "VIP Strategy Session",
            "vip-strategy",
            "Deep strategic session",
            30,
            isRoundRobin: false,
            hosts: new List<Guid> { hostId }
        );

        var startTime = new DateTime(2026, 11, 5, 10, 0, 0, DateTimeKind.Utc);

        // First booking succeeds
        var firstBooking = await service.BookAppointmentAsync(
            tenantId,
            eventType.Id,
            "Alice Smith",
            "alice@example.com",
            "+1234567890",
            startTime,
            "UTC",
            "Quarterly Review"
        );

        firstBooking.Should().NotBeNull();
        firstBooking.Status.Should().Be(CalendarBookingStatus.Scheduled);

        // Act & Assert: Attempting to book the identical time slot must throw
        var act = async () => await service.BookAppointmentAsync(
            tenantId,
            eventType.Id,
            "Bob Jones",
            "bob@example.com",
            "+1987654321",
            startTime,
            "UTC",
            "Conflict attempt"
        );

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already booked*");
    }

    [Fact]
    public async Task GetPublicBookingPageAsync_WithValidTenantAndEventSlug_ReturnsEventTypeAndSlots()
    {
        // Arrange
        using var db = CreateDbContext();
        var service = new CalendarSchedulingService(db);
        var tenantId = Guid.NewGuid();
        var hostId = Guid.NewGuid();

        var tenant = new Tenant { Id = tenantId, Name = "Acme Corp", Slug = "acme-corp" };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var eventType = await service.CreateEventTypeAsync(
            tenantId,
            "Product Demo 30",
            "product-demo-30",
            "30 min demo",
            30,
            isRoundRobin: false,
            hosts: new List<Guid> { hostId }
        );

        // Act
        var targetDate = new DateTime(2026, 11, 5, 0, 0, 0, DateTimeKind.Utc);
        var result = await service.GetPublicBookingPageAsync("acme-corp", "product-demo-30", targetDate);

        // Assert
        result.Should().NotBeNull();
        result!.Value.EventType.Should().NotBeNull();
        result.Value.EventType.Slug.Should().Be("product-demo-30");
        result.Value.Slots.Should().NotBeEmpty();
    }

    [Fact]
    public async Task BookPublicAppointmentAsync_PersistsBookingWithWebRtcUrl()
    {
        // Arrange
        using var db = CreateDbContext();
        var service = new CalendarSchedulingService(db);
        var tenantId = Guid.NewGuid();
        var hostId = Guid.NewGuid();

        var tenant = new Tenant { Id = tenantId, Name = "Growth Dynamics", Slug = "growth-dyn" };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        await service.CreateEventTypeAsync(
            tenantId,
            "Growth Strategy Call",
            "growth-call",
            "1-on-1 discovery call",
            45,
            isRoundRobin: false,
            hosts: new List<Guid> { hostId }
        );

        var startTime = new DateTime(2026, 11, 6, 14, 0, 0, DateTimeKind.Utc);

        // Act
        var booking = await service.BookPublicAppointmentAsync(
            "growth-dyn",
            "growth-call",
            "Sarah Connor",
            "sarah@skynet-resistance.com",
            "+15550192834",
            startTime,
            "America/New_York",
            "Discuss omnicommunication workflows"
        );

        // Assert
        booking.Should().NotBeNull();
        booking.TenantId.Should().Be(tenantId);
        booking.AttendeeName.Should().Be("Sarah Connor");
        booking.AttendeeEmail.Should().Be("sarah@skynet-resistance.com");
        booking.Status.Should().Be(CalendarBookingStatus.Scheduled);
        booking.MeetingUrl.Should().StartWith("https://meet.sgrowing.io/room/");
        booking.IcsUid.Should().EndWith("@sgrowing.io");
    }

    [Fact]
    public async Task CancelBookingAsync_MarksStatusCancelledAndStoresReason()
    {
        // Arrange
        using var db = CreateDbContext();
        var service = new CalendarSchedulingService(db);
        var tenantId = Guid.NewGuid();
        var hostId = Guid.NewGuid();

        var eventType = await service.CreateEventTypeAsync(
            tenantId,
            "Intro Meeting",
            "intro",
            "Introductory call",
            30,
            isRoundRobin: false,
            hosts: new List<Guid> { hostId }
        );

        var startTime = new DateTime(2026, 11, 7, 11, 0, 0, DateTimeKind.Utc);
        var booking = await service.BookAppointmentAsync(
            tenantId,
            eventType.Id,
            "Dave Miller",
            "dave@miller.co",
            "+15551234567",
            startTime,
            "UTC",
            "Project kickoff"
        );

        // Act
        var cancelled = await service.CancelBookingAsync(tenantId, booking.Id, "Schedule conflict with client");

        // Assert
        cancelled.Status.Should().Be(CalendarBookingStatus.Cancelled);
        cancelled.Notes.Should().Contain("Cancelled: Schedule conflict with client");
    }

    [Fact]
    public async Task RescheduleBookingAsync_UpdatesTimeslot_WhenNoConflict()
    {
        // Arrange
        using var db = CreateDbContext();
        var service = new CalendarSchedulingService(db);
        var tenantId = Guid.NewGuid();
        var hostId = Guid.NewGuid();

        var eventType = await service.CreateEventTypeAsync(
            tenantId,
            "Consultation",
            "consult",
            "Consultation",
            30,
            isRoundRobin: false,
            hosts: new List<Guid> { hostId }
        );

        var initialTime = new DateTime(2026, 11, 8, 10, 0, 0, DateTimeKind.Utc);
        var booking = await service.BookAppointmentAsync(
            tenantId,
            eventType.Id,
            "Emma Watson",
            "emma@cinema.com",
            "+442079460912",
            initialTime,
            "Europe/London"
        );

        var newTime = new DateTime(2026, 11, 8, 15, 0, 0, DateTimeKind.Utc);

        // Act
        var rescheduled = await service.RescheduleBookingAsync(tenantId, booking.Id, newTime);

        // Assert
        rescheduled.StartTimeUtc.Should().Be(newTime);
        rescheduled.EndTimeUtc.Should().Be(newTime.AddMinutes(30));
        rescheduled.Status.Should().Be(CalendarBookingStatus.Scheduled);
    }

    [Fact]
    public async Task RescheduleBookingAsync_WhenConflictExists_ThrowsInvalidOperationException()
    {
        // Arrange
        using var db = CreateDbContext();
        var service = new CalendarSchedulingService(db);
        var tenantId = Guid.NewGuid();
        var hostId = Guid.NewGuid();

        var eventType = await service.CreateEventTypeAsync(
            tenantId,
            "Consultation",
            "consult-2",
            "Consultation",
            30,
            isRoundRobin: false,
            hosts: new List<Guid> { hostId }
        );

        var slotA = new DateTime(2026, 11, 9, 10, 0, 0, DateTimeKind.Utc);
        var slotB = new DateTime(2026, 11, 9, 11, 0, 0, DateTimeKind.Utc);

        var bookingA = await service.BookAppointmentAsync(
            tenantId,
            eventType.Id,
            "Client A",
            "a@client.com",
            "+123456",
            slotA,
            "UTC"
        );

        var bookingB = await service.BookAppointmentAsync(
            tenantId,
            eventType.Id,
            "Client B",
            "b@client.com",
            "+654321",
            slotB,
            "UTC"
        );

        // Act & Assert: Rescheduling bookingA into slotB must fail because host is already booked
        var act = async () => await service.RescheduleBookingAsync(tenantId, bookingA.Id, slotB);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*scheduling conflict*");
    }
}
