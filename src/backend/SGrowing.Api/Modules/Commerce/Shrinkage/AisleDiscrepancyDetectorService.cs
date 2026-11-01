using System.Threading;

namespace SGrowing.Api.Modules.Commerce.Shrinkage;

/// <summary>
/// Autonomous detector service for identifying inventory rack misplacements,
/// unauthorized aisle relocations, and planogram discrepancy deviations.
/// </summary>
public sealed class AisleDiscrepancyDetectorService(IRfidBleIndoorPositioningEngine positioningEngine)
    : IAisleDiscrepancyDetectorService
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, AisleDiscrepancyIncident> _activeIncidentsByTag = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, AisleDiscrepancyIncident> _incidentsById = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public (double X, double Y) GetZoneCenter(WarehouseZone zone) => zone switch
    {
        WarehouseZone.HighValueAisle => (17.5, 22.5),
        WarehouseZone.BulkStorageRack => (60.0, 22.5),
        WarehouseZone.PackingStation => (25.0, 65.0),
        WarehouseZone.QuarantineZone => (67.5, 65.0),
        WarehouseZone.DockExitGate => (95.0, 95.0),
        _ => (50.0, 50.0)
    };

    /// <inheritdoc />
    public AisleDiscrepancyIncident? AuditAssetPlacement(
        string tagId,
        string sku,
        WarehouseZone expectedZone,
        bool hasActivePickTask)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tagId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);

        var location = positioningEngine.GetAssetLocation(tagId);
        if (location == null)
        {
            return null;
        }

        var actualZone = location.Zone;

        // If actual zone matches planogram expected zone, there is no misplacement
        if (actualZone == expectedZone)
        {
            lock (_lock)
            {
                if (_activeIncidentsByTag.Remove(tagId, out var existing))
                {
                    _incidentsById[existing.IncidentId] = existing with
                    {
                        IsResolved = true,
                        ResolvedAtUtc = DateTime.UtcNow,
                        ResolutionNotes = "Asset automatically verified inside expected planogram zone."
                    };
                }
            }
            return null;
        }

        // Misplacement or unauthorized movement detected
        var (expX, expY) = GetZoneCenter(expectedZone);
        double deviation = Math.Round(Math.Sqrt(Math.Pow(location.X - expX, 2) + Math.Pow(location.Y - expY, 2)), 2);

        var severity = actualZone == WarehouseZone.DockExitGate && !hasActivePickTask
            ? ShrinkageAlertSeverity.CriticalTheftAlert
            : !hasActivePickTask
                ? ShrinkageAlertSeverity.SuspiciousMovement
                : ShrinkageAlertSeverity.InformationalMisplacement;

        string incidentId = $"DISC-{Guid.NewGuid():N}"[..13].ToUpperInvariant();
        var incident = new AisleDiscrepancyIncident(
            IncidentId: incidentId,
            TagId: tagId,
            Sku: sku,
            ExpectedZone: expectedZone,
            ActualZone: actualZone,
            DistanceDeviationMeters: deviation,
            HasActivePickTask: hasActivePickTask,
            DetectedAtUtc: DateTime.UtcNow,
            Severity: severity,
            IsResolved: false);

        lock (_lock)
        {
            _activeIncidentsByTag[tagId] = incident;
            _incidentsById[incidentId] = incident;
        }

        return incident;
    }

    /// <inheritdoc />
    public IReadOnlyList<AisleDiscrepancyIncident> GetActiveDiscrepancies()
    {
        lock (_lock)
        {
            return [.. _activeIncidentsByTag.Values];
        }
    }

    /// <inheritdoc />
    public bool ResolveDiscrepancy(string incidentId, string? notes = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(incidentId);

        lock (_lock)
        {
            if (!_incidentsById.TryGetValue(incidentId, out var incident))
            {
                return false;
            }

            if (incident.IsResolved)
            {
                return true;
            }

            var resolved = incident with
            {
                IsResolved = true,
                ResolvedAtUtc = DateTime.UtcNow,
                ResolutionNotes = notes ?? "Resolved by warehouse inventory supervisor."
            };

            _incidentsById[incidentId] = resolved;
            if (_activeIncidentsByTag.TryGetValue(incident.TagId, out var active) && active.IncidentId == incidentId)
            {
                _activeIncidentsByTag.Remove(incident.TagId);
            }

            return true;
        }
    }

    /// <inheritdoc />
    public AuditDiscrepanciesResponse AuditAllTrackedAssets(
        Func<string, (WarehouseZone ExpectedZone, bool HasPickTask)> planogramResolver)
    {
        ArgumentNullException.ThrowIfNull(planogramResolver);

        var allAssets = positioningEngine.GetAllTrackedAssets();

        foreach (var (tagId, (_, sku, _)) in allAssets)
        {
            var (expectedZone, hasPickTask) = planogramResolver(tagId);
            AuditAssetPlacement(tagId, sku, expectedZone, hasPickTask);
        }

        var active = GetActiveDiscrepancies();
        return new AuditDiscrepanciesResponse(
            HasDiscrepancies: active.Count > 0,
            Incidents: active,
            TotalAudited: allAssets.Count,
            TimestampUtc: DateTime.UtcNow);
    }
}
