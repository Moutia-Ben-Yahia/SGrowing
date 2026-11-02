namespace SGrowing.Tests.Aviation;

using FluentAssertions;
using SGrowing.Api.Modules.Aviation.AircraftTurnaroundOptimizer;
using Xunit;

public class AircraftTurnaroundTests
{
    private readonly AircraftTurnaroundOptimizerEngine _engine = new();

    [Fact]
    public void OptimizeTurnaround_StandardTurnaroundSequence_MeetsDepartureSlot()
    {
        var inBlock = DateTimeOffset.UtcNow;
        var scheduledDep = inBlock.AddMinutes(50); // 50 min allocated slot

        var tasks = new List<TurnaroundTask>
        {
            new("T-DEBOARD", "Deboarding", DurationMinutes: 12.0, DependsOnTaskIds: []),
            new("T-CLEAN", "Cleaning", DurationMinutes: 10.0, DependsOnTaskIds: ["T-DEBOARD"]),
            new("T-CATER", "Catering", DurationMinutes: 12.0, DependsOnTaskIds: ["T-DEBOARD"]),
            new("T-FUEL", "Refueling", DurationMinutes: 15.0, DependsOnTaskIds: ["T-DEBOARD"]),
            new("T-BOARD", "Boarding", DurationMinutes: 15.0, DependsOnTaskIds: ["T-CLEAN", "T-CATER"]),
            new("T-PUSH", "Pushback", DurationMinutes: 3.0, DependsOnTaskIds: ["T-BOARD", "T-FUEL"])
        };

        var request = new FlightTurnaroundRequest("AF-1234", "Airbus A320neo", "STAND-E12", inBlock, scheduledDep, tasks, FuelRequiredLiters: 6000.0, PassengerCount: 160);
        var result = _engine.OptimizeTurnaround(request);

        result.FlightNumber.Should().Be("AF-1234");
        result.WillMeetSlot.Should().BeTrue();
        result.SlotSlackMinutes.Should().BeGreaterThan(0.0);
        result.Schedule.Should().HaveCount(6);
        result.BottleneckAnalysis.Should().Contain("On-Time");
    }

    [Fact]
    public void OptimizeTurnaround_LargeFuelRequirementExtendsDuration_DetectsSlotDelayRisk()
    {
        var inBlock = DateTimeOffset.UtcNow;
        var tightDeparture = inBlock.AddMinutes(35); // Very tight 35 min slot

        var tasks = new List<TurnaroundTask>
        {
            new("T-DEBOARD", "Deboarding", 15.0, []),
            new("T-FUEL", "Refueling", 15.0, ["T-DEBOARD"]),
            new("T-PUSH", "Pushback", 5.0, ["T-FUEL"])
        };

        // 36,000 Liters -> 36000 / 1200 = 30 minutes refueling duration alone!
        var request = new FlightTurnaroundRequest("LH-5678", "Boeing 787-9", "STAND-G04", inBlock, tightDeparture, tasks, FuelRequiredLiters: 36_000.0, PassengerCount: 280);
        var result = _engine.OptimizeTurnaround(request);

        result.WillMeetSlot.Should().BeFalse();
        result.SlotSlackMinutes.Should().BeLessThan(0.0);
        result.BottleneckAnalysis.Should().Contain("Slot Delay Risk");
    }

    [Fact]
    public void OptimizeTurnaround_EmptyTaskList_ReturnsInstantTobtWithFullSlack()
    {
        var inBlock = DateTimeOffset.UtcNow;
        var dep = inBlock.AddMinutes(45);

        var request = new FlightTurnaroundRequest("BA-999", "A321", "STAND-A1", inBlock, dep, [], 0, 0);
        var result = _engine.OptimizeTurnaround(request);

        result.TurnaroundDurationMinutes.Should().Be(0.0);
        result.WillMeetSlot.Should().BeTrue();
        result.SlotSlackMinutes.Should().Be(45.0);
        result.Schedule.Should().BeEmpty();
    }

    [Fact]
    public void OptimizeTurnaround_TasksWithParallelIndependentBranches_CorrectlyCalculatesTobt()
    {
        var inBlock = DateTimeOffset.UtcNow;
        var dep = inBlock.AddMinutes(60);

        var tasks = new List<TurnaroundTask>
        {
            new("T-CARGO", "CargoDischarge", DurationMinutes: 20.0, DependsOnTaskIds: []),
            new("T-PAX", "PaxDeboard", DurationMinutes: 10.0, DependsOnTaskIds: []),
            new("T-FINAL", "DepartureClearance", DurationMinutes: 5.0, DependsOnTaskIds: ["T-CARGO", "T-PAX"])
        };

        var request = new FlightTurnaroundRequest("QR-88", "B777F", "STAND-C2", inBlock, dep, tasks, 0, 0);
        var result = _engine.OptimizeTurnaround(request);

        // Max branch is Cargo (20 min) + Final (5 min) = 25 minutes
        result.TurnaroundDurationMinutes.Should().Be(25.0);
        result.WillMeetSlot.Should().BeTrue();
    }
}
