using System.Threading;

namespace SGrowing.Api.Modules.Commerce.Shrinkage;

/// <summary>
/// Real-time dock perimeter theft interception service.
/// Detects unallocated items entering loading dock exit gates and coordinates security alerts.
/// </summary>
public sealed class DockShrinkageAlertService : IDockShrinkageAlertService
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, DockShrinkageSecurityAlert> _alertsById = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DockShrinkageSecurityAlert> _activeAlertsByTag = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public DockShrinkageSecurityAlert? EvaluateDockPerimeter(
        string tagId,
        string sku,
        IndoorLocationPoint location,
        decimal itemValueDzd)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tagId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);
        ArgumentNullException.ThrowIfNull(location);

        // Check if inside dock exit gate perimeter (X >= 90m and Y >= 90m or zone is DockExitGate)
        bool isInDockZone = location.IsInDockExitPerimeter() || location.Zone == WarehouseZone.DockExitGate;
        if (!isInDockZone)
        {
            return null;
        }

        lock (_lock)
        {
            // If an active alarm already exists for this tag, update its trigger location and return
            if (_activeAlertsByTag.TryGetValue(tagId, out var existing))
            {
                var updated = existing with
                {
                    TriggerLocation = location,
                    InterceptNotes = $"{existing.InterceptNotes} | Updated breach coordinates: ({location.X:F1}m, {location.Y:F1}m)"
                };
                _alertsById[existing.AlertId] = updated;
                _activeAlertsByTag[tagId] = updated;
                return updated;
            }

            string dockGateId = location.X >= 95.0 ? "DOCK-GATE-01" : "DOCK-GATE-02";
            string alertId = $"ALERT-DCK-{Guid.NewGuid():N}"[..18].ToUpperInvariant();
            string notes = $"PERIMETER BREACH: Tag {tagId} (SKU: {sku}) valued at {itemValueDzd:N2} DZD breached dock perimeter at ({location.X:F1}m, {location.Y:F1}m). Immediate security intercept triggered.";

            var alert = new DockShrinkageSecurityAlert(
                AlertId: alertId,
                TagId: tagId,
                Sku: sku,
                ItemValueDzd: itemValueDzd,
                DockGateId: dockGateId,
                Status: SecurityInterceptStatus.ActiveAlarm,
                EmittedAtUtc: DateTime.UtcNow,
                InterceptNotes: notes,
                Severity: ShrinkageAlertSeverity.CriticalTheftAlert,
                TriggerLocation: location);

            _alertsById[alertId] = alert;
            _activeAlertsByTag[tagId] = alert;

            return alert;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<DockShrinkageSecurityAlert> GetActiveSecurityAlerts()
    {
        lock (_lock)
        {
            return [.. _activeAlertsByTag.Values];
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<DockShrinkageSecurityAlert> GetAllSecurityAlerts()
    {
        lock (_lock)
        {
            return [.. _alertsById.Values];
        }
    }

    /// <inheritdoc />
    public bool AcknowledgeAlert(
        string alertId,
        SecurityInterceptStatus newStatus,
        string? notes = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(alertId);

        lock (_lock)
        {
            if (!_alertsById.TryGetValue(alertId, out var alert))
            {
                return false;
            }

            string updatedNotes = string.IsNullOrWhiteSpace(notes)
                ? alert.InterceptNotes
                : $"{alert.InterceptNotes} | [Status: {newStatus}] {notes}";

            var updatedAlert = alert with
            {
                Status = newStatus,
                InterceptNotes = updatedNotes
            };

            _alertsById[alertId] = updatedAlert;

            if (newStatus is SecurityInterceptStatus.FalsePositiveCleared or SecurityInterceptStatus.Recovered)
            {
                if (_activeAlertsByTag.TryGetValue(alert.TagId, out var active) && active.AlertId == alertId)
                {
                    _activeAlertsByTag.Remove(alert.TagId);
                }
            }
            else
            {
                _activeAlertsByTag[alert.TagId] = updatedAlert;
            }

            return true;
        }
    }

    /// <inheritdoc />
    public decimal GetTotalAtRiskValueDzd()
    {
        lock (_lock)
        {
            decimal total = 0m;
            foreach (var alert in _activeAlertsByTag.Values)
            {
                total += alert.ItemValueDzd;
            }
            return total;
        }
    }
}
