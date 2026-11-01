namespace SGrowing.Api.Modules.Logistics.Esg;

/// <summary>
/// Engine for calculating Scope-1, Scope-2, and Scope-3 GHG emissions adhering to the GHG Protocol Corporate Standard.
/// </summary>
public interface IScope3CarbonEmissionsEngine
{
    /// <summary>
    /// Calculates the certified carbon footprint of a freight consignment including transport, packaging, and cold storage.
    /// </summary>
    ShipmentCarbonFootprint CalculateShipmentFootprint(CalculateCarbonRequest request);

    /// <summary>
    /// Retrieves the aggregated emissions balance sheet for a tenant in a given reporting year.
    /// </summary>
    EsgBalanceSheet GetAggregatedEmissions(string tenantId, int year);

    /// <summary>
    /// Retrieves all calculated shipment footprints for a given tenant.
    /// </summary>
    IReadOnlyList<ShipmentCarbonFootprint> GetShipmentsForTenant(string tenantId);
}
