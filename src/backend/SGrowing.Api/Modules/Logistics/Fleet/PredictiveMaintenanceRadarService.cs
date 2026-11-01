namespace SGrowing.Api.Modules.Logistics.Fleet;

/// <summary>
/// Service implementing 2-parameter Weibull hazard distributions for predictive vehicle component wear
/// and autonomous maintenance work order synthesis.
/// </summary>
public sealed class PredictiveMaintenanceRadarService : IPredictiveMaintenanceRadarService
{
    private readonly Lock _lock = new();
    private readonly List<MaintenanceWorkOrder> _workOrders = [];

    // Weibull parameters per specification
    private const double BrakePadsBeta = 2.8;
    private const double BrakePadsEta = 40000.0;
    private const decimal BrakePadsEstimatedCostDzd = 18000m;

    private const double EngineOilBeta = 2.2;
    private const double EngineOilBaseEta = 10000.0;
    private const decimal EngineOilEstimatedCostDzd = 9500m;

    private const double TiresBeta = 3.2;
    private const double TiresEta = 50000.0;
    private const decimal TiresEstimatedCostDzd = 45000m;

    private const double DefaultBeta = 2.5;
    private const double DefaultEta = 30000.0;
    private const decimal DefaultCostDzd = 15000m;

    // Thresholds for autonomous work order triggering
    private const double CriticalFailureProbabilityPct = 75.0;
    private const double CriticalRulDistanceKm = 1500.0;
    private const double UpcomingFailureProbabilityPct = 40.0;
    private const double UpcomingRulDistanceKm = 5000.0;

    /// <inheritdoc />
    public WeibullRulEstimate EstimateComponentRul(
        string vehicleId,
        string component,
        double currentKm,
        double ambientTempCelsius = 25.0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(vehicleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(component);

        if (currentKm < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(currentKm), "Current odometer reading cannot be negative.");
        }

        var (beta, eta, estCost) = ResolveWeibullParameters(component, ambientTempCelsius);

        // Cumulative failure probability: F(t) = 1 - exp(- (t / eta)^beta)
        var failureProb = 1.0 - Math.Exp(-Math.Pow(currentKm / eta, beta));
        var failureProbPct = Math.Round(Math.Clamp(failureProb * 100.0, 0.0, 100.0), 2);

        var expectedRemainingKm = Math.Round(Math.Max(0.0, eta - currentKm), 1);

        MaintenanceUrgency urgency;
        string recommendedAction;

        if (failureProbPct >= CriticalFailureProbabilityPct || expectedRemainingKm < CriticalRulDistanceKm)
        {
            urgency = MaintenanceUrgency.ImmediateActionRequired;
            recommendedAction = $"Critical: Immediate service required for {component}. Failure probability is {failureProbPct:F1}% (RUL: {expectedRemainingKm:F0} km).";

            // Trigger autonomous work order if none exists
            EnsureAutonomousWorkOrder(vehicleId, component, urgency, estCost, failureProbPct, expectedRemainingKm);
        }
        else if (failureProbPct >= UpcomingFailureProbabilityPct || expectedRemainingKm < UpcomingRulDistanceKm)
        {
            urgency = MaintenanceUrgency.Upcoming;
            recommendedAction = $"Upcoming: Plan service for {component} in the next scheduled maintenance window (RUL: {expectedRemainingKm:F0} km).";
        }
        else
        {
            urgency = MaintenanceUrgency.Routine;
            recommendedAction = $"Routine: {component} is operating within nominal parameters (RUL: {expectedRemainingKm:F0} km).";
        }

        return new WeibullRulEstimate(
            VehicleId: vehicleId,
            ComponentName: component,
            CurrentOdometerKm: currentKm,
            ExpectedRemainingKm: expectedRemainingKm,
            FailureProbabilityPct: failureProbPct,
            BetaShape: beta,
            EtaScale: eta,
            Urgency: urgency,
            RecommendedAction: recommendedAction);
    }

    /// <inheritdoc />
    public IReadOnlyList<WeibullRulEstimate> GenerateMaintenanceForecast(
        string vehicleId,
        double currentKm,
        double ambientTempCelsius = 25.0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(vehicleId);

        var components = new[] { "BrakePads", "EngineOil", "Tires" };
        var estimates = new List<WeibullRulEstimate>(components.Length);

        foreach (var comp in components)
        {
            estimates.Add(EstimateComponentRul(vehicleId, comp, currentKm, ambientTempCelsius));
        }

        return estimates;
    }

    /// <inheritdoc />
    public MaintenanceWorkOrder CreateWorkOrder(CreateWorkOrderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.VehicleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Component);

        if (request.EstimatedCostDzd < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request.EstimatedCostDzd), "Estimated cost cannot be negative.");
        }

        var workOrderId = $"WO-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var workOrder = new MaintenanceWorkOrder(
            WorkOrderId: workOrderId,
            VehicleId: request.VehicleId,
            Component: request.Component,
            Urgency: request.Urgency,
            Status: WorkOrderStatus.Pending,
            EstimatedCostDzd: request.EstimatedCostDzd,
            CreatedAtUtc: DateTime.UtcNow,
            Notes: request.Notes);

        lock (_lock)
        {
            _workOrders.Add(workOrder);
        }

        return workOrder;
    }

    /// <inheritdoc />
    public IReadOnlyList<MaintenanceWorkOrder> GetWorkOrdersForVehicle(string vehicleId)
    {
        if (string.IsNullOrWhiteSpace(vehicleId))
        {
            return [];
        }

        lock (_lock)
        {
            return _workOrders
                .Where(w => string.Equals(w.VehicleId, vehicleId, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(w => w.CreatedAtUtc)
                .ToList();
        }
    }

    /// <inheritdoc />
    public MaintenanceWorkOrder? GetWorkOrder(string workOrderId)
    {
        if (string.IsNullOrWhiteSpace(workOrderId))
        {
            return null;
        }

        lock (_lock)
        {
            return _workOrders.FirstOrDefault(w => string.Equals(w.WorkOrderId, workOrderId, StringComparison.OrdinalIgnoreCase));
        }
    }

    private void EnsureAutonomousWorkOrder(
        string vehicleId,
        string component,
        MaintenanceUrgency urgency,
        decimal estimatedCostDzd,
        double failureProbPct,
        double rulKm)
    {
        lock (_lock)
        {
            var hasActiveOrder = _workOrders.Any(w =>
                string.Equals(w.VehicleId, vehicleId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(w.Component, component, StringComparison.OrdinalIgnoreCase) &&
                w.Status is WorkOrderStatus.Pending or WorkOrderStatus.Scheduled or WorkOrderStatus.InProgress);

            if (!hasActiveOrder)
            {
                var workOrderId = $"WO-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
                var autoOrder = new MaintenanceWorkOrder(
                    WorkOrderId: workOrderId,
                    VehicleId: vehicleId,
                    Component: component,
                    Urgency: urgency,
                    Status: WorkOrderStatus.Pending,
                    EstimatedCostDzd: estimatedCostDzd,
                    CreatedAtUtc: DateTime.UtcNow,
                    Notes: $"Autonomous Radar Trigger: {component} exceeded risk limits ({failureProbPct:F1}% failure prob, {rulKm:F0} km RUL).");

                _workOrders.Add(autoOrder);
            }
        }
    }

    private static (double Beta, double Eta, decimal EstimatedCost) ResolveWeibullParameters(
        string component,
        double ambientTempCelsius)
    {
        var normalized = component.Replace(" ", "").ToLowerInvariant();

        return normalized switch
        {
            "brakepads" or "brakes" => (BrakePadsBeta, BrakePadsEta, BrakePadsEstimatedCostDzd),
            "engineoil" or "oil" => CalculateEngineOilParameters(ambientTempCelsius),
            "tires" or "tire" => (TiresBeta, TiresEta, TiresEstimatedCostDzd),
            _ => (DefaultBeta, DefaultEta, DefaultCostDzd)
        };
    }

    private static (double Beta, double Eta, decimal EstimatedCost) CalculateEngineOilParameters(double ambientTempCelsius)
    {
        var effectiveEta = EngineOilBaseEta;

        // Thermal shear degradation acceleration in extreme heat (> 38°C)
        if (ambientTempCelsius > 38.0)
        {
            var tempOverage = ambientTempCelsius - 38.0;
            effectiveEta /= (1.0 + 0.025 * tempOverage);
        }
        // Cold start degradation (< 0°C)
        else if (ambientTempCelsius < 0.0)
        {
            effectiveEta *= 0.90;
        }

        return (EngineOilBeta, Math.Round(effectiveEta, 1), EngineOilEstimatedCostDzd);
    }
}
