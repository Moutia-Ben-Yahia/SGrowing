namespace SGrowing.Tests.Physics;

using System;
using FluentAssertions;
using SGrowing.Api.Modules.Physics.StedSuperResolutionMicroscopy;
using Xunit;

public class StedSuperResolutionMicroscopyTests
{
    private readonly StedSuperResolutionMicroscopyEngine _engine = new();

    [Fact]
    public void AnalyzeStedMicroscopy_NominalLaserDose_ReturnsNanoscaleSuperResolution()
    {
        var telemetry = new StedMicroscopyTelemetry(
            MicroscopeId: "STED-SCOPE-01",
            ExcitationWavelengthNanometers: 640.0,
            StedDepletionWavelengthNanometers: 775.0,
            ObjectiveNumericalAperture: 1.40,
            StedDoughnutLaserPowerMilliwatts: 110.0,
            SaturationIntensityMegawattsPerSquareCm: 10.0,
            SpecimenDepthMicrons: 12.0,
            PhotobleachingDecayRatePercentPerFrame: 2.5,
            AdaptiveOpticsWavefrontRmsErrorWavelengths: 0.03
        );

        var result = _engine.AnalyzeStedMicroscopy(telemetry);

        result.MicroscopeId.Should().Be("STED-SCOPE-01");
        result.MicroscopyRegime.Should().Be("NANOSCALE_SUPER_RESOLUTION_OPTIMAL");
        result.IsImagingOptimal.Should().BeTrue();
        result.AbbeDiffractionLimitNanometers.Should().BeApproximately(228.57, 1.0);
        result.EffectiveStedResolutionNanometers.Should().BeLessThan(175.0);
        result.ResolutionImprovementFactor.Should().BeGreaterThan(1.3);
        result.LaserPowerAndOpticsGuidance.Should().ContainEquivalentOf("sub-diffraction nanoscale resolution attained");
    }

    [Fact]
    public void AnalyzeStedMicroscopy_ExcessivePhotobleaching_ReturnsPhototoxicityWarning()
    {
        var telemetry = new StedMicroscopyTelemetry(
            MicroscopeId: "STED-SCOPE-02",
            ExcitationWavelengthNanometers: 561.0,
            StedDepletionWavelengthNanometers: 775.0,
            ObjectiveNumericalAperture: 1.40,
            StedDoughnutLaserPowerMilliwatts: 220.0, // High power causing bleaching
            SaturationIntensityMegawattsPerSquareCm: 10.0,
            SpecimenDepthMicrons: 8.0,
            PhotobleachingDecayRatePercentPerFrame: 9.5, // > 8.0%
            AdaptiveOpticsWavefrontRmsErrorWavelengths: 0.04
        );

        var result = _engine.AnalyzeStedMicroscopy(telemetry);

        result.MicroscopyRegime.Should().Be("SEVERE_PHOTOBLEACHING_PHOTOTOXICITY");
        result.IsImagingOptimal.Should().BeFalse();
        result.PhototoxicityRiskIndex.Should().BeGreaterThan(1.25);
        result.LaserPowerAndOpticsGuidance.Should().ContainEquivalentOf("fluorophore destruction");
    }

    [Fact]
    public void AnalyzeStedMicroscopy_HighWavefrontAberration_ReturnsAberrationDeformedDoughnut()
    {
        var telemetry = new StedMicroscopyTelemetry(
            MicroscopeId: "STED-SCOPE-03",
            ExcitationWavelengthNanometers: 640.0,
            StedDepletionWavelengthNanometers: 775.0,
            ObjectiveNumericalAperture: 1.40,
            StedDoughnutLaserPowerMilliwatts: 90.0,
            SaturationIntensityMegawattsPerSquareCm: 10.0,
            SpecimenDepthMicrons: 55.0, // Deep imaging with aberration
            PhotobleachingDecayRatePercentPerFrame: 2.0,
            AdaptiveOpticsWavefrontRmsErrorWavelengths: 0.18 // > 0.12 lambda
        );

        var result = _engine.AnalyzeStedMicroscopy(telemetry);

        result.MicroscopyRegime.Should().Be("ABERRATION_DEFORMED_DOUGHNUT_CREST");
        result.IsImagingOptimal.Should().BeFalse();
        result.LaserPowerAndOpticsGuidance.Should().ContainEquivalentOf("deformable mirror Zernike correction");
    }

    [Fact]
    public void AnalyzeStedMicroscopy_UnderdepletedLaserPower_ReturnsDiffractionLimited()
    {
        var telemetry = new StedMicroscopyTelemetry(
            MicroscopeId: "STED-SCOPE-04",
            ExcitationWavelengthNanometers: 640.0,
            StedDepletionWavelengthNanometers: 775.0,
            ObjectiveNumericalAperture: 1.40,
            StedDoughnutLaserPowerMilliwatts: 15.0, // < 25 mW
            SaturationIntensityMegawattsPerSquareCm: 10.0,
            SpecimenDepthMicrons: 5.0,
            PhotobleachingDecayRatePercentPerFrame: 0.5,
            AdaptiveOpticsWavefrontRmsErrorWavelengths: 0.02
        );

        var result = _engine.AnalyzeStedMicroscopy(telemetry);

        result.MicroscopyRegime.Should().Be("DIFFRACTION_LIMITED_UNDERDEPLETED");
        result.IsImagingOptimal.Should().BeFalse();
        result.LaserPowerAndOpticsGuidance.Should().ContainEquivalentOf("classical Abbe diffraction limit");
    }

    [Fact]
    public void AnalyzeStedMicroscopy_NullTelemetry_ThrowsArgumentNullException()
    {
        var act = () => _engine.AnalyzeStedMicroscopy(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
