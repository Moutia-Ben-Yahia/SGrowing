using FluentAssertions;
using SGrowing.Api.Modules.Energy.CryoFusionPelletExtruder;
using Xunit;

namespace SGrowing.Tests.Energy;

public class CryoFusionPelletExtruderTests
{
    private readonly CryoFusionPelletExtrusionEngine _engine = new();

    [Fact]
    public void EvaluateExtrusion_NominalContinuousPacing_DeliversDeepPenetration()
    {
        var tel = new ExtruderCryostatTelemetry(
            ExtruderId: "ITER-PELLET-PACER-01",
            IceBarrelTemperatureKelvin: 15.0,
            ExtrusionPressureMegaPascals: 3.2,
            ExtrusionSpeedMmPerSec: 40.0,
            PelletDiameterMm: 4.0,
            PelletLengthMm: 5.0,
            InjectionFrequencyHertz: 20.0,
            GasGunMuzzleVelocityMPerSec: 1400.0
        );

        var request = new PelletExtrusionEvaluationRequest(tel, new PelletExtrusionLimits());
        var assessment = _engine.EvaluateExtrusion(request);

        assessment.ExtruderId.Should().Be("ITER-PELLET-PACER-01");
        assessment.CryogenicThermalSublimationRiskDetected.Should().BeFalse();
        assessment.MechanicalJammingRiskDetected.Should().BeFalse();
        assessment.ExtruderOperationalRegime.Should().Be("STEADY_CRYOGENIC_FUELING");
        assessment.DeuteriumTritiumFuelingRateGramsPerSec.Should().BeGreaterThan(0.20);
        assessment.EstimatedSeparatrixPenetrationDepthMeters.Should().BeGreaterThan(0.30);
        assessment.RecommendedCryostatControlDirective.Should().Contain("NOMINAL_PACING");
    }

    [Fact]
    public void EvaluateExtrusion_ThermalSublimationDanger_CommandsCoolingBoost()
    {
        // Barrel reaches 18.2 K (exceeds 17.5 K limit near 18.7K D2 triple point)
        var tel = new ExtruderCryostatTelemetry(
            ExtruderId: "EXTRUDER-THERMAL-RUNAWAY",
            IceBarrelTemperatureKelvin: 18.2, // Near melting!
            ExtrusionPressureMegaPascals: 2.8
        );

        var limits = new PelletExtrusionLimits(MaxAllowableBarrelTemperatureKelvin: 17.5);
        var request = new PelletExtrusionEvaluationRequest(tel, limits);
        var assessment = _engine.EvaluateExtrusion(request);

        assessment.CryogenicThermalSublimationRiskDetected.Should().BeTrue();
        assessment.ExtruderOperationalRegime.Should().Be("BARREL_THERMAL_SUBLIMATION_DANGER");
        assessment.RecommendedCryostatControlDirective.Should().Contain("BOOST_LIQUID_HELIUM_COOLING");
    }

    [Fact]
    public void EvaluateExtrusion_NozzleOverpressure_DetectsMechanicalJam()
    {
        // Extrusion pressure reaches 6.8 MPa (exceeds 6.0 MPa limit)
        var tel = new ExtruderCryostatTelemetry(
            ExtruderId: "EXTRUDER-PLUGGED",
            IceBarrelTemperatureKelvin: 14.5,
            ExtrusionPressureMegaPascals: 6.8 // Overpressure!
        );

        var limits = new PelletExtrusionLimits(MaxAllowableExtrusionPressureMpa: 6.0);
        var request = new PelletExtrusionEvaluationRequest(tel, limits);
        var assessment = _engine.EvaluateExtrusion(request);

        assessment.MechanicalJammingRiskDetected.Should().BeTrue();
        assessment.ExtruderOperationalRegime.Should().Be("EXTRUSION_NOZZLE_JAM_CRITICAL");
        assessment.RecommendedCryostatControlDirective.Should().Contain("THROTTLE_EXTRUSION_DRIVE");
    }

    [Fact]
    public void EvaluateExtrusion_CalculatesFuelingRateSensibly()
    {
        var tel = new ExtruderCryostatTelemetry("TEST-MASS-RATE", PelletDiameterMm: 3.0, PelletLengthMm: 4.0, InjectionFrequencyHertz: 10.0);
        var request = new PelletExtrusionEvaluationRequest(tel, new PelletExtrusionLimits());
        var assessment = _engine.EvaluateExtrusion(request);

        assessment.DeuteriumTritiumFuelingRateGramsPerSec.Should().BeGreaterThan(0.05);
    }
}
