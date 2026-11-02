namespace SGrowing.Tests.Energy;

using SGrowing.Api.Modules.Energy.PlasmaRunawayElectronSuppression;
using Xunit;

public class PlasmaRunawayElectronSuppressionTests
{
    private readonly PlasmaRunawaySuppressionEngine _engine = new();

    [Fact]
    public void AnalyzeDisruption_StablePlasmaConfinement_RemainsInStandby()
    {
        // Low electric field (0.05 V/m), ne = 10 x 10^19 m^-3, Ecrit = 0.077 V/m -> Overdrive ~ 0.65 (< 1.0)
        var telemetry = new PlasmaRunawayTelemetry(
            TokamakId: "ITER-Q10-BURN-01",
            PlasmaCurrentMegaAmps: 15.0,
            CurrentQuenchRateMaPerSec: 25.0,
            InducedToroidalElectricFieldVPerM: 0.05,
            ElectronDensity1e19PerM3: 10.0,
            CoulombLogarithm: 15.0,
            HardXRayPhotonCountsPerMs: 150.0,
            ShatteredPelletGasSpecies: "Neon-Deuterium");

        var result = _engine.AnalyzeDisruption(telemetry);

        Assert.Equal("ITER-Q10-BURN-01", result.TokamakId);
        Assert.Equal("STABLE_THERMAL_CONFINEMENT", result.MitigationState);
        Assert.Equal("STANDBY", result.MitigationCommand);
        Assert.False(result.IsRunawayAvalancheDetected);
        Assert.True(result.ElectricFieldOverdriveRatio < 1.0);
        Assert.Equal(0.0, result.AvalancheGrowthRatePerMs);
    }

    [Fact]
    public void AnalyzeDisruption_DisruptionPrecursor_ArmsMassiveGasInjectors()
    {
        // Electric field = 2.0 V/m, Ecrit = 0.077 V/m -> Overdrive = 25.9 (> 10, < 100)
        var telemetry = new PlasmaRunawayTelemetry(
            TokamakId: "SPARC-HIGHFIELD-02",
            PlasmaCurrentMegaAmps: 8.7,
            CurrentQuenchRateMaPerSec: 380.0,
            InducedToroidalElectricFieldVPerM: 2.0,
            ElectronDensity1e19PerM3: 10.0,
            CoulombLogarithm: 15.0,
            HardXRayPhotonCountsPerMs: 4500.0,
            ShatteredPelletGasSpecies: "Pure-Argon");

        var result = _engine.AnalyzeDisruption(telemetry);

        Assert.True(result.IsRunawayAvalancheDetected);
        Assert.Equal("RUNAWAY_PRECURSOR_WARNING", result.MitigationState);
        Assert.Equal("ARM_MASSIVE_GAS_INJECTORS", result.MitigationCommand);
        Assert.Contains("RUNAWAY PRECURSOR DETECTED", result.SuppressionAdvisory);
    }

    [Fact]
    public void AnalyzeDisruption_CatastrophicAvalanche_FiresShatteredPelletInjectionImmediately()
    {
        // Massive quench rate 850 MA/s, E = 25 V/m, HXR = 12000 cps
        var telemetry = new PlasmaRunawayTelemetry(
            TokamakId: "STEP-SPHERICAL-03",
            PlasmaCurrentMegaAmps: 12.0,
            CurrentQuenchRateMaPerSec: 850.0,
            InducedToroidalElectricFieldVPerM: 25.0,
            ElectronDensity1e19PerM3: 10.0,
            CoulombLogarithm: 15.0,
            HardXRayPhotonCountsPerMs: 12000.0,
            ShatteredPelletGasSpecies: "Neon-Deuterium");

        var result = _engine.AnalyzeDisruption(telemetry);

        Assert.True(result.IsRunawayAvalancheDetected);
        Assert.Equal("EMERGENCY_SPI_KILLER_PELLET_FIRE", result.MitigationState);
        Assert.Equal("FIRE_SHATTERED_PELLET_INJECTION_IMMEDIATELY", result.MitigationCommand);
        Assert.True(result.RosenbluthDensity1e19PerM3 > 2000.0);
        Assert.Contains("CATASTROPHIC RUNAWAY AVALANCHE", result.SuppressionAdvisory);
    }

    [Fact]
    public void AnalyzeDisruption_NegativeDensity_ThrowsArgumentOutOfRangeException()
    {
        var invalid = new PlasmaRunawayTelemetry(
            TokamakId: "ERR-TOK",
            PlasmaCurrentMegaAmps: 10.0,
            CurrentQuenchRateMaPerSec: 100.0,
            InducedToroidalElectricFieldVPerM: 5.0,
            ElectronDensity1e19PerM3: -2.0,
            CoulombLogarithm: 15.0,
            HardXRayPhotonCountsPerMs: 50.0,
            ShatteredPelletGasSpecies: "Neon");

        Assert.Throws<ArgumentOutOfRangeException>(() => _engine.AnalyzeDisruption(invalid));
    }
}
