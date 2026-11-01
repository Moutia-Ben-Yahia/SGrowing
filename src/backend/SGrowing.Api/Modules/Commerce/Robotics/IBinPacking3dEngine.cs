namespace SGrowing.Api.Modules.Commerce.Robotics;

/// <summary>
/// Autonomous 3D Bin-Packing Optimization Engine.
/// Solves cuboid container packing with orthogonal rotation, gravity/stability,
/// heavy-base priority, center of gravity balance, and standard carton selection.
/// </summary>
public interface IBinPacking3dEngine
{
    /// <summary>
    /// Evaluates candidate standard cartons and packs items into the smallest carton
    /// that achieves 100% placement with maximum volumetric density.
    /// </summary>
    PackingSolution PackItems(
        IReadOnlyList<PackingItem> items,
        IReadOnlyList<StandardCartonSpec>? availableCartons = null,
        bool enforceBalance = false);

    /// <summary>
    /// Packs items into a designated carton specification.
    /// </summary>
    PackingSolution PackIntoSpecificCarton(
        IReadOnlyList<PackingItem> items,
        StandardCartonSpec carton,
        bool enforceBalance = false);

    /// <summary>
    /// Checks whether two 3D item placements strictly collide or overlap.
    /// </summary>
    bool CheckOverlap(PackedItemPlacement a, PackedItemPlacement b);

    /// <summary>
    /// Calculates the Center of Gravity (CoG) and evaluates if the load is balanced
    /// within 20% horizontal deviation from the container's geometric center.
    /// </summary>
    (double CoGX, double CoGY, double CoGZ, bool IsBalanced) CalculateCenterOfGravity(
        StandardCartonSpec carton,
        IReadOnlyList<PackedItemPlacement> placements,
        IReadOnlyDictionary<string, PackingItem> itemLookup);
}
