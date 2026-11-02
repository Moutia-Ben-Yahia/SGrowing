using FluentAssertions;
using SGrowing.Api.Modules.BioTech.OpticalCoherenceElastographyBiomechanics;
using Xunit;

namespace SGrowing.Tests.BioTech;

public sealed class OpticalCoherenceElastographyBiomechanicsTests
{
    private readonly OpticalCoherenceElastographyBiomechanicsEngine _engine = new();

    [Fact]
    public void AnalyzeTissueBiomechanics_NominalStroma_ReturnsNormalCompliantStroma()
    {
        var telemetry = new OceScanTelemetry(
            ScanId: "OCE-BREAST-MARGIN-01",
            AnatomicalSite: "BreastGlandularTissue",
            OpticalWavelengthNanometers: 1310.0,
            MeasuredShearWaveVelocityMetersPerSecond: 2.2,
            TissueDensityKgPerM3: 1050.0,
            PhaseStabilityStandardDeviationRadians: 0.035,
            ShearWaveAttenuationCoefficientNpPerMm: 0.25,
            AcousticRadiationForceTrackingActive: true
        );

        var result = _engine.AnalyzeTissueBiomechanics(telemetry);

        result.TissueStiffnessRegime.Should().Be("NORMAL_COMPLIANT_STROMA");
        result.IsMarginClear.Should().BeTrue();
        result.YoungsModulusKiloPascals.Should().BeLessThan(38.0);
        result.DisplacementSensitivityPicometers.Should().BeGreaterThan(0.0);
    }

    [Fact]
    public void AnalyzeTissueBiomechanics_StiffTumorMargin_ReturnsMalignantDetected()
    {
        var telemetry = new OceScanTelemetry(
            ScanId: "OCE-DESMOPLASTIC-02",
            AnatomicalSite: "BreastTissueMargin",
            OpticalWavelengthNanometers: 1310.0,
            MeasuredShearWaveVelocityMetersPerSecond: 4.8, // High velocity
            TissueDensityKgPerM3: 1060.0,
            PhaseStabilityStandardDeviationRadians: 0.040,
            ShearWaveAttenuationCoefficientNpPerMm: 0.45,
            AcousticRadiationForceTrackingActive: true
        );

        var result = _engine.AnalyzeTissueBiomechanics(telemetry);

        result.TissueStiffnessRegime.Should().Be("MALIGNANT_TUMOR_BOUNDARY_DETECTED");
        result.IsMarginClear.Should().BeFalse();
        result.YoungsModulusKiloPascals.Should().BeGreaterThan(38.0);
        result.SurgicalGuidanceRecommendation.Should().Contain("resect wider surgical margin");
    }

    [Fact]
    public void AnalyzeTissueBiomechanics_SoftCornealStroma_ReturnsEctasiaSofteningRisk()
    {
        var telemetry = new OceScanTelemetry(
            ScanId: "OCE-CORNEA-KERATOCONUS-03",
            AnatomicalSite: "CornealStroma",
            OpticalWavelengthNanometers: 840.0,
            MeasuredShearWaveVelocityMetersPerSecond: 1.4, // Soft corneal tissue
            TissueDensityKgPerM3: 1060.0,
            PhaseStabilityStandardDeviationRadians: 0.025,
            ShearWaveAttenuationCoefficientNpPerMm: 0.20,
            AcousticRadiationForceTrackingActive: true
        );

        var result = _engine.AnalyzeTissueBiomechanics(telemetry);

        result.TissueStiffnessRegime.Should().Be("CORNEAL_ECTASIA_SOFTENING_RISK");
        result.IsMarginClear.Should().BeTrue();
        result.YoungsModulusKiloPascals.Should().BeLessThan(12.0);
        result.SurgicalGuidanceRecommendation.Should().Contain("collagen cross-linking");
    }

    [Fact]
    public void AnalyzeTissueBiomechanics_HighPhaseNoise_ReturnsDecorrelationWarning()
    {
        var telemetry = new OceScanTelemetry(
            ScanId: "OCE-NOISY-04",
            AnatomicalSite: "CardiacMuscle",
            OpticalWavelengthNanometers: 1310.0,
            MeasuredShearWaveVelocityMetersPerSecond: 2.8,
            TissueDensityKgPerM3: 1060.0,
            PhaseStabilityStandardDeviationRadians: 0.35, // High phase noise > 0.22
            ShearWaveAttenuationCoefficientNpPerMm: 0.50,
            AcousticRadiationForceTrackingActive: true
        );

        var result = _engine.AnalyzeTissueBiomechanics(telemetry);

        result.TissueStiffnessRegime.Should().Be("PHASE_NOISE_DECORRELATION_WARNING");
        result.IsMarginClear.Should().BeFalse();
        result.SurgicalGuidanceRecommendation.Should().Contain("optical phase decorrelation");
    }
}
