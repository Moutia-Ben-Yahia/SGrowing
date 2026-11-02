namespace SGrowing.Tests.Energy;

using FluentAssertions;
using SGrowing.Api.Modules.Energy.GeothermalMicroSeismicity;
using Xunit;

public class GeothermalMicroSeismicityTests
{
    private readonly GeothermalMicroSeismicityArbiterEngine _engine = new();

    [Fact]
    public void EvaluateReservoirSeismicity_RedThresholdExceeded_CommandsImmediateShutIn()
    {
        var wells = new List<InjectionWell>
        {
            new("WELL-GT-01", "Soultz-sous-Forets EGS", DepthMeters: 4500.0, CurrentInjectionRateLitersPerSec: 50.0, WellheadPressureBar: 120.0, MaxAllowedPressureBar: 160.0)
        };

        // Red trigger event: M = 2.45 (> 2.0)
        var events = new List<MicroSeismicEvent>
        {
            new("EV-01", MagnitudeLocal: 2.45, HypocenterDepthMeters: 4400.0, DistanceFromWellMeters: 350.0, DateTimeOffset.UtcNow)
        };

        var request = new GeothermalSeismicAnalysisRequest(wells, events, AmberThresholdMagnitude: 1.0, RedThresholdMagnitude: 2.0);
        var assessment = _engine.EvaluateReservoirSeismicity(request);

        assessment.TotalEventsAudited.Should().Be(1);
        assessment.MaxMagnitudeRecorded.Should().Be(2.45);
        assessment.ReservoirSafetyPosture.Should().Contain("CRITICAL HAZARD");

        var command = assessment.Commands.Should().ContainSingle().Subject;
        command.TrafficLightStatus.Should().Be("Red");
        command.RecommendedFlowRateLitersPerSec.Should().Be(0.0);
        command.PressureAdjustmentBar.Should().BeLessThan(0.0);
        command.ActionReason.Should().Contain("Emergency shut-in");
    }

    [Fact]
    public void EvaluateReservoirSeismicity_AmberThresholdReached_ThrottlesFlowBy50Percent()
    {
        var wells = new List<InjectionWell>
        {
            new("WELL-GT-02", "Reykjanes Peninsula", 2500.0, CurrentInjectionRateLitersPerSec: 80.0, WellheadPressureBar: 90.0, MaxAllowedPressureBar: 130.0)
        };

        // Amber trigger: M = 1.35
        var events = new List<MicroSeismicEvent>
        {
            new("EV-02", 1.35, 2300.0, 500.0, DateTimeOffset.UtcNow)
        };

        var request = new GeothermalSeismicAnalysisRequest(wells, events);
        var assessment = _engine.EvaluateReservoirSeismicity(request);

        var command = assessment.Commands.Should().ContainSingle().Subject;
        command.TrafficLightStatus.Should().Be("Amber");
        command.RecommendedFlowRateLitersPerSec.Should().Be(40.0); // 50% of 80
        command.ActionReason.Should().Contain("cut injection rate by 50%");
    }

    [Fact]
    public void EvaluateReservoirSeismicity_LowMicroSeismicity_MaintainsGreenStatus()
    {
        var wells = new List<InjectionWell>
        {
            new("WELL-GT-03", "Habanero EGS", 4000.0, 60.0, 100.0, 150.0)
        };

        var events = new List<MicroSeismicEvent>
        {
            new("EV-TINY", 0.45, 3900.0, 120.0, DateTimeOffset.UtcNow)
        };

        var request = new GeothermalSeismicAnalysisRequest(wells, events);
        var assessment = _engine.EvaluateReservoirSeismicity(request);

        var command = assessment.Commands.Should().ContainSingle().Subject;
        command.TrafficLightStatus.Should().Be("Green");
        command.RecommendedFlowRateLitersPerSec.Should().Be(60.0);
        assessment.ReservoirSafetyPosture.Should().Contain("OPERATIONAL STABILITY");
    }

    [Fact]
    public void EvaluateReservoirSeismicity_EmptyEventsList_ReturnsSafeStatusWithZeroAudited()
    {
        var wells = new List<InjectionWell>
        {
            new("WELL-GT-04", "Cornwall Geothermal", 5000.0, 30.0, 80.0, 110.0)
        };

        var request = new GeothermalSeismicAnalysisRequest(wells, []);
        var assessment = _engine.EvaluateReservoirSeismicity(request);

        assessment.TotalEventsAudited.Should().Be(0);
        assessment.MaxMagnitudeRecorded.Should().Be(0.0);
        assessment.Commands[0].TrafficLightStatus.Should().Be("Green");
    }
}
