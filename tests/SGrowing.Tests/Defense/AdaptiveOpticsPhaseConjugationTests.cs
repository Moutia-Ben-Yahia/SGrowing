using System;
using SGrowing.Api.Modules.Defense.AdaptiveOpticsPhaseConjugation;
using Xunit;

namespace SGrowing.Tests.Defense;

public sealed class AdaptiveOpticsPhaseConjugationTests
{
    private readonly AdaptiveOpticsPhaseConjugationEngine _engine = new();

    [Fact]
    public void AnalyzeAtmosphericWavefront_MildTurbulence_AuthorizesHighPowerFiring()
    {
        var telemetry = new AtmosphericAdaptiveOpticsTelemetry(
            LaserSystemId: "HEL-DIR-IRONBEAM-01",
            TargetRangeKilometers: 2.5,
            LaserWavelengthNanometers: 1064.0,
            RefractiveIndexStructureConstantCn2: 1.0e-15,
            CrosswindVelocityMPerSec: 6.0,
            DeformableMirrorActuatorSpacingMeters: 0.015,
            ControlLoopBandwidthHz: 2000.0,
            MeasuredRytovVariance: 0.05,
            BeamDirectorApertureDiameterMeters: 0.50);

        var result = _engine.AnalyzeAtmosphericWavefront(telemetry);

        Assert.Equal("DIFFRACTION_LIMITED_FOCUS", result.BeamPropagationRegime);
        Assert.Equal("AUTHORIZE_FULL_POWER_HIGH_ENERGY_BEAM_FIRING", result.AdaptiveOpticsCommand);
        Assert.True(result.IsFullPowerAuthorized);
        Assert.True(result.EstimatedStrehlRatio >= 0.70);
    }

    [Fact]
    public void AnalyzeAtmosphericWavefront_HighBandwidthLag_IssuesLoopGainWarning()
    {
        // Cn2 = 3.0e-14, wind = 25 m/s -> r0 ~ 0.03 m -> fg = 0.43 * (25 / 0.03) = 358 Hz
        // Bandwidth = 500 Hz -> fg / bw = 358 / 500 = 0.716 > 0.25
        var telemetry = new AtmosphericAdaptiveOpticsTelemetry(
            LaserSystemId: "HEL-DIR-IRONBEAM-02",
            TargetRangeKilometers: 1.0,
            LaserWavelengthNanometers: 1064.0,
            RefractiveIndexStructureConstantCn2: 5.0e-15,
            CrosswindVelocityMPerSec: 35.0,
            DeformableMirrorActuatorSpacingMeters: 0.005,
            ControlLoopBandwidthHz: 300.0,
            MeasuredRytovVariance: 0.10,
            BeamDirectorApertureDiameterMeters: 0.50);

        var result = _engine.AnalyzeAtmosphericWavefront(telemetry);

        Assert.Equal("AO_BANDWIDTH_LAG_WARNING", result.BeamPropagationRegime);
        Assert.Equal("INCREASE_DEFORMABLE_MIRROR_LOOP_GAIN_TO_MAXIMUM", result.AdaptiveOpticsCommand);
        Assert.False(result.IsFullPowerAuthorized);
    }

    [Fact]
    public void AnalyzeAtmosphericWavefront_DeepScintillation_EngagesApertureAveraging()
    {
        // Measured Rytov = 0.95 > 0.80 -> Severe scintillation
        var telemetry = new AtmosphericAdaptiveOpticsTelemetry(
            LaserSystemId: "HEL-DIR-IRONBEAM-03",
            TargetRangeKilometers: 7.0,
            LaserWavelengthNanometers: 1064.0,
            RefractiveIndexStructureConstantCn2: 8.0e-14,
            CrosswindVelocityMPerSec: 15.0,
            DeformableMirrorActuatorSpacingMeters: 0.025,
            ControlLoopBandwidthHz: 1200.0,
            MeasuredRytovVariance: 0.95,
            BeamDirectorApertureDiameterMeters: 0.60);

        var result = _engine.AnalyzeAtmosphericWavefront(telemetry);

        Assert.Equal("SEVERE_TURBULENCE_SCINTILLATION", result.BeamPropagationRegime);
        Assert.Equal("ENGAGE_MULTI_BEAM_INCOHERENT_APERTURE_AVERAGING", result.AdaptiveOpticsCommand);
        Assert.False(result.IsFullPowerAuthorized);
    }

    [Fact]
    public void AnalyzeAtmosphericWavefront_CalculatedHighRytov_FlagsSevereTurbulence()
    {
        var telemetry = new AtmosphericAdaptiveOpticsTelemetry(
            LaserSystemId: "HEL-DIR-IRONBEAM-04",
            TargetRangeKilometers: 10.0,
            LaserWavelengthNanometers: 1064.0,
            RefractiveIndexStructureConstantCn2: 1.5e-13,
            CrosswindVelocityMPerSec: 12.0,
            DeformableMirrorActuatorSpacingMeters: 0.02,
            ControlLoopBandwidthHz: 1500.0,
            MeasuredRytovVariance: 0.0,
            BeamDirectorApertureDiameterMeters: 0.60);

        var result = _engine.AnalyzeAtmosphericWavefront(telemetry);

        Assert.Equal("SEVERE_TURBULENCE_SCINTILLATION", result.BeamPropagationRegime);
        Assert.True(result.EstimatedRytovVariance > 0.80);
        Assert.False(result.IsFullPowerAuthorized);
    }
}
