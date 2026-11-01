using System.Collections.Concurrent;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Commerce.Robotics;

/// <summary>
/// Minimal API endpoints and Dependency Injection setup for Phase 29:
/// Autonomous Multi-Modal Warehouse Robotics (AMR) &amp; 3D Bin-Packing Optimization Engine.
/// </summary>
public static class RoboticsEndpoints
{
    private static readonly ConcurrentDictionary<string, PackingSolution> SolutionCache = new();

    /// <summary>
    /// Registers autonomous robotics and 3D bin packing services in the DI container.
    /// </summary>
    public static IServiceCollection AddAmrRoboticsBinPackingServices(this IServiceCollection services)
    {
        services.AddSingleton<IBinPacking3dEngine, BinPacking3dEngine>();
        services.AddSingleton<IWarehousePickRouterService, WarehousePickRouterService>();
        services.AddSingleton<IPackingVisualizerService, PackingVisualizerService>();

        return services;
    }

    /// <summary>
    /// Maps the robotics and 3D bin packing route group under /api/v1/commerce/robotics.
    /// </summary>
    public static IEndpointRouteBuilder MapRoboticsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/commerce/robotics")
            .WithTags("Autonomous Multi-Modal Warehouse Robotics & 3D Bin-Packing Optimization Engine");

        // 1. POST /api/v1/commerce/robotics/cartonize - Calculate optimal 3D carton packing
        group.MapPost("/cartonize", (
            [FromBody] CartonizeRequest req,
            [FromServices] IBinPacking3dEngine engine,
            CancellationToken ct) =>
        {
            if (req.Items == null || req.Items.Count == 0)
            {
                return Results.BadRequest(new CartonizeResponse(false, null, "Item collection cannot be null or empty."));
            }

            var solution = engine.PackItems(req.Items, req.AvailableCartons, req.EnforceBalance);
            string solutionId = $"sol_{Guid.NewGuid():N}"[..12];
            SolutionCache[solutionId] = solution;

            return Results.Ok(new CartonizeResponse(solution.AllItemsPlaced, solution, solution.AllItemsPlaced
                ? $"Successfully packed {solution.PlacedItems.Count} items into carton '{solution.CartonSpec.Name}'. Visual ID: {solutionId}"
                : $"Partial packing: {solution.PlacedItems.Count} placed, {solution.UnplacedItems.Count} unplaced. Visual ID: {solutionId}"));
        })
        .WithName("CartonizeItems")
        .WithSummary("Calculates optimal 3D carton packing with rotation, heavy-base support, and center of gravity balance.");

        // 2. POST /api/v1/commerce/robotics/amr/optimize-route - Solve TSP pick route for warehouse bins
        group.MapPost("/amr/optimize-route", (
            [FromBody] OptimizeRouteRequest req,
            [FromServices] IWarehousePickRouterService router,
            CancellationToken ct) =>
        {
            if (req.PickLocations == null || req.PickLocations.Count == 0)
            {
                return Results.BadRequest(new OptimizeRouteResponse(false, null, 0, 0, "PickLocations cannot be null or empty."));
            }

            var startLoc = req.PickLocations.FirstOrDefault(p => p.LocationId == req.StartLocationId)
                           ?? new WarehouseBinLocation(req.StartLocationId ?? "DEPOT-01", "Aisle-00", 0, 0, 0, 0, 0);

            var (route, naiveDist, savedPct) = router.BenchmarkRoute(
                startLoc,
                req.PickLocations,
                req.AmrSpeedMetersPerSec,
                req.PickDwellTimeSeconds);

            return Results.Ok(new OptimizeRouteResponse(
                Success: true,
                Route: route,
                NaiveDistanceMeters: naiveDist,
                DistanceSavedPercentage: savedPct,
                Message: $"Optimized {route.Waypoints.Count - 1} picks. Reduced travel from {naiveDist:F1}m to {route.TotalDistanceMeters:F1}m (Saved {savedPct:F1}%)."));
        })
        .WithName("OptimizeAmrPickRoute")
        .WithSummary("Solves 2-Opt TSP warehouse picking route for AMRs, benchmarking vs naive sequence.");

        // 3. GET /api/v1/commerce/robotics/cartonize/{id}/svg - Get SVG packing visual
        group.MapGet("/cartonize/{id}/svg", (
            string id,
            [FromServices] IBinPacking3dEngine engine,
            [FromServices] IPackingVisualizerService visualizer,
            CancellationToken ct) =>
        {
            if (SolutionCache.TryGetValue(id, out var cachedSolution))
            {
                string svg = visualizer.GeneratePackingSvg(cachedSolution);
                return Results.Content(svg, "image/svg+xml");
            }

            // Generate a demo solution if requesting demo or preset
            var demoCarton = StandardCartonSpec.DefaultPresets[1]; // Box-M
            var demoItems = new List<PackingItem>
            {
                new("demo-1", "SKU-BASE-01", "Heavy Motor Base", new Dimensions3D(18, 10, 12), 2500, FragilityTier.HeavyBase),
                new("demo-2", "SKU-GEAR-02", "Drive Sprocket", new Dimensions3D(14, 8, 10), 1200, FragilityTier.Standard),
                new("demo-3", "SKU-GLASS-03", "Optic Sensor", new Dimensions3D(10, 8, 6), 350, FragilityTier.Fragile, Orientation: OrientationRestriction.ThisSideUp)
            };

            var demoSolution = engine.PackIntoSpecificCarton(demoItems, demoCarton);
            string demoSvg = visualizer.GeneratePackingSvg(demoSolution);
            return Results.Content(demoSvg, "image/svg+xml");
        })
        .WithName("GetCartonPackingSvg")
        .WithSummary("Generates an interactive dual-projection SVG visualization of the packed 3D carton.");

        // 4. GET /api/v1/commerce/robotics/health - Engine health
        group.MapGet("/health", () =>
        {
            var health = new RoboticsHealthResponse(
                Status: "Healthy",
                Version: "v1.0.0",
                SupportedCartonPresets: StandardCartonSpec.DefaultPresets.Count,
                Timestamp: DateTime.UtcNow);

            return Results.Ok(health);
        })
        .WithName("GetRoboticsEngineHealth")
        .WithSummary("Returns health check status and configuration metrics for the AMR and 3D bin packing engine.");

        return app;
    }
}
