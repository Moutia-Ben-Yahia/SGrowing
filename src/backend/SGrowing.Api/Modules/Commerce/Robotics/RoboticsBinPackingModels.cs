namespace SGrowing.Api.Modules.Commerce.Robotics;

/// <summary>
/// 3D Dimensions in centimeters.
/// </summary>
public record Dimensions3D(double Width, double Height, double Depth)
{
    public double VolumeCm3 => Math.Round(Width * Height * Depth, 4);

    public bool FitsInside(Dimensions3D container) =>
        Width <= container.Width && Height <= container.Height && Depth <= container.Depth;
}

/// <summary>
/// Physical fragility classification for warehouse packing rules.
/// </summary>
public enum FragilityTier
{
    Fragile = 0,
    Standard = 1,
    HeavyBase = 2
}

/// <summary>
/// Orientation restrictions for packing (e.g. liquids, upright sensors).
/// </summary>
public enum OrientationRestriction
{
    ThisSideUp = 0,
    AnyAllowed = 1
}

/// <summary>
/// Item to be packed inside a carton.
/// </summary>
public record PackingItem(
    string ItemId,
    string Sku,
    string Title,
    Dimensions3D Dimensions,
    double WeightGrams,
    FragilityTier FragilityTier = FragilityTier.Standard,
    bool AllowedRotations = true,
    OrientationRestriction Orientation = OrientationRestriction.AnyAllowed);

/// <summary>
/// Standard corrugated cardboard shipping carton specification.
/// </summary>
public record StandardCartonSpec(
    string CartonId,
    string Name,
    Dimensions3D InnerDimensions,
    double MaxWeightGrams,
    double TareWeightGrams)
{
    public double MaxGrossWeightGrams => MaxWeightGrams + TareWeightGrams;
    public double VolumeCm3 => InnerDimensions.VolumeCm3;

    /// <summary>
    /// Standard industrial warehouse carton catalog presets.
    /// </summary>
    public static readonly IReadOnlyList<StandardCartonSpec> DefaultPresets =
    [
        new("carton-s", "Box-S", new Dimensions3D(20, 15, 10), 3000, 150),
        new("carton-m", "Box-M", new Dimensions3D(35, 25, 20), 10000, 350),
        new("carton-l", "Box-L", new Dimensions3D(50, 40, 30), 25000, 700),
        new("carton-xl", "Box-XL", new Dimensions3D(70, 50, 45), 40000, 1200)
    ];
}

/// <summary>
/// Final placement coordinates and bounding box of a packed item inside the carton.
/// Coordinate system: X = Width (horizontal lateral), Y = Depth (horizontal longitudinal), Z = Height (vertical from bottom floor).
/// </summary>
public record PackedItemPlacement(
    string ItemId,
    string Sku,
    double PositionX,
    double PositionY,
    double PositionZ,
    double PlacedWidth,
    double PlacedHeight,
    double PlacedDepth,
    string RotationAxis,
    int LayerIndex)
{
    public double VolumeCm3 => Math.Round(PlacedWidth * PlacedHeight * PlacedDepth, 4);

    /// <summary>
    /// Checks whether this placement strictly intersects/overlaps with another placement.
    /// </summary>
    public bool OverlapsWith(PackedItemPlacement other, double tolerance = 0.001)
    {
        bool overlapX = PositionX < (other.PositionX + other.PlacedWidth - tolerance) &&
                        (PositionX + PlacedWidth - tolerance) > other.PositionX;

        bool overlapY = PositionY < (other.PositionY + other.PlacedDepth - tolerance) &&
                        (PositionY + PlacedDepth - tolerance) > other.PositionY;

        bool overlapZ = PositionZ < (other.PositionZ + other.PlacedHeight - tolerance) &&
                        (PositionZ + PlacedHeight - tolerance) > other.PositionZ;

        return overlapX && overlapY && overlapZ;
    }
}

/// <summary>
/// Result of the 3D bin packing optimization.
/// </summary>
public record PackingSolution(
    StandardCartonSpec CartonSpec,
    IReadOnlyList<PackedItemPlacement> PlacedItems,
    IReadOnlyList<PackingItem> UnplacedItems,
    double VolumeUtilizationPercentage,
    double TotalGrossWeightGrams,
    double CenterOfGravityX,
    double CenterOfGravityY,
    double CenterOfGravityZ,
    bool IsBalanced,
    IReadOnlyList<string> StepByStepInstructions)
{
    public bool AllItemsPlaced => UnplacedItems.Count == 0;
    public int TotalPlacedCount => PlacedItems.Count;
}

/// <summary>
/// Coordinate and location identifier for a warehouse pick bin.
/// </summary>
public record WarehouseBinLocation(
    string LocationId,
    string Aisle,
    int Bay,
    int Level,
    double CoordX,
    double CoordY,
    double CoordZ)
{
    /// <summary>
    /// Euclidean 3D spatial distance in meters between two warehouse bin locations.
    /// </summary>
    public double DistanceTo(WarehouseBinLocation other)
    {
        double dx = CoordX - other.CoordX;
        double dy = CoordY - other.CoordY;
        double dz = CoordZ - other.CoordZ;
        return Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
    }
}

/// <summary>
/// Optimized navigation route for an Autonomous Mobile Robot (AMR).
/// </summary>
public record AmrPickRoute(
    string RouteId,
    double TotalDistanceMeters,
    double EstimatedDurationSeconds,
    IReadOnlyList<WarehouseBinLocation> Waypoints,
    IReadOnlyList<string> OptimizedPickSequence);

// --- API Request and Response Contracts ---

public record CartonizeRequest(
    IReadOnlyList<PackingItem> Items,
    IReadOnlyList<StandardCartonSpec>? AvailableCartons = null,
    bool EnforceBalance = false);

public record CartonizeResponse(
    bool Success,
    PackingSolution? Solution,
    string? Message = null);

public record OptimizeRouteRequest(
    string StartLocationId,
    IReadOnlyList<WarehouseBinLocation> PickLocations,
    double AmrSpeedMetersPerSec = 1.5,
    double PickDwellTimeSeconds = 12.0);

public record OptimizeRouteResponse(
    bool Success,
    AmrPickRoute? Route,
    double NaiveDistanceMeters,
    double DistanceSavedPercentage,
    string? Message = null);

public record RoboticsHealthResponse(
    string Status,
    string Version,
    int SupportedCartonPresets,
    DateTime Timestamp);

public record VisualItemMetadata(
    string ItemId,
    string Sku,
    double X,
    double Y,
    double Z,
    double Width,
    double Height,
    double Depth,
    FragilityTier FragilityTier,
    string ColorHex);

public record PackingVisualMetadata(
    string CartonName,
    double CartonWidth,
    double CartonHeight,
    double CartonDepth,
    double VolumeUtilizationPercentage,
    bool IsBalanced,
    IReadOnlyList<VisualItemMetadata> Items);
