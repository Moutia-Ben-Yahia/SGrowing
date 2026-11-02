using FluentAssertions;
using SGrowing.Api.Modules.Commerce.WarehouseSlotting;
using Xunit;

namespace SGrowing.Tests.Commerce;

public sealed class WarehouseSlottingTests
{
    private readonly DynamicSlottingOptimizerEngine _slottingEngine = new();
    private readonly PalletLoadPhysicsPacker _palletPacker = new();

    #region Warehouse Slotting Optimization Tests

    [Fact]
    public void OptimizeSlotting_FastMovingProfiles_AssignsGoldenZoneLevel2()
    {
        var profiles = new List<SkuPickProfile>
        {
            new("SKU-FAST-PHONE-CASE", "Silicone Phone Case", PicksPerDay: 500.0, 15.0, 8.0, 2.0, WeightKg: 0.15, false, false),
            new("SKU-MED-CHARGER", "Fast Charger 65W", PicksPerDay: 100.0, 8.0, 6.0, 4.0, WeightKg: 0.25, false, false),
            new("SKU-SLOW-ADAPTER", "VGA to HDMI Adapter", PicksPerDay: 5.0, 10.0, 5.0, 2.0, WeightKg: 0.10, false, false)
        };

        var assignments = _slottingEngine.OptimizeSlotting(new OptimizeSlottingRequest("WH-ALGIERS-CENTRAL", profiles));

        assignments.Should().HaveCount(3);

        var fast = assignments.First(a => a.Sku == "SKU-FAST-PHONE-CASE");
        fast.Velocity.Should().Be(VelocityTier.FastA);
        fast.AssignedZone.Should().Be(WarehouseRackZone.GoldenZoneLevel2);
        fast.EstimatedPickTravelTimeSeconds.Should().BeLessThan(10.0);

        var slow = assignments.First(a => a.Sku == "SKU-SLOW-ADAPTER");
        slow.Velocity.Should().Be(VelocityTier.SlowC);
        slow.AssignedZone.Should().Be(WarehouseRackZone.TopLevel3);
    }

    [Fact]
    public void OptimizeSlotting_HeavyItemOver15Kg_AssignsFloorLevelBottomZone()
    {
        var profiles = new List<SkuPickProfile>
        {
            new("SKU-HEAVY-GENERATOR-MOTOR", "5kW Motor Assembly", PicksPerDay: 250.0, 60.0, 40.0, 50.0, WeightKg: 42.0, false, false)
        };

        var assignments = _slottingEngine.OptimizeSlotting(new OptimizeSlottingRequest("WH-ORAN-HUB", profiles));

        var heavy = assignments[0];
        heavy.AssignedZone.Should().Be(WarehouseRackZone.BottomLevel1, "Heavy loads must be slotted at floor level for safety.");
        heavy.Rationale.Should().Contain("15kg");
    }

    #endregion

    #region 3D Pallet Loading & Center of Mass Stability Tests

    [Fact]
    public void PackPallet_DenseAndFragileMix_PlacesDenseOnBottomAndFragileOnTop()
    {
        var cartons = new List<CartonItem>
        {
            new("CART-FRAGILE-GLASS", "SKU-GLASSWARE", WeightKg: 5.0, 30.0, 30.0, 30.0, DensityKgPerM3: 185.0, IsFragile: true),
            new("CART-HEAVY-TILES-1", "SKU-CERAMIC-TILES", WeightKg: 25.0, 40.0, 30.0, 15.0, DensityKgPerM3: 1380.0, IsFragile: false),
            new("CART-HEAVY-TILES-2", "SKU-CERAMIC-TILES", WeightKg: 25.0, 40.0, 30.0, 15.0, DensityKgPerM3: 1380.0, IsFragile: false),
            new("CART-HEAVY-TILES-3", "SKU-CERAMIC-TILES", WeightKg: 25.0, 40.0, 30.0, 15.0, DensityKgPerM3: 1380.0, IsFragile: false),
            new("CART-HEAVY-TILES-4", "SKU-CERAMIC-TILES", WeightKg: 25.0, 40.0, 30.0, 15.0, DensityKgPerM3: 1380.0, IsFragile: false)
        };

        var plan = _palletPacker.PackPallet(new PalletPackRequest("PALLET-001", cartons));

        plan.Should().NotBeNull();
        plan.TotalWeightKg.Should().Be(105.0);
        plan.Layers.Should().HaveCount(2);

        // Layer 1 should have the heavy ceramic tiles
        plan.Layers[0].Cartons.Should().OnlyContain(c => !c.IsFragile);

        // Layer 2 should have the fragile glassware
        plan.Layers[1].Cartons.Should().Contain(c => c.IsFragile);

        // Center of gravity must be well below 55% of height
        plan.CenterOfGravityHeightRatio.Should().BeLessThanOrEqualTo(0.55);
        plan.IsStable.Should().BeTrue();
        plan.StabilityVerdict.Should().StartWith("STABLE");
    }

    [Fact]
    public void PackPallet_EmptyCartons_ReturnsStableZeroMassPlan()
    {
        var plan = _palletPacker.PackPallet(new PalletPackRequest("PALLET-EMPTY", []));

        plan.TotalWeightKg.Should().Be(0.0);
        plan.Layers.Should().BeEmpty();
        plan.IsStable.Should().BeTrue();
    }

    #endregion
}
