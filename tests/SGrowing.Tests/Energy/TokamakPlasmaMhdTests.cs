namespace SGrowing.Tests.Energy;

using FluentAssertions;
using SGrowing.Api.Modules.Energy.TokamakPlasmaMhd;
using Xunit;

public class TokamakPlasmaMhdTests
{
    private readonly TokamakPlasmaMhdEngine _engine = new();

    [Fact]
    public void EvaluateDisruptionRisk_NominalShot_ReportsLowProbabilityAndNoMitigation()
    {
        var shot = new TokamakShotState(
            ReactorId: "ITER-TOKAMAK-01",
            ShotNumber: 104520,
            PlasmaCurrentMegaAmps: 10.0,
            ToroidalFieldTesla: 5.3,
            MajorRadiusMeters: 6.2,
            MinorRadiusMeters: 2.0,
            PlasmaElongationKappa: 1.7,
            ElectronDensity10Power19PerM3: 5.0,
            CoreElectronTemperatureKev: 12.0,
            LockedModeAmplitudeGauss: 1.5,
            RadiatedPowerFraction: 0.45,
            PoloidalBeta: 0.65
        );

        var request = new TokamakDisruptionEvaluationRequest(shot);
        var assessment = _engine.EvaluateDisruptionRisk(request);

        assessment.ReactorId.Should().Be("ITER-TOKAMAK-01");
        assessment.DisruptionProbabilityPercent.Should().BeLessThan(25.0);
        assessment.EmergencyMitigationRequired.Should().BeFalse();
        assessment.MitigationTechnique.Should().Be("NONE");
        assessment.OperationalDirective.Should().Contain("PLASMA_STABLE");
        assessment.InstabilityProfile.EdgeSafetyFactorQ95.Should().BeGreaterThan(2.5);
    }

    [Fact]
    public void EvaluateDisruptionRisk_LowQ95Breach_TriggersShatteredPelletInjection()
    {
        // High current relative to field pushing q95 <= 2.0
        var shot = new TokamakShotState(
            ReactorId: "JET-TOKAMAK",
            ShotNumber: 99401,
            PlasmaCurrentMegaAmps: 4.8,
            ToroidalFieldTesla: 1.8,
            MajorRadiusMeters: 2.96,
            MinorRadiusMeters: 0.9,
            PlasmaElongationKappa: 1.2,
            ElectronDensity10Power19PerM3: 5.0,
            CoreElectronTemperatureKev: 4.5,
            LockedModeAmplitudeGauss: 2.0,
            RadiatedPowerFraction: 0.50,
            PoloidalBeta: 0.8
        );

        var request = new TokamakDisruptionEvaluationRequest(shot);
        var assessment = _engine.EvaluateDisruptionRisk(request);

        assessment.EmergencyMitigationRequired.Should().BeTrue();
        assessment.MitigationTechnique.Should().Be("SHATTERED_PELLET_INJECTION_QUENCH");
        assessment.OperationalDirective.Should().Contain("EMERGENCY_SPI_FIRE");
        assessment.InstabilityProfile.LowQBoundaryBreached.Should().BeTrue();
    }

    [Fact]
    public void EvaluateDisruptionRisk_LockedModePresent_PrescribesEccdIslandStabilization()
    {
        var shot = new TokamakShotState(
            ReactorId: "KSTAR-SUPERCONDUCTING",
            ShotNumber: 31205,
            PlasmaCurrentMegaAmps: 1.0,
            ToroidalFieldTesla: 2.5,
            MajorRadiusMeters: 1.8,
            MinorRadiusMeters: 0.5,
            PlasmaElongationKappa: 1.6,
            ElectronDensity10Power19PerM3: 3.5,
            CoreElectronTemperatureKev: 5.0,
            LockedModeAmplitudeGauss: 14.5, // Severe locked mode > 10 Gauss
            RadiatedPowerFraction: 0.55,
            PoloidalBeta: 0.7
        );

        var request = new TokamakDisruptionEvaluationRequest(shot);
        var assessment = _engine.EvaluateDisruptionRisk(request);

        assessment.InstabilityProfile.TearingModeLocked.Should().BeTrue();
        assessment.MitigationTechnique.Should().Be("ECCD_ISLAND_STABILIZATION");
        assessment.OperationalDirective.Should().Contain("ACTIVE_ECCD_DEPOSIT");
    }

    [Fact]
    public void EvaluateDisruptionRisk_DensityLimitExceeded_ElevatesDisruptionProbability()
    {
        var shot = new TokamakShotState(
            ReactorId: "DIII-D-TOKAMAK",
            ShotNumber: 175400,
            PlasmaCurrentMegaAmps: 1.2,
            ToroidalFieldTesla: 2.1,
            MajorRadiusMeters: 1.67,
            MinorRadiusMeters: 0.67,
            PlasmaElongationKappa: 1.75,
            ElectronDensity10Power19PerM3: 12.0, // High density
            CoreElectronTemperatureKev: 3.0,
            LockedModeAmplitudeGauss: 3.0,
            RadiatedPowerFraction: 0.89, // High radiative fraction
            PoloidalBeta: 1.1
        );

        var request = new TokamakDisruptionEvaluationRequest(shot);
        var assessment = _engine.EvaluateDisruptionRisk(request);

        assessment.InstabilityProfile.DensityLimitExceeded.Should().BeTrue();
        assessment.DisruptionProbabilityPercent.Should().BeGreaterThan(50.0);
    }
}
