using FluentAssertions;
using SGrowing.Api.Modules.Manufacturing.MbeRheedGrowthKinetics;
using Xunit;

namespace SGrowing.Tests.Manufacturing;

public sealed class MbeRheedGrowthKineticsTests
{
    private readonly MbeRheedGrowthKineticsEngine _engine = new();

    [Fact]
    public void AnalyzeEpitaxialGrowth_NominalConditions_ReturnsFrankVanDerMerwe2D()
    {
        var telemetry = new MbeGrowthTelemetry(
            ChamberId: "MBE-VEECO-GEN20-01",
            GrowthTemperatureCelsius: 580.0,
            GroupVFluxTorr: 1.5e-5,   // As4 flux
            GroupIIIFluxTorr: 8.0e-7, // Ga flux -> ratio = 18.75
            RheedOscillationPeriodSeconds: 2.5,
            RheedSpecularIntensityNormalized: 0.88,
            SurfaceAdatomDiffusionLengthNanometers: 35.0,
            SubstrateAzimuthRotatorActive: true
        );

        var result = _engine.AnalyzeEpitaxialGrowth(telemetry);

        result.EpitaxialGrowthMode.Should().Be("FRANK_VAN_DER_MERWE_2D_OPTIMAL");
        result.IsCrystalStructureUniform.Should().BeTrue();
        result.FluxRatioVtoIII.Should().BeGreaterThan(10.0);
        result.MonolayerGrowthRateNanometersPerSecond.Should().BeGreaterThan(0.10);
        result.InterfacialRoughnessEstimateAngstroms.Should().BeLessThan(1.5);
    }

    [Fact]
    public void AnalyzeEpitaxialGrowth_HighSubstrateTemp_ReturnsArsenicDesorptionRoughening()
    {
        var telemetry = new MbeGrowthTelemetry(
            ChamberId: "MBE-RIBER-412-02",
            GrowthTemperatureCelsius: 665.0, // High temperature > 640C
            GroupVFluxTorr: 8.0e-6,
            GroupIIIFluxTorr: 9.0e-7,
            RheedOscillationPeriodSeconds: 2.2,
            RheedSpecularIntensityNormalized: 0.55,
            SurfaceAdatomDiffusionLengthNanometers: 40.0,
            SubstrateAzimuthRotatorActive: true
        );

        var result = _engine.AnalyzeEpitaxialGrowth(telemetry);

        result.EpitaxialGrowthMode.Should().Be("ARSENIC_DESORPTION_SURFACE_ROUGHENING");
        result.IsCrystalStructureUniform.Should().BeFalse();
        result.EffusionCellShutterCommand.Should().Contain("Escalate Group V cracker valve");
    }

    [Fact]
    public void AnalyzeEpitaxialGrowth_DampenedIntensityShortDiffusion_ReturnsStranskiKrastanov3D()
    {
        var telemetry = new MbeGrowthTelemetry(
            ChamberId: "MBE-GENX-QD-03",
            GrowthTemperatureCelsius: 500.0,
            GroupVFluxTorr: 1.2e-5,
            GroupIIIFluxTorr: 8.0e-7,
            RheedOscillationPeriodSeconds: 2.0,
            RheedSpecularIntensityNormalized: 0.28, // Low intensity < 0.40
            SurfaceAdatomDiffusionLengthNanometers: 8.0, // Short diffusion
            SubstrateAzimuthRotatorActive: true
        );

        var result = _engine.AnalyzeEpitaxialGrowth(telemetry);

        result.EpitaxialGrowthMode.Should().Be("STRANSKI_KRASTANOV_3D_ISLANDING");
        result.IsCrystalStructureUniform.Should().BeFalse();
        result.EffusionCellShutterCommand.Should().Contain("Close Group III shutter");
    }

    [Fact]
    public void AnalyzeEpitaxialGrowth_LongDiffusionSlowGrowth_ReturnsStepFlow()
    {
        var telemetry = new MbeGrowthTelemetry(
            ChamberId: "MBE-STEPFLOW-04",
            GrowthTemperatureCelsius: 620.0,
            GroupVFluxTorr: 1.8e-5,
            GroupIIIFluxTorr: 4.0e-7,
            RheedOscillationPeriodSeconds: 8.0, // Slow period > 6.0s
            RheedSpecularIntensityNormalized: 0.92,
            SurfaceAdatomDiffusionLengthNanometers: 75.0, // Long diffusion > 60 nm
            SubstrateAzimuthRotatorActive: true
        );

        var result = _engine.AnalyzeEpitaxialGrowth(telemetry);

        result.EpitaxialGrowthMode.Should().Be("STEP_FLOW_EQUILIBRIUM");
        result.IsCrystalStructureUniform.Should().BeTrue();
        result.EffusionCellShutterCommand.Should().Contain("step-flow epitaxy established");
    }
}
