namespace SGrowing.Tests.Energy;

using FluentAssertions;
using SGrowing.Api.Modules.Energy.HvdcGridStabilizer;
using Xunit;

public class HvdcGridStabilizerTests
{
    private readonly HvdcGridStabilizerEngine _engine = new();

    [Fact]
    public void StabilizeGrid_SevereUnderFrequency_InjectsSyntheticInertiaAndFastFrequencyResponse()
    {
        var converter = new HvdcConverterStation("CONV-CELTIC-01", "Celtic Interconnector", RatedCapacityMw: 1000.0, CurrentPowerFlowMw: 400.0, MaxRampRateMwPerSec: 100.0, InertiaConstantH: 4.5);

        // PMU detecting 49.75 Hz dip (generation trip, deltaF = -0.25 Hz)
        var pmuReadings = new List<SynchrophasorReading>
        {
            new("PMU-SUB-PARIS", BusVoltageKv: 400.0, FrequencyHz: 49.75, PhaseAngleDegrees: 12.0, RateOfChangeOfFrequencyHzPerSec: -0.45, DateTimeOffset.UtcNow),
            new("PMU-SUB-DUBLIN", BusVoltageKv: 400.0, FrequencyHz: 49.76, PhaseAngleDegrees: 16.5, RateOfChangeOfFrequencyHzPerSec: -0.42, DateTimeOffset.UtcNow)
        };

        var request = new GridStabilizationRequest([converter], pmuReadings, NominalFrequencyHz: 50.0);
        var result = _engine.StabilizeGrid(request);

        result.GridFrequencyStable.Should().BeFalse();
        result.MeanFrequencyHz.Should().BeLessThan(49.80);

        var action = result.Actions.Should().ContainSingle().Subject;
        action.ControlMode.Should().Be("FastFrequencyResponse");
        action.SyntheticInertiaInjectedMw.Should().BeGreaterThan(0.0);
        action.ActivePowerAdjustmentMw.Should().BeGreaterThan(0.0);
        result.GridResilienceSummary.Should().Contain("Under-Frequency Transient");
    }

    [Fact]
    public void StabilizeGrid_InterAreaPhaseAngleDrift_EngagesPowerOscillationDamping()
    {
        var converter = new HvdcConverterStation("CONV-NORDLINK-02", "NordLink Converter", RatedCapacityMw: 1400.0, CurrentPowerFlowMw: 700.0, MaxRampRateMwPerSec: 150.0, InertiaConstantH: 5.0);

        // Frequency normal at 50.01 Hz, but angle drift is 24° (> 15° limit)
        var pmuReadings = new List<SynchrophasorReading>
        {
            new("PMU-OSLO", 420.0, 50.01, PhaseAngleDegrees: 5.0, 0.01, DateTimeOffset.UtcNow),
            new("PMU-BERLIN", 420.0, 50.02, PhaseAngleDegrees: 29.0, 0.01, DateTimeOffset.UtcNow)
        };

        var request = new GridStabilizationRequest([converter], pmuReadings, NominalFrequencyHz: 50.0, MaxAllowedAngleDriftDegrees: 15.0);
        var result = _engine.StabilizeGrid(request);

        result.GridFrequencyStable.Should().BeFalse();
        result.MaxPhaseAngleDeviationDegrees.Should().Be(24.0);

        var action = result.Actions.Should().ContainSingle().Subject;
        action.ControlMode.Should().Be("PowerOscillationDamping");
        action.ActivePowerAdjustmentMw.Should().BeGreaterThan(0.0);
        result.GridResilienceSummary.Should().Contain("Inter-Area Oscillation");
    }

    [Fact]
    public void StabilizeGrid_NominalGridConditions_MaintainsBaseScheduledDispatch()
    {
        var converter = new HvdcConverterStation("CONV-INELFE-03", "INELFE Pyrenees", 1000.0, 500.0, 80.0, 4.0);

        var pmuReadings = new List<SynchrophasorReading>
        {
            new("PMU-MADRID", 400.0, 50.01, 10.0, 0.005, DateTimeOffset.UtcNow),
            new("PMU-TOULOUSE", 400.0, 50.00, 14.0, 0.005, DateTimeOffset.UtcNow)
        };

        var request = new GridStabilizationRequest([converter], pmuReadings);
        var result = _engine.StabilizeGrid(request);

        result.GridFrequencyStable.Should().BeTrue();
        result.Actions[0].ControlMode.Should().Be("BaseScheduledDispatch");
        result.Actions[0].ActivePowerAdjustmentMw.Should().Be(0.0);
        result.GridResilienceSummary.Should().Contain("Grid Stable");
    }

    [Fact]
    public void StabilizeGrid_EmptyReadings_ReturnsZeroSubstationsAndStable()
    {
        var converter = new HvdcConverterStation("CONV-BASE", "Base Conv", 500.0, 100.0, 50.0, 3.0);
        var request = new GridStabilizationRequest([converter], []);
        var result = _engine.StabilizeGrid(request);

        result.SubstationsMonitored.Should().Be(0);
        result.GridFrequencyStable.Should().BeTrue();
        result.Actions.Should().BeEmpty();
    }
}
