using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Commerce.Shrinkage;

/// <summary>
/// Minimal API endpoints and Dependency Injection registration for Phase 48:
/// Autonomous Real-Time Inventory Shrinkage Radar, RFID/BLE Mesh &amp; Theft Prevention.
/// </summary>
public static class ShrinkageEndpoints
{
    /// <summary>
    /// Registers real-time RFID/BLE indoor positioning, aisle discrepancy detection,
    /// and dock perimeter theft prevention services into the DI service collection.
    /// </summary>
    public static IServiceCollection AddShrinkageServices(this IServiceCollection services)
    {
        services.AddSingleton<IRfidBleIndoorPositioningEngine, RfidBleIndoorPositioningEngine>();
        services.AddSingleton<IAisleDiscrepancyDetectorService, AisleDiscrepancyDetectorService>();
        services.AddSingleton<IDockShrinkageAlertService, DockShrinkageAlertService>();

        return services;
    }

    /// <summary>
    /// Maps real-time shrinkage radar and theft interception endpoints under /api/v1/commerce/shrinkage.
    /// </summary>
    public static IEndpointRouteBuilder MapShrinkageEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/commerce/shrinkage")
            .WithTags("Autonomous Inventory Shrinkage Radar, RFID/BLE Mesh & Theft Prevention");

        // 1. POST /api/v1/commerce/shrinkage/telemetry/rssi - Ingest raw RSSI telemetry
        group.MapPost("/telemetry/rssi", (
            [FromBody] IngestRssiRequest req,
            [FromServices] IRfidBleIndoorPositioningEngine positioningEngine,
            [FromServices] IAisleDiscrepancyDetectorService discrepancyService,
            [FromServices] IDockShrinkageAlertService dockAlertService) =>
        {
            if (string.IsNullOrWhiteSpace(req.TagId) || string.IsNullOrWhiteSpace(req.Sku))
            {
                return Results.BadRequest(new { message = "TagId and Sku are required." });
            }

            if (req.RssiReadings == null || req.RssiReadings.Count == 0)
            {
                return Results.BadRequest(new { message = "At least one RSSI anchor reading is required." });
            }

            var beacon = new RfidBleTelemetryBeacon(
                TagId: req.TagId,
                Sku: req.Sku,
                RssiReadings: req.RssiReadings,
                BatteryPct: req.BatteryPct,
                TimestampUtc: req.TimestampUtc ?? DateTime.UtcNow,
                TagType: req.TagType);

            var location = positioningEngine.EstimateLocation(beacon);

            // Real-time perimeter theft evaluation
            decimal itemVal = req.ItemValueDzd ?? 25000m;
            var dockAlert = dockAlertService.EvaluateDockPerimeter(req.TagId, req.Sku, location, itemVal);

            // Planogram discrepancy audit
            var expectedZone = req.ExpectedZone ?? WarehouseZone.HighValueAisle;
            var discrepancy = discrepancyService.AuditAssetPlacement(req.TagId, req.Sku, expectedZone, req.HasActivePickTask);

            string message = dockAlert != null
                ? $"CRITICAL THEFT WARNING: Asset breached dock perimeter at ({location.X:F1}m, {location.Y:F1}m)."
                : discrepancy != null
                    ? $"Discrepancy detected: Asset located in {location.Zone} instead of {expectedZone}."
                    : $"Telemetry processed: Location ({location.X:F1}m, {location.Y:F1}m) in {location.Zone}.";

            var response = new IngestRssiResponse(
                Success: true,
                TagId: req.TagId,
                Location: location,
                DiscrepancyIncident: discrepancy,
                DockSecurityAlert: dockAlert,
                Message: message);

            return Results.Ok(response);
        })
        .WithName("IngestShrinkageRssiTelemetry")
        .WithSummary("Ingests multi-anchor RSSI telemetry, computes trilaterated coordinates, and checks dock perimeters.");

        // 2. POST /api/v1/commerce/shrinkage/anchors - Register fixed anchor node
        group.MapPost("/anchors", (
            [FromBody] RegisterAnchorRequest req,
            [FromServices] IRfidBleIndoorPositioningEngine positioningEngine) =>
        {
            if (string.IsNullOrWhiteSpace(req.AnchorId))
            {
                return Results.BadRequest(new { message = "AnchorId is required." });
            }

            var anchor = new FixedAnchorNode(
                AnchorId: req.AnchorId,
                X: req.X,
                Y: req.Y,
                Z: req.Z,
                Floor: req.Floor,
                Zone: req.Zone,
                Model: req.Model,
                IsActive: true);

            positioningEngine.RegisterAnchor(anchor);

            return Results.Created($"/api/v1/commerce/shrinkage/anchors/{anchor.AnchorId}", anchor);
        })
        .WithName("RegisterShrinkageAnchor")
        .WithSummary("Registers or updates a fixed ceiling/pillar anchor node in the positioning mesh.");

        // 3. GET /api/v1/commerce/shrinkage/anchors - List all registered anchors
        group.MapGet("/anchors", (
            [FromServices] IRfidBleIndoorPositioningEngine positioningEngine) =>
        {
            var anchors = positioningEngine.GetAllAnchors();
            return Results.Ok(anchors);
        })
        .WithName("ListShrinkageAnchors")
        .WithSummary("Lists all active fixed anchor nodes registered in the warehouse.");

        // 4. GET /api/v1/commerce/shrinkage/assets/{tagId}/location - Get asset location
        group.MapGet("/assets/{tagId}/location", (
            string tagId,
            [FromServices] IRfidBleIndoorPositioningEngine positioningEngine) =>
        {
            var all = positioningEngine.GetAllTrackedAssets();
            if (!all.TryGetValue(tagId, out var item))
            {
                return Results.NotFound(new { message = $"Asset tag '{tagId}' not currently tracked." });
            }

            var response = new AssetLocationResponse(
                TagId: tagId,
                Sku: item.Sku,
                Location: item.Location,
                BatteryPct: item.BatteryPct,
                LastSeenUtc: item.Location.TimestampUtc);

            return Results.Ok(response);
        })
        .WithName("GetAssetShrinkageLocation")
        .WithSummary("Retrieves the latest trilaterated coordinates and battery status for a tracked asset.");

        // 5. GET /api/v1/commerce/shrinkage/assets - List all tracked assets
        group.MapGet("/assets", (
            [FromServices] IRfidBleIndoorPositioningEngine positioningEngine) =>
        {
            var all = positioningEngine.GetAllTrackedAssets();
            var responseList = all.Select(kv => new AssetLocationResponse(
                TagId: kv.Key,
                Sku: kv.Value.Sku,
                Location: kv.Value.Location,
                BatteryPct: kv.Value.BatteryPct,
                LastSeenUtc: kv.Value.Location.TimestampUtc)).ToList();

            return Results.Ok(responseList);
        })
        .WithName("ListTrackedShrinkageAssets")
        .WithSummary("Lists all currently active inventory assets tracked across the warehouse.");

        // 6. GET /api/v1/commerce/shrinkage/discrepancies/active - Get active aisle discrepancies
        group.MapGet("/discrepancies/active", (
            [FromServices] IAisleDiscrepancyDetectorService discrepancyService) =>
        {
            var active = discrepancyService.GetActiveDiscrepancies();
            return Results.Ok(active);
        })
        .WithName("GetActiveAisleDiscrepancies")
        .WithSummary("Retrieves all unresolved rack misplacements and unauthorized movements.");

        // 7. POST /api/v1/commerce/shrinkage/discrepancies/{incidentId}/resolve - Resolve incident
        group.MapPost("/discrepancies/{incidentId}/resolve", (
            string incidentId,
            [FromBody] ResolveDiscrepancyRequest? req,
            [FromServices] IAisleDiscrepancyDetectorService discrepancyService) =>
        {
            bool resolved = discrepancyService.ResolveDiscrepancy(incidentId, req?.ResolutionNotes);
            if (!resolved)
            {
                return Results.NotFound(new { message = $"Discrepancy incident '{incidentId}' not found." });
            }

            return Results.Ok(new { message = $"Incident '{incidentId}' resolved successfully." });
        })
        .WithName("ResolveAisleDiscrepancy")
        .WithSummary("Marks an aisle discrepancy incident as resolved with supervisor notes.");

        // 8. GET /api/v1/commerce/shrinkage/alerts/dock - Get active dock alerts
        group.MapGet("/alerts/dock", (
            [FromServices] IDockShrinkageAlertService dockAlertService) =>
        {
            var alerts = dockAlertService.GetActiveSecurityAlerts();
            return Results.Ok(alerts);
        })
        .WithName("GetActiveDockSecurityAlerts")
        .WithSummary("Retrieves real-time active perimeter breach alarms at dock exit gates.");

        // 9. POST /api/v1/commerce/shrinkage/alerts/{alertId}/acknowledge - Acknowledge alert
        group.MapPost("/alerts/{alertId}/acknowledge", (
            string alertId,
            [FromBody] AcknowledgeAlertRequest req,
            [FromServices] IDockShrinkageAlertService dockAlertService) =>
        {
            bool updated = dockAlertService.AcknowledgeAlert(alertId, req.NewStatus, req.Notes);
            if (!updated)
            {
                return Results.NotFound(new { message = $"Dock security alert '{alertId}' not found." });
            }

            return Results.Ok(new { message = $"Alert '{alertId}' status updated to {req.NewStatus}." });
        })
        .WithName("AcknowledgeDockSecurityAlert")
        .WithSummary("Updates intercept status of a dock security alarm (e.g. SecurityDispatched, Recovered).");

        // 10. GET /api/v1/commerce/shrinkage/summary - Warehouse-wide shrinkage radar summary
        group.MapGet("/summary", (
            [FromServices] IRfidBleIndoorPositioningEngine positioningEngine,
            [FromServices] IAisleDiscrepancyDetectorService discrepancyService,
            [FromServices] IDockShrinkageAlertService dockAlertService) =>
        {
            var assets = positioningEngine.GetAllTrackedAssets();
            var discrepancies = discrepancyService.GetActiveDiscrepancies();
            var alerts = dockAlertService.GetActiveSecurityAlerts();
            decimal atRiskDzd = dockAlertService.GetTotalAtRiskValueDzd();

            var zoneMetrics = Enum.GetValues<WarehouseZone>()
                .Select(z => new WarehouseZoneShrinkageMetric(
                    Zone: z,
                    AssetCount: assets.Values.Count(a => a.Location.Zone == z),
                    DiscrepancyCount: discrepancies.Count(d => d.ActualZone == z),
                    AlertCount: alerts.Count(a => a.TriggerLocation?.Zone == z)))
                .ToList();

            var summary = new ShrinkageSummaryResponse(
                TotalTrackedAssets: assets.Count,
                ActiveDiscrepanciesCount: discrepancies.Count,
                ActiveDockAlertsCount: alerts.Count,
                TotalAtRiskValueDzd: atRiskDzd,
                ZoneMetrics: zoneMetrics,
                GeneratedAtUtc: DateTime.UtcNow);

            return Results.Ok(summary);
        })
        .WithName("GetShrinkageRadarSummary")
        .WithSummary("Returns aggregated real-time metrics across all warehouse zones, active alarms, and at-risk value.");

        return app;
    }
}
