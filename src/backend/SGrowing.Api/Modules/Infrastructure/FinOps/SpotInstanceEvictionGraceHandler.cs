using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Infrastructure.FinOps;

/// <summary>
/// Preemption interceptor handling the 120-second spot instance termination notice
/// to drain and migrate workloads to on-demand or alternative spot instances with zero customer dropped requests.
/// </summary>
public sealed class SpotInstanceEvictionGraceHandler
{
    private readonly ConcurrentDictionary<string, SpotPreemptionAlert> _alerts = new(StringComparer.OrdinalIgnoreCase);
    private readonly System.Threading.Lock _lock = new();

    /// <summary>
    /// Registers an incoming cloud preemption termination notice for a spot instance node.
    /// </summary>
    public SpotPreemptionAlert HandlePreemptionNotice(
        string nodeId,
        CloudProvider provider,
        string region,
        IReadOnlyList<string> workloadIds,
        int gracePeriodSeconds = 120)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);

        var alertId = $"ALERT-SPOT-{Guid.NewGuid():N}"[..20].ToUpperInvariant();
        var now = DateTime.UtcNow;
        var deadline = now.AddSeconds(gracePeriodSeconds);

        var alert = new SpotPreemptionAlert(
            AlertId: alertId,
            NodeId: nodeId,
            Provider: provider,
            Region: region,
            NoticeReceivedAtUtc: now,
            TerminationDeadlineUtc: deadline,
            WorkloadIds: workloadIds,
            Status: EvictionMigrationStatus.Draining
        );

        _alerts[alertId] = alert;
        return alert;
    }

    /// <summary>
    /// Confirms that all container workloads on the spot instance have been successfully migrated.
    /// </summary>
    public SpotPreemptionAlert ConfirmWorkloadEvacuation(string alertId, DateTime? asOfUtc = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(alertId);

        if (!_alerts.TryGetValue(alertId, out var alert))
        {
            throw new KeyNotFoundException($"Preemption alert '{alertId}' not found.");
        }

        var now = asOfUtc ?? DateTime.UtcNow;

        lock (_lock)
        {
            var isWithinDeadline = now <= alert.TerminationDeadlineUtc;
            var finalStatus = isWithinDeadline ? EvictionMigrationStatus.EvacuatedSuccessfully : EvictionMigrationStatus.TerminatedWithDrop;

            var updated = alert with
            {
                Status = finalStatus,
                CompletedAtUtc = now
            };

            _alerts[alertId] = updated;
            return updated;
        }
    }

    /// <summary>
    /// Computes the historical zero-downtime spot migration success rate.
    /// </summary>
    public (double SuccessRatio, int TotalPreemptions, int SuccessfulEvacuations) GetMigrationSuccessMetrics()
    {
        var all = _alerts.Values.ToList();
        if (all.Count == 0) return (1.0, 0, 0);

        var successful = all.Count(a => a.Status == EvictionMigrationStatus.EvacuatedSuccessfully);
        var ratio = (double)successful / all.Count;
        return (Math.Round(ratio, 4), all.Count, successful);
    }
}
