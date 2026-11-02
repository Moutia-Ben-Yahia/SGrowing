using FluentAssertions;
using SGrowing.Api.Modules.Energy.SuperhotEgsMicroSeismicity;
using Xunit;

namespace SGrowing.Tests.Energy;

public class SuperhotEgsMicroSeismicityTests
{
    private readonly SuperhotEgsSeismicityEngine _engine = new();

    [Fact]
    public void EvaluateSeismicity_LowMagnitudeEvents_MaintainsGreenLightStimulation()
    {
        var events = new List<SeismicEventRecord>
        {
            new(1, MagnitudeLocal: 0.2, DepthKilometers: 4.1, PeakGroundVelocityMmPerSec: 0.15, 1000),
            new(2, MagnitudeLocal: 0.5, DepthKilometers: 4.2, PeakGroundVelocityMmPerSec: 0.35, 2000),
            new(3, MagnitudeLocal: 0.6, DepthKilometers: 4.0, PeakGroundVelocityMmPerSec: 0.40, 3000),
            new(4, MagnitudeLocal: 0.4, DepthKilometers: 4.3, PeakGroundVelocityMmPerSec: 0.25, 4000)
        };

        var tel = new EgsWellTelemetry(
            WellId: "EGS-SUPERHOT-WELL-01",
            InjectionFlowRateLitersPerSec: 40.0,
            WellheadPressureBar: 160.0,
            RecentSeismicEvents: events
        );

        var request = new SuperhotEgsEvaluationRequest(tel, new TrafficLightThresholds());
        var plan = _engine.EvaluateSeismicity(request);

        plan.WellId.Should().Be("EGS-SUPERHOT-WELL-01");
        plan.TrafficLightStatus.Should().Be("GREEN");
        plan.TotalRecordedSeismicEvents.Should().Be(4);
        plan.MaxObservedMagnitude.Should().Be(0.6);
        plan.RecommendedInjectionFlowRateLitersPerSec.Should().Be(40.0);
        plan.MitigationDirective.Should().Contain("CONTINUE_STIMULATION");
    }

    [Fact]
    public void EvaluateSeismicity_AmberLightThreshold_HalvesInjectionFlow()
    {
        // One event reaches M = 1.4 (amber threshold 1.0 <= M < 2.0)
        var events = new List<SeismicEventRecord>
        {
            new(1, MagnitudeLocal: 0.5, DepthKilometers: 4.1, PeakGroundVelocityMmPerSec: 0.3, 1000),
            new(2, MagnitudeLocal: 1.4, DepthKilometers: 4.2, PeakGroundVelocityMmPerSec: 0.9, 2000) // Amber!
        };

        var tel = new EgsWellTelemetry(
            WellId: "EGS-AMBER-WELL",
            InjectionFlowRateLitersPerSec: 50.0,
            WellheadPressureBar: 180.0,
            RecentSeismicEvents: events
        );

        var request = new SuperhotEgsEvaluationRequest(tel, new TrafficLightThresholds());
        var plan = _engine.EvaluateSeismicity(request);

        plan.TrafficLightStatus.Should().Be("AMBER");
        plan.RecommendedInjectionFlowRateLitersPerSec.Should().Be(25.0); // Halved
        plan.RecommendedWellheadPressureBar.Should().BeLessThan(180.0);
        plan.MitigationDirective.Should().Contain("THROTTLE_INJECTION");
    }

    [Fact]
    public void EvaluateSeismicity_RedLightBreach_CommandsEmergencyShutIn()
    {
        // Event reaches M = 2.3 (red threshold M >= 2.0)
        var events = new List<SeismicEventRecord>
        {
            new(1, MagnitudeLocal: 2.3, DepthKilometers: 3.9, PeakGroundVelocityMmPerSec: 3.2, 1000) // Red light!
        };

        var tel = new EgsWellTelemetry(
            WellId: "EGS-RED-EMERGENCY",
            InjectionFlowRateLitersPerSec: 60.0,
            WellheadPressureBar: 200.0,
            RecentSeismicEvents: events
        );

        var request = new SuperhotEgsEvaluationRequest(tel, new TrafficLightThresholds());
        var plan = _engine.EvaluateSeismicity(request);

        plan.TrafficLightStatus.Should().Be("RED");
        plan.RecommendedInjectionFlowRateLitersPerSec.Should().Be(0.0); // Complete shut-in
        plan.RecommendedWellheadPressureBar.Should().Be(80.0); // Bleed-off to 40%
        plan.MitigationDirective.Should().Contain("EMERGENCY_SHUT_IN");
    }

    [Fact]
    public void EvaluateSeismicity_EmptyEvents_ReturnsGreenBaseline()
    {
        var tel = new EgsWellTelemetry("EGS-EMPTY-SEISMIC", RecentSeismicEvents: []);
        var request = new SuperhotEgsEvaluationRequest(tel, new TrafficLightThresholds());
        var plan = _engine.EvaluateSeismicity(request);

        plan.TotalRecordedSeismicEvents.Should().Be(0);
        plan.TrafficLightStatus.Should().Be("GREEN");
        plan.MaxObservedMagnitude.Should().Be(0.0);
    }
}
