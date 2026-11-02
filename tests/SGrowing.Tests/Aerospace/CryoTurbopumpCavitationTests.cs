namespace SGrowing.Tests.Aerospace;

using FluentAssertions;
using SGrowing.Api.Modules.Aerospace.CryoTurbopumpCavitation;
using Xunit;

public class CryoTurbopumpCavitationTests
{
    private readonly CryoTurbopumpCavitationEngine _engine = new();

    [Fact]
    public void EvaluateCavitation_NominalFlow_ReportsOptimalMarginAndFullThrottle()
    {
        var propellant = new CryoPropellantCondition(
            PropellantName: "LiquidOxygen_LOX",
            InletPressureBar: 5.5,
            VaporPressureBar: 1.2,
            FluidDensityKgPerM3: 1141.0,
            TemperatureKelvin: 90.0
        );

        var inducer = new InducerFlowTelemetry(
            TurbopumpId: "TP-RAPTOR-LOX-01",
            RotationalSpeedRpm: 28000.0,
            MassFlowKgPerSec: 650.0,
            TipSpeedMps: 110.0,
            SuctionHeadMeters: 4.5,
            VibrationRmsG: 2.1
        );

        var request = new CavitationEvaluationRequest(propellant, inducer);
        var directive = _engine.EvaluateCavitation(request);

        directive.TurbopumpId.Should().Be("TP-RAPTOR-LOX-01");
        directive.CavitationDetected.Should().BeFalse();
        directive.ChuggingResonanceRisk.Should().BeFalse();
        directive.ActionUrgency.Should().Be("NOMINAL");
        directive.NpshMarginMeters.Should().BeGreaterThan(5.0);
        directive.ThrottleAndPressurizationDirective.Should().Contain("TURBOPUMP_NOMINAL");
    }

    [Fact]
    public void EvaluateCavitation_LowInletPressure_DetectsCavitationAndCommandsPressurization()
    {
        // Tank pressure decaying close to vapor pressure
        var propellant = new CryoPropellantCondition(
            PropellantName: "LiquidOxygen_LOX",
            InletPressureBar: 1.5,
            VaporPressureBar: 1.3, // deltaP only 0.2 bar
            FluidDensityKgPerM3: 1141.0,
            TemperatureKelvin: 92.0
        );

        var inducer = new InducerFlowTelemetry(
            TurbopumpId: "TP-BE4-LOX",
            RotationalSpeedRpm: 24000.0,
            MassFlowKgPerSec: 500.0,
            TipSpeedMps: 120.0,
            SuctionHeadMeters: 1.0,
            VibrationRmsG: 4.0
        );

        var request = new CavitationEvaluationRequest(propellant, inducer);
        var directive = _engine.EvaluateCavitation(request);

        directive.CavitationDetected.Should().BeTrue();
        directive.ActionUrgency.Should().BeOneOf("ADAPTIVE_TRIM", "EMERGENCY_THROTTLE_DOWN");
        directive.ThrottleAndPressurizationDirective.Should().Contain("pressurization");
    }

    [Fact]
    public void EvaluateCavitation_HighVibrationAndNegativeMargin_TriggersEmergencyThrottleDown()
    {
        var propellant = new CryoPropellantCondition(
            PropellantName: "LiquidMethane_LCH4",
            InletPressureBar: 1.3,
            VaporPressureBar: 1.25,
            FluidDensityKgPerM3: 422.0,
            TemperatureKelvin: 112.0
        );

        var inducer = new InducerFlowTelemetry(
            TurbopumpId: "TP-METHALOX-01",
            RotationalSpeedRpm: 36000.0,
            MassFlowKgPerSec: 220.0,
            TipSpeedMps: 140.0,
            SuctionHeadMeters: 0.5,
            VibrationRmsG: 12.5 // Severe chugging vibration > 8.5g
        );

        var request = new CavitationEvaluationRequest(propellant, inducer);
        var directive = _engine.EvaluateCavitation(request);

        directive.ChuggingResonanceRisk.Should().BeTrue();
        directive.CavitationDetected.Should().BeTrue();
        directive.ActionUrgency.Should().Be("EMERGENCY_THROTTLE_DOWN");
        directive.ThrottleAndPressurizationDirective.Should().Contain("CRITICAL_CAVITATION_CHUGGING");
    }

    [Fact]
    public void EvaluateCavitation_MethanePropellant_ComputesAccurateSigma()
    {
        var propellant = new CryoPropellantCondition("LiquidMethane_LCH4", 4.0, 1.5, 422.0, 110.0);
        var inducer = new InducerFlowTelemetry("TP-TEST", 20000.0, 150.0, 95.0, 3.0, 1.5);

        var request = new CavitationEvaluationRequest(propellant, inducer);
        var directive = _engine.EvaluateCavitation(request);

        directive.CavitationNumberSigma.Should().BeGreaterThan(0.05);
        directive.CavitationDetected.Should().BeFalse();
    }
}
