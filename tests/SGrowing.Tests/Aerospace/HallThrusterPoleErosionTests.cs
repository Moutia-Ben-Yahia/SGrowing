using FluentAssertions;
using SGrowing.Api.Modules.Aerospace.HallThrusterPoleErosion;
using Xunit;

namespace SGrowing.Tests.Aerospace;

public class HallThrusterPoleErosionTests
{
    private readonly HallThrusterPoleErosionEngine _engine = new();

    [Fact]
    public void EvaluateThruster_NominalStationKeeping_YieldsHighIspAndSteadyHealth()
    {
        var tel = new HallThrusterTelemetry(
            ThrusterId: "HALL-XENON-5KW-01",
            PropellantType: "Xenon",
            AnodeDischargeVoltageVolts: 300.0,
            AnodeDischargeCurrentAmperes: 15.0,
            PropellantMassFlowRateMgPerSec: 5.0,
            MagneticCoilCurrentAmperes: 3.5,
            BreathingModeOscillationPeakKHz: 22.0,
            InnerPolePieceErosionMicronsPer1000Hours: 12.0,
            CumulativeFiredHours: 3000.0
        );

        var request = new HallThrusterEvaluationRequest(tel, new HallThrusterLimits());
        var assessment = _engine.EvaluateThruster(request);

        assessment.ThrusterId.Should().Be("HALL-XENON-5KW-01");
        assessment.PolePieceErosionCriticalDetected.Should().BeFalse();
        assessment.ThrusterHealthState.Should().Be("NOMINAL_PROPULSION_STEADY");
        assessment.SpecificImpulseSeconds.Should().BeInRange(1800.0, 2100.0);
        assessment.ThrustMilliNewtons.Should().BeGreaterThan(80.0);
        assessment.EstimatedRemainingOperationalHours.Should().BeGreaterThan(5000.0);
        assessment.MagneticFieldAdjustmentDirective.Should().Contain("PROPULSION_NOMINAL");
    }

    [Fact]
    public void EvaluateThruster_SeverePoleErosion_TriggersMagneticShieldingSurge()
    {
        // 45 um/1000h erosion exceeds 40 um/1000h threshold
        var tel = new HallThrusterTelemetry(
            ThrusterId: "HALL-THRUSTER-ERODED",
            AnodeDischargeVoltageVolts: 400.0,
            InnerPolePieceErosionMicronsPer1000Hours: 45.0, // High sputtering!
            CumulativeFiredHours: 1800.0
        );

        var limits = new HallThrusterLimits(MaxAllowablePoleErosionMicronsPer1000Hours: 40.0);
        var request = new HallThrusterEvaluationRequest(tel, limits);
        var assessment = _engine.EvaluateThruster(request);

        assessment.PolePieceErosionCriticalDetected.Should().BeTrue();
        assessment.ThrusterHealthState.Should().Be("CRITICAL_POLE_SPUTTERING_HAZARD");
        assessment.MagneticFieldAdjustmentDirective.Should().Contain("INCREASE_MAGNETIC_SHIELDING");
    }

    [Fact]
    public void EvaluateThruster_BreathingModeInstability_CommandsVoltageTrim()
    {
        // 34 kHz breathing mode is outside 15-28 kHz band
        var tel = new HallThrusterTelemetry(
            ThrusterId: "HALL-BREATHING-ANOMALY",
            BreathingModeOscillationPeakKHz: 34.0, // Unstable oscillation!
            InnerPolePieceErosionMicronsPer1000Hours: 15.0
        );

        var request = new HallThrusterEvaluationRequest(tel, new HallThrusterLimits());
        var assessment = _engine.EvaluateThruster(request);

        assessment.PolePieceErosionCriticalDetected.Should().BeFalse();
        assessment.ThrusterHealthState.Should().Be("BREATHING_MODE_DISCHARGE_INSTABILITY");
        assessment.MagneticFieldAdjustmentDirective.Should().Contain("TRIM_DISCHARGE_VOLTAGE");
    }

    [Fact]
    public void EvaluateThruster_CalculatesAnodeEfficiencySensibly()
    {
        var tel = new HallThrusterTelemetry("HALL-EFFICIENCY-TEST", AnodeDischargeVoltageVolts: 350.0, AnodeDischargeCurrentAmperes: 12.0);
        var request = new HallThrusterEvaluationRequest(tel, new HallThrusterLimits());
        var assessment = _engine.EvaluateThruster(request);

        assessment.AnodeEfficiencyPercent.Should().BeInRange(35.0, 68.0);
    }
}
