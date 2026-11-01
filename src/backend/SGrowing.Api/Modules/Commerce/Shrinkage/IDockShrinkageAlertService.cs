namespace SGrowing.Api.Modules.Commerce.Shrinkage;

/// <summary>
/// Service interface for real-time warehouse dock perimeter theft interception and automated security alerts.
/// </summary>
public interface IDockShrinkageAlertService
{
    /// <summary>
    /// Evaluates whether an asset has crossed the dock exit gate perimeter (X >= 90m, Y >= 90m).
    /// Instantly emits a CriticalTheftAlert with asset valuation in DZD if perimeter is breached.
    /// </summary>
    DockShrinkageSecurityAlert? EvaluateDockPerimeter(
        string tagId,
        string sku,
        IndoorLocationPoint location,
        decimal itemValueDzd);

    /// <summary>
    /// Retrieves all currently active security alerts (ActiveAlarm or SecurityDispatched).
    /// </summary>
    IReadOnlyList<DockShrinkageSecurityAlert> GetActiveSecurityAlerts();

    /// <summary>
    /// Retrieves all security alerts including historical cleared and recovered alerts.
    /// </summary>
    IReadOnlyList<DockShrinkageSecurityAlert> GetAllSecurityAlerts();

    /// <summary>
    /// Updates the intercept lifecycle status of a dock security alert (e.g., SecurityDispatched, Recovered).
    /// </summary>
    bool AcknowledgeAlert(
        string alertId,
        SecurityInterceptStatus newStatus,
        string? notes = null);

    /// <summary>
    /// Computes the aggregate monetary value (in Algerian Dinars - DZD) of all inventory currently at risk in active dock alerts.
    /// </summary>
    decimal GetTotalAtRiskValueDzd();
}
