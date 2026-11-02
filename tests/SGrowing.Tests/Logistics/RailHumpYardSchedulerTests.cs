namespace SGrowing.Tests.Logistics;

using FluentAssertions;
using SGrowing.Api.Modules.Logistics.RailHumpYardScheduler;
using Xunit;

public class RailHumpYardSchedulerTests
{
    private readonly RailHumpYardSchedulerEngine _engine = new();

    [Fact]
    public void ScheduleHumpShunting_MultipleDestinations_GroupsIntoCutsAndAssignsTracks()
    {
        var tracks = new List<ClassificationTrack>
        {
            new("TRACK-LYON", "Lyon-Guillotiere", CapacityMeters: 600.0, CurrentOccupancyMeters: 100.0, AllowsHazardousCargo: true),
            new("TRACK-MARSEILLE", "Marseille-Miramas", CapacityMeters: 750.0, CurrentOccupancyMeters: 50.0, AllowsHazardousCargo: true)
        };

        var wagons = new List<FreightWagon>
        {
            new("W-01", "Steel Coils", 80.0, "Lyon-Guillotiere", false, 1),
            new("W-02", "Steel Coils", 75.0, "Lyon-Guillotiere", false, 2),
            new("W-03", "Chemicals", 65.0, "Marseille-Miramas", true, 3), // Destination switches -> Cut 2
            new("W-04", "Containerized Auto Parts", 45.0, "Marseille-Miramas", false, 4)
        };

        var request = new HumpShuntingRequest("TRAIN-FREIGHT-881", wagons, tracks);
        var result = _engine.ScheduleHumpShunting(request);

        result.InboundTrainId.Should().Be("TRAIN-FREIGHT-881");
        result.TotalWagonsSorted.Should().Be(4);
        result.TotalCutsProduced.Should().Be(2);
        result.AllHazardousSegregated.Should().BeTrue();

        result.ShuntingPlan[0].TargetTrackId.Should().Be("TRACK-LYON");
        result.ShuntingPlan[0].RollCutNumber.Should().Be(1);
        result.ShuntingPlan[2].TargetTrackId.Should().Be("TRACK-MARSEILLE");
        result.ShuntingPlan[2].RollCutNumber.Should().Be(2);
        result.ShuntingPlan[2].BufferCarRequired.Should().BeTrue(); // Hazmat wagon
    }

    [Fact]
    public void ScheduleHumpShunting_HeavyWagon_AssignsLowerReleaseSpeedForRetarders()
    {
        var tracks = new List<ClassificationTrack>
        {
            new("TRACK-MAIN", "Rotterdam", 800.0, 0.0, true)
        };

        var wagons = new List<FreightWagon>
        {
            new("W-HEAVY", "Iron Ore", GrossWeightTons: 88.0, "Rotterdam", false, 1),
            new("W-LIGHT", "Empty Boxcar", GrossWeightTons: 22.0, "Rotterdam", false, 2)
        };

        var request = new HumpShuntingRequest("TRAIN-BULK-01", wagons, tracks);
        var result = _engine.ScheduleHumpShunting(request);

        result.ShuntingPlan[0].TargetRollingSpeedMps.Should().Be(3.8); // Heavy
        result.ShuntingPlan[1].TargetRollingSpeedMps.Should().Be(4.5); // Light
    }

    [Fact]
    public void ScheduleHumpShunting_EmptyWagonList_ReturnsZeroSortedAndZeroCuts()
    {
        var tracks = new List<ClassificationTrack>
        {
            new("TRACK-1", "Depot", 500.0, 0.0, true)
        };

        var request = new HumpShuntingRequest("TRAIN-EMPTY", [], tracks);
        var result = _engine.ScheduleHumpShunting(request);

        result.TotalWagonsSorted.Should().Be(0);
        result.TotalCutsProduced.Should().Be(0);
        result.EfficiencySummary.Should().Contain("Empty Inbound Train");
    }

    [Fact]
    public void ScheduleHumpShunting_NoDedicatedTrackForDestination_FallsBackGracefully()
    {
        var tracks = new List<ClassificationTrack>
        {
            new("TRACK-DEFAULT", "Antwerp", 500.0, 0.0, true)
        };

        var wagons = new List<FreightWagon>
        {
            new("W-UNKNOWN", "Grain", 55.0, "UnknownDestinationYard", false, 1)
        };

        var request = new HumpShuntingRequest("TRAIN-MYSTERY", wagons, tracks);
        var result = _engine.ScheduleHumpShunting(request);

        result.ShuntingPlan.Should().ContainSingle();
        result.ShuntingPlan[0].TargetTrackId.Should().Be("TRACK-DEFAULT");
    }
}
