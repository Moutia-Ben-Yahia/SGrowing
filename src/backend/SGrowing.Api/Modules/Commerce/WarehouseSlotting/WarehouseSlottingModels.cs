namespace SGrowing.Api.Modules.Commerce.WarehouseSlotting;

public enum VelocityTier
{
    FastA, // Top 20% items accounting for 80% picks
    MediumB, // Next 30% items
    SlowC   // Remaining 50% slow-moving long-tail items
}

public enum WarehouseRackZone
{
    GoldenZoneLevel2, // Waist-to-shoulder height, closest to outbound packing dock
    BottomLevel1,     // Floor-level heavy bulk pallet positions
    TopLevel3,        // Overhead storage for slow replenishment
    MezzanineBin      // Small-item picking bins
}

public sealed record SkuPickProfile(
    string Sku,
    string Description,
    double PicksPerDay,
    double DimensionsLengthCm,
    double DimensionsWidthCm,
    double DimensionsHeightCm,
    double WeightKg,
    bool IsFragile,
    bool RequiresClimateControl
);

public sealed record SlottingAssignment(
    string Sku,
    VelocityTier Velocity,
    WarehouseRackZone AssignedZone,
    string LocationAisleBayShelf,
    double EstimatedPickTravelTimeSeconds,
    string Rationale
);

public sealed record CartonItem(
    string CartonId,
    string Sku,
    double WeightKg,
    double LengthCm,
    double WidthCm,
    double HeightCm,
    double DensityKgPerM3,
    bool IsFragile
);

public sealed record PalletLayer(
    int LayerIndex,
    double LayerHeightCm,
    double LayerWeightKg,
    IReadOnlyList<CartonItem> Cartons
);

public sealed record PalletLoadPlan(
    string PalletId,
    double PalletLengthCm,
    double PalletWidthCm,
    double MaxLoadHeightCm,
    double TotalWeightKg,
    double CenterOfGravityXCm,
    double CenterOfGravityYCm,
    double CenterOfGravityZCm,
    double CenterOfGravityHeightRatio, // Z_cg / TotalHeight <= 0.55
    bool IsStable,
    IReadOnlyList<PalletLayer> Layers,
    string StabilityVerdict
);

public sealed record OptimizeSlottingRequest(
    string WarehouseId,
    IReadOnlyList<SkuPickProfile> Profiles
);

public sealed record PalletPackRequest(
    string PalletId,
    IReadOnlyList<CartonItem> Cartons,
    double PalletLengthCm = 120.0, // Standard Euro/ISO pallet
    double PalletWidthCm = 80.0,
    double MaxPalletHeightCm = 180.0,
    double MaxLoadWeightKg = 1000.0
);
