using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Logistics.Fleet;

/// <summary>
/// Minimal API endpoints and dependency injection extensions for Phase 36:
/// Autonomous Fleet Telematics, Real-Time Fuel Optimization &amp; Predictive Vehicle Maintenance.
/// </summary>
public static class FleetEndpoints
{
    /// <summary>
    /// Registers all Phase 36 Autonomous Fleet Telematics, Eco-Scoring, and Predictive Maintenance services.
    /// </summary>
    public static IServiceCollection AddFleetServices(this IServiceCollection services)
    {
        services.AddSingleton<IObd2TelemetryStreamerEngine, Obd2TelemetryStreamerEngine>();
        services.AddSingleton<IDriverSafetyAndEcoScoringService, DriverSafetyAndEcoScoringService>();
        services.AddSingleton<IPredictiveMaintenanceRadarService, PredictiveMaintenanceRadarService>();

        return services;
    }

    /// <summary>
    /// Maps the Phase 36 Fleet Minimal API endpoints under /api/v1/logistics/fleet.
    /// </summary>
    public static IEndpointRouteBuilder MapFleetEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/logistics/fleet")
            .WithTags("Autonomous Fleet Telematics & Predictive Maintenance");

        // 1. POST /api/v1/logistics/fleet/telemetry/ingest
        group.MapPost("/telemetry/ingest", (
            [FromBody] IngestTelemetryRequest request,
            [FromServices] IObd2TelemetryStreamerEngine streamerEngine) =>
        {
            try
            {
                var response = streamerEngine.IngestTelemetry(request.Packet);
                return Results.Ok(response);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { Error = ex.Message });
            }
        })
        .WithName("IngestVehicleTelemetry")
        .WithSummary("Ingests high-frequency OBD2 CAN bus telemetry packets with real-time deadband filtering.");

        // 2. POST /api/v1/logistics/fleet/telemetry/ingest/batch
        group.MapPost("/telemetry/ingest/batch", (
            [FromBody] IngestTelemetryBatchRequest request,
            [FromServices] IObd2TelemetryStreamerEngine streamerEngine) =>
        {
            try
            {
                var responses = streamerEngine.IngestBatch(request.Packets);
                return Results.Ok(responses);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { Error = ex.Message });
            }
        })
        .WithName("IngestTelemetryBatch")
        .WithSummary("Ingests a batch of ordered OBD2 telemetry packets.");

        // 3. GET /api/v1/logistics/fleet/vehicles/{vehicleId}/diagnostics
        group.MapGet("/vehicles/{vehicleId}/diagnostics", (
            [FromRoute] string vehicleId,
            [FromServices] IObd2TelemetryStreamerEngine streamerEngine) =>
        {
            var state = streamerEngine.GetLatestVehicleState(vehicleId);
            if (state is null)
            {
                return Results.NotFound(new { Message = $"Vehicle '{vehicleId}' has no recorded telematics." });
            }

            var activeDtcs = streamerEngine.GetActiveTroubleCodes(vehicleId);
            var response = new VehicleDiagnosticsResponse(
                VehicleId: vehicleId,
                State: state,
                ActiveDtcCount: activeDtcs.Count,
                ActiveTroubleCodes: activeDtcs);

            return Results.Ok(response);
        })
        .WithName("GetVehicleDiagnostics")
        .WithSummary("Retrieves current health, ECU state, and active Diagnostic Trouble Codes for a vehicle.");

        // 4. GET /api/v1/logistics/fleet/vehicles/{vehicleId}/rul
        group.MapGet("/vehicles/{vehicleId}/rul", (
            [FromRoute] string vehicleId,
            [FromQuery] double currentKm,
            [FromQuery] double? ambientTempCelsius,
            [FromServices] IPredictiveMaintenanceRadarService radarService) =>
        {
            try
            {
                var forecast = radarService.GenerateMaintenanceForecast(
                    vehicleId,
                    currentKm,
                    ambientTempCelsius ?? 25.0);

                return Results.Ok(forecast);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { Error = ex.Message });
            }
        })
        .WithName("GetVehicleRemainingUsefulLife")
        .WithSummary("Computes 2-parameter Weibull Remaining Useful Life (RUL) forecasts for vehicle components.");

        // 5. GET /api/v1/logistics/fleet/drivers/{driverId}/eco-score
        group.MapGet("/drivers/{driverId}/eco-score", (
            [FromRoute] string driverId,
            [FromServices] IDriverSafetyAndEcoScoringService scoringService) =>
        {
            var score = scoringService.GetDriverScore(driverId);
            if (score is null)
            {
                return Results.NotFound(new { Message = $"No driving evaluation found for driver '{driverId}'." });
            }

            return Results.Ok(score);
        })
        .WithName("GetDriverEcoScore")
        .WithSummary("Retrieves eco-driving score, harsh kinematic event counts, and fuel bonus eligibility.");

        // 6. POST /api/v1/logistics/fleet/work-orders
        group.MapPost("/work-orders", (
            [FromBody] CreateWorkOrderRequest request,
            [FromServices] IPredictiveMaintenanceRadarService radarService) =>
        {
            try
            {
                var workOrder = radarService.CreateWorkOrder(request);
                return Results.Created($"/api/v1/logistics/fleet/work-orders/{workOrder.WorkOrderId}", workOrder);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { Error = ex.Message });
            }
        })
        .WithName("CreateMaintenanceWorkOrder")
        .WithSummary("Creates a new vehicle maintenance work order.");

        // 7. GET /api/v1/logistics/fleet/work-orders/{vehicleId}
        group.MapGet("/work-orders/{vehicleId}", (
            [FromRoute] string vehicleId,
            [FromServices] IPredictiveMaintenanceRadarService radarService) =>
        {
            var workOrders = radarService.GetWorkOrdersForVehicle(vehicleId);
            return Results.Ok(workOrders);
        })
        .WithName("GetVehicleWorkOrders")
        .WithSummary("Retrieves all pending, scheduled, and past maintenance work orders for a vehicle.");

        return app;
    }
}
