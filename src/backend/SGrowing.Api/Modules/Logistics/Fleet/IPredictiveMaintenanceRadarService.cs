namespace SGrowing.Api.Modules.Logistics.Fleet;

/// <summary>
/// Predictive vehicle maintenance service based on 2-parameter Weibull reliability hazard modeling.
/// </summary>
public interface IPredictiveMaintenanceRadarService
{
    /// <summary>
    /// Computes the Remaining Useful Life (RUL) and cumulative failure probability for a specific vehicle component.
    /// Automatically triggers a maintenance work order if failure probability exceeds 75% or RUL is less than 1,500 km.
    /// </summary>
    WeibullRulEstimate EstimateComponentRul(
        string vehicleId,
        string component,
        double currentKm,
        double ambientTempCelsius = 25.0);

    /// <summary>
    /// Generates a comprehensive predictive maintenance forecast across all key wear components (Brake Pads, Engine Oil, Tires).
    /// </summary>
    IReadOnlyList<WeibullRulEstimate> GenerateMaintenanceForecast(string vehicleId, double currentKm, double ambientTempCelsius = 25.0);

    /// <summary>
    /// Manually or programmatically creates a maintenance work order.
    /// </summary>
    MaintenanceWorkOrder CreateWorkOrder(CreateWorkOrderRequest request);

    /// <summary>
    /// Retrieves all recorded maintenance work orders for a vehicle.
    /// </summary>
    IReadOnlyList<MaintenanceWorkOrder> GetWorkOrdersForVehicle(string vehicleId);

    /// <summary>
    /// Retrieves a work order by its unique identifier.
    /// </summary>
    MaintenanceWorkOrder? GetWorkOrder(string workOrderId);
}
