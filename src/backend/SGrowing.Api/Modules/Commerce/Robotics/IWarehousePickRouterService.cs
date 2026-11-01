namespace SGrowing.Api.Modules.Commerce.Robotics;

/// <summary>
/// Autonomous Mobile Robot (AMR) Multi-Pick Route Optimization Service.
/// Solves the Traveling Salesperson Problem (TSP) across warehouse aisles using
/// Nearest-Neighbor initialization and 2-Opt heuristic local search.
/// </summary>
public interface IWarehousePickRouterService
{
    /// <summary>
    /// Solves the multi-pick TSP route starting from a depot or starting bin.
    /// </summary>
    AmrPickRoute OptimizePickRoute(
        WarehouseBinLocation startLocation,
        IReadOnlyList<WarehouseBinLocation> pickLocations,
        double amrSpeedMetersPerSec = 1.5,
        double pickDwellTimeSeconds = 12.0);

    /// <summary>
    /// Solves the TSP pick route and compares it against the naive input sequence
    /// to calculate total distance savings and benchmark percentage.
    /// </summary>
    (AmrPickRoute Route, double NaiveDistanceMeters, double DistanceSavedPercentage) BenchmarkRoute(
        WarehouseBinLocation startLocation,
        IReadOnlyList<WarehouseBinLocation> pickLocations,
        double amrSpeedMetersPerSec = 1.5,
        double pickDwellTimeSeconds = 12.0);

    /// <summary>
    /// Computes the total path distance traversing from startLocation through the given sequence.
    /// </summary>
    double CalculateTotalDistance(
        WarehouseBinLocation startLocation,
        IReadOnlyList<WarehouseBinLocation> sequence);
}
