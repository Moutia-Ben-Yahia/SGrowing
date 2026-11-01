using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SGrowing.Api.Modules.Commerce.Robotics;
using Xunit;

namespace SGrowing.Tests.Commerce;

public class AmrRoboticsBinPackingTests
{
    private readonly BinPacking3dEngine _engine = new();
    private readonly WarehousePickRouterService _router = new();
    private readonly PackingVisualizerService _visualizer = new();

    #region 1. 3D Geometry, Dimensions & Volume Calculations

    [Fact]
    public void Dimensions3D_VolumeCalculation_ShouldReturnAccurateVolumeCm3()
    {
        var dim = new Dimensions3D(20.0, 15.0, 10.0);

        dim.VolumeCm3.Should().Be(3000.0);
    }

    [Fact]
    public void Dimensions3D_FitsInside_ShouldReturnTrueWhenWithinBounds_AndFalseWhenExceeding()
    {
        var container = new Dimensions3D(35.0, 20.0, 25.0);
        var smallItem = new Dimensions3D(30.0, 15.0, 20.0);
        var oversizedItem = new Dimensions3D(40.0, 15.0, 20.0);

        smallItem.FitsInside(container).Should().BeTrue();
        oversizedItem.FitsInside(container).Should().BeFalse();
    }

    [Fact]
    public void PackedItemPlacement_VolumeCalculation_ShouldMatchDimensionsProduct()
    {
        var placement = new PackedItemPlacement(
            ItemId: "item-1",
            Sku: "SKU-001",
            PositionX: 0,
            PositionY: 0,
            PositionZ: 0,
            PlacedWidth: 12.0,
            PlacedHeight: 8.0,
            PlacedDepth: 5.0,
            RotationAxis: "None",
            LayerIndex: 0);

        placement.VolumeCm3.Should().Be(480.0);
    }

    [Fact]
    public void PackedItemPlacement_OverlapsWith_DisjointBoxes_ShouldReturnFalse()
    {
        var boxA = new PackedItemPlacement("1", "A", 0, 0, 0, 10, 10, 10, "None", 0);
        var boxB = new PackedItemPlacement("2", "B", 15, 0, 0, 10, 10, 10, "None", 0);

        boxA.OverlapsWith(boxB).Should().BeFalse();
        boxB.OverlapsWith(boxA).Should().BeFalse();
    }

    [Fact]
    public void PackedItemPlacement_OverlapsWith_IntersectingBoxes_ShouldReturnTrue()
    {
        var boxA = new PackedItemPlacement("1", "A", 0, 0, 0, 10, 10, 10, "None", 0);
        var boxB = new PackedItemPlacement("2", "B", 5, 5, 5, 10, 10, 10, "None", 0);

        boxA.OverlapsWith(boxB).Should().BeTrue();
        boxB.OverlapsWith(boxA).Should().BeTrue();
    }

    #endregion

    #region 2. Overlap Detection & Border Conditions

    [Fact]
    public void PackedItemPlacement_OverlapsWith_AbuttingFaces_ShouldReturnFalse()
    {
        // Box A ends exactly where Box B begins along X axis (X=10)
        var boxA = new PackedItemPlacement("1", "A", 0, 0, 0, 10, 10, 10, "None", 0);
        var boxB = new PackedItemPlacement("2", "B", 10, 0, 0, 10, 10, 10, "None", 0);

        boxA.OverlapsWith(boxB).Should().BeFalse();
        boxB.OverlapsWith(boxA).Should().BeFalse();
    }

    [Fact]
    public void BinPacking3dEngine_CheckOverlap_ShouldDelegateToPlacementOverlapsWith()
    {
        var boxA = new PackedItemPlacement("1", "A", 0, 0, 0, 10, 10, 10, "None", 0);
        var boxB = new PackedItemPlacement("2", "B", 2, 2, 2, 10, 10, 10, "None", 0);

        _engine.CheckOverlap(boxA, boxB).Should().BeTrue();
    }

    [Fact]
    public void PackedItemPlacement_OverlapsWith_PartialOverlapInAllThreeAxes_ShouldDetectCollision()
    {
        var boxA = new PackedItemPlacement("1", "A", 2, 2, 2, 8, 8, 8, "None", 0);
        var boxB = new PackedItemPlacement("2", "B", 7, 7, 7, 8, 8, 8, "None", 0);

        boxA.OverlapsWith(boxB).Should().BeTrue();
    }

    #endregion

    #region 3. Item Rotation Constraints & Orthogonal Permutations

    [Fact]
    public void PackItems_WhenAllowedRotationsFalse_ShouldMaintainOriginalOrientation()
    {
        var carton = new StandardCartonSpec("c1", "Custom", new Dimensions3D(50, 50, 50), 20000, 200);
        var item = new PackingItem(
            ItemId: "fix-1",
            Sku: "SKU-FIXED",
            Title: "Fixed Orientation Machine Part",
            Dimensions: new Dimensions3D(25, 10, 15),
            WeightGrams: 1000,
            AllowedRotations: false);

        var solution = _engine.PackIntoSpecificCarton([item], carton);

        solution.AllItemsPlaced.Should().BeTrue();
        var placed = solution.PlacedItems[0];
        placed.PlacedWidth.Should().Be(25.0);
        placed.PlacedHeight.Should().Be(10.0);
        placed.PlacedDepth.Should().Be(15.0);
    }

    [Fact]
    public void PackItems_WhenThisSideUp_ShouldPreserveHeightDimensionAlongZ()
    {
        var carton = new StandardCartonSpec("c1", "Custom", new Dimensions3D(40, 40, 40), 20000, 200);
        var item = new PackingItem(
            ItemId: "up-1",
            Sku: "SKU-LIQUID-01",
            Title: "Chemical Solvent Bottle",
            Dimensions: new Dimensions3D(12, 25, 8),
            WeightGrams: 800,
            Orientation: OrientationRestriction.ThisSideUp);

        var solution = _engine.PackIntoSpecificCarton([item], carton);

        solution.AllItemsPlaced.Should().BeTrue();
        var placed = solution.PlacedItems[0];
        // Vertical dimension (PlacedHeight) must remain original height 25.0
        placed.PlacedHeight.Should().Be(25.0);
    }

    [Fact]
    public void PackItems_WhenThisSideUp_AllowsYawRotationAroundZ()
    {
        // Container has Width=15, Depth=30, Height=25.
        // Item has Width=25, Height=10, Depth=12 (Dimensions: 25 x 10 x 12).
        // Since Width(25) > ContainerWidth(15), item can only fit if rotated around Z axis (Yaw: W becomes 12, D becomes 25).
        var carton = new StandardCartonSpec("c-narrow", "NarrowBox", new Dimensions3D(15, 25, 30), 10000, 200);
        var item = new PackingItem(
            ItemId: "yaw-1",
            Sku: "SKU-SENSOR-YAW",
            Title: "Optical Sensor",
            Dimensions: new Dimensions3D(25, 10, 12),
            WeightGrams: 500,
            Orientation: OrientationRestriction.ThisSideUp);

        var solution = _engine.PackIntoSpecificCarton([item], carton);

        solution.AllItemsPlaced.Should().BeTrue();
        var placed = solution.PlacedItems[0];
        placed.PlacedHeight.Should().Be(10.0); // Height preserved
        placed.PlacedWidth.Should().Be(12.0);  // Yaw rotated
        placed.PlacedDepth.Should().Be(25.0);
    }

    [Fact]
    public void PackItems_WhenAnyAllowed_CanRotateInAnyOrthogonalAxis()
    {
        // Container Width=12, Depth=15, Height=35.
        // Item Width=30, Height=8, Depth=10.
        // Item can only fit if longest dimension (30) is aligned with Height (35) or similar orthogonal permutation.
        var carton = new StandardCartonSpec("c-tall", "TallBox", new Dimensions3D(12, 35, 15), 10000, 200);
        var item = new PackingItem(
            ItemId: "rot-1",
            Sku: "SKU-ROD",
            Title: "Steel Rod",
            Dimensions: new Dimensions3D(30, 8, 10),
            WeightGrams: 1200,
            Orientation: OrientationRestriction.AnyAllowed);

        var solution = _engine.PackIntoSpecificCarton([item], carton);

        solution.AllItemsPlaced.Should().BeTrue();
        var placed = solution.PlacedItems[0];
        placed.PlacedWidth.Should().BeLessThanOrEqualTo(12.0);
        placed.PlacedHeight.Should().BeLessThanOrEqualTo(35.0);
        placed.PlacedDepth.Should().BeLessThanOrEqualTo(15.0);
    }

    [Fact]
    public void PackItems_IdenticalDimensions_CubicItem_ShouldPackSeamlessly()
    {
        var carton = StandardCartonSpec.DefaultPresets[0]; // Box-S (20x15x10)
        var cube = new PackingItem(
            ItemId: "cube-1",
            Sku: "SKU-CUBE-10",
            Title: "Precision Calibration Cube",
            Dimensions: new Dimensions3D(10, 10, 10),
            WeightGrams: 600);

        var solution = _engine.PackIntoSpecificCarton([cube], carton);

        solution.AllItemsPlaced.Should().BeTrue();
        solution.PlacedItems[0].PlacedWidth.Should().Be(10.0);
        solution.PlacedItems[0].PlacedHeight.Should().Be(10.0);
        solution.PlacedItems[0].PlacedDepth.Should().Be(10.0);
    }

    #endregion

    #region 4. Heavy-Base Placement Priority & Foundation Ordering

    [Fact]
    public void PackItems_HeavyBaseItem_ShouldBePlacedOnContainerFloorZZero()
    {
        var carton = StandardCartonSpec.DefaultPresets[1]; // Box-M (35x25x20)
        var lightFragile = new PackingItem("1", "SKU-GLASS", "Glass Flask", new Dimensions3D(10, 8, 10), 150, FragilityTier.Fragile);
        var heavyBase = new PackingItem("2", "SKU-ANVIL", "Cast Iron Base", new Dimensions3D(15, 8, 12), 4500, FragilityTier.HeavyBase);

        var solution = _engine.PackIntoSpecificCarton([lightFragile, heavyBase], carton);

        solution.AllItemsPlaced.Should().BeTrue();
        var placedHeavy = solution.PlacedItems.First(p => p.Sku == "SKU-ANVIL");
        placedHeavy.PositionZ.Should().Be(0.0);
        placedHeavy.LayerIndex.Should().Be(0);
    }

    [Fact]
    public void PackItems_MultipleHeavyBaseItems_ShouldPrioritizeHeavierWeightFirst()
    {
        var carton = StandardCartonSpec.DefaultPresets[2]; // Box-L
        var heavy1 = new PackingItem("h1", "HEAVY-2KG", "Battery Pack", new Dimensions3D(15, 10, 10), 2000, FragilityTier.HeavyBase);
        var heavy2 = new PackingItem("h2", "HEAVY-6KG", "Transformer", new Dimensions3D(15, 10, 10), 6000, FragilityTier.HeavyBase);

        var solution = _engine.PackIntoSpecificCarton([heavy1, heavy2], carton);

        solution.AllItemsPlaced.Should().BeTrue();
        // HEAVY-6KG placed at (0, 0, 0)
        var pHeavy2 = solution.PlacedItems.First(p => p.Sku == "HEAVY-6KG");
        pHeavy2.PositionX.Should().Be(0.0);
        pHeavy2.PositionY.Should().Be(0.0);
        pHeavy2.PositionZ.Should().Be(0.0);
    }

    [Fact]
    public void PackItems_HeavyBaseCannotBePlacedOnTopOfFragileItem()
    {
        var carton = new StandardCartonSpec("c-narrow-tall", "Tower", new Dimensions3D(12, 30, 12), 15000, 200);
        var fragileBase = new PackingItem("f1", "FRAGILE-1", "Porcelain Vase", new Dimensions3D(10, 10, 10), 400, FragilityTier.Fragile);
        var heavy = new PackingItem("h1", "HEAVY-1", "Steel Weight", new Dimensions3D(10, 10, 10), 5000, FragilityTier.HeavyBase);

        var solution = _engine.PackIntoSpecificCarton([fragileBase, heavy], carton);

        // Heavy item must be placed on floor, not on top of fragile item
        var placedHeavy = solution.PlacedItems.First(p => p.Sku == "HEAVY-1");
        placedHeavy.PositionZ.Should().Be(0.0);
    }

    [Fact]
    public void PackItems_SortingOrder_PrioritizesHeavyBaseThenStandardThenFragile()
    {
        var carton = StandardCartonSpec.DefaultPresets[2];
        var fragile = new PackingItem("1", "SKU-F", "Wine Glass", new Dimensions3D(8, 8, 8), 100, FragilityTier.Fragile);
        var standard = new PackingItem("2", "SKU-S", "Book", new Dimensions3D(12, 5, 10), 500, FragilityTier.Standard);
        var heavy = new PackingItem("3", "SKU-H", "Motor", new Dimensions3D(15, 10, 10), 3500, FragilityTier.HeavyBase);

        var solution = _engine.PackIntoSpecificCarton([fragile, standard, heavy], carton);

        solution.AllItemsPlaced.Should().BeTrue();
        var placedH = solution.PlacedItems.First(p => p.Sku == "SKU-H");
        placedH.PositionZ.Should().Be(0.0);
    }

    #endregion

    #region 5. Gravity, Surface Support & Stability Validation

    [Fact]
    public void PackItems_SingleItem_MustRestOnFloorZZero()
    {
        var carton = StandardCartonSpec.DefaultPresets[0];
        var item = new PackingItem("1", "SKU-SINGLE", "Solo Item", new Dimensions3D(10, 5, 8), 400);

        var solution = _engine.PackIntoSpecificCarton([item], carton);

        solution.AllItemsPlaced.Should().BeTrue();
        solution.PlacedItems[0].PositionZ.Should().Be(0.0);
        solution.PlacedItems[0].LayerIndex.Should().Be(0);
    }

    [Fact]
    public void PackItems_StackedItem_MustBeSupportedByBelowItemTopSurface()
    {
        // Container with small floor footprint so items must stack vertically
        var carton = new StandardCartonSpec("c-stack", "StackBox", new Dimensions3D(15, 25, 15), 10000, 200);
        var baseItem = new PackingItem("base", "BASE-01", "Foundation Block", new Dimensions3D(12, 8, 12), 1500, FragilityTier.Standard);
        var topItem = new PackingItem("top", "TOP-02", "Upper Package", new Dimensions3D(10, 6, 10), 500, FragilityTier.Standard);

        var solution = _engine.PackIntoSpecificCarton([baseItem, topItem], carton);

        solution.AllItemsPlaced.Should().BeTrue();
        var placedTop = solution.PlacedItems.First(p => p.Sku == "TOP-02");
        var placedBase = solution.PlacedItems.First(p => p.Sku == "BASE-01");

        // Top item must be resting on top of base item
        placedTop.PositionZ.Should().Be(placedBase.PositionZ + placedBase.PlacedHeight);
        placedTop.LayerIndex.Should().Be(placedBase.LayerIndex + 1);
    }

    [Fact]
    public void PackItems_FloatingItem_ShouldNeverOccurInPackingSolution()
    {
        var carton = StandardCartonSpec.DefaultPresets[1];
        var items = new List<PackingItem>
        {
            new("1", "A", "Base A", new Dimensions3D(15, 8, 12), 1000),
            new("2", "B", "Base B", new Dimensions3D(15, 8, 10), 1000),
            new("3", "C", "Stacked C", new Dimensions3D(10, 6, 8), 400)
        };

        var solution = _engine.PackIntoSpecificCarton(items, carton);

        foreach (var p in solution.PlacedItems)
        {
            if (p.PositionZ > 0.001)
            {
                // Must have a supporting item directly below at top surface Z == p.PositionZ
                solution.PlacedItems.Any(below =>
                    Math.Abs((below.PositionZ + below.PlacedHeight) - p.PositionZ) < 0.001).Should().BeTrue();
            }
        }
    }

    [Fact]
    public void PackItems_StackedItem_AssignsIncrementedLayerIndex()
    {
        var carton = new StandardCartonSpec("c-layers", "LayerBox", new Dimensions3D(15, 30, 15), 10000, 200);
        var item1 = new PackingItem("1", "L1", "Layer 0", new Dimensions3D(10, 8, 10), 800);
        var item2 = new PackingItem("2", "L2", "Layer 1", new Dimensions3D(10, 8, 10), 500);

        var solution = _engine.PackIntoSpecificCarton([item1, item2], carton);

        solution.AllItemsPlaced.Should().BeTrue();
        solution.PlacedItems.Min(p => p.LayerIndex).Should().Be(0);
        solution.PlacedItems.Max(p => p.LayerIndex).Should().Be(1);
    }

    [Fact]
    public void PackItems_WhenCartonHeightIsInsufficient_ShouldMarkItemAsUnplaced()
    {
        // Container height is 10cm. Item height is 15cm, and rotations are disabled.
        var carton = new StandardCartonSpec("c-short", "ShortBox", new Dimensions3D(20, 10, 20), 5000, 100);
        var item = new PackingItem("1", "TALL-01", "Tall Part", new Dimensions3D(10, 15, 10), 500, AllowedRotations: false);

        var solution = _engine.PackIntoSpecificCarton([item], carton);

        solution.AllItemsPlaced.Should().BeFalse();
        solution.UnplacedItems.Should().ContainSingle(u => u.Sku == "TALL-01");
    }

    #endregion

    #region 6. Best Carton Selection & Volumetric Density

    [Fact]
    public void PackItems_SmallItems_ShouldSelectBoxS()
    {
        var item1 = new PackingItem("1", "S1", "Small Widget", new Dimensions3D(8, 6, 8), 300);
        var item2 = new PackingItem("2", "S2", "Small Cable", new Dimensions3D(8, 4, 6), 150);

        var solution = _engine.PackItems([item1, item2]);

        solution.AllItemsPlaced.Should().BeTrue();
        solution.CartonSpec.Name.Should().Be("Box-S");
    }

    [Fact]
    public void PackItems_MediumItems_ExceedingBoxS_ShouldSelectBoxM()
    {
        // Item dimensions (25 x 15 x 18) exceed Box-S (20 x 15 x 10) in Width and Height
        var item = new PackingItem("m1", "MED-01", "Medium Appliance", new Dimensions3D(25, 15, 18), 3500);

        var solution = _engine.PackItems([item]);

        solution.AllItemsPlaced.Should().BeTrue();
        solution.CartonSpec.Name.Should().Be("Box-M");
    }

    [Fact]
    public void PackItems_LargeItems_ExceedingBoxM_ShouldSelectBoxL()
    {
        // Item dimensions (42 x 28 x 22) exceed Box-M (35 x 25 x 20)
        var item = new PackingItem("l1", "LARGE-01", "Large Chassis", new Dimensions3D(42, 22, 28), 9000);

        var solution = _engine.PackItems([item]);

        solution.AllItemsPlaced.Should().BeTrue();
        solution.CartonSpec.Name.Should().Be("Box-L");
    }

    [Fact]
    public void PackItems_OverweightItems_ShouldSelectCartonWithAdequateWeightCapacity()
    {
        // Small dimensions (15 x 8 x 10) fits Box-S volume, but weight is 4500g (exceeds Box-S 3000g limit)
        var denseItem = new PackingItem("dense", "LEAD-WEIGHT", "Lead Counterweight", new Dimensions3D(15, 8, 10), 4500);

        var solution = _engine.PackItems([denseItem]);

        solution.AllItemsPlaced.Should().BeTrue();
        solution.CartonSpec.Name.Should().Be("Box-M"); // Upgrades to Box-M due to weight
    }

    [Fact]
    public void PackItems_OversizedItemExceedingAllCartons_ShouldBeReturnedAsUnplaced()
    {
        // Dimensions 100 x 100 x 100 exceeds Box-XL (70 x 50 x 45)
        var monster = new PackingItem("mon-1", "OVERSIZED-01", "Industrial Generator", new Dimensions3D(100, 100, 100), 50000);

        var solution = _engine.PackItems([monster]);

        solution.AllItemsPlaced.Should().BeFalse();
        solution.UnplacedItems.Should().ContainSingle(u => u.Sku == "OVERSIZED-01");
    }

    #endregion

    #region 7. Center of Gravity & Balance Detection

    [Fact]
    public void CalculateCenterOfGravity_CentrallyPlacedItem_ShouldBeBalanced()
    {
        var carton = new StandardCartonSpec("c-sq", "SquareBox", new Dimensions3D(40, 20, 40), 10000, 200);
        // Place item centered in the box: Box center is X=20, Y=20, Z=10.
        // Item Width=20, Depth=20, Height=10 placed at X=10, Y=10, Z=0.
        // Item center is X=10+10=20, Y=10+10=20, Z=5.
        var placement = new PackedItemPlacement("1", "CENTERED", 10, 10, 0, 20, 10, 20, "None", 0);
        var item = new PackingItem("1", "CENTERED", "Center Weight", new Dimensions3D(20, 10, 20), 2000);
        var lookup = new Dictionary<string, PackingItem> { ["1"] = item };

        var (cogX, cogY, cogZ, isBalanced) = _engine.CalculateCenterOfGravity(carton, [placement], lookup);

        cogX.Should().Be(20.0);
        cogY.Should().Be(20.0);
        cogZ.Should().Be(5.0);
        isBalanced.Should().BeTrue();
    }

    [Fact]
    public void CalculateCenterOfGravity_SingleItemInCorner_CalculatesAccurateCoG()
    {
        var carton = new StandardCartonSpec("c1", "TestBox", new Dimensions3D(40, 20, 40), 10000, 200);
        var placement = new PackedItemPlacement("1", "CORNER", 0, 0, 0, 10, 10, 10, "None", 0);
        var item = new PackingItem("1", "CORNER", "Corner Box", new Dimensions3D(10, 10, 10), 1000);
        var lookup = new Dictionary<string, PackingItem> { ["1"] = item };

        var (cogX, cogY, cogZ, _) = _engine.CalculateCenterOfGravity(carton, [placement], lookup);

        cogX.Should().Be(5.0);
        cogY.Should().Be(5.0);
        cogZ.Should().Be(5.0);
    }

    [Fact]
    public void CalculateCenterOfGravity_HeavilyAsymmetricLoad_FlagsImbalancedWhenDeviationExceeds20Percent()
    {
        // Carton Width=100, Depth=100, Height=40. Center is (50, 50, 20).
        // 20% limit of 50 is [40, 60].
        // Heavy item placed at corner X=0..10, Y=0..10 -> CoG is (5, 5).
        // Deviation is |5 - 50| / 50 = 90% (exceeds 20%).
        var carton = new StandardCartonSpec("c-large", "WideBox", new Dimensions3D(100, 40, 100), 20000, 500);
        var cornerPlacement = new PackedItemPlacement("1", "OFF-CENTER", 0, 0, 0, 10, 10, 10, "None", 0);
        var item = new PackingItem("1", "OFF-CENTER", "Corner Heavy", new Dimensions3D(10, 10, 10), 5000);
        var lookup = new Dictionary<string, PackingItem> { ["1"] = item };

        var (_, _, _, isBalanced) = _engine.CalculateCenterOfGravity(carton, [cornerPlacement], lookup);

        isBalanced.Should().BeFalse();
    }

    [Fact]
    public void PackItems_EnforceBalance_ProducesValidSolution()
    {
        var item = new PackingItem("1", "SKU-BAL", "Single Package", new Dimensions3D(15, 10, 15), 1000);

        var solution = _engine.PackItems([item], enforceBalance: true);

        solution.AllItemsPlaced.Should().BeTrue();
    }

    [Fact]
    public void PackingSolution_GrossWeight_IncludesCartonTareWeight()
    {
        var carton = StandardCartonSpec.DefaultPresets[0]; // Tare = 150g
        var item = new PackingItem("1", "SKU-W", "Test Weight", new Dimensions3D(10, 8, 10), 850);

        var solution = _engine.PackIntoSpecificCarton([item], carton);

        solution.TotalGrossWeightGrams.Should().Be(850 + 150); // 1000g
    }

    #endregion

    #region 8. Step-by-Step Packing Instructions

    [Fact]
    public void PackItems_GeneratesStepByStepInstructions_ForPackersAndRobots()
    {
        var carton = StandardCartonSpec.DefaultPresets[1];
        var item1 = new PackingItem("1", "SKU-STEP1", "Primary Unit", new Dimensions3D(15, 10, 15), 1500, FragilityTier.HeavyBase);
        var item2 = new PackingItem("2", "SKU-STEP2", "Secondary Accessory", new Dimensions3D(10, 6, 8), 300, FragilityTier.Standard);

        var solution = _engine.PackIntoSpecificCarton([item1, item2], carton);

        solution.StepByStepInstructions.Should().NotBeEmpty();
        solution.StepByStepInstructions.Should().Contain(s => s.Contains("Cartonization Setup: Prepare carton 'Box-M'"));
        solution.StepByStepInstructions.Should().Contain(s => s.Contains("Step 1: Pack SKU 'SKU-STEP1'"));
        solution.StepByStepInstructions.Should().Contain(s => s.Contains("Final Quality Inspection: Total items packed"));
    }

    [Fact]
    public void PackItems_EmptyItemsList_ReturnsDefaultEmptySolutionGracefully()
    {
        var solution = _engine.PackItems([]);

        solution.PlacedItems.Should().BeEmpty();
        solution.UnplacedItems.Should().BeEmpty();
        solution.VolumeUtilizationPercentage.Should().Be(0.0);
        solution.IsBalanced.Should().BeTrue();
        solution.StepByStepInstructions.Should().ContainSingle(s => s == "No items to pack.");
    }

    #endregion

    #region 9. AMR Pick Route Optimization & TSP 2-Opt

    [Fact]
    public void OptimizePickRoute_EmptyPicks_ReturnsZeroDistanceRoute()
    {
        var start = new WarehouseBinLocation("DEPOT", "Aisle-00", 0, 0, 0, 0, 0);

        var route = _router.OptimizePickRoute(start, []);

        route.TotalDistanceMeters.Should().Be(0.0);
        route.EstimatedDurationSeconds.Should().Be(0.0);
        route.Waypoints.Should().ContainSingle();
        route.OptimizedPickSequence.Should().BeEmpty();
    }

    [Fact]
    public void OptimizePickRoute_SinglePick_ReturnsDirectPath()
    {
        var start = new WarehouseBinLocation("DEPOT", "Aisle-00", 0, 0, 0, 0, 0);
        var pick = new WarehouseBinLocation("LOC-01", "Aisle-01", 5, 1, 10, 0, 0);

        var route = _router.OptimizePickRoute(start, [pick], amrSpeedMetersPerSec: 2.0, pickDwellTimeSeconds: 10.0);

        route.TotalDistanceMeters.Should().Be(10.0);
        // Duration = 10m / 2.0 m/s (5.0s) + 1 pick * 10.0s = 15.0s
        route.EstimatedDurationSeconds.Should().Be(15.0);
        route.OptimizedPickSequence.Should().ContainSingle(s => s == "LOC-01");
    }

    [Fact]
    public void BenchmarkRoute_AlternatingAislesZigZag_ReducesDistanceByAtLeast40Percent()
    {
        // Warehouse setup with 2 aisles spaced 50 meters apart.
        // Aisle 1 is at X=0, Aisle 2 is at X=50.
        // Points along Y axis from Y=10 to Y=50.
        // Naive input order alternates back and forth across the 50m gap between aisles!
        var start = new WarehouseBinLocation("DEPOT", "Aisle-00", 0, 0, 0, 0, 0);

        var zigZagPicks = new List<WarehouseBinLocation>
        {
            new("A1-1", "Aisle-01", 1, 1, 0, 10, 0),
            new("A2-5", "Aisle-02", 5, 1, 50, 50, 0),
            new("A1-2", "Aisle-01", 2, 1, 0, 20, 0),
            new("A2-4", "Aisle-02", 4, 1, 50, 40, 0),
            new("A1-3", "Aisle-01", 3, 1, 0, 30, 0),
            new("A2-3", "Aisle-02", 3, 1, 50, 30, 0),
            new("A1-4", "Aisle-01", 4, 1, 0, 40, 0),
            new("A2-2", "Aisle-02", 2, 1, 50, 20, 0),
            new("A1-5", "Aisle-01", 5, 1, 0, 50, 0),
            new("A2-1", "Aisle-02", 1, 1, 50, 10, 0),
        };

        var (route, naiveDistance, distanceSavedPercentage) = _router.BenchmarkRoute(start, zigZagPicks);

        route.TotalDistanceMeters.Should().BeLessThan(naiveDistance);
        // Core requirement: distance reduction must be >= 40%
        distanceSavedPercentage.Should().BeGreaterThanOrEqualTo(40.0);
    }

    [Fact]
    public void OptimizePickRoute_CalculatesAccurateEstimatedDuration_WithSpeedAndDwellTime()
    {
        var start = new WarehouseBinLocation("DEPOT", "Aisle-00", 0, 0, 0, 0, 0);
        var picks = new List<WarehouseBinLocation>
        {
            new("P1", "Aisle-01", 1, 1, 10, 0, 0),
            new("P2", "Aisle-01", 2, 1, 20, 0, 0)
        };

        // Total distance: 0 -> 10 -> 20 = 20m
        // Speed: 2.0 m/s -> travel time = 10s
        // Dwell time: 15s per pick * 2 picks = 30s
        // Total duration: 40s
        var route = _router.OptimizePickRoute(start, picks, amrSpeedMetersPerSec: 2.0, pickDwellTimeSeconds: 15.0);

        route.TotalDistanceMeters.Should().Be(20.0);
        route.EstimatedDurationSeconds.Should().Be(40.0);
    }

    [Fact]
    public void CalculateTotalDistance_TraversesSequenceCorrectly()
    {
        var start = new WarehouseBinLocation("S", "Aisle-00", 0, 0, 0, 0, 0);
        var p1 = new WarehouseBinLocation("1", "Aisle-01", 1, 1, 3, 4, 0); // dist from (0,0) is 5
        var p2 = new WarehouseBinLocation("2", "Aisle-01", 2, 1, 6, 8, 0); // dist from (3,4) is 5

        double total = _router.CalculateTotalDistance(start, [p1, p2]);

        total.Should().Be(10.0);
    }

    #endregion

    #region 10. SVG Visualization & Metadata Generation

    [Fact]
    public void PackingVisualizerService_GeneratePackingSvg_ProducesValidSvgWithAllPlacementsAndCoG()
    {
        var carton = StandardCartonSpec.DefaultPresets[1]; // Box-M
        var item1 = new PackingItem("1", "SKU-ALPHA", "Alpha Module", new Dimensions3D(15, 10, 12), 1200, FragilityTier.HeavyBase);
        var item2 = new PackingItem("2", "SKU-BETA", "Beta Sensor", new Dimensions3D(10, 6, 8), 400, FragilityTier.Fragile);

        var solution = _engine.PackIntoSpecificCarton([item1, item2], carton);
        string svg = _visualizer.GeneratePackingSvg(solution);

        svg.Should().NotBeNullOrWhiteSpace();
        svg.Should().StartWith("<svg");
        svg.Should().EndWith("</svg>" + Environment.NewLine);
        svg.Should().Contain("Box-M");
        svg.Should().Contain("SKU-ALPHA");
        svg.Should().Contain("SKU-BETA");
        svg.Should().Contain("Center of Gravity");
        svg.Should().Contain("Top-Down Footprint View (XY Plane)");
        svg.Should().Contain("Front Elevation View (XZ Plane)");
    }

    [Fact]
    public void PackingVisualizerService_GenerateLayerSvg_RendersSpecificLayerOnly()
    {
        var carton = new StandardCartonSpec("c-layer", "LayerBox", new Dimensions3D(20, 30, 20), 10000, 200);
        var baseItem = new PackingItem("1", "BASE-SKU", "Base Unit", new Dimensions3D(15, 10, 15), 1500);
        var topItem = new PackingItem("2", "TOP-SKU", "Top Unit", new Dimensions3D(10, 8, 10), 400);

        var solution = _engine.PackIntoSpecificCarton([baseItem, topItem], carton);

        string layer0Svg = _visualizer.GenerateLayerSvg(solution, layerIndex: 0);
        string layer1Svg = _visualizer.GenerateLayerSvg(solution, layerIndex: 1);

        layer0Svg.Should().Contain("Layer 0 Top-Down View");
        layer0Svg.Should().Contain("BASE-SKU");

        layer1Svg.Should().Contain("Layer 1 Top-Down View");
        layer1Svg.Should().Contain("TOP-SKU");
    }

    [Fact]
    public void PackingVisualizerService_ExtractVisualMetadata_ReturnsAccurateCoordinatesAndDimensions()
    {
        var carton = StandardCartonSpec.DefaultPresets[0]; // Box-S
        var item = new PackingItem("1", "SKU-META", "Meta Item", new Dimensions3D(12, 8, 10), 750);

        var solution = _engine.PackIntoSpecificCarton([item], carton);
        var metadata = _visualizer.ExtractVisualMetadata(solution);

        metadata.Should().NotBeNull();
        metadata.CartonName.Should().Be("Box-S");
        metadata.CartonWidth.Should().Be(20.0);
        metadata.CartonHeight.Should().Be(15.0);
        metadata.CartonDepth.Should().Be(10.0);
        metadata.Items.Should().ContainSingle(i => i.Sku == "SKU-META");
        metadata.Items[0].ColorHex.Should().StartWith("#");
    }

    #endregion

    #region 11. Dependency Injection Registration

    [Fact]
    public void AddAmrRoboticsBinPackingServices_RegistersAllRequiredServicesAsSingletons_WithoutBuildingProvider()
    {
        // Rule: Direct ServiceDescriptor assertion on ServiceCollection without calling BuildServiceProvider()
        var services = new ServiceCollection();
        services.AddAmrRoboticsBinPackingServices();

        services.Should().Contain(d =>
            d.ServiceType == typeof(IBinPacking3dEngine) &&
            d.ImplementationType == typeof(BinPacking3dEngine) &&
            d.Lifetime == ServiceLifetime.Singleton);

        services.Should().Contain(d =>
            d.ServiceType == typeof(IWarehousePickRouterService) &&
            d.ImplementationType == typeof(WarehousePickRouterService) &&
            d.Lifetime == ServiceLifetime.Singleton);

        services.Should().Contain(d =>
            d.ServiceType == typeof(IPackingVisualizerService) &&
            d.ImplementationType == typeof(PackingVisualizerService) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    #endregion
}
