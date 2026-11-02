using SGrowing.Api.Modules.BioTech.CryoEtSubtomogramAveraging;
using Xunit;

namespace SGrowing.Tests.BioTech;

public class CryoEtTomographyTests
{
    private readonly CryoEtTomographyEngine _engine = new();

    [Fact]
    public void AnalyzeTomography_NominalRibosomeDataset_ReturnsHighResReady()
    {
        var telemetry = new CryoEtTomographyTelemetry
        {
            TiltSeriesDatasetId = "CRYO-TILT-70S-001",
            TargetMacromolecule = "Ribosome_70S_InSitu",
            MaxTiltAngleDegrees = 60.0,
            TiltStepAngleDegrees = 2.0,
            CumulativeElectronDoseElectronsPerAngstrom2 = 90.0,
            CtfDefocusMicrons = 2.0,
            BeamInducedDriftRateAngstromsPerFrame = 1.1,
            CrystallineIceDiffractionPeakIntensityArbitraryUnits = 0.03,
            SubtomogramParticlesCount = 18000,
            TargetFscCutoffResolutionAngstroms = 3.6
        };

        var result = _engine.AnalyzeTomography(telemetry);

        Assert.Equal("CRYO-TILT-70S-001", result.TiltSeriesDatasetId);
        Assert.True(result.IsHighResolutionReconstructionReady);
        Assert.True(result.IsSpecimenVitreousIceConfirmed);
        Assert.True(result.IsRadiationDamageControlled);
        Assert.True(result.IsBeamInducedDriftCompensated);
        Assert.True(result.CalculatedGlobalFscResolutionAngstroms <= 4.0);
        Assert.True(result.MissingWedgeFourierFractionPercent <= 35.0);
        Assert.True(result.DoseFractionationPreservationScore >= 8.0);
        Assert.Empty(result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeTomography_CrystallineIceContamination_TriggersDiffractionAlert()
    {
        var telemetry = new CryoEtTomographyTelemetry
        {
            TiltSeriesDatasetId = "CRYO-ICE-CRYSTALLINE",
            CrystallineIceDiffractionPeakIntensityArbitraryUnits = 0.28 // Above 0.10
        };

        var result = _engine.AnalyzeTomography(telemetry);

        Assert.False(result.IsSpecimenVitreousIceConfirmed);
        Assert.False(result.IsHighResolutionReconstructionReady);
        Assert.Contains("CRYSTALLINE_ICE_DEVITRIFICATION_DIFFRACTION", result.ActiveRegimes);
        Assert.NotEmpty(result.RecommendedMitigations);
    }

    [Fact]
    public void AnalyzeTomography_ExcessiveRadiationDose_TriggersDamageAlert()
    {
        var telemetry = new CryoEtTomographyTelemetry
        {
            TiltSeriesDatasetId = "CRYO-RADIATION-EXCESS",
            CumulativeElectronDoseElectronsPerAngstrom2 = 145.0 // Exceeds 120 e-/A^2
        };

        var result = _engine.AnalyzeTomography(telemetry);

        Assert.False(result.IsRadiationDamageControlled);
        Assert.Contains("CUMULATIVE_ELECTRON_DOSE_RADIATION_DAMAGE", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeTomography_HighBeamDrift_TriggersMotionExceedance()
    {
        var telemetry = new CryoEtTomographyTelemetry
        {
            TiltSeriesDatasetId = "CRYO-MOTION-EXCESS",
            BeamInducedDriftRateAngstromsPerFrame = 3.4 // Exceeds 2.0 A/frame
        };

        var result = _engine.AnalyzeTomography(telemetry);

        Assert.False(result.IsBeamInducedDriftCompensated);
        Assert.Contains("BEAM_INDUCED_MOTION_DRIFT_EXCEEDANCE", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeTomography_MissingWedge_DecreasesWithHigherTilt()
    {
        var tilt45Telemetry = new CryoEtTomographyTelemetry
        {
            TiltSeriesDatasetId = "TILT-45",
            MaxTiltAngleDegrees = 45.0
        };

        var tilt70Telemetry = new CryoEtTomographyTelemetry
        {
            TiltSeriesDatasetId = "TILT-70",
            MaxTiltAngleDegrees = 70.0
        };

        var res45 = _engine.AnalyzeTomography(tilt45Telemetry);
        var res70 = _engine.AnalyzeTomography(tilt70Telemetry);

        Assert.True(res45.MissingWedgeFourierFractionPercent > res70.MissingWedgeFourierFractionPercent);
    }
}
