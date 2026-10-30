using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace SGrowing.Api.Modules.Commerce.Warehouses;

#region Request Contracts

public record OptimizeFleetDispatchRequest(
    Guid HubId,
    List<Guid>? StopIds = null,
    List<Guid>? DriverIds = null,
    TimeSpan? DepartureTime = null,
    double AverageSpeedKmh = 35.0,
    int ServiceTimePerStopMinutes = 12,
    double LatePenaltyPerMinute = 0.5,
    int MaxIterations2Opt = 200);

public record DispatchRunsheetsApiRequest(
    Guid HubId,
    FleetOptimizationResult? OptimizationResult = null,
    List<Guid>? SpecificClusterIds = null);

public record CreateDeliveryStopRequest(
    Guid HubId,
    string TrackingNumber,
    string CustomerName,
    string CustomerPhone,
    string DeliveryAddress,
    string City,
    double Latitude,
    double Longitude,
    double ParcelWeightKg,
    decimal CodAmount,
    string Currency,
    TimeSpan TimeWindowStart,
    TimeSpan TimeWindowEnd,
    string WindowLabel = "Morning",
    string Priority = "Standard");

#endregion

public static class FleetDispatchEndpoints
{
    public static IEndpointRouteBuilder MapFleetDispatchEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/logistics/fleet-dispatch")
            .WithTags("Multi-Warehouse PostGIS Geodetic Courier Dispatch & Fleet Clustering");

        // 1. GET /hubs - List logistics hubs and active drivers
        group.MapGet("/hubs", async (
            [FromServices] IServiceProvider sp,
            CancellationToken ct) =>
        {
            var service = sp.GetService(typeof(IGeodeticFleetDispatchService)) as IGeodeticFleetDispatchService
                          ?? GeodeticFleetDispatchService.Instance;

            var hubs = await service.GetLogisticsHubsAsync(ct);
            var drivers = await service.GetActiveDriversAsync(null, ct);

            var response = hubs.Select(h => new
            {
                Hub = h,
                ActiveDrivers = drivers.Where(d => d.AssignedHubId == h.HubId).ToList()
            });

            return Results.Ok(response);
        })
        .WithName("ListFleetDispatchHubs")
        .WithSummary("List regional logistics hubs with their geodetic coordinates and assigned active courier drivers.");

        // 2. GET /stops - List pending delivery stops
        group.MapGet("/stops", async (
            [FromQuery] Guid? hubId,
            [FromQuery] string? status,
            [FromServices] IServiceProvider sp,
            CancellationToken ct) =>
        {
            var service = sp.GetService(typeof(IGeodeticFleetDispatchService)) as IGeodeticFleetDispatchService
                          ?? GeodeticFleetDispatchService.Instance;

            var stops = await service.GetDeliveryStopsAsync(hubId, status, ct);
            return Results.Ok(stops);
        })
        .WithName("ListFleetDispatchStops")
        .WithSummary("List delivery stops with GPS coordinates, parcel weights, time windows, and COD amounts.");

        // POST /stops - Add a delivery stop
        group.MapPost("/stops", async (
            [FromBody] CreateDeliveryStopRequest req,
            [FromServices] IServiceProvider sp,
            CancellationToken ct) =>
        {
            var service = sp.GetService(typeof(IGeodeticFleetDispatchService)) as IGeodeticFleetDispatchService
                          ?? GeodeticFleetDispatchService.Instance;

            var stop = new DeliveryStop
            {
                StopId = Guid.NewGuid(),
                HubId = req.HubId,
                TrackingNumber = req.TrackingNumber,
                CustomerName = req.CustomerName,
                CustomerPhone = req.CustomerPhone,
                DeliveryAddress = req.DeliveryAddress,
                City = req.City,
                Latitude = req.Latitude,
                Longitude = req.Longitude,
                ParcelWeightKg = req.ParcelWeightKg,
                CodAmount = req.CodAmount,
                Currency = req.Currency,
                TimeWindowStart = req.TimeWindowStart,
                TimeWindowEnd = req.TimeWindowEnd,
                WindowLabel = req.WindowLabel,
                Priority = req.Priority,
                Status = "Pending"
            };

            var created = await service.AddDeliveryStopAsync(stop, ct);
            return Results.Created($"/api/v1/logistics/fleet-dispatch/stops/{created.StopId}", created);
        })
        .WithName("CreateFleetDispatchStop")
        .WithSummary("Register a new delivery stop for geodetic routing.");

        // 3. POST /optimize - Run Geodesic K-Means clustering and 2-Opt TSP optimization
        group.MapPost("/optimize", async (
            [FromBody] OptimizeFleetDispatchRequest req,
            [FromServices] IServiceProvider sp,
            CancellationToken ct) =>
        {
            var service = sp.GetService(typeof(IGeodeticFleetDispatchService)) as IGeodeticFleetDispatchService
                          ?? GeodeticFleetDispatchService.Instance;

            var domainReq = new FleetOptimizationRequest
            {
                HubId = req.HubId,
                StopIds = req.StopIds,
                DriverIds = req.DriverIds,
                DepartureTime = req.DepartureTime ?? new TimeSpan(8, 30, 0),
                AverageSpeedKmh = req.AverageSpeedKmh > 0 ? req.AverageSpeedKmh : 35.0,
                ServiceTimePerStopMinutes = req.ServiceTimePerStopMinutes >= 0 ? req.ServiceTimePerStopMinutes : 12,
                LatePenaltyPerMinute = req.LatePenaltyPerMinute >= 0 ? req.LatePenaltyPerMinute : 0.5,
                MaxIterations2Opt = req.MaxIterations2Opt > 0 ? req.MaxIterations2Opt : 200
            };

            try
            {
                var result = await service.OptimizeFleetDispatchAsync(domainReq, ct);
                return Results.Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .WithName("OptimizeFleetDispatch")
        .WithSummary("Execute Geodesic K-Means clustering and 2-Opt TSP route minimization with time window constraints.");

        // 4. POST /dispatch - Finalize and generate driver runsheet manifests
        group.MapPost("/dispatch", async (
            [FromBody] DispatchRunsheetsApiRequest req,
            [FromServices] IServiceProvider sp,
            CancellationToken ct) =>
        {
            var service = sp.GetService(typeof(IGeodeticFleetDispatchService)) as IGeodeticFleetDispatchService
                          ?? GeodeticFleetDispatchService.Instance;

            var domainReq = new DispatchRunsheetsRequest
            {
                HubId = req.HubId,
                OptimizationResult = req.OptimizationResult,
                SpecificClusterIds = req.SpecificClusterIds
            };

            var manifests = await service.DispatchRunsheetsAsync(domainReq, ct);
            return Results.Ok(manifests);
        })
        .WithName("DispatchDriverRunsheets")
        .WithSummary("Generate driver runsheet manifests (RUN-YYYYMMDD-DRVXX) with total distances and COD tallies.");

        // 5. GET /runsheets/{id} - Get runsheet detail (by Guid or runsheet code)
        group.MapGet("/runsheets/{id}", async (
            [FromRoute] string id,
            [FromServices] IServiceProvider sp,
            CancellationToken ct) =>
        {
            var service = sp.GetService(typeof(IGeodeticFleetDispatchService)) as IGeodeticFleetDispatchService
                          ?? GeodeticFleetDispatchService.Instance;

            DriverRunsheetManifest? manifest = null;
            if (Guid.TryParse(id, out var runsheetId))
            {
                manifest = await service.GetRunsheetByIdAsync(runsheetId, ct);
            }

            manifest ??= await service.GetRunsheetByCodeAsync(id, ct);

            if (manifest == null)
            {
                return Results.NotFound(new { error = $"Runsheet '{id}' was not found." });
            }

            return Results.Ok(manifest);
        })
        .WithName("GetDriverRunsheetDetail")
        .WithSummary("Retrieve a driver runsheet manifest by its unique GUID or manifest code (e.g. RUN-20261030-DRV01).");

        // 6. GET /runsheets - List all runsheets
        group.MapGet("/runsheets", async (
            [FromQuery] Guid? hubId,
            [FromServices] IServiceProvider sp,
            CancellationToken ct) =>
        {
            var service = sp.GetService(typeof(IGeodeticFleetDispatchService)) as IGeodeticFleetDispatchService
                          ?? GeodeticFleetDispatchService.Instance;

            var list = await service.GetAllRunsheetsAsync(hubId, ct);
            return Results.Ok(list);
        })
        .WithName("ListDriverRunsheets")
        .WithSummary("List all generated and dispatched driver runsheet manifests.");

        // 7. POST /reset - Reset in-memory store to initial seed data
        group.MapPost("/reset", (
            [FromServices] IServiceProvider sp) =>
        {
            var service = sp.GetService(typeof(IGeodeticFleetDispatchService)) as IGeodeticFleetDispatchService
                          ?? GeodeticFleetDispatchService.Instance;

            service.ResetToSeedData();
            return Results.Ok(new { message = "Fleet dispatch seed data reset successfully." });
        })
        .WithName("ResetFleetDispatchSeedData")
        .WithSummary("Reset hubs, drivers, stops, and runsheets to factory seed data.");

        return app;
    }
}
