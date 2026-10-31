using System.Collections.Concurrent;
using System.Text;

namespace SGrowing.Api.Modules.Scheduling;

#region Domain Enums & Models

public enum AppointmentStatus
{
    Proposed,
    Confirmed,
    Rescheduled,
    Completed,
    Cancelled,
    NoShow
}

public enum RoutingTopology
{
    WeightedRoundRobin,
    LeastBusy,
    Collective
}

public record WeeklyWorkingHours(
    DayOfWeek DayOfWeek,
    TimeSpan StartTime,
    TimeSpan EndTime
);

public record BusyInterval(
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    string Reason
);

public class HostSchedule
{
    public Guid HostId { get; set; } = Guid.NewGuid();
    public string HostName { get; set; } = string.Empty;
    public string HostEmail { get; set; } = string.Empty;
    public string TimeZoneId { get; set; } = "UTC";
    public int Weight { get; set; } = 1;
    public bool IsActive { get; set; } = true;
    public TimeSpan BufferTime { get; set; } = TimeSpan.FromMinutes(15);
    public TimeSpan MinimumNotice { get; set; } = TimeSpan.FromHours(2);
    public List<WeeklyWorkingHours> WorkingHours { get; set; } = new();
    public List<BusyInterval> ExternalBusyIntervals { get; set; } = new();
    public List<string> Specialties { get; set; } = new();

    // Routing stats tracking
    public int WeightedRoundRobinAssignments { get; set; }
    public DateTimeOffset? LastAssignedAtUtc { get; set; }
}

public class Appointment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid HostId { get; set; }
    public string HostName { get; set; } = string.Empty;
    public string HostEmail { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string AttendeeName { get; set; } = string.Empty;
    public string AttendeeEmail { get; set; } = string.Empty;
    public string? AttendeePhone { get; set; }
    public string? AttendeeNotes { get; set; }
    public DateTimeOffset StartUtc { get; set; }
    public DateTimeOffset EndUtc { get; set; }
    public int DurationMinutes { get; set; }
    public string ProspectTimeZoneId { get; set; } = "UTC";
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Confirmed;
    public string MeetingLink { get; set; } = string.Empty;
    public string? RescheduleReason { get; set; }
    public string? CancellationReason { get; set; }
    public int SequenceNumber { get; set; }
    public RoutingTopology TopologyUsed { get; set; } = RoutingTopology.WeightedRoundRobin;
    public List<Guid>? CollectiveHostIds { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public record TimeSlot(
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    string StartLocalFormatted,
    string EndLocalFormatted,
    string TimeZoneId,
    Guid? HostId,
    string? HostName,
    List<Guid>? AvailableHostIds = null
);

public record SlotQuery(
    Guid? HostId = null,
    List<Guid>? CollectiveHostIds = null,
    DateTimeOffset? RangeStartUtc = null,
    DateTimeOffset? RangeEndUtc = null,
    int DurationMinutes = 30,
    string TargetTimeZoneId = "UTC",
    RoutingTopology Topology = RoutingTopology.WeightedRoundRobin
);

public record BookAppointmentRequest(
    Guid? HostId,
    List<Guid>? CollectiveHostIds,
    RoutingTopology Topology,
    string Title,
    string? Description,
    string AttendeeName,
    string AttendeeEmail,
    string? AttendeePhone,
    string? AttendeeNotes,
    DateTimeOffset StartUtc,
    int DurationMinutes,
    string ProspectTimeZoneId
);

public record RescheduleAppointmentRequest(
    DateTimeOffset NewStartUtc,
    int? NewDurationMinutes = null,
    string? Reason = null
);

public record CancelAppointmentRequest(
    string? Reason = null
);

public record HostRoutingMetric(
    Guid HostId,
    string HostName,
    string HostEmail,
    string TimeZoneId,
    int Weight,
    bool IsActive,
    int ConfirmedBookingsCount,
    int TotalAssignedCount,
    DateTimeOffset? LastAssignedAtUtc,
    double UtilizationRatio
);

public record RoutingStatsResult(
    int TotalAppointments,
    int ActiveAppointments,
    int CancelledAppointments,
    Dictionary<RoutingTopology, int> TopologyBookingsCount,
    List<HostRoutingMetric> HostMetrics
);

#endregion

public interface IAppointmentSchedulingService
{
    Task<IReadOnlyList<HostSchedule>> GetHostsAsync(CancellationToken cancellationToken = default);
    Task<HostSchedule?> GetHostByIdAsync(Guid hostId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TimeSlot>> GetAvailableSlotsAsync(SlotQuery query, CancellationToken cancellationToken = default);
    Task<Appointment> BookAppointmentAsync(BookAppointmentRequest request, CancellationToken cancellationToken = default);
    Task<Appointment> RescheduleAppointmentAsync(Guid appointmentId, RescheduleAppointmentRequest request, CancellationToken cancellationToken = default);
    Task<Appointment> CancelAppointmentAsync(Guid appointmentId, CancelAppointmentRequest request, CancellationToken cancellationToken = default);
    Task<Appointment?> GetAppointmentByIdAsync(Guid appointmentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Appointment>> GetAllAppointmentsAsync(CancellationToken cancellationToken = default);
    Task<string> GenerateIcsCalendarAsync(Guid appointmentId, CancellationToken cancellationToken = default);
    Task<RoutingStatsResult> GetRoutingStatsAsync(CancellationToken cancellationToken = default);
}

public class AppointmentSchedulingService : IAppointmentSchedulingService
{
    private readonly ConcurrentDictionary<Guid, HostSchedule> _hosts = new();
    private readonly ConcurrentDictionary<Guid, Appointment> _appointments = new();
    private readonly object _syncLock = new();

    public AppointmentSchedulingService()
    {
        SeedDemoData();
    }

    #region Host Management

    public Task<IReadOnlyList<HostSchedule>> GetHostsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<HostSchedule>>(_hosts.Values.OrderBy(h => h.HostName).ToList());
    }

    public Task<HostSchedule?> GetHostByIdAsync(Guid hostId, CancellationToken cancellationToken = default)
    {
        _hosts.TryGetValue(hostId, out var host);
        return Task.FromResult(host);
    }

    #endregion

    #region Temporal Slot Calculation

    public Task<IReadOnlyList<TimeSlot>> GetAvailableSlotsAsync(SlotQuery query, CancellationToken cancellationToken = default)
    {
        var targetTz = ResolveTimeZone(query.TargetTimeZoneId);
        var nowUtc = DateTimeOffset.UtcNow;
        var rangeStart = query.RangeStartUtc ?? nowUtc;
        var rangeEnd = query.RangeEndUtc ?? rangeStart.AddDays(7);
        var duration = TimeSpan.FromMinutes(query.DurationMinutes <= 0 ? 30 : query.DurationMinutes);

        if (rangeEnd <= rangeStart)
        {
            throw new ArgumentException("RangeEndUtc must be after RangeStartUtc.");
        }

        // Clamp minimum range start to nowUtc
        if (rangeStart < nowUtc)
        {
            rangeStart = nowUtc;
        }

        var results = new List<TimeSlot>();

        // Scenario 1: Collective Availability (Multiple hosts all available simultaneously)
        if (query.CollectiveHostIds != null && query.CollectiveHostIds.Count > 1)
        {
            var requestedHosts = query.CollectiveHostIds
                .Select(id => _hosts.TryGetValue(id, out var h) ? h : null)
                .Where(h => h != null && h.IsActive)
                .Cast<HostSchedule>()
                .ToList();

            if (requestedHosts.Count == query.CollectiveHostIds.Count)
            {
                results = CalculateCollectiveSlots(requestedHosts, rangeStart, rangeEnd, duration, targetTz, nowUtc);
            }
            return Task.FromResult<IReadOnlyList<TimeSlot>>(results);
        }

        // Scenario 2: Single Specific Host
        if (query.HostId.HasValue)
        {
            if (_hosts.TryGetValue(query.HostId.Value, out var host) && host.IsActive)
            {
                var slots = CalculateHostSlots(host, rangeStart, rangeEnd, duration, targetTz, nowUtc);
                results.AddRange(slots);
            }
            return Task.FromResult<IReadOnlyList<TimeSlot>>(results);
        }

        // Scenario 3: All active hosts or routing across team
        var activeHosts = _hosts.Values.Where(h => h.IsActive).ToList();
        var candidateSlotMap = new Dictionary<DateTimeOffset, List<Guid>>();

        foreach (var host in activeHosts)
        {
            var hostSlots = CalculateHostSlots(host, rangeStart, rangeEnd, duration, targetTz, nowUtc);
            foreach (var slot in hostSlots)
            {
                if (!candidateSlotMap.TryGetValue(slot.StartUtc, out var hostList))
                {
                    hostList = new List<Guid>();
                    candidateSlotMap[slot.StartUtc] = hostList;
                }
                hostList.Add(host.HostId);
            }
        }

        // Deduplicate and project slots for the team
        foreach (var kvp in candidateSlotMap.OrderBy(k => k.Key))
        {
            var startUtc = kvp.Key;
            var endUtc = startUtc + duration;
            var availableHostIds = kvp.Value;

            var localStart = TimeZoneInfo.ConvertTime(startUtc, targetTz);
            var localEnd = TimeZoneInfo.ConvertTime(endUtc, targetTz);

            // Select an initial suggested host based on topology
            Guid? chosenHostId = null;
            string? chosenHostName = null;

            if (availableHostIds.Count > 0)
            {
                var candidateHosts = availableHostIds
                    .Select(id => _hosts.TryGetValue(id, out var h) ? h : null)
                    .Where(h => h != null)
                    .Cast<HostSchedule>()
                    .ToList();

                var selected = query.Topology == RoutingTopology.LeastBusy
                    ? SelectLeastBusyHost(candidateHosts)
                    : SelectWeightedRoundRobinHost(candidateHosts, dryRun: true);

                if (selected != null)
                {
                    chosenHostId = selected.HostId;
                    chosenHostName = selected.HostName;
                }
            }

            results.Add(new TimeSlot(
                StartUtc: startUtc,
                EndUtc: endUtc,
                StartLocalFormatted: localStart.ToString("yyyy-MM-dd HH:mm"),
                EndLocalFormatted: localEnd.ToString("yyyy-MM-dd HH:mm"),
                TimeZoneId: targetTz.Id,
                HostId: chosenHostId,
                HostName: chosenHostName,
                AvailableHostIds: availableHostIds
            ));
        }

        return Task.FromResult<IReadOnlyList<TimeSlot>>(results);
    }

    private List<TimeSlot> CalculateHostSlots(
        HostSchedule host,
        DateTimeOffset rangeStartUtc,
        DateTimeOffset rangeEndUtc,
        TimeSpan duration,
        TimeZoneInfo targetTz,
        DateTimeOffset nowUtc)
    {
        var slots = new List<TimeSlot>();
        var hostTz = ResolveTimeZone(host.TimeZoneId);
        var minNoticeStartUtc = nowUtc + host.MinimumNotice;

        // Fetch host's busy periods: active appointments + external intervals
        var busyIntervals = GetHostBusyIntervals(host.HostId, host.BufferTime);

        // Convert range to Host's local time to accurately iterate through calendar days
        var hostRangeStart = TimeZoneInfo.ConvertTime(rangeStartUtc, hostTz);
        var hostRangeEnd = TimeZoneInfo.ConvertTime(rangeEndUtc, hostTz);

        var currentDay = hostRangeStart.Date;
        var endDay = hostRangeEnd.Date;

        while (currentDay <= endDay)
        {
            var dayOfWeek = currentDay.DayOfWeek;
            var scheduleForDay = host.WorkingHours.Where(w => w.DayOfWeek == dayOfWeek).ToList();

            foreach (var wh in scheduleForDay)
            {
                // Construct local daily window with host's timezone
                var localWindowStart = currentDay.Add(wh.StartTime);
                var localWindowEnd = currentDay.Add(wh.EndTime);

                // Convert local window to UTC with DST safety
                var windowStartUtc = ConvertToUtc(localWindowStart, hostTz);
                var windowEndUtc = ConvertToUtc(localWindowEnd, hostTz);

                // Step in 15-minute grid alignment
                var step = TimeSpan.FromMinutes(15);
                var candidateStart = windowStartUtc;

                while (candidateStart + duration <= windowEndUtc)
                {
                    var candidateEnd = candidateStart + duration;

                    // Slot must fall within requested range and respect minimum notice
                    if (candidateStart >= rangeStartUtc && candidateEnd <= rangeEndUtc && candidateStart >= minNoticeStartUtc)
                    {
                        // Check for collision with any busy interval (including buffer)
                        bool isBusy = busyIntervals.Any(b => candidateStart < b.EndUtc && candidateEnd > b.StartUtc);

                        if (!isBusy)
                        {
                            var targetStartLocal = TimeZoneInfo.ConvertTime(candidateStart, targetTz);
                            var targetEndLocal = TimeZoneInfo.ConvertTime(candidateEnd, targetTz);

                            slots.Add(new TimeSlot(
                                StartUtc: candidateStart,
                                EndUtc: candidateEnd,
                                StartLocalFormatted: targetStartLocal.ToString("yyyy-MM-dd HH:mm"),
                                EndLocalFormatted: targetEndLocal.ToString("yyyy-MM-dd HH:mm"),
                                TimeZoneId: targetTz.Id,
                                HostId: host.HostId,
                                HostName: host.HostName,
                                AvailableHostIds: new List<Guid> { host.HostId }
                            ));
                        }
                    }

                    candidateStart = candidateStart.Add(step);
                }
            }

            currentDay = currentDay.AddDays(1);
        }

        return slots;
    }

    private List<TimeSlot> CalculateCollectiveSlots(
        List<HostSchedule> hosts,
        DateTimeOffset rangeStartUtc,
        DateTimeOffset rangeEndUtc,
        TimeSpan duration,
        TimeZoneInfo targetTz,
        DateTimeOffset nowUtc)
    {
        // Calculate slots for each host individually
        var hostSlots = hosts
            .Select(h => CalculateHostSlots(h, rangeStartUtc, rangeEndUtc, duration, targetTz, nowUtc))
            .ToList();

        if (hostSlots.Count == 0) return new List<TimeSlot>();

        // Find intersection: slots starting at the exact same StartUtc that are present for ALL hosts
        var commonStartTimes = hostSlots[0]
            .Select(s => s.StartUtc)
            .ToHashSet();

        for (int i = 1; i < hostSlots.Count; i++)
        {
            var nextStarts = hostSlots[i].Select(s => s.StartUtc).ToHashSet();
            commonStartTimes.IntersectWith(nextStarts);
        }

        var collectiveHostIds = hosts.Select(h => h.HostId).ToList();
        var result = new List<TimeSlot>();

        foreach (var startUtc in commonStartTimes.OrderBy(t => t))
        {
            var endUtc = startUtc + duration;
            var localStart = TimeZoneInfo.ConvertTime(startUtc, targetTz);
            var localEnd = TimeZoneInfo.ConvertTime(endUtc, targetTz);

            result.Add(new TimeSlot(
                StartUtc: startUtc,
                EndUtc: endUtc,
                StartLocalFormatted: localStart.ToString("yyyy-MM-dd HH:mm"),
                EndLocalFormatted: localEnd.ToString("yyyy-MM-dd HH:mm"),
                TimeZoneId: targetTz.Id,
                HostId: null,
                HostName: string.Join(", ", hosts.Select(h => h.HostName)),
                AvailableHostIds: collectiveHostIds
            ));
        }

        return result;
    }

    private List<BusyInterval> GetHostBusyIntervals(Guid hostId, TimeSpan buffer)
    {
        var intervals = new List<BusyInterval>();

        // 1. External busy intervals
        if (_hosts.TryGetValue(hostId, out var host))
        {
            foreach (var ext in host.ExternalBusyIntervals)
            {
                intervals.Add(new BusyInterval(
                    ext.StartUtc - buffer,
                    ext.EndUtc + buffer,
                    $"External Busy: {ext.Reason}"
                ));
            }
        }

        // 2. Existing active appointments
        var activeAppointments = _appointments.Values
            .Where(a => (a.HostId == hostId || (a.CollectiveHostIds != null && a.CollectiveHostIds.Contains(hostId)))
                        && (a.Status == AppointmentStatus.Proposed ||
                            a.Status == AppointmentStatus.Confirmed ||
                            a.Status == AppointmentStatus.Rescheduled))
            .ToList();

        foreach (var apt in activeAppointments)
        {
            intervals.Add(new BusyInterval(
                apt.StartUtc - buffer,
                apt.EndUtc + buffer,
                $"Appointment: {apt.Title}"
            ));
        }

        return intervals;
    }

    #endregion

    #region Team Routing Topologies

    private HostSchedule? SelectWeightedRoundRobinHost(List<HostSchedule> candidates, bool dryRun = false)
    {
        if (candidates.Count == 0) return null;
        if (candidates.Count == 1)
        {
            var only = candidates[0];
            if (!dryRun)
            {
                only.WeightedRoundRobinAssignments++;
                only.LastAssignedAtUtc = DateTimeOffset.UtcNow;
            }
            return only;
        }

        // Weighted Round-Robin calculation:
        // Score = assignments / weight
        // The host with the lowest normalized assignment ratio is picked next.
        // In case of a tie, the one assigned least recently (or never) wins.
        var selected = candidates
            .OrderBy(c => (double)c.WeightedRoundRobinAssignments / Math.Max(1, c.Weight))
            .ThenBy(c => c.LastAssignedAtUtc ?? DateTimeOffset.MinValue)
            .First();

        if (!dryRun)
        {
            selected.WeightedRoundRobinAssignments++;
            selected.LastAssignedAtUtc = DateTimeOffset.UtcNow;
        }

        return selected;
    }

    private HostSchedule? SelectLeastBusyHost(List<HostSchedule> candidates)
    {
        if (candidates.Count == 0) return null;

        // Count confirmed/active appointments per candidate host
        var hostBookingsCount = candidates.ToDictionary(
            c => c.HostId,
            c => _appointments.Values.Count(a => a.HostId == c.HostId &&
                                                  (a.Status == AppointmentStatus.Confirmed ||
                                                   a.Status == AppointmentStatus.Rescheduled ||
                                                   a.Status == AppointmentStatus.Proposed))
        );

        return candidates
            .OrderBy(c => hostBookingsCount[c.HostId])
            .ThenBy(c => c.LastAssignedAtUtc ?? DateTimeOffset.MinValue)
            .First();
    }

    #endregion

    #region Booking Lifecycle State Machine

    public Task<Appointment> BookAppointmentAsync(BookAppointmentRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.AttendeeName))
            throw new ArgumentException("AttendeeName is required.", nameof(request.AttendeeName));
        if (string.IsNullOrWhiteSpace(request.AttendeeEmail))
            throw new ArgumentException("AttendeeEmail is required.", nameof(request.AttendeeEmail));
        if (request.DurationMinutes <= 0)
            throw new ArgumentException("DurationMinutes must be greater than zero.", nameof(request.DurationMinutes));

        var duration = TimeSpan.FromMinutes(request.DurationMinutes);
        var endUtc = request.StartUtc + duration;
        var nowUtc = DateTimeOffset.UtcNow;

        lock (_syncLock)
        {
            HostSchedule? host = null;
            List<Guid>? collectiveIds = null;

            if (request.Topology == RoutingTopology.Collective && request.CollectiveHostIds != null && request.CollectiveHostIds.Count > 1)
            {
                // Verify all collective hosts exist and are free
                var collectiveHosts = request.CollectiveHostIds
                    .Select(id => _hosts.TryGetValue(id, out var h) ? h : null)
                    .Where(h => h != null && h.IsActive)
                    .Cast<HostSchedule>()
                    .ToList();

                if (collectiveHosts.Count != request.CollectiveHostIds.Count)
                {
                    throw new InvalidOperationException("One or more collective hosts not found or inactive.");
                }

                foreach (var h in collectiveHosts)
                {
                    if (HasConflict(h.HostId, request.StartUtc, endUtc, h.BufferTime, null))
                    {
                        throw new InvalidOperationException($"Collective host '{h.HostName}' has a scheduling conflict during the requested time.");
                    }
                }

                host = collectiveHosts[0]; // Lead host
                collectiveIds = collectiveHosts.Select(h => h.HostId).ToList();
            }
            else if (request.HostId.HasValue)
            {
                if (!_hosts.TryGetValue(request.HostId.Value, out host) || !host.IsActive)
                {
                    throw new InvalidOperationException($"Host '{request.HostId}' is not found or inactive.");
                }

                if (HasConflict(host.HostId, request.StartUtc, endUtc, host.BufferTime, null))
                {
                    throw new InvalidOperationException($"Selected host '{host.HostName}' has a conflict or buffer collision for this slot.");
                }
            }
            else
            {
                // Route dynamically across active hosts
                var availableCandidates = _hosts.Values
                    .Where(h => h.IsActive && !HasConflict(h.HostId, request.StartUtc, endUtc, h.BufferTime, null))
                    .ToList();

                if (availableCandidates.Count == 0)
                {
                    throw new InvalidOperationException("No available team hosts found for the requested time slot.");
                }

                host = request.Topology switch
                {
                    RoutingTopology.LeastBusy => SelectLeastBusyHost(availableCandidates),
                    _ => SelectWeightedRoundRobinHost(availableCandidates, dryRun: false)
                };

                if (host == null)
                {
                    throw new InvalidOperationException("Routing engine could not select an appropriate host.");
                }
            }

            var appointmentId = Guid.NewGuid();
            var appointment = new Appointment
            {
                Id = appointmentId,
                HostId = host.HostId,
                HostName = host.HostName,
                HostEmail = host.HostEmail,
                Title = string.IsNullOrWhiteSpace(request.Title) ? $"Strategic Session with {host.HostName}" : request.Title,
                Description = request.Description,
                AttendeeName = request.AttendeeName,
                AttendeeEmail = request.AttendeeEmail,
                AttendeePhone = request.AttendeePhone,
                AttendeeNotes = request.AttendeeNotes,
                StartUtc = request.StartUtc,
                EndUtc = endUtc,
                DurationMinutes = request.DurationMinutes,
                ProspectTimeZoneId = string.IsNullOrWhiteSpace(request.ProspectTimeZoneId) ? "UTC" : request.ProspectTimeZoneId,
                Status = AppointmentStatus.Confirmed,
                MeetingLink = $"https://meet.sgrowing.net/room-{appointmentId.ToString()[..8]}",
                SequenceNumber = 0,
                TopologyUsed = request.Topology,
                CollectiveHostIds = collectiveIds,
                CreatedAtUtc = nowUtc,
                UpdatedAtUtc = nowUtc
            };

            _appointments[appointment.Id] = appointment;
            return Task.FromResult(appointment);
        }
    }

    public Task<Appointment> RescheduleAppointmentAsync(Guid appointmentId, RescheduleAppointmentRequest request, CancellationToken cancellationToken = default)
    {
        lock (_syncLock)
        {
            if (!_appointments.TryGetValue(appointmentId, out var appointment))
            {
                throw new KeyNotFoundException($"Appointment '{appointmentId}' not found.");
            }

            if (appointment.Status == AppointmentStatus.Cancelled)
            {
                throw new InvalidOperationException("Cannot reschedule a cancelled appointment.");
            }

            if (appointment.Status == AppointmentStatus.Completed)
            {
                throw new InvalidOperationException("Cannot reschedule a completed appointment.");
            }

            int durationMinutes = request.NewDurationMinutes ?? appointment.DurationMinutes;
            var duration = TimeSpan.FromMinutes(durationMinutes);
            var newEndUtc = request.NewStartUtc + duration;

            if (!_hosts.TryGetValue(appointment.HostId, out var host))
            {
                throw new InvalidOperationException($"Host '{appointment.HostId}' not found.");
            }

            // Check conflicts ignoring this appointment's current slot
            if (HasConflict(host.HostId, request.NewStartUtc, newEndUtc, host.BufferTime, appointmentId))
            {
                throw new InvalidOperationException($"Host '{host.HostName}' has a conflict during the requested rescheduled time slot.");
            }

            if (appointment.CollectiveHostIds != null && appointment.CollectiveHostIds.Count > 1)
            {
                foreach (var cid in appointment.CollectiveHostIds)
                {
                    if (_hosts.TryGetValue(cid, out var ch) && HasConflict(ch.HostId, request.NewStartUtc, newEndUtc, ch.BufferTime, appointmentId))
                    {
                        throw new InvalidOperationException($"Collective host '{ch.HostName}' has a conflict during the rescheduled slot.");
                    }
                }
            }

            appointment.StartUtc = request.NewStartUtc;
            appointment.EndUtc = newEndUtc;
            appointment.DurationMinutes = durationMinutes;
            appointment.Status = AppointmentStatus.Rescheduled;
            appointment.RescheduleReason = request.Reason ?? "Attendee requested reschedule";
            appointment.SequenceNumber++;
            appointment.UpdatedAtUtc = DateTimeOffset.UtcNow;

            return Task.FromResult(appointment);
        }
    }

    public Task<Appointment> CancelAppointmentAsync(Guid appointmentId, CancelAppointmentRequest request, CancellationToken cancellationToken = default)
    {
        lock (_syncLock)
        {
            if (!_appointments.TryGetValue(appointmentId, out var appointment))
            {
                throw new KeyNotFoundException($"Appointment '{appointmentId}' not found.");
            }

            if (appointment.Status == AppointmentStatus.Cancelled)
            {
                return Task.FromResult(appointment);
            }

            appointment.Status = AppointmentStatus.Cancelled;
            appointment.CancellationReason = request.Reason ?? "Cancelled by attendee or host";
            appointment.SequenceNumber++;
            appointment.UpdatedAtUtc = DateTimeOffset.UtcNow;

            return Task.FromResult(appointment);
        }
    }

    public Task<Appointment?> GetAppointmentByIdAsync(Guid appointmentId, CancellationToken cancellationToken = default)
    {
        _appointments.TryGetValue(appointmentId, out var appointment);
        return Task.FromResult(appointment);
    }

    public Task<IReadOnlyList<Appointment>> GetAllAppointmentsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<Appointment>>(
            _appointments.Values.OrderByDescending(a => a.StartUtc).ToList()
        );
    }

    private bool HasConflict(Guid hostId, DateTimeOffset startUtc, DateTimeOffset endUtc, TimeSpan buffer, Guid? excludeAppointmentId)
    {
        // 1. External busy intervals
        if (_hosts.TryGetValue(hostId, out var host))
        {
            foreach (var ext in host.ExternalBusyIntervals)
            {
                var busyStart = ext.StartUtc - buffer;
                var busyEnd = ext.EndUtc + buffer;
                if (startUtc < busyEnd && endUtc > busyStart)
                {
                    return true;
                }
            }
        }

        // 2. Active appointments
        foreach (var apt in _appointments.Values)
        {
            if (apt.Id == excludeAppointmentId) continue;

            if (apt.Status == AppointmentStatus.Cancelled ||
                apt.Status == AppointmentStatus.NoShow)
            {
                continue;
            }

            bool matchesHost = apt.HostId == hostId || (apt.CollectiveHostIds != null && apt.CollectiveHostIds.Contains(hostId));
            if (!matchesHost) continue;

            var aptBusyStart = apt.StartUtc - buffer;
            var aptBusyEnd = apt.EndUtc + buffer;

            if (startUtc < aptBusyEnd && endUtc > aptBusyStart)
            {
                return true;
            }
        }

        return false;
    }

    #endregion

    #region RFC 5545 .ics Calendar Invitation Synthesizer

    public Task<string> GenerateIcsCalendarAsync(Guid appointmentId, CancellationToken cancellationToken = default)
    {
        if (!_appointments.TryGetValue(appointmentId, out var apt))
        {
            throw new KeyNotFoundException($"Appointment '{appointmentId}' not found.");
        }

        var sb = new StringBuilder();
        var dtStamp = DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmssZ");
        var dtStart = apt.StartUtc.ToString("yyyyMMddTHHmmssZ");
        var dtEnd = apt.EndUtc.ToString("yyyyMMddTHHmmssZ");

        var statusString = apt.Status switch
        {
            AppointmentStatus.Cancelled => "CANCELLED",
            _ => "CONFIRMED"
        };

        var method = apt.Status == AppointmentStatus.Cancelled ? "CANCEL" : "REQUEST";

        // RFC 5545 requires CRLF line endings
        sb.Append("BEGIN:VCALENDAR\r\n");
        sb.Append("PRODID:-//SGrowing//Temporal Scheduling Engine v1.0//EN\r\n");
        sb.Append("VERSION:2.0\r\n");
        sb.Append("CALSCALE:GREGORIAN\r\n");
        sb.Append($"METHOD:{method}\r\n");
        sb.Append("BEGIN:VEVENT\r\n");
        sb.Append($"UID:sgrowing-apt-{apt.Id}@sgrowing.net\r\n");
        sb.Append($"DTSTAMP:{dtStamp}\r\n");
        sb.Append($"DTSTART:{dtStart}\r\n");
        sb.Append($"DTEND:{dtEnd}\r\n");
        sb.Append($"SUMMARY:{EscapeIcsText(apt.Title)}\r\n");

        var desc = $"{apt.Description ?? "Strategic Consultation"}\\n\\nJoin Video Room: {apt.MeetingLink}\\nStatus: {apt.Status}";
        if (!string.IsNullOrEmpty(apt.RescheduleReason))
        {
            desc += $"\\nReschedule Note: {apt.RescheduleReason}";
        }
        sb.Append($"DESCRIPTION:{EscapeIcsText(desc)}\r\n");

        sb.Append($"LOCATION:{EscapeIcsText(apt.MeetingLink)}\r\n");
        sb.Append($"URL:{apt.MeetingLink}\r\n");
        sb.Append($"ORGANIZER;CN={EscapeIcsText(apt.HostName)}:mailto:{apt.HostEmail}\r\n");
        sb.Append($"ATTENDEE;CUTYPE=INDIVIDUAL;ROLE=REQ-PARTICIPANT;PARTSTAT=ACCEPTED;CN={EscapeIcsText(apt.AttendeeName)}:mailto:{apt.AttendeeEmail}\r\n");
        sb.Append($"STATUS:{statusString}\r\n");
        sb.Append($"SEQUENCE:{apt.SequenceNumber}\r\n");
        sb.Append("TRANSP:OPAQUE\r\n");
        sb.Append("CLASS:PUBLIC\r\n");
        sb.Append("END:VEVENT\r\n");
        sb.Append("END:VCALENDAR\r\n");

        return Task.FromResult(sb.ToString());
    }

    private static string EscapeIcsText(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        return text
            .Replace(@"\", @"\\")
            .Replace(";", @"\;")
            .Replace(",", @"\,")
            .Replace("\r\n", @"\n")
            .Replace("\n", @"\n");
    }

    #endregion

    #region Routing Stats

    public Task<RoutingStatsResult> GetRoutingStatsAsync(CancellationToken cancellationToken = default)
    {
        var appointments = _appointments.Values.ToList();
        var total = appointments.Count;
        var active = appointments.Count(a => a.Status == AppointmentStatus.Confirmed || a.Status == AppointmentStatus.Rescheduled);
        var cancelled = appointments.Count(a => a.Status == AppointmentStatus.Cancelled);

        var topologyMap = new Dictionary<RoutingTopology, int>
        {
            [RoutingTopology.WeightedRoundRobin] = appointments.Count(a => a.TopologyUsed == RoutingTopology.WeightedRoundRobin),
            [RoutingTopology.LeastBusy] = appointments.Count(a => a.TopologyUsed == RoutingTopology.LeastBusy),
            [RoutingTopology.Collective] = appointments.Count(a => a.TopologyUsed == RoutingTopology.Collective)
        };

        var hostMetrics = _hosts.Values.Select(h =>
        {
            var hostConfirmed = appointments.Count(a => a.HostId == h.HostId &&
                (a.Status == AppointmentStatus.Confirmed || a.Status == AppointmentStatus.Rescheduled));
            var totalAssigned = h.WeightedRoundRobinAssignments;
            var util = total > 0 ? (double)hostConfirmed / total : 0.0;

            return new HostRoutingMetric(
                HostId: h.HostId,
                HostName: h.HostName,
                HostEmail: h.HostEmail,
                TimeZoneId: h.TimeZoneId,
                Weight: h.Weight,
                IsActive: h.IsActive,
                ConfirmedBookingsCount: hostConfirmed,
                TotalAssignedCount: totalAssigned,
                LastAssignedAtUtc: h.LastAssignedAtUtc,
                UtilizationRatio: Math.Round(util, 3)
            );
        }).OrderByDescending(m => m.ConfirmedBookingsCount).ToList();

        var result = new RoutingStatsResult(
            TotalAppointments: total,
            ActiveAppointments: active,
            CancelledAppointments: cancelled,
            TopologyBookingsCount: topologyMap,
            HostMetrics: hostMetrics
        );

        return Task.FromResult(result);
    }

    #endregion

    #region Helper & Seed Data

    private static TimeZoneInfo ResolveTimeZone(string timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return TimeZoneInfo.Utc;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            // Normalize known IANA aliases if running on Windows
            return timeZoneId switch
            {
                "America/New_York" => TryFindTz("Eastern Standard Time"),
                "Europe/Paris" => TryFindTz("Romance Standard Time"),
                "Africa/Tunis" => TryFindTz("W. Central Africa Standard Time"),
                "Asia/Tokyo" => TryFindTz("Tokyo Standard Time"),
                "Europe/London" => TryFindTz("GMT Standard Time"),
                _ => TimeZoneInfo.Utc
            };
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }

    private static TimeZoneInfo TryFindTz(string id)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch
        {
            return TimeZoneInfo.Utc;
        }
    }

    private static DateTimeOffset ConvertToUtc(DateTime localDateTime, TimeZoneInfo tz)
    {
        // Guard against invalid/ambiguous time during DST transitions
        if (tz.IsInvalidTime(localDateTime))
        {
            // Push forward into valid range
            localDateTime = localDateTime.AddHours(1);
        }

        var offset = tz.GetUtcOffset(localDateTime);
        return new DateTimeOffset(localDateTime, offset).ToUniversalTime();
    }

    private void SeedDemoData()
    {
        var standardWorkWeek = new List<WeeklyWorkingHours>
        {
            new(DayOfWeek.Monday, TimeSpan.FromHours(9), TimeSpan.FromHours(17)),
            new(DayOfWeek.Tuesday, TimeSpan.FromHours(9), TimeSpan.FromHours(17)),
            new(DayOfWeek.Wednesday, TimeSpan.FromHours(9), TimeSpan.FromHours(17)),
            new(DayOfWeek.Thursday, TimeSpan.FromHours(9), TimeSpan.FromHours(17)),
            new(DayOfWeek.Friday, TimeSpan.FromHours(9), TimeSpan.FromHours(17))
        };

        // Host 1: Sarah Jenkins (US East, High Weight)
        var host1 = new HostSchedule
        {
            HostId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            HostName = "Sarah Jenkins",
            HostEmail = "sarah.jenkins@sgrowing.net",
            TimeZoneId = "America/New_York",
            Weight = 3,
            IsActive = true,
            BufferTime = TimeSpan.FromMinutes(15),
            MinimumNotice = TimeSpan.FromHours(1),
            WorkingHours = new List<WeeklyWorkingHours>(standardWorkWeek),
            Specialties = new List<string> { "Enterprise FinTech", "Architecture Reviews", "Key Account Strategy" }
        };

        // Host 2: Dr. Alexandre Mercier (Paris, High-Touch)
        var host2 = new HostSchedule
        {
            HostId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            HostName = "Dr. Alexandre Mercier",
            HostEmail = "alexandre.mercier@sgrowing.net",
            TimeZoneId = "Europe/Paris",
            Weight = 2,
            IsActive = true,
            BufferTime = TimeSpan.FromMinutes(15),
            MinimumNotice = TimeSpan.FromHours(2),
            WorkingHours = new List<WeeklyWorkingHours>(standardWorkWeek),
            Specialties = new List<string> { "EMEA Scaling", "Platform Migrations", "AI Automation" }
        };

        // Host 3: Tariq Mansour (Tunis, Emerging Markets)
        var host3 = new HostSchedule
        {
            HostId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            HostName = "Tariq Mansour",
            HostEmail = "tariq.mansour@sgrowing.net",
            TimeZoneId = "Africa/Tunis",
            Weight = 1,
            IsActive = true,
            BufferTime = TimeSpan.FromMinutes(15),
            MinimumNotice = TimeSpan.FromHours(1),
            WorkingHours = new List<WeeklyWorkingHours>(standardWorkWeek),
            Specialties = new List<string> { "Maghreb Expansion", "Logistics Tech", "High-Volume COD" }
        };

        // Host 4: Kenji Tanaka (Tokyo, APAC Eng)
        var host4 = new HostSchedule
        {
            HostId = Guid.Parse("44444444-4444-4444-4444-444444444444"),
            HostName = "Kenji Tanaka",
            HostEmail = "kenji.tanaka@sgrowing.net",
            TimeZoneId = "Asia/Tokyo",
            Weight = 2,
            IsActive = true,
            BufferTime = TimeSpan.FromMinutes(15),
            MinimumNotice = TimeSpan.FromHours(2),
            WorkingHours = new List<WeeklyWorkingHours>(standardWorkWeek),
            Specialties = new List<string> { "APAC Infrastructure", "Distributed Event Streaming", "GraphQL/gRPC" }
        };

        _hosts[host1.HostId] = host1;
        _hosts[host2.HostId] = host2;
        _hosts[host3.HostId] = host3;
        _hosts[host4.HostId] = host4;

        // Seed sample appointments
        var now = DateTimeOffset.UtcNow;
        var tomorrow14Utc = now.Date.AddDays(1).AddHours(14);

        var demoApt1 = new Appointment
        {
            Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            HostId = host1.HostId,
            HostName = host1.HostName,
            HostEmail = host1.HostEmail,
            Title = "Cloud Infrastructure Architecture Review",
            Description = "Comprehensive deep dive into multi-region scaling and caching strategy.",
            AttendeeName = "David Vance",
            AttendeeEmail = "david.vance@acmecorp.com",
            AttendeePhone = "+1 (555) 234-8901",
            StartUtc = tomorrow14Utc,
            EndUtc = tomorrow14Utc.AddMinutes(45),
            DurationMinutes = 45,
            ProspectTimeZoneId = "America/New_York",
            Status = AppointmentStatus.Confirmed,
            MeetingLink = "https://meet.sgrowing.net/room-arch-rev",
            TopologyUsed = RoutingTopology.WeightedRoundRobin,
            CreatedAtUtc = now.AddHours(-12),
            UpdatedAtUtc = now.AddHours(-12)
        };

        var tomorrow10Utc = now.Date.AddDays(1).AddHours(10);
        var demoApt2 = new Appointment
        {
            Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            HostId = host2.HostId,
            HostName = host2.HostName,
            HostEmail = host2.HostEmail,
            Title = "EMEA Logistics ERP Integration Consultation",
            Description = "Cross-border carrier onboarding and automated customs clearance review.",
            AttendeeName = "Elena Rostova",
            AttendeeEmail = "elena.rostova@globex.de",
            AttendeePhone = "+49 30 1234567",
            StartUtc = tomorrow10Utc,
            EndUtc = tomorrow10Utc.AddMinutes(30),
            DurationMinutes = 30,
            ProspectTimeZoneId = "Europe/Paris",
            Status = AppointmentStatus.Confirmed,
            MeetingLink = "https://meet.sgrowing.net/room-emea-erp",
            TopologyUsed = RoutingTopology.LeastBusy,
            CreatedAtUtc = now.AddHours(-8),
            UpdatedAtUtc = now.AddHours(-8)
        };

        _appointments[demoApt1.Id] = demoApt1;
        _appointments[demoApt2.Id] = demoApt2;

        host1.WeightedRoundRobinAssignments = 1;
        host1.LastAssignedAtUtc = now.AddHours(-12);
        host2.WeightedRoundRobinAssignments = 1;
        host2.LastAssignedAtUtc = now.AddHours(-8);
    }

    #endregion
}
