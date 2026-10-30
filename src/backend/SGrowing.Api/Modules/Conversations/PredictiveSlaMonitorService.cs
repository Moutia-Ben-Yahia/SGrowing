using System.Collections.Concurrent;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Conversations;

public enum SlaRiskLevel
{
    Safe,
    Warning,
    ImminentBreach
}

public enum SlaEscalationStage
{
    Nominal,
    Warning,
    Critical,
    Breached
}

public enum SlaPriorityTier
{
    Standard,
    High,
    Urgent,
    Critical24x7
}

public enum SlaMetricTarget
{
    FirstResponse,
    Resolution
}

public record SeniorSpecialistAgent(
    string Id,
    string Name,
    string Tier,
    int ActiveQueueCount
);

public record TicketSlaProjectionDto(
    Guid TicketId,
    string TicketNumber,
    string Title,
    string CustomerBrand,
    string AssignedAgentId,
    string AssignedAgentName,
    string AssignedAgentTier,
    SlaPriorityTier Priority,
    SlaMetricTarget TargetType,
    int QueueAhead,
    double ComplexityWeight,
    double AvgAgentVelocityMinutes,
    DateTime CreatedAtUtc,
    DateTime DueAtUtc,
    DateTime PredictedCompletionAtUtc,
    double TargetWorkingMinutes,
    double ElapsedWorkingMinutes,
    double RemainingWorkingMinutes,
    double PercentageElapsed,
    double BufferMinutes,
    SlaEscalationStage EscalationStage,
    SlaRiskLevel RiskLevel,
    bool Is24x7Tier,
    bool PriorityBoosted,
    bool AutoReassigned,
    string? AutoReassignedTo,
    string? ExecutiveAlert,
    DateTime LastEvaluatedAtUtc,
    DateTime? FirstRespondedAtUtc,
    DateTime? ResolvedAtUtc,
    bool IsFirstResponseCompliant,
    bool IsResolutionCompliant
);

public record SlaMonitorMetricsDto(
    double FirstResponseCompliancePercentage,
    double ResolutionCompliancePercentage,
    int TotalMonitoredTickets,
    int ImminentBreachesCount,
    int CriticalCount,
    int WarningCount,
    int SafeCount,
    int BreachedCount,
    double ActiveQueueVelocity,
    DateTime LastSweepAtUtc
);

public record EscalationSweepResultDto(
    DateTime SweepTimeUtc,
    int EvaluatedCount,
    int WarningEscalationsCount,
    int CriticalEscalationsCount,
    int AutoReassignedCount,
    int BreachedCount,
    List<TicketSlaProjectionDto> Tickets
);

public record EvaluateTicketRequest(
    Guid TicketId,
    int? QueueAhead = null,
    double? AvgAgentVelocityMinutes = null,
    double? ComplexityWeight = null
);

public record ManualReassignRequest(
    Guid TicketId,
    string TargetAgentId,
    string TargetAgentName,
    string Reason
);

public interface IPredictiveSlaMonitorService
{
    // Business Hours Calendar Calculations
    double CalculateElapsedBusinessMinutes(DateTime startUtc, DateTime endUtc, bool is24x7 = false);
    DateTime CalculateDueDate(DateTime startUtc, double targetMinutes, bool is24x7 = false);
    double CalculateRemainingBusinessMinutes(DateTime nowUtc, DateTime dueUtc, bool is24x7 = false);
    bool IsWithinBusinessHours(DateTime dateTimeUtc, bool is24x7 = false);

    // Predictive Velocity Projection
    (DateTime PredictedCompletionUtc, double BufferMinutes, SlaRiskLevel RiskLevel) ProjectVelocityToBreach(
        DateTime nowUtc,
        DateTime dueUtc,
        int queueAhead,
        double avgAgentVelocityMinutes,
        double complexityWeight = 0.0);

    // Multi-Stage Escalation Ladder
    (SlaEscalationStage Stage, bool PriorityBoosted, bool NeedsAutoReassignment, bool TriggerExecutiveAlert) EvaluateEscalationStage(
        double elapsedWorkingMinutes,
        double targetWorkingMinutes);

    // State & Evaluation Operations
    Task<IReadOnlyList<TicketSlaProjectionDto>> GetMonitoredTicketsAsync(string? riskFilter = null, CancellationToken ct = default);
    Task<TicketSlaProjectionDto> EvaluateTicketAsync(Guid ticketId, int? queueAhead = null, double? velocity = null, double? complexity = null, CancellationToken ct = default);
    Task<EscalationSweepResultDto> RunEscalationSweepAsync(CancellationToken ct = default);
    Task<TicketSlaProjectionDto> ManualReassignAsync(Guid ticketId, string targetAgentId, string targetAgentName, string reason, CancellationToken ct = default);
    Task<SlaMonitorMetricsDto> GetMetricsAsync(CancellationToken ct = default);

    // Configuration & Testing Helpers
    void AddStatutoryHoliday(DateOnly holiday);
    void SetBusinessHours(TimeOnly startUtc, TimeOnly endUtc);
    void ResetToSeedData();
}

public class PredictiveSlaMonitorService : IPredictiveSlaMonitorService
{
    private TimeOnly _businessHoursStart = new(9, 0); // 09:00 UTC
    private TimeOnly _businessHoursEnd = new(18, 0);   // 18:00 UTC
    private readonly HashSet<DateOnly> _statutoryHolidays = new();
    private readonly object _lock = new();

    // Internal mutable ticket model for thread-safe state storage
    private class MonitoredTicketState
    {
        public Guid TicketId { get; set; }
        public string TicketNumber { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string CustomerBrand { get; set; } = string.Empty;
        public string AssignedAgentId { get; set; } = string.Empty;
        public string AssignedAgentName { get; set; } = string.Empty;
        public string AssignedAgentTier { get; set; } = "Tier-1 Support";
        public SlaPriorityTier Priority { get; set; }
        public SlaMetricTarget TargetType { get; set; }
        public int QueueAhead { get; set; }
        public double ComplexityWeight { get; set; }
        public double AvgAgentVelocityMinutes { get; set; } // Tickets processed per minute (e.g. 0.05 = 1 ticket every 20m)
        public DateTime CreatedAtUtc { get; set; }
        public double TargetWorkingMinutes { get; set; }
        public bool Is24x7Tier { get; set; }
        public bool PriorityBoosted { get; set; }
        public bool AutoReassigned { get; set; }
        public string? AutoReassignedTo { get; set; }
        public string? ExecutiveAlert { get; set; }
        public DateTime LastEvaluatedAtUtc { get; set; }
        public DateTime? FirstRespondedAtUtc { get; set; }
        public DateTime? ResolvedAtUtc { get; set; }
    }

    private readonly ConcurrentDictionary<Guid, MonitoredTicketState> _tickets = new();
    private readonly List<SeniorSpecialistAgent> _seniorSpecialists = new();
    private DateTime _lastSweepAtUtc = DateTime.UtcNow;

    public PredictiveSlaMonitorService()
    {
        // Seed default statutory holidays (e.g. 2026 common holidays)
        _statutoryHolidays.Add(new DateOnly(2026, 1, 1));   // New Year's Day
        _statutoryHolidays.Add(new DateOnly(2026, 12, 25)); // Christmas Day

        // Seed Senior Tier-2 Specialists Pool for Auto-Reassignment
        _seniorSpecialists.Add(new SeniorSpecialistAgent("agent-tier2-01", "Sarah Chen", "Senior Escalation Specialist (Tier-2)", 2));
        _seniorSpecialists.Add(new SeniorSpecialistAgent("agent-tier2-02", "Alexei Morozov", "Staff Support Engineer (Tier-2)", 1));
        _seniorSpecialists.Add(new SeniorSpecialistAgent("agent-tier2-03", "Elena Vance", "Principal SRE / Incident Lead", 0));

        ResetToSeedData();
    }

    public void AddStatutoryHoliday(DateOnly holiday)
    {
        lock (_lock)
        {
            _statutoryHolidays.Add(holiday);
        }
    }

    public void SetBusinessHours(TimeOnly startUtc, TimeOnly endUtc)
    {
        lock (_lock)
        {
            _businessHoursStart = startUtc;
            _businessHoursEnd = endUtc;
        }
    }

    public bool IsWithinBusinessHours(DateTime dateTimeUtc, bool is24x7 = false)
    {
        if (is24x7) return true;

        var dayOfWeek = dateTimeUtc.DayOfWeek;
        if (dayOfWeek == DayOfWeek.Saturday || dayOfWeek == DayOfWeek.Sunday)
            return false;

        var dateOnly = DateOnly.FromDateTime(dateTimeUtc);
        lock (_lock)
        {
            if (_statutoryHolidays.Contains(dateOnly))
                return false;

            var timeOnly = TimeOnly.FromDateTime(dateTimeUtc);
            return timeOnly >= _businessHoursStart && timeOnly < _businessHoursEnd;
        }
    }

    public double CalculateElapsedBusinessMinutes(DateTime startUtc, DateTime endUtc, bool is24x7 = false)
    {
        if (endUtc <= startUtc) return 0.0;
        if (is24x7) return (endUtc - startUtc).TotalMinutes;

        double totalWorkingMinutes = 0.0;
        var currentDay = startUtc.Date;
        var endDay = endUtc.Date;

        TimeOnly startHour;
        TimeOnly endHour;
        HashSet<DateOnly> holidays;

        lock (_lock)
        {
            startHour = _businessHoursStart;
            endHour = _businessHoursEnd;
            holidays = new HashSet<DateOnly>(_statutoryHolidays);
        }

        while (currentDay <= endDay)
        {
            var dayOfWeek = currentDay.DayOfWeek;
            var dateOnly = DateOnly.FromDateTime(currentDay);

            if (dayOfWeek != DayOfWeek.Saturday && dayOfWeek != DayOfWeek.Sunday && !holidays.Contains(dateOnly))
            {
                var windowStart = currentDay.Add(startHour.ToTimeSpan());
                var windowEnd = currentDay.Add(endHour.ToTimeSpan());

                var overlapStart = startUtc > windowStart ? startUtc : windowStart;
                var overlapEnd = endUtc < windowEnd ? endUtc : windowEnd;

                if (overlapEnd > overlapStart)
                {
                    totalWorkingMinutes += (overlapEnd - overlapStart).TotalMinutes;
                }
            }

            currentDay = currentDay.AddDays(1);
        }

        return Math.Round(totalWorkingMinutes, 2);
    }

    public DateTime CalculateDueDate(DateTime startUtc, double targetMinutes, bool is24x7 = false)
    {
        if (targetMinutes <= 0) return startUtc;
        if (is24x7) return startUtc.AddMinutes(targetMinutes);

        TimeOnly startHour;
        TimeOnly endHour;
        HashSet<DateOnly> holidays;

        lock (_lock)
        {
            startHour = _businessHoursStart;
            endHour = _businessHoursEnd;
            holidays = new HashSet<DateOnly>(_statutoryHolidays);
        }

        var current = startUtc;
        var remainingMinutes = targetMinutes;
        int maxIterations = 5000; // Circuit breaker

        while (remainingMinutes > 0 && maxIterations-- > 0)
        {
            var dayOfWeek = current.DayOfWeek;
            var dateOnly = DateOnly.FromDateTime(current);

            // If non-business day (weekend or holiday), advance to start of next business day
            if (dayOfWeek == DayOfWeek.Saturday || dayOfWeek == DayOfWeek.Sunday || holidays.Contains(dateOnly))
            {
                current = current.Date.AddDays(1).Add(startHour.ToTimeSpan());
                continue;
            }

            var windowStart = current.Date.Add(startHour.ToTimeSpan());
            var windowEnd = current.Date.Add(endHour.ToTimeSpan());

            if (current < windowStart)
            {
                current = windowStart;
            }
            else if (current >= windowEnd)
            {
                current = current.Date.AddDays(1).Add(startHour.ToTimeSpan());
                continue;
            }

            var availableInCurrentDay = (windowEnd - current).TotalMinutes;
            if (remainingMinutes <= availableInCurrentDay)
            {
                current = current.AddMinutes(remainingMinutes);
                remainingMinutes = 0;
            }
            else
            {
                remainingMinutes -= availableInCurrentDay;
                current = current.Date.AddDays(1).Add(startHour.ToTimeSpan());
            }
        }

        return current;
    }

    public double CalculateRemainingBusinessMinutes(DateTime nowUtc, DateTime dueUtc, bool is24x7 = false)
    {
        if (nowUtc >= dueUtc) return 0.0;
        return CalculateElapsedBusinessMinutes(nowUtc, dueUtc, is24x7);
    }

    public (DateTime PredictedCompletionUtc, double BufferMinutes, SlaRiskLevel RiskLevel) ProjectVelocityToBreach(
        DateTime nowUtc,
        DateTime dueUtc,
        int queueAhead,
        double avgAgentVelocityMinutes,
        double complexityWeight = 0.0)
    {
        // Guard against zero/negative velocity (tickets per minute)
        var velocity = avgAgentVelocityMinutes > 0.0001 ? avgAgentVelocityMinutes : 0.05;
        var queueFactor = Math.Max(0, queueAhead) + 1;
        var complexityMultiplier = 1.0 + Math.Max(0.0, complexityWeight);

        // Required minutes = ((QueueAhead + 1) / AvgAgentVelocityMinutes) * (1.0 + ComplexityWeight)
        var estimatedMinutes = (queueFactor / velocity) * complexityMultiplier;
        var predictedCompletionUtc = nowUtc.AddMinutes(estimatedMinutes);

        // Buffer to breach: Delta_T = T_due - T_predicted
        var bufferMinutes = Math.Round((dueUtc - predictedCompletionUtc).TotalMinutes, 2);

        // Forecast Risk Level: Safe (>60m), Warning (0m <= Delta_T <= 60m), ImminentBreach (<0m)
        SlaRiskLevel riskLevel;
        if (bufferMinutes > 60.0)
        {
            riskLevel = SlaRiskLevel.Safe;
        }
        else if (bufferMinutes >= 0.0)
        {
            riskLevel = SlaRiskLevel.Warning;
        }
        else
        {
            riskLevel = SlaRiskLevel.ImminentBreach;
        }

        return (predictedCompletionUtc, bufferMinutes, riskLevel);
    }

    public (SlaEscalationStage Stage, bool PriorityBoosted, bool NeedsAutoReassignment, bool TriggerExecutiveAlert) EvaluateEscalationStage(
        double elapsedWorkingMinutes,
        double targetWorkingMinutes)
    {
        if (targetWorkingMinutes <= 0)
        {
            return (SlaEscalationStage.Breached, true, true, true);
        }

        var percentage = (elapsedWorkingMinutes / targetWorkingMinutes) * 100.0;

        if (percentage < 70.0)
        {
            return (SlaEscalationStage.Nominal, false, false, false);
        }
        if (percentage < 85.0)
        {
            // Warning stage: Priority boosted, yellow flag
            return (SlaEscalationStage.Warning, true, false, false);
        }
        if (percentage < 100.0)
        {
            // Critical stage: Auto-reassigned to senior tier-2 on-call specialist
            return (SlaEscalationStage.Critical, true, true, false);
        }

        // Breached stage: Recorded in compliance audit, executive escalation alert triggered
        return (SlaEscalationStage.Breached, true, true, true);
    }

    public Task<IReadOnlyList<TicketSlaProjectionDto>> GetMonitoredTicketsAsync(string? riskFilter = null, CancellationToken ct = default)
    {
        var nowUtc = DateTime.UtcNow;
        var results = new List<TicketSlaProjectionDto>();

        foreach (var ticket in _tickets.Values)
        {
            var projection = MapToProjection(ticket, nowUtc);
            if (string.IsNullOrWhiteSpace(riskFilter) || riskFilter.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                results.Add(projection);
            }
            else if (Enum.TryParse<SlaRiskLevel>(riskFilter, true, out var parsedRisk) && projection.RiskLevel == parsedRisk)
            {
                results.Add(projection);
            }
            else if (Enum.TryParse<SlaEscalationStage>(riskFilter, true, out var parsedStage) && projection.EscalationStage == parsedStage)
            {
                results.Add(projection);
            }
            else if (riskFilter.Equals("imminent", StringComparison.OrdinalIgnoreCase) && projection.RiskLevel == SlaRiskLevel.ImminentBreach)
            {
                results.Add(projection);
            }
        }

        // Sort by risk severity: ImminentBreach first, then Critical, then lowest buffer
        var ordered = results
            .OrderBy(t => t.RiskLevel == SlaRiskLevel.ImminentBreach ? 0 : t.RiskLevel == SlaRiskLevel.Warning ? 1 : 2)
            .ThenBy(t => t.BufferMinutes)
            .ToList();

        return Task.FromResult<IReadOnlyList<TicketSlaProjectionDto>>(ordered);
    }

    public Task<TicketSlaProjectionDto> EvaluateTicketAsync(Guid ticketId, int? queueAhead = null, double? velocity = null, double? complexity = null, CancellationToken ct = default)
    {
        if (!_tickets.TryGetValue(ticketId, out var ticket))
        {
            throw new KeyNotFoundException($"Monitored ticket {ticketId} was not found.");
        }

        if (queueAhead.HasValue) ticket.QueueAhead = queueAhead.Value;
        if (velocity.HasValue && velocity.Value > 0) ticket.AvgAgentVelocityMinutes = velocity.Value;
        if (complexity.HasValue && complexity.Value >= 0) ticket.ComplexityWeight = complexity.Value;

        var nowUtc = DateTime.UtcNow;
        ApplyEscalationRules(ticket, nowUtc);

        return Task.FromResult(MapToProjection(ticket, nowUtc));
    }

    public Task<EscalationSweepResultDto> RunEscalationSweepAsync(CancellationToken ct = default)
    {
        var nowUtc = DateTime.UtcNow;
        _lastSweepAtUtc = nowUtc;

        int warningCount = 0;
        int criticalCount = 0;
        int autoReassignedCount = 0;
        int breachedCount = 0;

        var projections = new List<TicketSlaProjectionDto>();

        foreach (var ticket in _tickets.Values)
        {
            var previousStage = EvaluateEscalationStage(
                CalculateElapsedBusinessMinutes(ticket.CreatedAtUtc, ticket.LastEvaluatedAtUtc, ticket.Is24x7Tier),
                ticket.TargetWorkingMinutes).Stage;

            var result = ApplyEscalationRules(ticket, nowUtc);

            if (result.EscalationStage == SlaEscalationStage.Warning && previousStage != SlaEscalationStage.Warning)
                warningCount++;
            if (result.EscalationStage == SlaEscalationStage.Critical && previousStage != SlaEscalationStage.Critical)
                criticalCount++;
            if (result.AutoReassigned && ticket.AutoReassignedTo != null)
                autoReassignedCount++;
            if (result.EscalationStage == SlaEscalationStage.Breached && previousStage != SlaEscalationStage.Breached)
                breachedCount++;

            projections.Add(result);
        }

        var sweepResult = new EscalationSweepResultDto(
            SweepTimeUtc: nowUtc,
            EvaluatedCount: projections.Count,
            WarningEscalationsCount: warningCount,
            CriticalEscalationsCount: criticalCount,
            AutoReassignedCount: autoReassignedCount,
            BreachedCount: breachedCount,
            Tickets: projections.OrderBy(p => p.BufferMinutes).ToList()
        );

        return Task.FromResult(sweepResult);
    }

    public Task<TicketSlaProjectionDto> ManualReassignAsync(Guid ticketId, string targetAgentId, string targetAgentName, string reason, CancellationToken ct = default)
    {
        if (!_tickets.TryGetValue(ticketId, out var ticket))
        {
            throw new KeyNotFoundException($"Monitored ticket {ticketId} was not found.");
        }

        ticket.AssignedAgentId = targetAgentId;
        ticket.AssignedAgentName = targetAgentName;
        ticket.AssignedAgentTier = "Supervisor Reassigned (Tier-2)";
        ticket.AutoReassigned = true;
        ticket.AutoReassignedTo = $"{targetAgentName} (Manual by Supervisor: {reason})";
        ticket.LastEvaluatedAtUtc = DateTime.UtcNow;

        return Task.FromResult(MapToProjection(ticket, DateTime.UtcNow));
    }

    public Task<SlaMonitorMetricsDto> GetMetricsAsync(CancellationToken ct = default)
    {
        var nowUtc = DateTime.UtcNow;
        var allProjections = _tickets.Values.Select(t => MapToProjection(t, nowUtc)).ToList();

        int total = allProjections.Count;
        int imminent = allProjections.Count(p => p.RiskLevel == SlaRiskLevel.ImminentBreach);
        int critical = allProjections.Count(p => p.EscalationStage == SlaEscalationStage.Critical);
        int warning = allProjections.Count(p => p.EscalationStage == SlaEscalationStage.Warning);
        int breached = allProjections.Count(p => p.EscalationStage == SlaEscalationStage.Breached);
        int safe = allProjections.Count(p => p.RiskLevel == SlaRiskLevel.Safe && p.EscalationStage == SlaEscalationStage.Nominal);

        var firstResponseTickets = allProjections.Where(p => p.TargetType == SlaMetricTarget.FirstResponse).ToList();
        double frtCompliance = firstResponseTickets.Count > 0
            ? Math.Round((double)firstResponseTickets.Count(p => p.IsFirstResponseCompliant) / firstResponseTickets.Count * 100.0, 1)
            : 100.0;

        var resolutionTickets = allProjections.Where(p => p.TargetType == SlaMetricTarget.Resolution).ToList();
        double resCompliance = resolutionTickets.Count > 0
            ? Math.Round((double)resolutionTickets.Count(p => p.IsResolutionCompliant) / resolutionTickets.Count * 100.0, 1)
            : 100.0;

        double avgVelocity = allProjections.Count > 0
            ? Math.Round(allProjections.Average(p => p.AvgAgentVelocityMinutes), 3)
            : 0.05;

        var metrics = new SlaMonitorMetricsDto(
            FirstResponseCompliancePercentage: frtCompliance,
            ResolutionCompliancePercentage: resCompliance,
            TotalMonitoredTickets: total,
            ImminentBreachesCount: imminent,
            CriticalCount: critical,
            WarningCount: warning,
            SafeCount: safe,
            BreachedCount: breached,
            ActiveQueueVelocity: avgVelocity,
            LastSweepAtUtc: _lastSweepAtUtc
        );

        return Task.FromResult(metrics);
    }

    private TicketSlaProjectionDto ApplyEscalationRules(MonitoredTicketState ticket, DateTime nowUtc)
    {
        ticket.LastEvaluatedAtUtc = nowUtc;
        var elapsed = CalculateElapsedBusinessMinutes(ticket.CreatedAtUtc, nowUtc, ticket.Is24x7Tier);
        var escalation = EvaluateEscalationStage(elapsed, ticket.TargetWorkingMinutes);

        if (escalation.PriorityBoosted)
        {
            ticket.PriorityBoosted = true;
            if (ticket.Priority == SlaPriorityTier.Standard)
                ticket.Priority = SlaPriorityTier.High;
            else if (ticket.Priority == SlaPriorityTier.High)
                ticket.Priority = SlaPriorityTier.Urgent;
        }

        // Auto-reassign to Senior Tier-2 on Critical or Breached stage if not already reassigned
        if (escalation.NeedsAutoReassignment && !ticket.AutoReassigned)
        {
            var bestSpecialist = SelectLowestQueueSeniorSpecialist();
            if (bestSpecialist != null)
            {
                ticket.AssignedAgentId = bestSpecialist.Id;
                ticket.AssignedAgentName = bestSpecialist.Name;
                ticket.AssignedAgentTier = bestSpecialist.Tier;
                ticket.AutoReassigned = true;
                ticket.AutoReassignedTo = bestSpecialist.Name;
                // Decrement queue ahead since senior specialist handles it directly
                ticket.QueueAhead = Math.Max(0, ticket.QueueAhead - 1);
            }
        }

        if (escalation.TriggerExecutiveAlert)
        {
            ticket.ExecutiveAlert = $"Executive SLA Breach Escalation: SLA target of {ticket.TargetWorkingMinutes}m exceeded by {(elapsed - ticket.TargetWorkingMinutes):F1}m at {nowUtc:yyyy-MM-dd HH:mm:ss} UTC.";
        }

        return MapToProjection(ticket, nowUtc);
    }

    private SeniorSpecialistAgent? SelectLowestQueueSeniorSpecialist()
    {
        lock (_lock)
        {
            if (_seniorSpecialists.Count == 0) return null;
            var lowest = _seniorSpecialists.OrderBy(s => s.ActiveQueueCount).First();
            // Increment assigned queue count
            int index = _seniorSpecialists.IndexOf(lowest);
            _seniorSpecialists[index] = lowest with { ActiveQueueCount = lowest.ActiveQueueCount + 1 };
            return _seniorSpecialists[index];
        }
    }

    private TicketSlaProjectionDto MapToProjection(MonitoredTicketState ticket, DateTime nowUtc)
    {
        var dueUtc = CalculateDueDate(ticket.CreatedAtUtc, ticket.TargetWorkingMinutes, ticket.Is24x7Tier);
        var elapsedMinutes = CalculateElapsedBusinessMinutes(ticket.CreatedAtUtc, nowUtc, ticket.Is24x7Tier);
        var remainingMinutes = CalculateRemainingBusinessMinutes(nowUtc, dueUtc, ticket.Is24x7Tier);
        var percentage = Math.Round((elapsedMinutes / ticket.TargetWorkingMinutes) * 100.0, 1);

        var (predictedUtc, bufferMinutes, riskLevel) = ProjectVelocityToBreach(
            nowUtc,
            dueUtc,
            ticket.QueueAhead,
            ticket.AvgAgentVelocityMinutes,
            ticket.ComplexityWeight
        );

        var escalation = EvaluateEscalationStage(elapsedMinutes, ticket.TargetWorkingMinutes);

        // Compliance checks
        bool isFirstResponseCompliant = ticket.TargetType == SlaMetricTarget.FirstResponse
            ? (ticket.FirstRespondedAtUtc != null
                ? CalculateElapsedBusinessMinutes(ticket.CreatedAtUtc, ticket.FirstRespondedAtUtc.Value, ticket.Is24x7Tier) <= ticket.TargetWorkingMinutes
                : elapsedMinutes < ticket.TargetWorkingMinutes)
            : true;

        bool isResolutionCompliant = ticket.TargetType == SlaMetricTarget.Resolution
            ? (ticket.ResolvedAtUtc != null
                ? CalculateElapsedBusinessMinutes(ticket.CreatedAtUtc, ticket.ResolvedAtUtc.Value, ticket.Is24x7Tier) <= ticket.TargetWorkingMinutes
                : elapsedMinutes < ticket.TargetWorkingMinutes)
            : true;

        return new TicketSlaProjectionDto(
            TicketId: ticket.TicketId,
            TicketNumber: ticket.TicketNumber,
            Title: ticket.Title,
            CustomerBrand: ticket.CustomerBrand,
            AssignedAgentId: ticket.AssignedAgentId,
            AssignedAgentName: ticket.AssignedAgentName,
            AssignedAgentTier: ticket.AssignedAgentTier,
            Priority: ticket.Priority,
            TargetType: ticket.TargetType,
            QueueAhead: ticket.QueueAhead,
            ComplexityWeight: ticket.ComplexityWeight,
            AvgAgentVelocityMinutes: ticket.AvgAgentVelocityMinutes,
            CreatedAtUtc: ticket.CreatedAtUtc,
            DueAtUtc: dueUtc,
            PredictedCompletionAtUtc: predictedUtc,
            TargetWorkingMinutes: ticket.TargetWorkingMinutes,
            ElapsedWorkingMinutes: elapsedMinutes,
            RemainingWorkingMinutes: remainingMinutes,
            PercentageElapsed: percentage,
            BufferMinutes: bufferMinutes,
            EscalationStage: escalation.Stage,
            RiskLevel: riskLevel,
            Is24x7Tier: ticket.Is24x7Tier,
            PriorityBoosted: ticket.PriorityBoosted,
            AutoReassigned: ticket.AutoReassigned,
            AutoReassignedTo: ticket.AutoReassignedTo,
            ExecutiveAlert: ticket.ExecutiveAlert,
            LastEvaluatedAtUtc: ticket.LastEvaluatedAtUtc,
            FirstRespondedAtUtc: ticket.FirstRespondedAtUtc,
            ResolvedAtUtc: ticket.ResolvedAtUtc,
            IsFirstResponseCompliant: isFirstResponseCompliant,
            IsResolutionCompliant: isResolutionCompliant
        );
    }

    private DateTime GetRecentBusinessAnchor(DateTime nowUtc)
    {
        var current = nowUtc;
        while (current.DayOfWeek == DayOfWeek.Saturday || current.DayOfWeek == DayOfWeek.Sunday)
        {
            current = current.AddDays(-1);
        }

        var windowStart = current.Date.Add(_businessHoursStart.ToTimeSpan());
        var windowEnd = current.Date.Add(_businessHoursEnd.ToTimeSpan());

        if (nowUtc > windowEnd)
        {
            return windowEnd;
        }
        else if (nowUtc < windowStart)
        {
            var prev = current.AddDays(-1);
            while (prev.DayOfWeek == DayOfWeek.Saturday || prev.DayOfWeek == DayOfWeek.Sunday)
            {
                prev = prev.AddDays(-1);
            }
            return prev.Date.Add(_businessHoursEnd.ToTimeSpan());
        }
        else
        {
            return nowUtc;
        }
    }

    public void ResetToSeedData()
    {
        _tickets.Clear();
        var now = DateTime.UtcNow;
        var anchor = GetRecentBusinessAnchor(now);

        // Seed 1: Critical (88% elapsed), 24/7 Tier, SAML SSO issue -> Auto-reassignment to Tier 2
        var id1 = Guid.Parse("11111111-1111-1111-1111-111111111101");
        _tickets[id1] = new MonitoredTicketState
        {
            TicketId = id1,
            TicketNumber = "TICK-4091",
            Title = "Enterprise SSO SAML Certificate Expiration in Production",
            CustomerBrand = "Acme Financial Services",
            AssignedAgentId = "agent-101",
            AssignedAgentName = "Dave Miller",
            AssignedAgentTier = "Tier-1 Support",
            Priority = SlaPriorityTier.Critical24x7,
            TargetType = SlaMetricTarget.FirstResponse,
            QueueAhead = 2,
            ComplexityWeight = 0.3,
            AvgAgentVelocityMinutes = 0.05,
            CreatedAtUtc = now.AddMinutes(-53),
            TargetWorkingMinutes = 60,
            Is24x7Tier = true,
            PriorityBoosted = true,
            AutoReassigned = true,
            AutoReassignedTo = "Elena Vance (Principal SRE / Incident Lead)",
            LastEvaluatedAtUtc = now
        };

        // Seed 2: Warning (77% elapsed), High Priority, Stripe webhook
        var id2 = Guid.Parse("11111111-1111-1111-1111-111111111102");
        _tickets[id2] = new MonitoredTicketState
        {
            TicketId = id2,
            TicketNumber = "TICK-4092",
            Title = "Stripe Webhook Retries Exhausted - Payment Captures Stalled",
            CustomerBrand = "GlobalCorp Logistics",
            AssignedAgentId = "agent-102",
            AssignedAgentName = "Jessica Alba",
            AssignedAgentTier = "Tier-1 Support",
            Priority = SlaPriorityTier.High,
            TargetType = SlaMetricTarget.Resolution,
            QueueAhead = 1,
            ComplexityWeight = 0.15,
            AvgAgentVelocityMinutes = 0.05,
            CreatedAtUtc = anchor.AddMinutes(-92),
            TargetWorkingMinutes = 120,
            Is24x7Tier = false,
            PriorityBoosted = true,
            AutoReassigned = false,
            LastEvaluatedAtUtc = now
        };

        // Seed 3: Nominal (18% elapsed), Standard Priority, Custom CNAME SSL
        var id3 = Guid.Parse("11111111-1111-1111-1111-111111111103");
        _tickets[id3] = new MonitoredTicketState
        {
            TicketId = id3,
            TicketNumber = "TICK-4093",
            Title = "Custom CNAME Domain SSL Handshake Timeout",
            CustomerBrand = "Nordic Retail Group",
            AssignedAgentId = "agent-103",
            AssignedAgentName = "Tom Davis",
            AssignedAgentTier = "Tier-1 Support",
            Priority = SlaPriorityTier.Standard,
            TargetType = SlaMetricTarget.Resolution,
            QueueAhead = 0,
            ComplexityWeight = 0.1,
            AvgAgentVelocityMinutes = 0.06,
            CreatedAtUtc = anchor.AddMinutes(-43),
            TargetWorkingMinutes = 240,
            Is24x7Tier = false,
            PriorityBoosted = false,
            AutoReassigned = false,
            LastEvaluatedAtUtc = now
        };

        // Seed 4: Breached (111% elapsed), Standard, CSV Data Export Hang
        var id4 = Guid.Parse("11111111-1111-1111-1111-111111111104");
        _tickets[id4] = new MonitoredTicketState
        {
            TicketId = id4,
            TicketNumber = "TICK-4094",
            Title = "Data Export CSV Stream Hanging on Large Datasets (>100k rows)",
            CustomerBrand = "Apex MedTech",
            AssignedAgentId = "agent-104",
            AssignedAgentName = "Maya Lin",
            AssignedAgentTier = "Tier-1 Support",
            Priority = SlaPriorityTier.Standard,
            TargetType = SlaMetricTarget.Resolution,
            QueueAhead = 3,
            ComplexityWeight = 0.25,
            AvgAgentVelocityMinutes = 0.04,
            CreatedAtUtc = anchor.AddMinutes(-200),
            TargetWorkingMinutes = 180,
            Is24x7Tier = false,
            PriorityBoosted = true,
            AutoReassigned = true,
            AutoReassignedTo = "Alexei Morozov (Staff Support Engineer)",
            ExecutiveAlert = "Executive SLA Breach Escalation: SLA target of 180m exceeded.",
            LastEvaluatedAtUtc = now
        };

        // Seed 5: Critical (89% elapsed), High, Autonomous Funnel Step Failure
        var id5 = Guid.Parse("11111111-1111-1111-1111-111111111105");
        _tickets[id5] = new MonitoredTicketState
        {
            TicketId = id5,
            TicketNumber = "TICK-4095",
            Title = "Autonomous Funnel Step Action Not Firing for VIP Cohort",
            CustomerBrand = "Zenith OmniBrands",
            AssignedAgentId = "agent-105",
            AssignedAgentName = "Liam Johnson",
            AssignedAgentTier = "Tier-1 Support",
            Priority = SlaPriorityTier.High,
            TargetType = SlaMetricTarget.Resolution,
            QueueAhead = 3,
            ComplexityWeight = 0.4,
            AvgAgentVelocityMinutes = 0.05,
            CreatedAtUtc = anchor.AddMinutes(-107),
            TargetWorkingMinutes = 120,
            Is24x7Tier = false,
            PriorityBoosted = true,
            AutoReassigned = true,
            AutoReassignedTo = "Sarah Chen (Senior Escalation Specialist)",
            LastEvaluatedAtUtc = now
        };

        // Seed 6: Nominal (12% elapsed), Standard, WhatsApp Cloud session refresh
        var id6 = Guid.Parse("11111111-1111-1111-1111-111111111106");
        _tickets[id6] = new MonitoredTicketState
        {
            TicketId = id6,
            TicketNumber = "TICK-4096",
            Title = "WhatsApp Cloud Session Refresh Authentication Error",
            CustomerBrand = "Starlight Digital",
            AssignedAgentId = "agent-106",
            AssignedAgentName = "Chloe Martin",
            AssignedAgentTier = "Tier-1 Support",
            Priority = SlaPriorityTier.Standard,
            TargetType = SlaMetricTarget.FirstResponse,
            QueueAhead = 1,
            ComplexityWeight = 0.0,
            AvgAgentVelocityMinutes = 0.07,
            CreatedAtUtc = anchor.AddMinutes(-36),
            TargetWorkingMinutes = 300,
            Is24x7Tier = false,
            PriorityBoosted = false,
            AutoReassigned = false,
            LastEvaluatedAtUtc = now
        };
    }
}
