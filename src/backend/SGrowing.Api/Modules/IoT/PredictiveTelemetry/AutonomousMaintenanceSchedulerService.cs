using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.IoT.PredictiveTelemetry;

/// <summary>
/// Autonomous maintenance dispatch coordinator that generates work orders,
/// determines urgency priorities, and reserves inventory parts before machinery fails.
/// </summary>
public sealed class AutonomousMaintenanceSchedulerService
{
    private readonly ConcurrentDictionary<string, AutonomousWorkOrder> _workOrders = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, int> _sparePartInventory = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SKU-BEARING-6205"] = 12,
        ["SKU-V-BELT-B42"] = 8,
        ["SKU-SEAL-VITON-50MM"] = 25,
        ["SKU-MOTOR-PUMP-IMPELLER"] = 4
    };

    public AutonomousWorkOrder EvaluateAndSchedule(
        AnomalyDetectionResult anomaly,
        MachineryRulEstimate rul,
        string preferredSparePartSku = "SKU-BEARING-6205")
    {
        ArgumentNullException.ThrowIfNull(anomaly);
        ArgumentNullException.ThrowIfNull(rul);

        var priority = MaintenancePriority.P3RoutineInspection;
        var leadTime = TimeSpan.FromDays(7);
        var laborHours = 2.0;

        if (anomaly.Severity == AnomalySeverity.EmergencyShutdown || rul.EstimatedRulHours < 48.0)
        {
            priority = MaintenancePriority.P1EmergencyRepair;
            leadTime = TimeSpan.FromHours(2);
            laborHours = 6.0;
        }
        else if (anomaly.Severity == AnomalySeverity.Critical || rul.EstimatedRulHours < 250.0)
        {
            priority = MaintenancePriority.P2PreventiveSchedule;
            leadTime = TimeSpan.FromDays(2);
            laborHours = 4.0;
        }

        // Auto-reserve spare part if in stock
        var autoReserved = false;
        if (_sparePartInventory.TryGetValue(preferredSparePartSku, out var currentStock) && currentStock > 0)
        {
            _sparePartInventory[preferredSparePartSku] = currentStock - 1;
            autoReserved = true;
        }

        var workOrderId = $"WO-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var order = new AutonomousWorkOrder(
            WorkOrderId: workOrderId,
            MachineId: anomaly.MachineId,
            Priority: priority,
            RequiredSparePartSku: preferredSparePartSku,
            QuantityNeeded: 1,
            ScheduledWindowUtc: DateTime.UtcNow.Add(leadTime),
            EstimatedLaborHours: laborHours,
            FailureModeHypothesis: $"{anomaly.DominantMetricAnomaly} fatigue / wear-out ({rul.RecommendedAction})",
            AutoReservedSparePart: autoReserved,
            CreatedAtUtc: DateTime.UtcNow
        );

        _workOrders[workOrderId] = order;
        return order;
    }

    public IReadOnlyList<AutonomousWorkOrder> GetActiveWorkOrders() =>
        _workOrders.Values.ToList();

    public int GetSparePartStock(string sku) =>
        _sparePartInventory.TryGetValue(sku, out var stock) ? stock : 0;
}
