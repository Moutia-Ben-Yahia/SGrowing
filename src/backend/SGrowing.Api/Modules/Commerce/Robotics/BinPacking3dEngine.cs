namespace SGrowing.Api.Modules.Commerce.Robotics;

/// <summary>
/// Autonomous 3D Bin-Packing Optimization Engine.
/// Implements 3D Cuboid Extreme-Points packing with:
/// - 6-axis orthogonal rotation (when allowed) and ThisSideUp constraint enforcement.
/// - Heavy-Base floor placement priority and Fragile item protection.
/// - Gravity & horizontal surface support validation (>= 60% base area contact).
/// - Smallest standard carton selection (Box-S, Box-M, Box-L, Box-XL).
/// - Center of Gravity (CoG) calculation and parcel balance validation (<= 20% deviation).
/// - Comprehensive step-by-step packing instructions for robotics and warehouse pickers.
/// </summary>
public class BinPacking3dEngine : IBinPacking3dEngine
{
    private const double Tolerance = 0.001;
    private const double MinSupportRatio = 0.60;
    private const double BalanceToleranceFraction = 0.20;

    private readonly record struct Point3D(double X, double Y, double Z);

    private readonly record struct OrientationOption(
        double Width,
        double Height,
        double Depth,
        string AxisName);

    /// <inheritdoc />
    public PackingSolution PackItems(
        IReadOnlyList<PackingItem> items,
        IReadOnlyList<StandardCartonSpec>? availableCartons = null,
        bool enforceBalance = false)
    {
        var cartons = (availableCartons is { Count: > 0 } ? availableCartons : StandardCartonSpec.DefaultPresets)
            .OrderBy(c => c.VolumeCm3)
            .ToList();

        if (items.Count == 0)
        {
            var fallback = cartons[0];
            return new PackingSolution(
                CartonSpec: fallback,
                PlacedItems: [],
                UnplacedItems: [],
                VolumeUtilizationPercentage: 0.0,
                TotalGrossWeightGrams: fallback.TareWeightGrams,
                CenterOfGravityX: fallback.InnerDimensions.Width / 2.0,
                CenterOfGravityY: fallback.InnerDimensions.Depth / 2.0,
                CenterOfGravityZ: 0.0,
                IsBalanced: true,
                StepByStepInstructions: ["No items to pack."]);
        }

        double totalItemsVolume = items.Sum(i => i.Dimensions.VolumeCm3);
        double totalItemsWeight = items.Sum(i => i.WeightGrams);

        // Filter cartons that can theoretically hold the weight and total volume
        var candidateCartons = cartons
            .Where(c => c.MaxWeightGrams >= totalItemsWeight && c.VolumeCm3 >= totalItemsVolume)
            .ToList();

        // If no carton satisfies both theoretical limits, try all available cartons largest first
        if (candidateCartons.Count == 0)
        {
            candidateCartons = [.. cartons.OrderByDescending(c => c.VolumeCm3)];
        }

        PackingSolution? bestPartialSolution = null;

        foreach (var carton in candidateCartons)
        {
            var solution = PackIntoSpecificCarton(items, carton, enforceBalance);

            if (solution.AllItemsPlaced)
            {
                if (!enforceBalance || solution.IsBalanced)
                {
                    return solution;
                }

                // If enforceBalance is required but unbalanced, store it as candidate while checking larger cartons
                bestPartialSolution ??= solution;
            }
            else
            {
                if (bestPartialSolution is null || solution.PlacedItems.Count > bestPartialSolution.PlacedItems.Count)
                {
                    bestPartialSolution = solution;
                }
            }
        }

        // If no carton achieved complete balanced packing, return best partial or largest carton solution
        return bestPartialSolution ?? PackIntoSpecificCarton(items, cartons[^1], enforceBalance);
    }

    /// <inheritdoc />
    public PackingSolution PackIntoSpecificCarton(
        IReadOnlyList<PackingItem> items,
        StandardCartonSpec carton,
        bool enforceBalance = false)
    {
        var itemLookup = items.ToDictionary(i => i.ItemId);

        // Try standard bottom-left-floor packing
        var solution = ExecutePackingPass(items, carton, itemLookup, centerBiased: false);

        // If balance enforcement is enabled and solution is imbalanced, try center-biased packing
        if (enforceBalance && !solution.IsBalanced && solution.AllItemsPlaced)
        {
            var centerSolution = ExecutePackingPass(items, carton, itemLookup, centerBiased: true);
            if (centerSolution.AllItemsPlaced && (centerSolution.IsBalanced || !solution.IsBalanced))
            {
                return centerSolution;
            }
        }

        return solution;
    }

    private PackingSolution ExecutePackingPass(
        IReadOnlyList<PackingItem> items,
        StandardCartonSpec carton,
        IReadOnlyDictionary<string, PackingItem> itemLookup,
        bool centerBiased)
    {
        // 1. Sort items:
        // Priority 1: HeavyBase first (tier 2), then Standard (tier 1), then Fragile (tier 0)
        // Priority 2: Weight descending (heavier items create solid bottom foundation)
        // Priority 3: Volume descending (larger items first)
        var sortedItems = items
            .OrderByDescending(i => (int)i.FragilityTier)
            .ThenByDescending(i => i.WeightGrams)
            .ThenByDescending(i => i.Dimensions.VolumeCm3)
            .ToList();

        var placedItems = new List<PackedItemPlacement>();
        var unplacedItems = new List<PackingItem>();

        var candidatePoints = new List<Point3D> { new(0, 0, 0) };

        double containerW = carton.InnerDimensions.Width;
        double containerH = carton.InnerDimensions.Height;
        double containerD = carton.InnerDimensions.Depth;

        double currentPlacedWeight = 0;

        foreach (var item in sortedItems)
        {
            // Weight capacity guard
            if (currentPlacedWeight + item.WeightGrams > carton.MaxWeightGrams)
            {
                unplacedItems.Add(item);
                continue;
            }

            var allowedOrientations = GetAllowedOrientations(item);
            (Point3D Point, OrientationOption Orientation, int LayerIndex)? bestPlacement = null;
            double bestScore = double.MaxValue;

            // Sort candidate points:
            // Lowest height Z first (floor first), then Y (depth), then X (width)
            var sortedPoints = candidatePoints.OrderBy(p => p.Z);
            if (centerBiased)
            {
                double midX = containerW / 2.0;
                double midY = containerD / 2.0;
                sortedPoints = sortedPoints
                    .ThenBy(p => Math.Abs(p.X - midX) + Math.Abs(p.Y - midY))
                    .ThenBy(p => p.Y)
                    .ThenBy(p => p.X);
            }
            else
            {
                sortedPoints = sortedPoints.ThenBy(p => p.Y).ThenBy(p => p.X);
            }

            foreach (var point in sortedPoints)
            {
                // HeavyBase items strictly prefer floor (Z == 0) if floor space exists
                if (item.FragilityTier == FragilityTier.HeavyBase && point.Z > Tolerance &&
                    candidatePoints.Any(p => p.Z <= Tolerance))
                {
                    continue;
                }

                foreach (var orientation in allowedOrientations)
                {
                    // Check bounds
                    if (point.X + orientation.Width > containerW + Tolerance ||
                        point.Y + orientation.Depth > containerD + Tolerance ||
                        point.Z + orientation.Height > containerH + Tolerance)
                    {
                        continue;
                    }

                    var candidatePlacement = new PackedItemPlacement(
                        item.ItemId,
                        item.Sku,
                        point.X,
                        point.Y,
                        point.Z,
                        orientation.Width,
                        orientation.Height,
                        orientation.Depth,
                        orientation.AxisName,
                        0);

                    // Check collisions with all placed items
                    bool collides = placedItems.Any(p => CheckOverlap(candidatePlacement, p));
                    if (collides)
                    {
                        continue;
                    }

                    // Check gravity & surface support
                    if (!ValidateSupportAndStability(candidatePlacement, placedItems, itemLookup, out int layerIndex))
                    {
                        continue;
                    }

                    // Calculate placement fitness score
                    // Minimize Z, then compact surface coverage
                    double score = (point.Z * 1000.0) + (point.Y * 10.0) + point.X;
                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestPlacement = (point, orientation, layerIndex);
                    }
                }

                // If found a valid placement on floor (Z == 0), don't look higher
                if (bestPlacement is not null && bestPlacement.Value.Point.Z <= Tolerance)
                {
                    break;
                }
            }

            if (bestPlacement is not null)
            {
                var bp = bestPlacement.Value;
                var finalPlacement = new PackedItemPlacement(
                    item.ItemId,
                    item.Sku,
                    bp.Point.X,
                    bp.Point.Y,
                    bp.Point.Z,
                    bp.Orientation.Width,
                    bp.Orientation.Height,
                    bp.Orientation.Depth,
                    bp.Orientation.AxisName,
                    bp.LayerIndex);

                placedItems.Add(finalPlacement);
                currentPlacedWeight += item.WeightGrams;

                // Update candidate points
                UpdateCandidatePoints(candidatePoints, finalPlacement, placedItems, containerW, containerH, containerD);
            }
            else
            {
                unplacedItems.Add(item);
            }
        }

        // Center of Gravity and Balance calculations
        var (cogX, cogY, cogZ, isBalanced) = CalculateCenterOfGravity(carton, placedItems, itemLookup);

        double totalPlacedVolume = placedItems.Sum(p => p.VolumeCm3);
        double utilization = carton.VolumeCm3 > 0
            ? Math.Round((totalPlacedVolume / carton.VolumeCm3) * 100.0, 2)
            : 0.0;

        double totalGrossWeight = currentPlacedWeight + carton.TareWeightGrams;

        // Build step-by-step loading instructions
        var instructions = GenerateInstructions(
            carton,
            placedItems,
            unplacedItems,
            itemLookup,
            cogX, cogY, cogZ,
            isBalanced,
            utilization,
            totalGrossWeight);

        return new PackingSolution(
            CartonSpec: carton,
            PlacedItems: placedItems,
            UnplacedItems: unplacedItems,
            VolumeUtilizationPercentage: utilization,
            TotalGrossWeightGrams: totalGrossWeight,
            CenterOfGravityX: Math.Round(cogX, 2),
            CenterOfGravityY: Math.Round(cogY, 2),
            CenterOfGravityZ: Math.Round(cogZ, 2),
            IsBalanced: isBalanced,
            StepByStepInstructions: instructions);
    }

    /// <inheritdoc />
    public bool CheckOverlap(PackedItemPlacement a, PackedItemPlacement b) => a.OverlapsWith(b, Tolerance);

    /// <inheritdoc />
    public (double CoGX, double CoGY, double CoGZ, bool IsBalanced) CalculateCenterOfGravity(
        StandardCartonSpec carton,
        IReadOnlyList<PackedItemPlacement> placements,
        IReadOnlyDictionary<string, PackingItem> itemLookup)
    {
        double geoX = carton.InnerDimensions.Width / 2.0;
        double geoY = carton.InnerDimensions.Depth / 2.0;
        double geoZ = carton.InnerDimensions.Height / 2.0;

        if (placements.Count == 0)
        {
            return (geoX, geoY, 0.0, true);
        }

        double totalMass = 0;
        double weightedX = 0;
        double weightedY = 0;
        double weightedZ = 0;

        foreach (var p in placements)
        {
            double mass = itemLookup.TryGetValue(p.ItemId, out var item) ? item.WeightGrams : 1.0;
            if (mass <= 0) mass = 1.0;

            double itemCenterX = p.PositionX + (p.PlacedWidth / 2.0);
            double itemCenterY = p.PositionY + (p.PlacedDepth / 2.0);
            double itemCenterZ = p.PositionZ + (p.PlacedHeight / 2.0);

            weightedX += mass * itemCenterX;
            weightedY += mass * itemCenterY;
            weightedZ += mass * itemCenterZ;
            totalMass += mass;
        }

        double cogX = weightedX / totalMass;
        double cogY = weightedY / totalMass;
        double cogZ = weightedZ / totalMass;

        double devX = geoX > 0 ? Math.Abs(cogX - geoX) / geoX : 0.0;
        double devY = geoY > 0 ? Math.Abs(cogY - geoY) / geoY : 0.0;

        bool isBalanced = devX <= BalanceToleranceFraction && devY <= BalanceToleranceFraction;

        return (cogX, cogY, cogZ, isBalanced);
    }

    private bool ValidateSupportAndStability(
        PackedItemPlacement candidate,
        IReadOnlyList<PackedItemPlacement> placedItems,
        IReadOnlyDictionary<string, PackingItem> itemLookup,
        out int layerIndex)
    {
        // 1. Container floor is completely supported
        if (candidate.PositionZ <= Tolerance)
        {
            layerIndex = 0;
            return true;
        }

        // 2. Must be supported by items directly below at top surface Z == candidate.PositionZ
        double supportedArea = 0;
        int maxBelowLayer = -1;
        bool candidateIsFragile = itemLookup.TryGetValue(candidate.ItemId, out var cItem) &&
                                  cItem.FragilityTier == FragilityTier.Fragile;

        foreach (var p in placedItems)
        {
            double topZ = p.PositionZ + p.PlacedHeight;
            if (Math.Abs(topZ - candidate.PositionZ) <= Tolerance)
            {
                // Fragile protection: heavy or standard items cannot be stacked on fragile items
                if (itemLookup.TryGetValue(p.ItemId, out var baseItem) &&
                    baseItem.FragilityTier == FragilityTier.Fragile &&
                    !candidateIsFragile)
                {
                    layerIndex = 0;
                    return false; // Prohibited: resting heavier item on fragile item
                }

                // Calculate XY intersection area
                double ox = Math.Max(0.0, Math.Min(candidate.PositionX + candidate.PlacedWidth, p.PositionX + p.PlacedWidth) -
                                          Math.Max(candidate.PositionX, p.PositionX));
                double oy = Math.Max(0.0, Math.Min(candidate.PositionY + candidate.PlacedDepth, p.PositionY + p.PlacedDepth) -
                                          Math.Max(candidate.PositionY, p.PositionY));

                if (ox > 0 && oy > 0)
                {
                    supportedArea += ox * oy;
                    if (p.LayerIndex > maxBelowLayer)
                    {
                        maxBelowLayer = p.LayerIndex;
                    }
                }
            }
        }

        double baseArea = candidate.PlacedWidth * candidate.PlacedDepth;
        if (baseArea <= Tolerance)
        {
            layerIndex = 0;
            return false;
        }

        double supportRatio = supportedArea / baseArea;
        if (supportRatio >= MinSupportRatio - Tolerance)
        {
            layerIndex = maxBelowLayer + 1;
            return true;
        }

        layerIndex = 0;
        return false;
    }

    private void UpdateCandidatePoints(
        List<Point3D> candidatePoints,
        PackedItemPlacement placed,
        IReadOnlyList<PackedItemPlacement> allPlaced,
        double containerW,
        double containerH,
        double containerD)
    {
        // Remove points strictly inside the newly placed item
        candidatePoints.RemoveAll(p =>
            p.X >= placed.PositionX - Tolerance &&
            p.X < placed.PositionX + placed.PlacedWidth - Tolerance &&
            p.Y >= placed.PositionY - Tolerance &&
            p.Y < placed.PositionY + placed.PlacedDepth - Tolerance &&
            p.Z >= placed.PositionZ - Tolerance &&
            p.Z < placed.PositionZ + placed.PlacedHeight - Tolerance);

        // Add 3 standard corner points
        var newPoints = new List<Point3D>
        {
            new(placed.PositionX + placed.PlacedWidth, placed.PositionY, placed.PositionZ),
            new(placed.PositionX, placed.PositionY + placed.PlacedDepth, placed.PositionZ),
            new(placed.PositionX, placed.PositionY, placed.PositionZ + placed.PlacedHeight)
        };

        // Projected corners with existing placements for tight cuboid packing
        foreach (var other in allPlaced)
        {
            newPoints.Add(new(other.PositionX + other.PlacedWidth, placed.PositionY, placed.PositionZ));
            newPoints.Add(new(placed.PositionX, other.PositionY + other.PlacedDepth, placed.PositionZ));
            newPoints.Add(new(placed.PositionX, placed.PositionY, other.PositionZ + other.PlacedHeight));
        }

        foreach (var np in newPoints)
        {
            // Verify bounds
            if (np.X < 0 || np.X >= containerW - Tolerance ||
                np.Y < 0 || np.Y >= containerD - Tolerance ||
                np.Z < 0 || np.Z >= containerH - Tolerance)
            {
                continue;
            }

            // Verify not inside any placed item
            bool insideAny = allPlaced.Any(p =>
                np.X >= p.PositionX - Tolerance && np.X < p.PositionX + p.PlacedWidth - Tolerance &&
                np.Y >= p.PositionY - Tolerance && np.Y < p.PositionY + p.PlacedDepth - Tolerance &&
                np.Z >= p.PositionZ - Tolerance && np.Z < p.PositionZ + p.PlacedHeight - Tolerance);

            if (!insideAny && !candidatePoints.Any(existing =>
                Math.Abs(existing.X - np.X) < Tolerance &&
                Math.Abs(existing.Y - np.Y) < Tolerance &&
                Math.Abs(existing.Z - np.Z) < Tolerance))
            {
                candidatePoints.Add(np);
            }
        }
    }

    private static IReadOnlyList<OrientationOption> GetAllowedOrientations(PackingItem item)
    {
        double w = item.Dimensions.Width;
        double h = item.Dimensions.Height;
        double d = item.Dimensions.Depth;

        if (!item.AllowedRotations)
        {
            return [new(w, h, d, "None (Original)")];
        }

        if (item.Orientation == OrientationRestriction.ThisSideUp)
        {
            // Height must remain along vertical Z axis.
            // Only horizontal rotation around vertical Z axis (yaw) is allowed.
            if (Math.Abs(w - d) < Tolerance)
            {
                return [new(w, h, d, "ThisSideUp-Original")];
            }

            return
            [
                new(w, h, d, "ThisSideUp-Original"),
                new(d, h, w, "ThisSideUp-Yaw90")
            ];
        }

        // 6-axis orthogonal rotations:
        // (PlacedWidth along X, PlacedHeight along Z, PlacedDepth along Y)
        OrientationOption[] allSix =
        [
            new(w, h, d, "Orthogonal-W-H-D"),
            new(d, h, w, "Orthogonal-D-H-W"),
            new(w, d, h, "Orthogonal-W-D-H"),
            new(h, d, w, "Orthogonal-H-D-W"),
            new(d, w, h, "Orthogonal-D-W-H"),
            new(h, w, d, "Orthogonal-H-W-D")
        ];

        // Deduplicate identical dimension triplets
        var distinct = new List<OrientationOption>();
        foreach (var opt in allSix)
        {
            if (!distinct.Any(e =>
                Math.Abs(e.Width - opt.Width) < Tolerance &&
                Math.Abs(e.Height - opt.Height) < Tolerance &&
                Math.Abs(e.Depth - opt.Depth) < Tolerance))
            {
                distinct.Add(opt);
            }
        }

        return distinct;
    }

    private static IReadOnlyList<string> GenerateInstructions(
        StandardCartonSpec carton,
        IReadOnlyList<PackedItemPlacement> placedItems,
        IReadOnlyList<PackingItem> unplacedItems,
        IReadOnlyDictionary<string, PackingItem> itemLookup,
        double cogX, double cogY, double cogZ,
        bool isBalanced,
        double utilization,
        double totalGrossWeight)
    {
        var steps = new List<string>();

        // Order placements for physical execution: bottom layers first, then back-to-front
        var sequenced = placedItems
            .OrderBy(p => p.LayerIndex)
            .ThenBy(p => p.PositionZ)
            .ThenBy(p => p.PositionY)
            .ThenBy(p => p.PositionX)
            .ToList();

        steps.Add($"Cartonization Setup: Prepare carton '{carton.Name}' ({carton.InnerDimensions.Width}x{carton.InnerDimensions.Height}x{carton.InnerDimensions.Depth} cm, Max Payload: {carton.MaxWeightGrams}g, Tare: {carton.TareWeightGrams}g).");

        int stepNum = 1;
        foreach (var p in sequenced)
        {
            itemLookup.TryGetValue(p.ItemId, out var item);
            string title = item?.Title ?? p.Sku;
            double weight = item?.WeightGrams ?? 0;
            string tier = item?.FragilityTier.ToString() ?? "Standard";

            string supportDesc = p.PositionZ <= Tolerance
                ? "Floor foundation"
                : $"Supported at Z={p.PositionZ:F1} cm (Layer {p.LayerIndex})";

            steps.Add($"Step {stepNum++}: Pack SKU '{p.Sku}' ('{title}') [{p.PlacedWidth}x{p.PlacedHeight}x{p.PlacedDepth} cm, {weight}g, {tier}]. Position at (X: {p.PositionX:F1}, Y: {p.PositionY:F1}, Z: {p.PositionZ:F1}) cm. {supportDesc}. Rotation: {p.RotationAxis}.");
        }

        if (unplacedItems.Count > 0)
        {
            steps.Add($"Warning: {unplacedItems.Count} item(s) could not be placed due to volume/weight constraints: {string.Join(", ", unplacedItems.Select(u => u.Sku))}.");
        }

        double geoX = carton.InnerDimensions.Width / 2.0;
        double geoY = carton.InnerDimensions.Depth / 2.0;
        double devXPct = geoX > 0 ? (Math.Abs(cogX - geoX) / geoX) * 100.0 : 0.0;
        double devYPct = geoY > 0 ? (Math.Abs(cogY - geoY) / geoY) * 100.0 : 0.0;

        string balanceStatus = isBalanced
            ? "BALANCED (Passed <= 20% limit)"
            : "IMBALANCED (Warning: Exceeds 20% center deviation)";

        steps.Add($"Final Quality Inspection: Total items packed: {placedItems.Count}/{placedItems.Count + unplacedItems.Count}. Volume utilization: {utilization:F1}%. Gross weight: {totalGrossWeight:F1}g. Center of Gravity: (X: {cogX:F1}, Y: {cogY:F1}, Z: {cogZ:F1}) cm vs Box Center: (X: {geoX:F1}, Y: {geoY:F1}) cm (Dev: X={devXPct:F1}%, Y={devYPct:F1}%). Load status: {balanceStatus}.");

        return steps;
    }
}
