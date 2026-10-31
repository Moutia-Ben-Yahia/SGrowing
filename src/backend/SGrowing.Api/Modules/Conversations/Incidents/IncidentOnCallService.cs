using System.Collections.Concurrent;
using System.Text.Json.Serialization;

namespace SGrowing.Api.Modules.Conversations.Incidents;

#region Enums

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum IncidentSeverity
{
    P1_Critical = 1,
    P2_High = 2,
    P3_Medium = 3,
    P4_Low = 4
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum IncidentStatus
{
    Triggered = 1,
    Acknowledged = 2,
    Investigating = 3,
    Mitigated = 4,
    Resolved = 5,
    Suppressed = 6
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EscalationTier
{
    Tier1_AssignedAgent = 1,
    Tier2_OnCallLead = 2,
    Tier3_DepartmentHead = 3,
    Tier4_ExternalWebhook = 4
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PagingChannel
{
    InAppBanner,
    Webhook,
    Sms,
    VoiceCall,
    SlackTelegram
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ShiftRotationType
{
    Daily,
    Weekly,
    Custom
}

#endregion

#region Models & DTOs

public record OnCallRoster(
    Guid RosterId,
    Guid TenantId,
    string Name,
    ShiftRotationType ShiftType,
    Guid PrimaryAgentId,
    string PrimaryAgentName,
    Guid? SecondaryAgentId,
    string? SecondaryAgentName,
    DateTime StartTimeUtc,
    DateTime EndTimeUtc,
    string Timezone,
    bool IsActive
);

public record EscalationPolicy(
    Guid PolicyId,
    Guid TenantId,
    string Name,
    IncidentSeverity Severity,
    int AckTimeoutMinutes,
    List<PagingChannel> PagingChannels,
    List<EscalationTier> TargetTiers
);

public record IncidentTimelineEntry(
    DateTime TimestampUtc,
    string Actor,
    string Action,
    string Notes
);

public record IncidentRecord
{
    public Guid IncidentId { get; init; } = Guid.NewGuid();
    public Guid TenantId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public IncidentSeverity Severity { get; init; }
    public IncidentStatus Status { get; set; } = IncidentStatus.Triggered;
    public Guid? SourceConversationId { get; init; }
    public Guid? AssignedToAgentId { get; set; }
    public string? AssignedToAgentName { get; set; }
    public EscalationTier CurrentTier { get; set; } = EscalationTier.Tier1_AssignedAgent;
    public DateTime TriggeredAtUtc { get; init; } = DateTime.UtcNow;
    public DateTime? AcknowledgedAtUtc { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    public DateTime AckDeadlineUtc { get; set; }
    public string? RootCause { get; set; }
    public string? ResolutionSummary { get; set; }
    public List<IncidentTimelineEntry> Timeline { get; init; } = new();
    public List<string> DispatchedPages { get; init; } = new();
}

public record TriggerIncidentRequest(
    Guid TenantId,
    string Title,
    string Description,
    IncidentSeverity Severity,
    Guid? SourceConversationId = null,
    Guid? PreferredAgentId = null,
    string? PreferredAgentName = null
);

public record CreateRosterRequest(
    Guid TenantId,
    string Name,
    ShiftRotationType ShiftType,
    Guid PrimaryAgentId,
    string PrimaryAgentName,
    Guid? SecondaryAgentId,
    string? SecondaryAgentName,
    DateTime StartTimeUtc,
    DateTime EndTimeUtc,
    string Timezone = "UTC"
);

public record CreateEscalationPolicyRequest(
    Guid TenantId,
    string Name,
    IncidentSeverity Severity,
    int AckTimeoutMinutes,
    List<PagingChannel> PagingChannels,
    List<EscalationTier> TargetTiers
);

public record IncidentMetrics(
    Guid TenantId,
    int TotalIncidents,
    int ActiveIncidents,
    int AcknowledgedCount,
    int ResolvedCount,
    double AverageMttaMinutes,
    double AverageMttrMinutes,
    int SlaBreachesCount
);

#endregion

public interface IIncidentOnCallService
{
    Task<OnCallRoster> CreateRosterAsync(CreateRosterRequest request, CancellationToken ct = default);
    Task<List<OnCallRoster>> GetActiveRostersAsync(Guid tenantId, CancellationToken ct = default);
    Task<(Guid? AgentId, string? AgentName)> GetCurrentOnCallAgentAsync(Guid tenantId, DateTime? atUtc = null, CancellationToken ct = default);

    Task<EscalationPolicy> CreateEscalationPolicyAsync(CreateEscalationPolicyRequest request, CancellationToken ct = default);
    Task<List<EscalationPolicy>> GetPoliciesAsync(Guid tenantId, CancellationToken ct = default);

    Task<IncidentRecord> TriggerIncidentAsync(TriggerIncidentRequest request, CancellationToken ct = default);
    Task<IncidentRecord> AcknowledgeIncidentAsync(Guid tenantId, Guid incidentId, Guid agentId, string agentName, CancellationToken ct = default);
    Task<IncidentRecord> UpdateIncidentStatusAsync(Guid tenantId, Guid incidentId, IncidentStatus newStatus, string actor, string notes, CancellationToken ct = default);
    Task<IncidentRecord> ResolveIncidentAsync(Guid tenantId, Guid incidentId, Guid agentId, string resolutionSummary, string rootCause, CancellationToken ct = default);

    Task<List<IncidentRecord>> ListIncidentsAsync(Guid tenantId, IncidentStatus? statusFilter = null, IncidentSeverity? severityFilter = null, CancellationToken ct = default);
    Task<IncidentRecord?> GetIncidentByIdAsync(Guid tenantId, Guid incidentId, CancellationToken ct = default);
    Task<List<IncidentRecord>> EvaluatePendingEscalationsSweepAsync(Guid tenantId, DateTime? nowUtc = null, CancellationToken ct = default);
    Task<IncidentMetrics> GetIncidentMetricsAsync(Guid tenantId, CancellationToken ct = default);
}

public class IncidentOnCallService : IIncidentOnCallService
{
    private readonly ConcurrentDictionary<Guid, IncidentRecord> _incidents = new();
    private readonly ConcurrentDictionary<Guid, OnCallRoster> _rosters = new();
    private readonly ConcurrentDictionary<Guid, EscalationPolicy> _policies = new();

    public Task<OnCallRoster> CreateRosterAsync(CreateRosterRequest request, CancellationToken ct = default)
    {
        if (request.TenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.Name)) throw new ArgumentException("Roster Name is required.", nameof(request));
        if (request.PrimaryAgentId == Guid.Empty) throw new ArgumentException("PrimaryAgentId is required.", nameof(request));

        var roster = new OnCallRoster(
            Guid.NewGuid(),
            request.TenantId,
            request.Name.Trim(),
            request.ShiftType,
            request.PrimaryAgentId,
            request.PrimaryAgentName.Trim(),
            request.SecondaryAgentId,
            request.SecondaryAgentName?.Trim(),
            request.StartTimeUtc,
            request.EndTimeUtc,
            string.IsNullOrWhiteSpace(request.Timezone) ? "UTC" : request.Timezone,
            true
        );

        _rosters[roster.RosterId] = roster;
        return Task.FromResult(roster);
    }

    public Task<List<OnCallRoster>> GetActiveRostersAsync(Guid tenantId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty) return Task.FromResult(new List<OnCallRoster>());
        var list = _rosters.Values
            .Where(r => r.TenantId == tenantId && r.IsActive)
            .OrderBy(r => r.StartTimeUtc)
            .ToList();
        return Task.FromResult(list);
    }

    public Task<(Guid? AgentId, string? AgentName)> GetCurrentOnCallAgentAsync(Guid tenantId, DateTime? atUtc = null, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty) return Task.FromResult<(Guid?, string?)>((null, null));
        var checkTime = atUtc ?? DateTime.UtcNow;

        var matching = _rosters.Values
            .Where(r => r.TenantId == tenantId && r.IsActive && r.StartTimeUtc <= checkTime && r.EndTimeUtc >= checkTime)
            .FirstOrDefault();

        if (matching != null)
        {
            return Task.FromResult<(Guid?, string?)>((matching.PrimaryAgentId, matching.PrimaryAgentName));
        }

        var fallback = _rosters.Values
            .Where(r => r.TenantId == tenantId && r.IsActive)
            .OrderByDescending(r => r.StartTimeUtc)
            .FirstOrDefault();

        if (fallback != null)
        {
            return Task.FromResult<(Guid?, string?)>((fallback.PrimaryAgentId, fallback.PrimaryAgentName));
        }

        return Task.FromResult<(Guid?, string?)>((null, null));
    }

    public Task<EscalationPolicy> CreateEscalationPolicyAsync(CreateEscalationPolicyRequest request, CancellationToken ct = default)
    {
        if (request.TenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.Name)) throw new ArgumentException("Policy Name is required.", nameof(request));
        if (request.AckTimeoutMinutes <= 0) throw new ArgumentException("AckTimeoutMinutes must be greater than zero.", nameof(request));

        var policy = new EscalationPolicy(
            Guid.NewGuid(),
            request.TenantId,
            request.Name.Trim(),
            request.Severity,
            request.AckTimeoutMinutes,
            request.PagingChannels ?? new List<PagingChannel> { PagingChannel.InAppBanner },
            request.TargetTiers ?? new List<EscalationTier> { EscalationTier.Tier1_AssignedAgent, EscalationTier.Tier2_OnCallLead }
        );

        _policies[policy.PolicyId] = policy;
        return Task.FromResult(policy);
    }

    public Task<List<EscalationPolicy>> GetPoliciesAsync(Guid tenantId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty) return Task.FromResult(new List<EscalationPolicy>());
        var list = _policies.Values.Where(p => p.TenantId == tenantId).ToList();
        return Task.FromResult(list);
    }

    public async Task<IncidentRecord> TriggerIncidentAsync(TriggerIncidentRequest request, CancellationToken ct = default)
    {
        if (request.TenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.Title)) throw new ArgumentException("Title is required.", nameof(request));

        var matchingPolicy = _policies.Values
            .FirstOrDefault(p => p.TenantId == request.TenantId && p.Severity == request.Severity);

        int timeoutMinutes = matchingPolicy?.AckTimeoutMinutes ?? request.Severity switch
        {
            IncidentSeverity.P1_Critical => 15,
            IncidentSeverity.P2_High => 30,
            IncidentSeverity.P3_Medium => 60,
            IncidentSeverity.P4_Low => 120,
            _ => 30
        };

        var (onCallAgentId, onCallAgentName) = await GetCurrentOnCallAgentAsync(request.TenantId, ct: ct);
        var assignedId = request.PreferredAgentId ?? onCallAgentId;
        var assignedName = request.PreferredAgentName ?? onCallAgentName ?? "Unassigned";

        var triggeredAt = DateTime.UtcNow;
        var deadline = triggeredAt.AddMinutes(timeoutMinutes);

        var channels = matchingPolicy?.PagingChannels ?? new List<PagingChannel>
        {
            PagingChannel.InAppBanner,
            request.Severity == IncidentSeverity.P1_Critical ? PagingChannel.Webhook : PagingChannel.SlackTelegram
        };

        var dispatchedPages = channels.Select(c => $"Dispatched {c} alert for {request.Severity} to {assignedName}").ToList();

        var incident = new IncidentRecord
        {
            TenantId = request.TenantId,
            Title = request.Title.Trim(),
            Description = request.Description?.Trim() ?? string.Empty,
            Severity = request.Severity,
            Status = IncidentStatus.Triggered,
            SourceConversationId = request.SourceConversationId,
            AssignedToAgentId = assignedId,
            AssignedToAgentName = assignedName,
            CurrentTier = EscalationTier.Tier1_AssignedAgent,
            TriggeredAtUtc = triggeredAt,
            AckDeadlineUtc = deadline,
            DispatchedPages = dispatchedPages
        };

        incident.Timeline.Add(new IncidentTimelineEntry(
            triggeredAt,
            "System",
            "IncidentTriggered",
            $"Incident triggered with severity {request.Severity}. Initial ACK deadline: {deadline:O} ({timeoutMinutes}m). Assigned to: {assignedName}."
        ));

        _incidents[incident.IncidentId] = incident;
        return incident;
    }

    public Task<IncidentRecord> AcknowledgeIncidentAsync(Guid tenantId, Guid incidentId, Guid agentId, string agentName, CancellationToken ct = default)
    {
        if (!_incidents.TryGetValue(incidentId, out var incident) || incident.TenantId != tenantId)
        {
            throw new KeyNotFoundException($"Incident {incidentId} not found for tenant {tenantId}.");
        }

        if (incident.Status == IncidentStatus.Resolved)
        {
            throw new InvalidOperationException("Cannot acknowledge an already resolved incident.");
        }

        if (incident.Status == IncidentStatus.Acknowledged)
        {
            return Task.FromResult(incident);
        }

        var now = DateTime.UtcNow;
        incident.Status = IncidentStatus.Acknowledged;
        incident.AcknowledgedAtUtc = now;
        incident.AssignedToAgentId = agentId;
        incident.AssignedToAgentName = agentName;

        incident.Timeline.Add(new IncidentTimelineEntry(
            now,
            agentName,
            "Acknowledged",
            $"Incident acknowledged by {agentName}. Escalation timer halted."
        ));

        return Task.FromResult(incident);
    }

    public Task<IncidentRecord> UpdateIncidentStatusAsync(Guid tenantId, Guid incidentId, IncidentStatus newStatus, string actor, string notes, CancellationToken ct = default)
    {
        if (!_incidents.TryGetValue(incidentId, out var incident) || incident.TenantId != tenantId)
        {
            throw new KeyNotFoundException($"Incident {incidentId} not found for tenant {tenantId}.");
        }

        var now = DateTime.UtcNow;
        incident.Status = newStatus;
        incident.Timeline.Add(new IncidentTimelineEntry(
            now,
            actor,
            $"StatusUpdated:{newStatus}",
            notes
        ));

        return Task.FromResult(incident);
    }

    public Task<IncidentRecord> ResolveIncidentAsync(Guid tenantId, Guid incidentId, Guid agentId, string resolutionSummary, string rootCause, CancellationToken ct = default)
    {
        if (!_incidents.TryGetValue(incidentId, out var incident) || incident.TenantId != tenantId)
        {
            throw new KeyNotFoundException($"Incident {incidentId} not found for tenant {tenantId}.");
        }

        if (incident.Status == IncidentStatus.Resolved)
        {
            return Task.FromResult(incident);
        }

        var now = DateTime.UtcNow;
        incident.Status = IncidentStatus.Resolved;
        incident.ResolvedAtUtc = now;
        incident.ResolutionSummary = resolutionSummary?.Trim();
        incident.RootCause = rootCause?.Trim();

        var elapsedMinutes = (now - incident.TriggeredAtUtc).TotalMinutes;

        incident.Timeline.Add(new IncidentTimelineEntry(
            now,
            $"Agent:{agentId}",
            "Resolved",
            $"Incident marked as Resolved in {elapsedMinutes:F1} minutes. Root Cause: {rootCause}. Summary: {resolutionSummary}."
        ));

        return Task.FromResult(incident);
    }

    public Task<List<IncidentRecord>> ListIncidentsAsync(Guid tenantId, IncidentStatus? statusFilter = null, IncidentSeverity? severityFilter = null, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty) return Task.FromResult(new List<IncidentRecord>());

        var query = _incidents.Values.Where(i => i.TenantId == tenantId);

        if (statusFilter.HasValue)
        {
            query = query.Where(i => i.Status == statusFilter.Value);
        }

        if (severityFilter.HasValue)
        {
            query = query.Where(i => i.Severity == severityFilter.Value);
        }

        var list = query.OrderByDescending(i => i.TriggeredAtUtc).ToList();
        return Task.FromResult(list);
    }

    public Task<IncidentRecord?> GetIncidentByIdAsync(Guid tenantId, Guid incidentId, CancellationToken ct = default)
    {
        if (_incidents.TryGetValue(incidentId, out var incident) && incident.TenantId == tenantId)
        {
            return Task.FromResult<IncidentRecord?>(incident);
        }
        return Task.FromResult<IncidentRecord?>(null);
    }

    public Task<List<IncidentRecord>> EvaluatePendingEscalationsSweepAsync(Guid tenantId, DateTime? nowUtc = null, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty) return Task.FromResult(new List<IncidentRecord>());
        var now = nowUtc ?? DateTime.UtcNow;
        var escalated = new List<IncidentRecord>();

        var unacknowledged = _incidents.Values
            .Where(i => i.TenantId == tenantId && i.Status == IncidentStatus.Triggered && i.AckDeadlineUtc <= now)
            .ToList();

        foreach (var inc in unacknowledged)
        {
            var nextTier = inc.CurrentTier switch
            {
                EscalationTier.Tier1_AssignedAgent => EscalationTier.Tier2_OnCallLead,
                EscalationTier.Tier2_OnCallLead => EscalationTier.Tier3_DepartmentHead,
                EscalationTier.Tier3_DepartmentHead => EscalationTier.Tier4_ExternalWebhook,
                _ => EscalationTier.Tier4_ExternalWebhook
            };

            inc.CurrentTier = nextTier;
            // Extend deadline by another 15 minutes for subsequent level
            inc.AckDeadlineUtc = now.AddMinutes(15);

            var pageNotice = $"AUTO-ESCALATED: Ack deadline breached. Promoted to {nextTier}. Next deadline {inc.AckDeadlineUtc:O}.";
            inc.DispatchedPages.Add(pageNotice);

            inc.Timeline.Add(new IncidentTimelineEntry(
                now,
                "EscalationDaemon",
                "TierEscalated",
                pageNotice
            ));

            escalated.Add(inc);
        }

        return Task.FromResult(escalated);
    }

    public Task<IncidentMetrics> GetIncidentMetricsAsync(Guid tenantId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty) return Task.FromResult(new IncidentMetrics(tenantId, 0, 0, 0, 0, 0, 0, 0));

        var tenantIncidents = _incidents.Values.Where(i => i.TenantId == tenantId).ToList();

        var total = tenantIncidents.Count;
        var active = tenantIncidents.Count(i => i.Status != IncidentStatus.Resolved && i.Status != IncidentStatus.Suppressed);
        var acked = tenantIncidents.Count(i => i.Status == IncidentStatus.Acknowledged || i.AcknowledgedAtUtc.HasValue);
        var resolved = tenantIncidents.Count(i => i.Status == IncidentStatus.Resolved);

        var ackTimes = tenantIncidents
            .Where(i => i.AcknowledgedAtUtc.HasValue)
            .Select(i => (i.AcknowledgedAtUtc!.Value - i.TriggeredAtUtc).TotalMinutes)
            .ToList();

        var resolveTimes = tenantIncidents
            .Where(i => i.ResolvedAtUtc.HasValue)
            .Select(i => (i.ResolvedAtUtc!.Value - i.TriggeredAtUtc).TotalMinutes)
            .ToList();

        double avgMtta = ackTimes.Count > 0 ? ackTimes.Average() : 0.0;
        double avgMttr = resolveTimes.Count > 0 ? resolveTimes.Average() : 0.0;

        var slaBreaches = tenantIncidents.Count(i => i.CurrentTier > EscalationTier.Tier1_AssignedAgent);

        return Task.FromResult(new IncidentMetrics(
            tenantId,
            total,
            active,
            acked,
            resolved,
            Math.Round(avgMtta, 2),
            Math.Round(avgMttr, 2),
            slaBreaches
        ));
    }
}
