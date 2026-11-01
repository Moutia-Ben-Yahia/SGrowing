namespace SGrowing.Api.Modules.Commerce.Shrinkage;

/// <summary>
/// Service interface for detecting inventory rack misplacement and unauthorized aisle movement.
/// </summary>
public interface IAisleDiscrepancyDetectorService
{
    /// <summary>
    /// Audits an asset's real-time position against its expected planogram zone and pick authorization.
    /// Emits an incident if a discrepancy or unauthorized movement is detected.
    /// </summary>
    AisleDiscrepancyIncident? AuditAssetPlacement(
        string tagId,
        string sku,
        WarehouseZone expectedZone,
        bool hasActivePickTask);

    /// <summary>
    /// Retrieves all currently active (unresolved) aisle discrepancy incidents.
    /// </summary>
    IReadOnlyList<AisleDiscrepancyIncident> GetActiveDiscrepancies();

    /// <summary>
    /// Resolves an active discrepancy incident with operational audit notes.
    /// </summary>
    bool ResolveDiscrepancy(string incidentId, string? notes = null);

    /// <summary>
    /// Audits all currently tracked inventory assets in the positioning mesh using a planogram resolver delegate.
    /// </summary>
    AuditDiscrepanciesResponse AuditAllTrackedAssets(
        Func<string, (WarehouseZone ExpectedZone, bool HasPickTask)> planogramResolver);

    /// <summary>
    /// Computes the reference center coordinates for a given warehouse zone.
    /// </summary>
    (double X, double Y) GetZoneCenter(WarehouseZone zone);
}
