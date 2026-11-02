namespace SGrowing.Api.Modules.BioTech.CryoEtSubtomogramAveraging;

public interface ICryoEtTomographyEngine
{
    CryoEtTomographyEvaluation AnalyzeTomography(CryoEtTomographyTelemetry telemetry);
}

public class CryoEtTomographyEngine : ICryoEtTomographyEngine
{
    public CryoEtTomographyEvaluation AnalyzeTomography(CryoEtTomographyTelemetry telemetry)
    {
        var regimes = new List<string>();
        var mitigations = new List<string>();

        // Missing wedge fraction in Fourier space = (90 - maxTilt) / 90
        double maxTilt = Math.Clamp(telemetry.MaxTiltAngleDegrees, 30.0, 85.0);
        double missingWedgeFractionPercent = ((90.0 - maxTilt) / 90.0) * 100.0;

        // Resolution model from particle count, drift, and dose
        double baseRes = telemetry.TargetFscCutoffResolutionAngstroms;
        if (telemetry.SubtomogramParticlesCount < 5000)
        {
            baseRes += (5000 - telemetry.SubtomogramParticlesCount) * 0.0004;
        }
        if (telemetry.BeamInducedDriftRateAngstromsPerFrame > 2.0)
        {
            baseRes += (telemetry.BeamInducedDriftRateAngstromsPerFrame - 2.0) * 1.5;
        }
        if (telemetry.CumulativeElectronDoseElectronsPerAngstrom2 > 120.0)
        {
            baseRes += (telemetry.CumulativeElectronDoseElectronsPerAngstrom2 - 120.0) * 0.035;
        }
        double calculatedResolution = Math.Clamp(baseRes, 2.0, 15.0);

        // Dose preservation score (0 - 10)
        double doseScore = 10.0;
        if (telemetry.CumulativeElectronDoseElectronsPerAngstrom2 > 100.0)
        {
            doseScore -= (telemetry.CumulativeElectronDoseElectronsPerAngstrom2 - 100.0) * 0.15;
        }
        doseScore = Math.Clamp(doseScore, 1.0, 10.0);

        bool isVitreous = telemetry.CrystallineIceDiffractionPeakIntensityArbitraryUnits <= 0.10;
        bool isRadiationControlled = telemetry.CumulativeElectronDoseElectronsPerAngstrom2 <= 120.0;
        bool isDriftCompensated = telemetry.BeamInducedDriftRateAngstromsPerFrame <= 2.0;

        if (!isVitreous)
        {
            regimes.Add("CRYSTALLINE_ICE_DEVITRIFICATION_DIFFRACTION");
            mitigations.Add("Discard grid or optimize plunge freezing liquid ethane temperature (-180C) to suppress cubic/hexagonal crystalline ice nucleation.");
        }

        if (!isRadiationControlled)
        {
            regimes.Add("CUMULATIVE_ELECTRON_DOSE_RADIATION_DAMAGE");
            mitigations.Add("Enable exposure dose weighting filter (Grant & Grigorieff) to attenuate damaged high-frequency signal in late tilt frames.");
        }

        if (!isDriftCompensated)
        {
            regimes.Add("BEAM_INDUCED_MOTION_DRIFT_EXCEEDANCE");
            mitigations.Add("Deploy MotionCor2/Warp patch-based motion correction and gold fiducial marker tracking to align tilt series frames.");
        }

        bool isHighResReady = isVitreous && isRadiationControlled && isDriftCompensated && calculatedResolution <= 4.5;

        return new CryoEtTomographyEvaluation
        {
            TiltSeriesDatasetId = telemetry.TiltSeriesDatasetId,
            CalculatedGlobalFscResolutionAngstroms = Math.Round(calculatedResolution, 2),
            MissingWedgeFourierFractionPercent = Math.Round(missingWedgeFractionPercent, 1),
            DoseFractionationPreservationScore = Math.Round(doseScore, 2),
            IsSpecimenVitreousIceConfirmed = isVitreous,
            IsRadiationDamageControlled = isRadiationControlled,
            IsBeamInducedDriftCompensated = isDriftCompensated,
            IsHighResolutionReconstructionReady = isHighResReady,
            ActiveRegimes = regimes,
            RecommendedMitigations = mitigations
        };
    }
}
