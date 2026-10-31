using System.Collections.Concurrent;
using System.Text.Json.Serialization;

namespace SGrowing.Api.Modules.Workforce.Shifts;

#region Enums

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ShiftType
{
    Morning,
    Afternoon,
    Evening,
    Graveyard,
    WeekendOnCall
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ShiftStatus
{
    Scheduled,
    CheckedIn,
    Completed,
    LateArrival,
    Absent,
    Cancelled
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SwapRequestStatus
{
    PendingManagerApproval,
    Approved,
    Rejected,
    Cancelled
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ChannelSpecialty
{
    WhatsApp,
    Voice,
    Email,
    LiveChat,
    Omnichannel
}

#endregion

#region Models & DTOs

public record ShiftTemplate(
    Guid TemplateId,
    Guid TenantId,
    string Name,
    ShiftType ShiftType,
    TimeSpan StartLocalTime,
    TimeSpan EndLocalTime,
    double DurationHours,
    Dictionary<ChannelSpecialty, int> RequiredChannelQuotas
);

public record AgentShiftSchedule
{
    public Guid ShiftId { get; init; } = Guid.NewGuid();
    public Guid TenantId { get; init; }
    public Guid AgentId { get; set; }
    public string AgentName { get; set; } = string.Empty;
    public Guid TemplateId { get; init; }
    public ShiftType ShiftType { get; init; }
    public DateTime StartTimeUtc { get; init; }
    public DateTime EndTimeUtc { get; init; }
    public ChannelSpecialty AssignedChannel { get; init; } = ChannelSpecialty.Omnichannel;
    public ShiftStatus Status { get; set; } = ShiftStatus.Scheduled;
    public DateTime? ActualClockInUtc { get; set; }
    public DateTime? ActualClockOutUtc { get; set; }
    public string? Notes { get; set; }
}

public record ShiftSwapRequest
{
    public Guid SwapId { get; init; } = Guid.NewGuid();
    public Guid TenantId { get; init; }
    public Guid RequesterAgentId { get; init; }
    public string RequesterAgentName { get; init; } = string.Empty;
    public Guid RequesterShiftId { get; init; }
    public Guid TargetAgentId { get; init; }
    public string TargetAgentName { get; init; } = string.Empty;
    public Guid TargetShiftId { get; init; }
    public string Reason { get; init; } = string.Empty;
    public SwapRequestStatus Status { get; set; } = SwapRequestStatus.PendingManagerApproval;
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
    public DateTime? ReviewedAtUtc { get; set; }
    public Guid? ReviewedByManagerId { get; set; }
}

public record HourlyCapacityGap(
    DateTime HourUtc,
    ChannelSpecialty Channel,
    int ForecastedInboundVolume,
    int RequiredStaffCount,
    int ScheduledStaffCount,
    int StaffingGap,
    bool IsUnderstaffed
);

public record CapacityAnalysisResult(
    Guid TenantId,
    DateTime DateUtc,
    int TotalForecastedVolume,
    int TotalScheduledAgents,
    int TotalGapsCount,
    List<HourlyCapacityGap> HourlyGaps
);

public record AgentAdherenceRecord(
    Guid AgentId,
    string AgentName,
    int TotalScheduledShifts,
    int OnTimeShifts,
    int LateArrivals,
    int Absences,
    double AdherencePercentage
);

public record CreateShiftTemplateRequest(
    Guid TenantId,
    string Name,
    ShiftType ShiftType,
    TimeSpan StartLocalTime,
    TimeSpan EndLocalTime,
    Dictionary<ChannelSpecialty, int>? RequiredChannelQuotas = null
);

public record ScheduleShiftRequest(
    Guid TenantId,
    Guid AgentId,
    string AgentName,
    Guid TemplateId,
    ShiftType ShiftType,
    DateTime StartTimeUtc,
    DateTime EndTimeUtc,
    ChannelSpecialty AssignedChannel = ChannelSpecialty.Omnichannel,
    string? Notes = null
);

public record ClockInShiftRequest(
    Guid TenantId,
    Guid ShiftId,
    Guid AgentId,
    DateTime? ClockInTimeUtc = null,
    string? IpAddress = null
);

public record ClockOutShiftRequest(
    Guid TenantId,
    Guid ShiftId,
    Guid AgentId,
    DateTime? ClockOutTimeUtc = null
);

public record RequestShiftSwapRequest(
    Guid TenantId,
    Guid RequesterAgentId,
    string RequesterAgentName,
    Guid RequesterShiftId,
    Guid TargetAgentId,
    string TargetAgentName,
    Guid TargetShiftId,
    string Reason
);

#endregion

public interface IWorkforceShiftService
{
    Task<ShiftTemplate> CreateShiftTemplateAsync(CreateShiftTemplateRequest request, CancellationToken ct = default);
    Task<List<ShiftTemplate>> GetShiftTemplatesAsync(Guid tenantId, CancellationToken ct = default);

    Task<AgentShiftSchedule> ScheduleShiftAsync(ScheduleShiftRequest request, CancellationToken ct = default);
    Task<List<AgentShiftSchedule>> GetSchedulesAsync(Guid tenantId, DateTime? fromUtc = null, DateTime? toUtc = null, Guid? agentId = null, CancellationToken ct = default);
    Task<AgentShiftSchedule?> GetScheduleByIdAsync(Guid tenantId, Guid shiftId, CancellationToken ct = default);

    Task<AgentShiftSchedule> ClockInShiftAsync(ClockInShiftRequest request, CancellationToken ct = default);
    Task<AgentShiftSchedule> ClockOutShiftAsync(ClockOutShiftRequest request, CancellationToken ct = default);

    Task<ShiftSwapRequest> RequestShiftSwapAsync(RequestShiftSwapRequest request, CancellationToken ct = default);
    Task<ShiftSwapRequest> ApproveShiftSwapAsync(Guid tenantId, Guid swapId, Guid managerId, CancellationToken ct = default);
    Task<ShiftSwapRequest> RejectShiftSwapAsync(Guid tenantId, Guid swapId, Guid managerId, CancellationToken ct = default);
    Task<List<ShiftSwapRequest>> GetPendingSwapRequestsAsync(Guid tenantId, CancellationToken ct = default);

    Task<CapacityAnalysisResult> EvaluateCapacityAndGapsAsync(Guid tenantId, DateTime dateUtc, int forecastedVolume, ChannelSpecialty channel = ChannelSpecialty.WhatsApp, CancellationToken ct = default);
    Task<List<AgentAdherenceRecord>> GetAdherenceReportAsync(Guid tenantId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
}

public class WorkforceShiftService : IWorkforceShiftService
{
    private readonly ConcurrentDictionary<Guid, ShiftTemplate> _templates = new();
    private readonly ConcurrentDictionary<Guid, AgentShiftSchedule> _schedules = new();
    private readonly ConcurrentDictionary<Guid, ShiftSwapRequest> _swaps = new();

    public Task<ShiftTemplate> CreateShiftTemplateAsync(CreateShiftTemplateRequest request, CancellationToken ct = default)
    {
        if (request.TenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.Name)) throw new ArgumentException("Template Name is required.", nameof(request));

        var duration = (request.EndLocalTime - request.StartLocalTime).TotalHours;
        if (duration < 0) duration += 24;

        var template = new ShiftTemplate(
            Guid.NewGuid(),
            request.TenantId,
            request.Name.Trim(),
            request.ShiftType,
            request.StartLocalTime,
            request.EndLocalTime,
            duration,
            request.RequiredChannelQuotas ?? new Dictionary<ChannelSpecialty, int>()
        );

        _templates[template.TemplateId] = template;
        return Task.FromResult(template);
    }

    public Task<List<ShiftTemplate>> GetShiftTemplatesAsync(Guid tenantId, CancellationToken ct = default)
    {
        var list = _templates.Values.Where(t => t.TenantId == tenantId).ToList();
        return Task.FromResult(list);
    }

    public Task<AgentShiftSchedule> ScheduleShiftAsync(ScheduleShiftRequest request, CancellationToken ct = default)
    {
        if (request.TenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(request));
        if (request.AgentId == Guid.Empty) throw new ArgumentException("AgentId is required.", nameof(request));
        if (request.StartTimeUtc >= request.EndTimeUtc)
        {
            throw new ArgumentException("StartTimeUtc must be earlier than EndTimeUtc.", nameof(request));
        }

        // 1. Conflict Check: Overlapping shifts for same agent
        var existingShifts = _schedules.Values
            .Where(s => s.TenantId == request.TenantId && s.AgentId == request.AgentId && s.Status != ShiftStatus.Cancelled)
            .ToList();

        var overlap = existingShifts.FirstOrDefault(s =>
            request.StartTimeUtc < s.EndTimeUtc && request.EndTimeUtc > s.StartTimeUtc);

        if (overlap != null)
        {
            throw new InvalidOperationException($"Shift overlaps with existing shift #{overlap.ShiftId} ({overlap.StartTimeUtc:O} - {overlap.EndTimeUtc:O}).");
        }

        // 2. Conflict Check: Minimum 11 hours rest period
        var tooClose = existingShifts.FirstOrDefault(s =>
            (request.StartTimeUtc >= s.EndTimeUtc && (request.StartTimeUtc - s.EndTimeUtc).TotalHours < 11) ||
            (s.StartTimeUtc >= request.EndTimeUtc && (s.StartTimeUtc - request.EndTimeUtc).TotalHours < 11));

        if (tooClose != null)
        {
            throw new InvalidOperationException($"Minimum 11-hour rest period rule violated with shift #{tooClose.ShiftId}.");
        }

        // 3. Weekly hours limit (max 48h in rolling 7-day window)
        var windowStart = request.StartTimeUtc.AddDays(-3.5);
        var windowEnd = request.EndTimeUtc.AddDays(3.5);
        var totalHours = existingShifts
            .Where(s => s.StartTimeUtc >= windowStart && s.EndTimeUtc <= windowEnd)
            .Sum(s => (s.EndTimeUtc - s.StartTimeUtc).TotalHours);

        var newDuration = (request.EndTimeUtc - request.StartTimeUtc).TotalHours;
        if (totalHours + newDuration > 48)
        {
            throw new InvalidOperationException($"Weekly working hours limit of 48h would be exceeded ({totalHours + newDuration:F1}h total).");
        }

        var schedule = new AgentShiftSchedule
        {
            TenantId = request.TenantId,
            AgentId = request.AgentId,
            AgentName = request.AgentName.Trim(),
            TemplateId = request.TemplateId,
            ShiftType = request.ShiftType,
            StartTimeUtc = request.StartTimeUtc,
            EndTimeUtc = request.EndTimeUtc,
            AssignedChannel = request.AssignedChannel,
            Status = ShiftStatus.Scheduled,
            Notes = request.Notes?.Trim()
        };

        _schedules[schedule.ShiftId] = schedule;
        return Task.FromResult(schedule);
    }

    public Task<List<AgentShiftSchedule>> GetSchedulesAsync(Guid tenantId, DateTime? fromUtc = null, DateTime? toUtc = null, Guid? agentId = null, CancellationToken ct = default)
    {
        var query = _schedules.Values.Where(s => s.TenantId == tenantId);

        if (fromUtc.HasValue) query = query.Where(s => s.EndTimeUtc >= fromUtc.Value);
        if (toUtc.HasValue) query = query.Where(s => s.StartTimeUtc <= toUtc.Value);
        if (agentId.HasValue) query = query.Where(s => s.AgentId == agentId.Value);

        var list = query.OrderBy(s => s.StartTimeUtc).ToList();
        return Task.FromResult(list);
    }

    public Task<AgentShiftSchedule?> GetScheduleByIdAsync(Guid tenantId, Guid shiftId, CancellationToken ct = default)
    {
        if (_schedules.TryGetValue(shiftId, out var shift) && shift.TenantId == tenantId)
        {
            return Task.FromResult<AgentShiftSchedule?>(shift);
        }
        return Task.FromResult<AgentShiftSchedule?>(null);
    }

    public Task<AgentShiftSchedule> ClockInShiftAsync(ClockInShiftRequest request, CancellationToken ct = default)
    {
        if (!_schedules.TryGetValue(request.ShiftId, out var shift) || shift.TenantId != request.TenantId)
        {
            throw new KeyNotFoundException($"Shift {request.ShiftId} not found.");
        }

        if (shift.AgentId != request.AgentId)
        {
            throw new UnauthorizedAccessException("Only the assigned agent can clock into this shift.");
        }

        var clockIn = request.ClockInTimeUtc ?? DateTime.UtcNow;
        shift.ActualClockInUtc = clockIn;

        // If clock-in is > 10m past scheduled start time, mark as LateArrival
        if (clockIn > shift.StartTimeUtc.AddMinutes(10))
        {
            shift.Status = ShiftStatus.LateArrival;
        }
        else
        {
            shift.Status = ShiftStatus.CheckedIn;
        }

        return Task.FromResult(shift);
    }

    public Task<AgentShiftSchedule> ClockOutShiftAsync(ClockOutShiftRequest request, CancellationToken ct = default)
    {
        if (!_schedules.TryGetValue(request.ShiftId, out var shift) || shift.TenantId != request.TenantId)
        {
            throw new KeyNotFoundException($"Shift {request.ShiftId} not found.");
        }

        if (shift.AgentId != request.AgentId)
        {
            throw new UnauthorizedAccessException("Only the assigned agent can clock out of this shift.");
        }

        var clockOut = request.ClockOutTimeUtc ?? DateTime.UtcNow;
        shift.ActualClockOutUtc = clockOut;
        shift.Status = ShiftStatus.Completed;

        return Task.FromResult(shift);
    }

    public Task<ShiftSwapRequest> RequestShiftSwapAsync(RequestShiftSwapRequest request, CancellationToken ct = default)
    {
        if (request.TenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(request));

        if (!_schedules.TryGetValue(request.RequesterShiftId, out var reqShift) || reqShift.AgentId != request.RequesterAgentId)
        {
            throw new InvalidOperationException("Requester shift not found or not assigned to requester.");
        }

        if (!_schedules.TryGetValue(request.TargetShiftId, out var tgtShift) || tgtShift.AgentId != request.TargetAgentId)
        {
            throw new InvalidOperationException("Target shift not found or not assigned to target agent.");
        }

        var swap = new ShiftSwapRequest
        {
            TenantId = request.TenantId,
            RequesterAgentId = request.RequesterAgentId,
            RequesterAgentName = request.RequesterAgentName.Trim(),
            RequesterShiftId = request.RequesterShiftId,
            TargetAgentId = request.TargetAgentId,
            TargetAgentName = request.TargetAgentName.Trim(),
            TargetShiftId = request.TargetShiftId,
            Reason = request.Reason?.Trim() ?? string.Empty
        };

        _swaps[swap.SwapId] = swap;
        return Task.FromResult(swap);
    }

    public Task<ShiftSwapRequest> ApproveShiftSwapAsync(Guid tenantId, Guid swapId, Guid managerId, CancellationToken ct = default)
    {
        if (!_swaps.TryGetValue(swapId, out var swap) || swap.TenantId != tenantId)
        {
            throw new KeyNotFoundException($"Swap request {swapId} not found.");
        }

        if (swap.Status != SwapRequestStatus.PendingManagerApproval)
        {
            throw new InvalidOperationException("Swap request is not in a pending state.");
        }

        if (_schedules.TryGetValue(swap.RequesterShiftId, out var reqShift) &&
            _schedules.TryGetValue(swap.TargetShiftId, out var tgtShift))
        {
            // Atomically swap the assigned agents
            var tempId = reqShift.AgentId;
            var tempName = reqShift.AgentName;

            reqShift.AgentId = tgtShift.AgentId;
            reqShift.AgentName = tgtShift.AgentName;

            tgtShift.AgentId = tempId;
            tgtShift.AgentName = tempName;
        }

        swap.Status = SwapRequestStatus.Approved;
        swap.ReviewedAtUtc = DateTime.UtcNow;
        swap.ReviewedByManagerId = managerId;

        return Task.FromResult(swap);
    }

    public Task<ShiftSwapRequest> RejectShiftSwapAsync(Guid tenantId, Guid swapId, Guid managerId, CancellationToken ct = default)
    {
        if (!_swaps.TryGetValue(swapId, out var swap) || swap.TenantId != tenantId)
        {
            throw new KeyNotFoundException($"Swap request {swapId} not found.");
        }

        swap.Status = SwapRequestStatus.Rejected;
        swap.ReviewedAtUtc = DateTime.UtcNow;
        swap.ReviewedByManagerId = managerId;

        return Task.FromResult(swap);
    }

    public Task<List<ShiftSwapRequest>> GetPendingSwapRequestsAsync(Guid tenantId, CancellationToken ct = default)
    {
        var list = _swaps.Values
            .Where(s => s.TenantId == tenantId && s.Status == SwapRequestStatus.PendingManagerApproval)
            .OrderByDescending(s => s.CreatedAtUtc)
            .ToList();
        return Task.FromResult(list);
    }

    public Task<CapacityAnalysisResult> EvaluateCapacityAndGapsAsync(Guid tenantId, DateTime dateUtc, int forecastedVolume, ChannelSpecialty channel = ChannelSpecialty.WhatsApp, CancellationToken ct = default)
    {
        var dayStart = dateUtc.Date;
        var activeShifts = _schedules.Values
            .Where(s => s.TenantId == tenantId && s.Status != ShiftStatus.Cancelled &&
                        s.StartTimeUtc < dayStart.AddDays(1) && s.EndTimeUtc > dayStart)
            .ToList();

        // Staffing ratio per channel
        int capacityPerAgentPerHour = channel switch
        {
            ChannelSpecialty.WhatsApp => 15,
            ChannelSpecialty.Voice => 5,
            ChannelSpecialty.Email => 25,
            _ => 10
        };

        var hourlyGaps = new List<HourlyCapacityGap>();
        int totalGaps = 0;

        for (int h = 0; h < 24; h++)
        {
            var hourStart = dayStart.AddHours(h);
            var hourEnd = hourStart.AddHours(1);

            // Active agents during this hour
            var scheduledStaff = activeShifts.Count(s => s.StartTimeUtc < hourEnd && s.EndTimeUtc > hourStart);

            // Mock peak distribution (peak between 09:00 - 18:00)
            double distributionFactor = (h >= 9 && h <= 18) ? 0.07 : 0.02;
            int hourlyForecast = (int)Math.Ceiling(forecastedVolume * distributionFactor);
            int requiredStaff = (int)Math.Ceiling((double)hourlyForecast / capacityPerAgentPerHour);

            int gap = requiredStaff - scheduledStaff;
            bool isUnderstaffed = gap > 0;
            if (isUnderstaffed) totalGaps++;

            hourlyGaps.Add(new HourlyCapacityGap(
                hourStart,
                channel,
                hourlyForecast,
                requiredStaff,
                scheduledStaff,
                gap,
                isUnderstaffed
            ));
        }

        return Task.FromResult(new CapacityAnalysisResult(
            tenantId,
            dayStart,
            forecastedVolume,
            activeShifts.Count,
            totalGaps,
            hourlyGaps
        ));
    }

    public Task<List<AgentAdherenceRecord>> GetAdherenceReportAsync(Guid tenantId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        var shifts = _schedules.Values
            .Where(s => s.TenantId == tenantId && s.StartTimeUtc >= fromUtc && s.EndTimeUtc <= toUtc)
            .GroupBy(s => (s.AgentId, s.AgentName))
            .Select(g =>
            {
                var total = g.Count();
                var late = g.Count(s => s.Status == ShiftStatus.LateArrival);
                var absent = g.Count(s => s.Status == ShiftStatus.Absent);
                var onTime = g.Count(s => s.Status == ShiftStatus.CheckedIn || s.Status == ShiftStatus.Completed);

                double adherence = total > 0 ? ((double)onTime / total) * 100.0 : 100.0;

                return new AgentAdherenceRecord(
                    g.Key.AgentId,
                    g.Key.AgentName,
                    total,
                    onTime,
                    late,
                    absent,
                    Math.Round(adherence, 1)
                );
            })
            .ToList();

        return Task.FromResult(shifts);
    }
}
