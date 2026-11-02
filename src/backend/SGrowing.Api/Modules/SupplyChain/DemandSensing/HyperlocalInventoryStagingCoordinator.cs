namespace SGrowing.Api.Modules.SupplyChain.DemandSensing;

/// <summary>
/// Autonomous inventory staging coordinator rebalancing stock between distribution hubs
/// 48 hours prior to incoming meteorological fronts.
/// </summary>
public sealed class HyperlocalInventoryStagingCoordinator
{
    public IReadOnlyList<RegionalInventoryTransferRecommendation> CoordinateStaging(
        IReadOnlyList<SensedDemandForecast> forecasts,
        string primarySurplusWarehouseId = "WH-ALGIERS-CENTRAL",
        string targetDeficitWarehouseId = "WH-OUARGLA-SAHARA")
    {
        ArgumentNullException.ThrowIfNull(forecasts);

        var list = new List<RegionalInventoryTransferRecommendation>();

        foreach (var forecast in forecasts.Where(f => f.StockoutRiskFlagged))
        {
            var unitsToTransfer = (int)Math.Ceiling(forecast.ProjectedInventoryDeficitUnits * 1.15); // +15% safety buffer
            var urgency = forecast.WeatherDemandMultiplier >= 2.0 ? "CRITICAL_48H" : "STANDARD_EXPEDITED";

            list.Add(new RegionalInventoryTransferRecommendation(
                RecommendationId: $"STG-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
                Sku: forecast.Sku,
                SourceWarehouseId: primarySurplusWarehouseId,
                TargetWarehouseId: targetDeficitWarehouseId,
                TransferQuantityUnits: unitsToTransfer,
                RequiredArrivalUtc: DateTime.UtcNow.AddHours(48),
                TransferUrgency: urgency,
                Rationale: $"Pre-position {unitsToTransfer} units to offset {forecast.ProjectedInventoryDeficitUnits:F0} unit weather surge deficit ({forecast.WeatherImpactExplanation})."
            ));
        }

        return list;
    }
}
