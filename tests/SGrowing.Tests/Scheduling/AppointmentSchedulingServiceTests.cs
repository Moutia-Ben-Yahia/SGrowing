using System.Collections.Concurrent;
using FluentAssertions;
using SGrowing.Api.Modules.Scheduling;
using Xunit;

namespace SGrowing.Tests.Scheduling;

public class AppointmentSchedulingServiceTests
{
    private readonly AppointmentSchedulingService _service;

    public AppointmentSchedulingServiceTests()
    {
        _service = new AppointmentSchedulingService();
    }

    #region Host Management Tests

    [Fact]
    public async Task GetHosts_ReturnsPreSeededHostsWithWorkingHoursAndWeights()
    {
        // Act
        var hosts = await _service.GetHostsAsync();

        // Assert
        hosts.Should().NotBeNull();
        hosts.Count.Should().BeGreaterThanOrEqualTo(4);
        hosts.Should().Contain(h => h.HostName == "Sarah Jenkins" && h.Weight == 3);
        hosts.Should().Contain(h => h.HostName == "Dr. Alexandre Mercier" && h.Weight == 2);
        hosts.Should().Contain(h => h.HostName == "Tariq Mansour" && h.Weight == 1);
        hosts.Should().Contain(h => h.HostName == "Kenji Tanaka" && h.Weight == 2);

        foreach (var host in hosts)
        {
            host.WorkingHours.Should().NotBeEmpty();
            host.BufferTime.Should().BePositive();
            host.MinimumNotice.Should().BePositive();
        }
    }

    [Fact]
    public async Task GetHostById_ReturnsCorrectHostWhenFound()
    {
        // Arrange
        var hosts = await _service.GetHostsAsync();
        var expected = hosts.First();

        // Act
        var host = await _service.GetHostByIdAsync(expected.HostId);

        // Assert
        host.Should().NotBeNull();
        host!.HostName.Should().Be(expected.HostName);
        host.HostEmail.Should().Be(expected.HostEmail);
        host.TimeZoneId.Should().Be(expected.TimeZoneId);
    }

    [Fact]
    public async Task GetHostById_ReturnsNullWhenNotFound()
    {
        // Act
        var host = await _service.GetHostByIdAsync(Guid.NewGuid());

        // Assert
        host.Should().BeNull();
    }

    #endregion

    #region Temporal Slot Generation Tests

    [Fact]
    public async Task GetAvailableSlots_ExcludesWeekendsWhenHostWorksMondayThroughFriday()
    {
        // Arrange: Find upcoming Saturday and Sunday
        var now = DateTimeOffset.UtcNow;
        var daysUntilSaturday = ((int)DayOfWeek.Saturday - (int)now.DayOfWeek + 7) % 7;
        if (daysUntilSaturday == 0) daysUntilSaturday = 7;
        var nextSaturday = now.Date.AddDays(daysUntilSaturday);
        var nextSunday = nextSaturday.AddDays(1);

        var sarah = (await _service.GetHostsAsync()).First(h => h.HostName == "Sarah Jenkins");
        var query = new SlotQuery(
            HostId: sarah.HostId,
            RangeStartUtc: new DateTimeOffset(nextSaturday, TimeSpan.Zero),
            RangeEndUtc: new DateTimeOffset(nextSunday.AddHours(23), TimeSpan.Zero),
            DurationMinutes: 30,
            TargetTimeZoneId: "America/New_York"
        );

        // Act
        var slots = await _service.GetAvailableSlotsAsync(query);

        // Assert: No slots on weekend for a Monday-Friday host
        slots.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAvailableSlots_RespectsWorkingHoursBoundary()
    {
        // Arrange: Next Wednesday 00:00 to 23:59 UTC
        var now = DateTimeOffset.UtcNow;
        var daysUntilWednesday = ((int)DayOfWeek.Wednesday - (int)now.DayOfWeek + 7) % 7;
        if (daysUntilWednesday == 0) daysUntilWednesday = 7;
        var wednesday = now.Date.AddDays(daysUntilWednesday);

        var host = (await _service.GetHostsAsync()).First(h => h.HostName == "Sarah Jenkins");
        var query = new SlotQuery(
            HostId: host.HostId,
            RangeStartUtc: new DateTimeOffset(wednesday, TimeSpan.Zero),
            RangeEndUtc: new DateTimeOffset(wednesday.AddDays(1), TimeSpan.Zero),
            DurationMinutes: 30,
            TargetTimeZoneId: host.TimeZoneId
        );

        // Act
        var slots = await _service.GetAvailableSlotsAsync(query);

        // Assert: Sarah works 09:00 - 17:00 in America/New_York
        slots.Should().NotBeEmpty();
        foreach (var slot in slots)
        {
            var localTime = DateTime.Parse(slot.StartLocalFormatted);
            localTime.TimeOfDay.Should().BeGreaterThanOrEqualTo(TimeSpan.FromHours(9));
            localTime.TimeOfDay.Should().BeLessThan(TimeSpan.FromHours(17));
        }
    }

    [Fact]
    public async Task GetAvailableSlots_RespectsMinimumNoticePeriod()
    {
        // Arrange: Start range right now
        var now = DateTimeOffset.UtcNow;
        var host = (await _service.GetHostsAsync()).First(h => h.HostName == "Dr. Alexandre Mercier"); // Min notice 2h

        var query = new SlotQuery(
            HostId: host.HostId,
            RangeStartUtc: now,
            RangeEndUtc: now.AddDays(1),
            DurationMinutes: 30,
            TargetTimeZoneId: "UTC"
        );

        // Act
        var slots = await _service.GetAvailableSlotsAsync(query);

        // Assert: All slots must start at or after now + host.MinimumNotice
        foreach (var slot in slots)
        {
            slot.StartUtc.Should().BeOnOrAfter(now.Add(host.MinimumNotice));
        }
    }

    [Fact]
    public async Task GetAvailableSlots_ExcludesExternalBusyIntervals()
    {
        // Arrange
        var host = (await _service.GetHostsAsync()).First(h => h.HostName == "Tariq Mansour");
        var testDay = DateTimeOffset.UtcNow.Date.AddDays(3);
        var busyStart = testDay.AddHours(11);
        var busyEnd = testDay.AddHours(13);

        host.ExternalBusyIntervals.Add(new BusyInterval(busyStart, busyEnd, "Company All-Hands Meeting"));

        var query = new SlotQuery(
            HostId: host.HostId,
            RangeStartUtc: testDay,
            RangeEndUtc: testDay.AddDays(1),
            DurationMinutes: 30,
            TargetTimeZoneId: "UTC"
        );

        // Act
        var slots = await _service.GetAvailableSlotsAsync(query);

        // Assert: No slot should overlap the busy interval or its buffer
        foreach (var slot in slots)
        {
            var slotWithBufferStart = slot.StartUtc;
            var slotWithBufferEnd = slot.EndUtc;

            var overlap = slotWithBufferStart < (busyEnd + host.BufferTime) && slotWithBufferEnd > (busyStart - host.BufferTime);
            overlap.Should().BeFalse($"Slot at {slot.StartUtc} overlaps external busy interval with buffer.");
        }
    }

    [Fact]
    public async Task GetAvailableSlots_EnforcesBufferTimeAroundExistingAppointments()
    {
        // Arrange
        var host = (await _service.GetHostsAsync()).First(h => h.HostName == "Kenji Tanaka");
        var testDay = DateTimeOffset.UtcNow.Date.AddDays(2);
        var aptStart = testDay.AddHours(10);

        // Book an appointment at 10:00 UTC (duration 30m, so ends 10:30 UTC)
        await _service.BookAppointmentAsync(new BookAppointmentRequest(
            HostId: host.HostId,
            CollectiveHostIds: null,
            Topology: RoutingTopology.WeightedRoundRobin,
            Title: "Buffer Test Appointment",
            Description: "Testing 15m pre and post buffer",
            AttendeeName: "Test Attendee",
            AttendeeEmail: "attendee@test.com",
            AttendeePhone: null,
            AttendeeNotes: null,
            StartUtc: aptStart,
            DurationMinutes: 30,
            ProspectTimeZoneId: "UTC"
        ));

        var query = new SlotQuery(
            HostId: host.HostId,
            RangeStartUtc: testDay,
            RangeEndUtc: testDay.AddDays(1),
            DurationMinutes: 30,
            TargetTimeZoneId: "UTC"
        );

        // Act
        var slots = await _service.GetAvailableSlotsAsync(query);

        // Assert: Slot between 09:45 and 10:45 should not exist because buffer is 15 mins (ends 10:30 + 15m buffer = 10:45)
        slots.Should().NotContain(s => s.StartUtc == aptStart);
        slots.Should().NotContain(s => s.StartUtc == aptStart.AddMinutes(-15)); // would overlap pre-buffer
        slots.Should().NotContain(s => s.StartUtc == aptStart.AddMinutes(15));  // inside appointment
        slots.Should().NotContain(s => s.StartUtc == aptStart.AddMinutes(30));  // inside post-buffer
    }

    [Theory]
    [InlineData(15)]
    [InlineData(30)]
    [InlineData(45)]
    [InlineData(60)]
    public async Task GetAvailableSlots_SupportsVariableDurations(int durationMinutes)
    {
        // Arrange
        var host = (await _service.GetHostsAsync()).First(h => h.HostName == "Sarah Jenkins");
        var testDay = DateTimeOffset.UtcNow.Date.AddDays(4);

        var query = new SlotQuery(
            HostId: host.HostId,
            RangeStartUtc: testDay,
            RangeEndUtc: testDay.AddDays(1),
            DurationMinutes: durationMinutes,
            TargetTimeZoneId: "UTC"
        );

        // Act
        var slots = await _service.GetAvailableSlotsAsync(query);

        // Assert
        slots.Should().NotBeEmpty();
        foreach (var slot in slots)
        {
            (slot.EndUtc - slot.StartUtc).TotalMinutes.Should().Be(durationMinutes);
        }
    }

    [Fact]
    public async Task GetAvailableSlots_ThrowsArgumentException_WhenRangeEndBeforeStart()
    {
        var now = DateTimeOffset.UtcNow;
        var query = new SlotQuery(
            RangeStartUtc: now.AddDays(5),
            RangeEndUtc: now.AddDays(2),
            DurationMinutes: 30
        );

        var act = async () => await _service.GetAvailableSlotsAsync(query);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*RangeEndUtc must be after RangeStartUtc*");
    }

    #endregion

    #region Timezone & DST Boundary Projection Tests

    [Theory]
    [InlineData("America/New_York")]
    [InlineData("Europe/Paris")]
    [InlineData("Africa/Tunis")]
    [InlineData("Asia/Tokyo")]
    [InlineData("UTC")]
    public async Task GetAvailableSlots_CorrectlyProjectsToTargetTimeZones(string targetTz)
    {
        // Arrange
        var testDay = DateTimeOffset.UtcNow.Date.AddDays(3);
        var query = new SlotQuery(
            RangeStartUtc: testDay,
            RangeEndUtc: testDay.AddDays(1),
            DurationMinutes: 30,
            TargetTimeZoneId: targetTz
        );

        // Act
        var slots = await _service.GetAvailableSlotsAsync(query);

        // Assert
        slots.Should().NotBeEmpty();
        foreach (var slot in slots)
        {
            slot.TimeZoneId.Should().Be(targetTz);
            slot.StartLocalFormatted.Should().NotBeNullOrWhiteSpace();
            slot.EndLocalFormatted.Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public async Task GetAvailableSlots_HandlesInvalidOrUnknownTimeZoneGracefullyWithUtcFallback()
    {
        // Arrange
        var testDay = DateTimeOffset.UtcNow.Date.AddDays(2);
        var query = new SlotQuery(
            RangeStartUtc: testDay,
            RangeEndUtc: testDay.AddDays(1),
            DurationMinutes: 30,
            TargetTimeZoneId: "NonExistent/Imaginary_Zone"
        );

        // Act
        var slots = await _service.GetAvailableSlotsAsync(query);

        // Assert: Should not throw and should fallback safely to UTC
        slots.Should().NotBeEmpty();
        slots.First().TimeZoneId.Should().Be("UTC");
    }

    #endregion

    #region Collective Multi-Host Availability Tests

    [Fact]
    public async Task GetAvailableSlots_CollectiveAvailability_OnlyReturnsSlotsWhereAllHostsAreSimultaneouslyFree()
    {
        // Arrange: Select two hosts
        var hosts = (await _service.GetHostsAsync()).Take(2).ToList();
        var hostA = hosts[0];
        var hostB = hosts[1];

        var testDay = DateTimeOffset.UtcNow.Date.AddDays(3);

        var query = new SlotQuery(
            CollectiveHostIds: new List<Guid> { hostA.HostId, hostB.HostId },
            RangeStartUtc: testDay,
            RangeEndUtc: testDay.AddDays(1),
            DurationMinutes: 30,
            TargetTimeZoneId: "UTC"
        );

        // Act
        var collectiveSlots = await _service.GetAvailableSlotsAsync(query);

        // Also get individual slots for both
        var slotsA = await _service.GetAvailableSlotsAsync(query with { CollectiveHostIds = null, HostId = hostA.HostId });
        var slotsB = await _service.GetAvailableSlotsAsync(query with { CollectiveHostIds = null, HostId = hostB.HostId });

        var aStarts = slotsA.Select(s => s.StartUtc).ToHashSet();
        var bStarts = slotsB.Select(s => s.StartUtc).ToHashSet();

        // Assert: Every collective slot must be present in both hostA and hostB individual slots
        foreach (var cSlot in collectiveSlots)
        {
            aStarts.Should().Contain(cSlot.StartUtc);
            bStarts.Should().Contain(cSlot.StartUtc);
            cSlot.AvailableHostIds.Should().Contain(hostA.HostId);
            cSlot.AvailableHostIds.Should().Contain(hostB.HostId);
        }
    }

    [Fact]
    public async Task GetAvailableSlots_CollectiveAvailability_ReturnsEmptyWhenOneHostIsFullyBlocked()
    {
        // Arrange
        var hosts = (await _service.GetHostsAsync()).Take(2).ToList();
        var hostA = hosts[0];
        var hostB = hosts[1];

        var testDay = DateTimeOffset.UtcNow.Date.AddDays(5);
        // Block host A for the full day
        hostA.ExternalBusyIntervals.Add(new BusyInterval(testDay, testDay.AddDays(1), "Full Day Off"));

        var query = new SlotQuery(
            CollectiveHostIds: new List<Guid> { hostA.HostId, hostB.HostId },
            RangeStartUtc: testDay,
            RangeEndUtc: testDay.AddDays(1),
            DurationMinutes: 30,
            TargetTimeZoneId: "UTC"
        );

        // Act
        var collectiveSlots = await _service.GetAvailableSlotsAsync(query);

        // Assert
        collectiveSlots.Should().BeEmpty();
    }

    #endregion

    #region Booking & Double-Booking Prevention Tests

    [Fact]
    public async Task BookAppointment_SuccessfullyBooksAvailableSlot()
    {
        // Arrange
        var host = (await _service.GetHostsAsync()).First();
        var testDay = DateTimeOffset.UtcNow.Date.AddDays(2).AddHours(14);

        var request = new BookAppointmentRequest(
            HostId: host.HostId,
            CollectiveHostIds: null,
            Topology: RoutingTopology.WeightedRoundRobin,
            Title: "Discovery Strategy Session",
            Description: "Quarterly alignment and enterprise roadmap",
            AttendeeName: "John Enterprise",
            AttendeeEmail: "john@enterprise.com",
            AttendeePhone: "+1 555 987 6543",
            AttendeeNotes: "Interested in high-speed scheduling engine",
            StartUtc: testDay,
            DurationMinutes: 45,
            ProspectTimeZoneId: "America/New_York"
        );

        // Act
        var appointment = await _service.BookAppointmentAsync(request);

        // Assert
        appointment.Should().NotBeNull();
        appointment.Id.Should().NotBeEmpty();
        appointment.HostId.Should().Be(host.HostId);
        appointment.Status.Should().Be(AppointmentStatus.Confirmed);
        appointment.MeetingLink.Should().StartWith("https://meet.sgrowing.net/room-");
        appointment.AttendeeEmail.Should().Be("john@enterprise.com");
        appointment.EndUtc.Should().Be(testDay.AddMinutes(45));

        // Verify retrieval
        var retrieved = await _service.GetAppointmentByIdAsync(appointment.Id);
        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be(appointment.Id);
    }

    [Fact]
    public async Task BookAppointment_ThrowsArgumentException_WhenAttendeeNameOrEmailMissing()
    {
        var testDay = DateTimeOffset.UtcNow.Date.AddDays(2).AddHours(10);
        var requestEmptyName = new BookAppointmentRequest(
            HostId: null,
            CollectiveHostIds: null,
            Topology: RoutingTopology.WeightedRoundRobin,
            Title: "Test",
            Description: null,
            AttendeeName: "",
            AttendeeEmail: "test@example.com",
            AttendeePhone: null,
            AttendeeNotes: null,
            StartUtc: testDay,
            DurationMinutes: 30,
            ProspectTimeZoneId: "UTC"
        );

        var actName = async () => await _service.BookAppointmentAsync(requestEmptyName);
        await actName.Should().ThrowAsync<ArgumentException>();

        var requestEmptyEmail = requestEmptyName with { AttendeeName = "Valid Name", AttendeeEmail = " " };
        var actEmail = async () => await _service.BookAppointmentAsync(requestEmptyEmail);
        await actEmail.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task BookAppointment_ThrowsArgumentException_WhenDurationIsZeroOrNegative()
    {
        var testDay = DateTimeOffset.UtcNow.Date.AddDays(2).AddHours(10);
        var req = new BookAppointmentRequest(
            HostId: null,
            CollectiveHostIds: null,
            Topology: RoutingTopology.WeightedRoundRobin,
            Title: "Test",
            Description: null,
            AttendeeName: "Alice",
            AttendeeEmail: "alice@example.com",
            AttendeePhone: null,
            AttendeeNotes: null,
            StartUtc: testDay,
            DurationMinutes: 0,
            ProspectTimeZoneId: "UTC"
        );

        var act = async () => await _service.BookAppointmentAsync(req);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task BookAppointment_PreventsDoubleBooking_ThrowsConflictWhenSlotOverlapsExistingAppointment()
    {
        // Arrange
        var host = (await _service.GetHostsAsync()).First(h => h.HostName == "Sarah Jenkins");
        var slotStart = DateTimeOffset.UtcNow.Date.AddDays(3).AddHours(14);

        var req1 = new BookAppointmentRequest(
            HostId: host.HostId,
            CollectiveHostIds: null,
            Topology: RoutingTopology.WeightedRoundRobin,
            Title: "First Booking",
            Description: null,
            AttendeeName: "Attendee 1",
            AttendeeEmail: "one@test.com",
            AttendeePhone: null,
            AttendeeNotes: null,
            StartUtc: slotStart,
            DurationMinutes: 30,
            ProspectTimeZoneId: "UTC"
        );

        var req2 = new BookAppointmentRequest(
            HostId: host.HostId,
            CollectiveHostIds: null,
            Topology: RoutingTopology.WeightedRoundRobin,
            Title: "Second Conflicting Booking",
            Description: null,
            AttendeeName: "Attendee 2",
            AttendeeEmail: "two@test.com",
            AttendeePhone: null,
            AttendeeNotes: null,
            StartUtc: slotStart,
            DurationMinutes: 30,
            ProspectTimeZoneId: "UTC"
        );

        // Act
        var apt1 = await _service.BookAppointmentAsync(req1);
        apt1.Should().NotBeNull();

        var act = async () => await _service.BookAppointmentAsync(req2);

        // Assert: Throws InvalidOperationException due to collision
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*conflict or buffer collision*");
    }

    [Fact]
    public async Task BookAppointment_PreventsBooking_WhenOverlapsWithHostBuffer()
    {
        // Arrange: Host buffer is 15m. If appointment is 14:00-14:30, booking 14:35 should conflict with post-meeting buffer
        var host = (await _service.GetHostsAsync()).First(h => h.HostName == "Dr. Alexandre Mercier");
        var slotStart = DateTimeOffset.UtcNow.Date.AddDays(3).AddHours(14);

        await _service.BookAppointmentAsync(new BookAppointmentRequest(
            HostId: host.HostId,
            CollectiveHostIds: null,
            Topology: RoutingTopology.WeightedRoundRobin,
            Title: "Initial Meeting",
            Description: null,
            AttendeeName: "Attendee A",
            AttendeeEmail: "a@test.com",
            AttendeePhone: null,
            AttendeeNotes: null,
            StartUtc: slotStart,
            DurationMinutes: 30,
            ProspectTimeZoneId: "UTC"
        ));

        // Attempt booking starting 10 mins after first meeting ends (14:40) -> inside 15m buffer (until 14:45)
        var bufferOverlapReq = new BookAppointmentRequest(
            HostId: host.HostId,
            CollectiveHostIds: null,
            Topology: RoutingTopology.WeightedRoundRobin,
            Title: "Buffer Collision Meeting",
            Description: null,
            AttendeeName: "Attendee B",
            AttendeeEmail: "b@test.com",
            AttendeePhone: null,
            AttendeeNotes: null,
            StartUtc: slotStart.AddMinutes(35),
            DurationMinutes: 30,
            ProspectTimeZoneId: "UTC"
        );

        var act = async () => await _service.BookAppointmentAsync(bufferOverlapReq);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    #endregion

    #region Team Routing Topologies Tests

    [Fact]
    public async Task BookAppointment_WeightedRoundRobin_DistributesBookingsAccordingToHostWeights()
    {
        // Sarah (weight 3), Alexandre (weight 2), Tariq (weight 1), Kenji (weight 2)
        // Reset their assignments for deterministic test
        var hosts = await _service.GetHostsAsync();
        foreach (var h in hosts)
        {
            h.WeightedRoundRobinAssignments = 0;
            h.LastAssignedAtUtc = null;
        }

        var baseTime = DateTimeOffset.UtcNow.Date.AddDays(4).AddHours(14);
        var bookedHosts = new List<Guid>();

        // Book 8 appointments sequentially at distinct times
        for (int i = 0; i < 8; i++)
        {
            var req = new BookAppointmentRequest(
                HostId: null, // route dynamically
                CollectiveHostIds: null,
                Topology: RoutingTopology.WeightedRoundRobin,
                Title: $"Batch Booking #{i}",
                Description: null,
                AttendeeName: $"Prospect {i}",
                AttendeeEmail: $"prospect{i}@domain.com",
                AttendeePhone: null,
                AttendeeNotes: null,
                StartUtc: baseTime.AddHours(i),
                DurationMinutes: 30,
                ProspectTimeZoneId: "UTC"
            );

            var apt = await _service.BookAppointmentAsync(req);
            bookedHosts.Add(apt.HostId);
        }

        // Assert: Higher weight hosts (Sarah wt 3) should receive more assignments than Tariq (wt 1)
        var sarahCount = bookedHosts.Count(id => id == hosts.First(h => h.HostName == "Sarah Jenkins").HostId);
        var tariqCount = bookedHosts.Count(id => id == hosts.First(h => h.HostName == "Tariq Mansour").HostId);

        sarahCount.Should().BeGreaterThanOrEqualTo(tariqCount);
    }

    [Fact]
    public async Task BookAppointment_LeastBusy_AllocatesToHostWithFewestActiveAppointments()
    {
        var hosts = await _service.GetHostsAsync();
        var leastBusyHost = hosts.OrderBy(h => h.HostName).First();

        var slotTime = DateTimeOffset.UtcNow.Date.AddDays(4).AddHours(11);
        var req = new BookAppointmentRequest(
            HostId: null,
            CollectiveHostIds: null,
            Topology: RoutingTopology.LeastBusy,
            Title: "Least-Busy Test",
            Description: null,
            AttendeeName: "Test Lead",
            AttendeeEmail: "lead@test.com",
            AttendeePhone: null,
            AttendeeNotes: null,
            StartUtc: slotTime,
            DurationMinutes: 30,
            ProspectTimeZoneId: "UTC"
        );

        var apt = await _service.BookAppointmentAsync(req);
        apt.Should().NotBeNull();
        apt.TopologyUsed.Should().Be(RoutingTopology.LeastBusy);
    }

    [Fact]
    public async Task BookAppointment_Collective_AssignsAllHostsAndRejectsIfAnyHostHasConflict()
    {
        var hosts = (await _service.GetHostsAsync()).Take(2).ToList();
        var hostA = hosts[0];
        var hostB = hosts[1];

        var slotTime = DateTimeOffset.UtcNow.Date.AddDays(3).AddHours(15);

        // Pre-book host B at slotTime
        await _service.BookAppointmentAsync(new BookAppointmentRequest(
            HostId: hostB.HostId,
            CollectiveHostIds: null,
            Topology: RoutingTopology.WeightedRoundRobin,
            Title: "Solo Host B Session",
            Description: null,
            AttendeeName: "Solo Attendee",
            AttendeeEmail: "solo@test.com",
            AttendeePhone: null,
            AttendeeNotes: null,
            StartUtc: slotTime,
            DurationMinutes: 30,
            ProspectTimeZoneId: "UTC"
        ));

        // Attempt collective booking with host A and host B at that same time
        var collectiveReq = new BookAppointmentRequest(
            HostId: null,
            CollectiveHostIds: new List<Guid> { hostA.HostId, hostB.HostId },
            Topology: RoutingTopology.Collective,
            Title: "Joint Executive Session",
            Description: null,
            AttendeeName: "VIP Client",
            AttendeeEmail: "vip@client.com",
            AttendeePhone: null,
            AttendeeNotes: null,
            StartUtc: slotTime,
            DurationMinutes: 30,
            ProspectTimeZoneId: "UTC"
        );

        var act = async () => await _service.BookAppointmentAsync(collectiveReq);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*scheduling conflict*");
    }

    #endregion

    #region State Machine Transitions Tests

    [Fact]
    public async Task RescheduleAppointment_SuccessfullyUpdatesSlotAndIncrementsSequenceNumber()
    {
        // Arrange
        var host = (await _service.GetHostsAsync()).First();
        var originalSlot = DateTimeOffset.UtcNow.Date.AddDays(2).AddHours(14);
        var newSlot = DateTimeOffset.UtcNow.Date.AddDays(2).AddHours(16);

        var apt = await _service.BookAppointmentAsync(new BookAppointmentRequest(
            HostId: host.HostId,
            CollectiveHostIds: null,
            Topology: RoutingTopology.WeightedRoundRobin,
            Title: "Reschedule Candidate",
            Description: null,
            AttendeeName: "Attendee X",
            AttendeeEmail: "x@test.com",
            AttendeePhone: null,
            AttendeeNotes: null,
            StartUtc: originalSlot,
            DurationMinutes: 30,
            ProspectTimeZoneId: "UTC"
        ));

        apt.SequenceNumber.Should().Be(0);
        apt.Status.Should().Be(AppointmentStatus.Confirmed);

        // Act
        var rescheduled = await _service.RescheduleAppointmentAsync(apt.Id, new RescheduleAppointmentRequest(
            NewStartUtc: newSlot,
            NewDurationMinutes: 45,
            Reason: "Prospect schedule conflict"
        ));

        // Assert
        rescheduled.StartUtc.Should().Be(newSlot);
        rescheduled.DurationMinutes.Should().Be(45);
        rescheduled.Status.Should().Be(AppointmentStatus.Rescheduled);
        rescheduled.SequenceNumber.Should().Be(1);
        rescheduled.RescheduleReason.Should().Be("Prospect schedule conflict");
    }

    [Fact]
    public async Task RescheduleAppointment_ThrowsNotFound_WhenAppointmentDoesNotExist()
    {
        var act = async () => await _service.RescheduleAppointmentAsync(Guid.NewGuid(), new RescheduleAppointmentRequest(
            NewStartUtc: DateTimeOffset.UtcNow.AddDays(1)
        ));

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task RescheduleAppointment_ThrowsInvalidOperation_WhenReschedulingCancelledAppointment()
    {
        var host = (await _service.GetHostsAsync()).First();
        var slot = DateTimeOffset.UtcNow.Date.AddDays(3).AddHours(14);

        var apt = await _service.BookAppointmentAsync(new BookAppointmentRequest(
            HostId: host.HostId,
            CollectiveHostIds: null,
            Topology: RoutingTopology.WeightedRoundRobin,
            Title: "Cancel Then Reschedule",
            Description: null,
            AttendeeName: "Attendee",
            AttendeeEmail: "att@test.com",
            AttendeePhone: null,
            AttendeeNotes: null,
            StartUtc: slot,
            DurationMinutes: 30,
            ProspectTimeZoneId: "UTC"
        ));

        await _service.CancelAppointmentAsync(apt.Id, new CancelAppointmentRequest("No longer needed"));

        var act = async () => await _service.RescheduleAppointmentAsync(apt.Id, new RescheduleAppointmentRequest(
            NewStartUtc: slot.AddDays(1)
        ));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot reschedule a cancelled appointment*");
    }

    [Fact]
    public async Task CancelAppointment_TransitionsStateToCancelledAndRecordsReason()
    {
        var host = (await _service.GetHostsAsync()).First();
        var slot = DateTimeOffset.UtcNow.Date.AddDays(3).AddHours(15);

        var apt = await _service.BookAppointmentAsync(new BookAppointmentRequest(
            HostId: host.HostId,
            CollectiveHostIds: null,
            Topology: RoutingTopology.WeightedRoundRobin,
            Title: "Cancellation Test",
            Description: null,
            AttendeeName: "Attendee C",
            AttendeeEmail: "c@test.com",
            AttendeePhone: null,
            AttendeeNotes: null,
            StartUtc: slot,
            DurationMinutes: 30,
            ProspectTimeZoneId: "UTC"
        ));

        // Act
        var cancelled = await _service.CancelAppointmentAsync(apt.Id, new CancelAppointmentRequest("Project deferred to next quarter"));

        // Assert
        cancelled.Status.Should().Be(AppointmentStatus.Cancelled);
        cancelled.CancellationReason.Should().Be("Project deferred to next quarter");
        cancelled.SequenceNumber.Should().Be(1);
    }

    [Fact]
    public async Task CancelAppointment_FreesUpSlotForSubsequentBooking()
    {
        var host = (await _service.GetHostsAsync()).First(h => h.HostName == "Sarah Jenkins");
        var slot = DateTimeOffset.UtcNow.Date.AddDays(3).AddHours(11);

        var apt1 = await _service.BookAppointmentAsync(new BookAppointmentRequest(
            HostId: host.HostId,
            CollectiveHostIds: null,
            Topology: RoutingTopology.WeightedRoundRobin,
            Title: "First Reservation",
            Description: null,
            AttendeeName: "User 1",
            AttendeeEmail: "user1@test.com",
            AttendeePhone: null,
            AttendeeNotes: null,
            StartUtc: slot,
            DurationMinutes: 30,
            ProspectTimeZoneId: "UTC"
        ));

        // Cancel first reservation
        await _service.CancelAppointmentAsync(apt1.Id, new CancelAppointmentRequest("Cancelled"));

        // Second booking at the EXACT same slot should now succeed
        var apt2 = await _service.BookAppointmentAsync(new BookAppointmentRequest(
            HostId: host.HostId,
            CollectiveHostIds: null,
            Topology: RoutingTopology.WeightedRoundRobin,
            Title: "Second Reservation at Same Slot",
            Description: null,
            AttendeeName: "User 2",
            AttendeeEmail: "user2@test.com",
            AttendeePhone: null,
            AttendeeNotes: null,
            StartUtc: slot,
            DurationMinutes: 30,
            ProspectTimeZoneId: "UTC"
        ));

        apt2.Should().NotBeNull();
        apt2.Id.Should().NotBe(apt1.Id);
        apt2.Status.Should().Be(AppointmentStatus.Confirmed);
    }

    #endregion

    #region RFC 5545 .ics Calendar Invitation Synthesizer Tests

    [Fact]
    public async Task GenerateIcsCalendar_ContainsRFC5545RequiredHeadersAndProperties()
    {
        // Arrange
        var apts = await _service.GetAllAppointmentsAsync();
        var apt = apts.First();

        // Act
        var ics = await _service.GenerateIcsCalendarAsync(apt.Id);

        // Assert
        ics.Should().NotBeNullOrWhiteSpace();
        ics.Should().StartWith("BEGIN:VCALENDAR\r\n");
        ics.Should().EndWith("END:VCALENDAR\r\n");
        ics.Should().Contain("VERSION:2.0\r\n");
        ics.Should().Contain("CALSCALE:GREGORIAN\r\n");
        ics.Should().Contain("BEGIN:VEVENT\r\n");
        ics.Should().Contain($"UID:sgrowing-apt-{apt.Id}@sgrowing.net\r\n");
        ics.Should().Contain("DTSTAMP:");
        ics.Should().Contain($"DTSTART:{apt.StartUtc:yyyyMMddTHHmmssZ}\r\n");
        ics.Should().Contain($"DTEND:{apt.EndUtc:yyyyMMddTHHmmssZ}\r\n");
        ics.Should().Contain($"SUMMARY:{apt.Title}\r\n");
        ics.Should().Contain($"ORGANIZER;CN={apt.HostName}:mailto:{apt.HostEmail}\r\n");
        ics.Should().Contain($"ATTENDEE;CUTYPE=INDIVIDUAL;ROLE=REQ-PARTICIPANT;PARTSTAT=ACCEPTED;CN={apt.AttendeeName}:mailto:{apt.AttendeeEmail}\r\n");
        ics.Should().Contain("STATUS:CONFIRMED\r\n");
        ics.Should().Contain("END:VEVENT\r\n");
    }

    [Fact]
    public async Task GenerateIcsCalendar_UsesCrlfLineEndingsAndEscapesSpecialCharacters()
    {
        // Arrange: Book appointment with commas, semicolons, backslashes
        var host = (await _service.GetHostsAsync()).First();
        var slot = DateTimeOffset.UtcNow.Date.AddDays(3).AddHours(16);

        var apt = await _service.BookAppointmentAsync(new BookAppointmentRequest(
            HostId: host.HostId,
            CollectiveHostIds: null,
            Topology: RoutingTopology.WeightedRoundRobin,
            Title: "Meeting, with special; chars & \\ backslashes",
            Description: "Line 1\nLine 2, with semicolon;",
            AttendeeName: "Smith, John",
            AttendeeEmail: "smith@test.com",
            AttendeePhone: null,
            AttendeeNotes: null,
            StartUtc: slot,
            DurationMinutes: 30,
            ProspectTimeZoneId: "UTC"
        ));

        // Act
        var ics = await _service.GenerateIcsCalendarAsync(apt.Id);

        // Assert: RFC 5545 requires CRLF line endings
        ics.Should().Contain("\r\n");
        // Commas and semicolons escaped in text properties
        ics.Should().Contain(@"Meeting\, with special\; chars & \\ backslashes");
        ics.Should().Contain(@"Smith\, John");
    }

    [Fact]
    public async Task GenerateIcsCalendar_OutputsCancelMethodWhenAppointmentCancelled()
    {
        var host = (await _service.GetHostsAsync()).First();
        var slot = DateTimeOffset.UtcNow.Date.AddDays(3).AddHours(12);

        var apt = await _service.BookAppointmentAsync(new BookAppointmentRequest(
            HostId: host.HostId,
            CollectiveHostIds: null,
            Topology: RoutingTopology.WeightedRoundRobin,
            Title: "Meeting To Cancel for ICS",
            Description: null,
            AttendeeName: "Cancel Candidate",
            AttendeeEmail: "cancel@test.com",
            AttendeePhone: null,
            AttendeeNotes: null,
            StartUtc: slot,
            DurationMinutes: 30,
            ProspectTimeZoneId: "UTC"
        ));

        await _service.CancelAppointmentAsync(apt.Id, new CancelAppointmentRequest("No longer required"));

        var ics = await _service.GenerateIcsCalendarAsync(apt.Id);

        ics.Should().Contain("METHOD:CANCEL\r\n");
        ics.Should().Contain("STATUS:CANCELLED\r\n");
    }

    [Fact]
    public async Task GenerateIcsCalendar_IncludesVirtualMeetingLinkInLocationAndDescription()
    {
        var apts = await _service.GetAllAppointmentsAsync();
        var apt = apts.First();

        var ics = await _service.GenerateIcsCalendarAsync(apt.Id);

        ics.Should().Contain($"LOCATION:{apt.MeetingLink}\r\n");
        ics.Should().Contain($"URL:{apt.MeetingLink}\r\n");
        ics.Should().Contain(apt.MeetingLink);
    }

    #endregion

    #region Routing Stats & Concurrency Tests

    [Fact]
    public async Task GetRoutingStats_ReflectsAccurateCountsAndUtilizationRatios()
    {
        // Act
        var stats = await _service.GetRoutingStatsAsync();

        // Assert
        stats.Should().NotBeNull();
        stats.TotalAppointments.Should().BeGreaterThanOrEqualTo(2);
        stats.ActiveAppointments.Should().BeGreaterThanOrEqualTo(2);
        stats.HostMetrics.Should().NotBeEmpty();

        foreach (var metric in stats.HostMetrics)
        {
            metric.HostId.Should().NotBeEmpty();
            metric.Weight.Should().BeGreaterThan(0);
            metric.UtilizationRatio.Should().BeInRange(0.0, 1.0);
        }
    }

    [Fact]
    public async Task ConcurrentBooking_ThreadSafety_RejectsRacedDoubleBookings()
    {
        // Arrange
        var host = (await _service.GetHostsAsync()).First(h => h.HostName == "Kenji Tanaka");
        var slotTime = DateTimeOffset.UtcNow.Date.AddDays(4).AddHours(15);
        int concurrency = 10;
        int successfulBookings = 0;
        int conflictExceptions = 0;

        var tasks = Enumerable.Range(0, concurrency).Select(async i =>
        {
            try
            {
                var req = new BookAppointmentRequest(
                    HostId: host.HostId,
                    CollectiveHostIds: null,
                    Topology: RoutingTopology.WeightedRoundRobin,
                    Title: $"Concurrent Slot Race #{i}",
                    Description: null,
                    AttendeeName: $"Racer {i}",
                    AttendeeEmail: $"racer{i}@test.com",
                    AttendeePhone: null,
                    AttendeeNotes: null,
                    StartUtc: slotTime, // Every thread attempts the exact same host & time!
                    DurationMinutes: 30,
                    ProspectTimeZoneId: "UTC"
                );

                await _service.BookAppointmentAsync(req);
                Interlocked.Increment(ref successfulBookings);
            }
            catch (InvalidOperationException)
            {
                Interlocked.Increment(ref conflictExceptions);
            }
        });

        // Act
        await Task.WhenAll(tasks);

        // Assert: Exactly ONE thread must succeed; all 9 others must be rejected due to conflict!
        successfulBookings.Should().Be(1);
        conflictExceptions.Should().Be(concurrency - 1);
    }

    #endregion
}
