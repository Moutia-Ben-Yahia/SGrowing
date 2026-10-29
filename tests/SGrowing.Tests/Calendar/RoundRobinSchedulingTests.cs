using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Calendar;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Calendar;

public class RoundRobinSchedulingTests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public async Task WeightedRoundRobin_DistributionAcrossMultipleSalesReps_RespectsHostWeights()
    {
        // Arrange: 3 sales reps with weights 2.0 (senior), 1.0 (standard), 0.5 (junior)
        using var db = CreateDbContext();
        var service = new CalendarSchedulingService(db);
        var tenantId = Guid.NewGuid();

        var repAlpha = Guid.NewGuid(); // Weight 2.0
        var repBeta = Guid.NewGuid();  // Weight 1.0
        var repGamma = Guid.NewGuid(); // Weight 0.5

        await service.SetHostAvailabilityAsync(tenantId, repAlpha, "Alpha Rep (Senior)", "Africa/Tunis", new List<DayAvailability>(), weight: 2.0);
        await service.SetHostAvailabilityAsync(tenantId, repBeta, "Beta Rep (Standard)", "Africa/Tunis", new List<DayAvailability>(), weight: 1.0);
        await service.SetHostAvailabilityAsync(tenantId, repGamma, "Gamma Rep (Junior)", "Africa/Tunis", new List<DayAvailability>(), weight: 0.5);

        var hostWeights = new Dictionary<Guid, double>
        {
            [repAlpha] = 2.0,
            [repBeta] = 1.0,
            [repGamma] = 0.5
        };

        var eventType = await service.CreateEventTypeAsync(
            tenantId,
            "Sales Qualification Call",
            "sales-qualification",
            "Lead distribution round-robin",
            30,
            isRoundRobin: true,
            color: "#6366f1",
            hosts: new List<Guid> { repAlpha, repBeta, repGamma },
            hostWeights: hostWeights
        );

        // Act: Sequentially assign 7 meetings across different time slots
        var baseDate = new DateTime(2026, 11, 10, 9, 0, 0, DateTimeKind.Utc);
        var assignedHosts = new List<Guid>();

        for (int i = 0; i < 7; i++)
        {
            var meetingTime = baseDate.AddHours(i);
            var assignedHost = await service.AssignHostViaRoundRobinAsync(tenantId, eventType.Id, meetingTime);
            assignedHosts.Add(assignedHost);

            // Record booking to update meeting counter for subsequent round-robin calculations
            await service.BookAppointmentAsync(
                tenantId,
                eventType.Id,
                $"Lead {i + 1}",
                $"lead{i + 1}@prospect.tn",
                $"+2169876000{i}",
                meetingTime,
                "Africa/Tunis",
                $"Assigned to host {assignedHost}"
            );
        }

        // Assert:
        // Expected counts: Alpha (4), Beta (2), Gamma (1)
        // Ratio matches 2.0 : 1.0 : 0.5 perfectly!
        var alphaCount = assignedHosts.Count(h => h == repAlpha);
        var betaCount = assignedHosts.Count(h => h == repBeta);
        var gammaCount = assignedHosts.Count(h => h == repGamma);

        alphaCount.Should().Be(4, "Senior rep with weight 2.0 should receive 4 meetings out of 7");
        betaCount.Should().Be(2, "Standard rep with weight 1.0 should receive 2 meetings out of 7");
        gammaCount.Should().Be(1, "Junior rep with weight 0.5 should receive 1 meeting out of 7");

        // Verify sequence: Alpha gets 1st (weight tie-break), then Beta, then Gamma, then Alpha, etc.
        assignedHosts[0].Should().Be(repAlpha);
        assignedHosts[1].Should().Be(repBeta);
        assignedHosts[2].Should().Be(repGamma);
        assignedHosts[3].Should().Be(repAlpha);
    }

    [Fact]
    public async Task ConflictCollisionPrevention_FiltersConflictedHost_SelectsAvailableHost()
    {
        // Arrange
        using var db = CreateDbContext();
        var service = new CalendarSchedulingService(db);
        var tenantId = Guid.NewGuid();

        var repAlpha = Guid.NewGuid(); // Weight 3.0 (would normally win)
        var repBeta = Guid.NewGuid();  // Weight 1.0

        await service.SetHostAvailabilityAsync(tenantId, repAlpha, "Alpha Heavy Weight", "UTC", new List<DayAvailability>(), weight: 3.0);
        await service.SetHostAvailabilityAsync(tenantId, repBeta, "Beta Backup", "UTC", new List<DayAvailability>(), weight: 1.0);

        var hostWeights = new Dictionary<Guid, double>
        {
            [repAlpha] = 3.0,
            [repBeta] = 1.0
        };

        var eventType = await service.CreateEventTypeAsync(
            tenantId,
            "Urgent Support Session",
            "urgent-support",
            "30 min slot",
            30,
            isRoundRobin: true,
            color: "#ef4444",
            hosts: new List<Guid> { repAlpha, repBeta },
            hostWeights: hostWeights
        );

        var conflictTime = new DateTime(2026, 11, 12, 10, 0, 0, DateTimeKind.Utc);

        // Pre-book repAlpha at 10:00 to create a conflict
        var existingBooking = CalendarBookingRecord.Create(
            tenantId,
            eventType.Id,
            repAlpha,
            "Alpha Heavy Weight",
            "Existing Client",
            "client@alpha.tn",
            "+21611111111",
            conflictTime,
            conflictTime.AddMinutes(30),
            "UTC",
            "Existing meeting"
        );
        db.CalendarBookingRecords.Add(existingBooking);
        await db.SaveChangesAsync();

        // Act: Assign slot at the exact conflict time
        var chosenHost = await service.AssignHostViaRoundRobinAsync(tenantId, eventType.Id, conflictTime);

        // Assert: Even though Rep Alpha has weight 3.0 and zero other bookings, Alpha is in conflict
        // and must be filtered out. Rep Beta must be selected!
        chosenHost.Should().Be(repBeta);
    }

    [Fact]
    public async Task ConflictCollisionPrevention_WhenAllHostsConflicted_ThrowsInvalidOperationException()
    {
        // Arrange
        using var db = CreateDbContext();
        var service = new CalendarSchedulingService(db);
        var tenantId = Guid.NewGuid();

        var host1 = Guid.NewGuid();
        var host2 = Guid.NewGuid();

        var eventType = await service.CreateEventTypeAsync(
            tenantId,
            "Double Conflict Call",
            "double-conflict",
            "30 min slot",
            30,
            isRoundRobin: true,
            hosts: new List<Guid> { host1, host2 }
        );

        var meetingTime = new DateTime(2026, 11, 15, 14, 0, 0, DateTimeKind.Utc);

        // Both hosts booked at 14:00
        db.CalendarBookingRecords.Add(CalendarBookingRecord.Create(
            tenantId, eventType.Id, host1, "Host 1", "Attendee 1", "a1@test.com", "123", meetingTime, meetingTime.AddMinutes(30)));
        db.CalendarBookingRecords.Add(CalendarBookingRecord.Create(
            tenantId, eventType.Id, host2, "Host 2", "Attendee 2", "a2@test.com", "456", meetingTime, meetingTime.AddMinutes(30)));
        await db.SaveChangesAsync();

        // Act & Assert
        var act = async () => await service.AssignHostViaRoundRobinAsync(tenantId, eventType.Id, meetingTime);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No hosts are available*scheduling conflicts*");
    }

    [Fact]
    public async Task PublicBooking_ComputesSlots_AndGeneratesRfc5545IcsInvitation()
    {
        // Arrange
        using var db = CreateDbContext();
        var service = new CalendarSchedulingService(db);
        var tenantId = Guid.NewGuid();
        var hostId = Guid.NewGuid();

        var tenant = new Tenant { Id = tenantId, Name = "SGrowing Maghreb", Slug = "sgrowing-maghreb" };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        await service.SetHostAvailabilityAsync(tenantId, hostId, "Sami Kallel", "Africa/Tunis", new List<DayAvailability>());

        var eventType = await service.CreateEventTypeAsync(
            tenantId,
            "Executive Enterprise Review",
            "executive-review",
            "Strategic architecture walkthrough",
            45,
            isRoundRobin: false,
            hosts: new List<Guid> { hostId }
        );

        var targetDate = new DateTime(2026, 11, 20, 0, 0, 0, DateTimeKind.Utc);

        // Act 1: Compute public booking page
        var publicPage = await service.GetPublicBookingPageAsync("sgrowing-maghreb", "executive-review", targetDate);

        publicPage.Should().NotBeNull();
        publicPage!.Value.EventType.Title.Should().Be("Executive Enterprise Review");
        publicPage.Value.Slots.Should().NotBeEmpty();
        publicPage.Value.Slots.First().HostName.Should().Be("Sami Kallel");

        // Act 2: Public booking execution
        var slotToBook = publicPage.Value.Slots.First().StartTimeUtc;
        var booking = await service.BookPublicAppointmentAsync(
            "sgrowing-maghreb",
            "executive-review",
            "Fatma Ben Salem",
            "fatma@distrib.tn",
            "+21622334455",
            slotToBook,
            "Africa/Tunis",
            "Interested in COD remittance auto-clearing"
        );

        booking.Should().NotBeNull();
        booking.AttendeeName.Should().Be("Fatma Ben Salem");
        booking.AttendeeEmail.Should().Be("fatma@distrib.tn");
        booking.MeetingUrl.Should().StartWith("https://meet.sgrowing.io/room/");
        booking.IcsUid.Should().EndWith("@sgrowing.io");
        booking.Status.Should().Be(CalendarBookingStatus.Scheduled);

        // Act 3: Generate .ics calendar invitation
        var ics = await service.GenerateIcsContentAsync(tenantId, booking.Id);

        ics.Should().Contain("BEGIN:VCALENDAR");
        ics.Should().Contain("VERSION:2.0");
        ics.Should().Contain("PRODID:-//SGrowing Enterprise//Calendar Engine//EN");
        ics.Should().Contain("BEGIN:VEVENT");
        ics.Should().Contain("SUMMARY:Executive Enterprise Review");
        ics.Should().Contain("ATTENDEE;CN=Fatma Ben Salem;RSVP=TRUE:mailto:fatma@distrib.tn");
        ics.Should().Contain("ORGANIZER;CN=Sami Kallel:mailto:host@sgrowing.io");
        ics.Should().Contain($"UID:{booking.IcsUid}");
        ics.Should().Contain($"LOCATION:{booking.MeetingUrl}");
        ics.Should().Contain("STATUS:CONFIRMED");
        ics.Should().Contain("END:VEVENT");
        ics.Should().Contain("END:VCALENDAR");
    }

    [Fact]
    public async Task HostWeights_ZeroOrNegative_DefaultsToOneWithoutZeroDivision()
    {
        // Arrange
        using var db = CreateDbContext();
        var service = new CalendarSchedulingService(db);
        var tenantId = Guid.NewGuid();

        var host1 = Guid.NewGuid();
        var host2 = Guid.NewGuid();

        var hostWeights = new Dictionary<Guid, double>
        {
            [host1] = 0.0,
            [host2] = -5.0
        };

        var eventType = await service.CreateEventTypeAsync(
            tenantId,
            "Default Weight Event",
            "default-weight",
            "Testing 0 or negative weight safety",
            30,
            isRoundRobin: true,
            hosts: new List<Guid> { host1, host2 },
            hostWeights: hostWeights
        );

        var meetingTime = new DateTime(2026, 11, 22, 11, 0, 0, DateTimeKind.Utc);

        // Act: Should not throw DivideByZeroException
        var assigned = await service.AssignHostViaRoundRobinAsync(tenantId, eventType.Id, meetingTime);

        // Assert
        (assigned == host1 || assigned == host2).Should().BeTrue();
    }

    [Fact]
    public async Task SetHostWeightsAsync_And_GetEventHostsWithStatsAsync_PersistsAndReturnsStats()
    {
        // Arrange
        using var db = CreateDbContext();
        var service = new CalendarSchedulingService(db);
        var tenantId = Guid.NewGuid();

        var hostA = Guid.NewGuid();
        var hostB = Guid.NewGuid();

        await service.SetHostAvailabilityAsync(tenantId, hostA, "Karim Ben Amor", "UTC", new List<DayAvailability>(), weight: 1.0);
        await service.SetHostAvailabilityAsync(tenantId, hostB, "Leila Toumi", "UTC", new List<DayAvailability>(), weight: 1.0);

        var eventType = await service.CreateEventTypeAsync(
            tenantId,
            "Consulting Session",
            "consulting-session",
            "Strategy",
            30,
            isRoundRobin: true,
            hosts: new List<Guid> { hostA, hostB }
        );

        // Book 2 meetings via round-robin:
        // Meeting 1 is assigned to hostA (count: A=1, B=0)
        // Meeting 2 is assigned to hostB (count: A=1, B=1)
        var t1 = new DateTime(2026, 11, 25, 9, 0, 0, DateTimeKind.Utc);
        var t2 = new DateTime(2026, 11, 25, 10, 0, 0, DateTimeKind.Utc);
        await service.BookAppointmentAsync(tenantId, eventType.Id, "Client 1", "c1@test.com", "123", t1);
        await service.BookAppointmentAsync(tenantId, eventType.Id, "Client 2", "c2@test.com", "456", t2);

        // Update weights dynamically
        var newWeights = new Dictionary<Guid, double>
        {
            [hostA] = 2.5,
            [hostB] = 1.5
        };
        await service.SetHostWeightsAsync(tenantId, eventType.Id, newWeights);

        // Act
        var stats = await service.GetEventHostsWithStatsAsync(tenantId, eventType.Id);

        // Assert
        stats.Should().HaveCount(2);

        var statA = stats.FirstOrDefault(s => s.HostUserId == hostA);
        statA.Should().NotBeNull();
        statA!.HostName.Should().Be("Karim Ben Amor");
        statA.Weight.Should().Be(2.5);
        statA.AssignedMeetingsCount.Should().Be(1);
        statA.Score.Should().Be(0.4); // 1 / 2.5 = 0.4

        var statB = stats.FirstOrDefault(s => s.HostUserId == hostB);
        statB.Should().NotBeNull();
        statB!.HostName.Should().Be("Leila Toumi");
        statB.Weight.Should().Be(1.5);
        statB.AssignedMeetingsCount.Should().Be(1);
        statB.Score.Should().Be(0.67); // 1 / 1.5 = 0.67
    }
}
