using System;

namespace SGrowing.Api.Modules.BioTech.OpticalCoherenceElastographyBiomechanics;

public sealed class OpticalCoherenceElastographyBiomechanicsEngine : IOpticalCoherenceElastographyBiomechanicsEngine
{
    public OceScanAnalysisResult AnalyzeTissueBiomechanics(OceScanTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        var density = Math.Max(900.0, telemetry.TissueDensityKgPerM3);
        var cs = Math.Max(0.1, telemetry.MeasuredShearWaveVelocityMetersPerSecond);

        // Shear modulus G = rho * cs^2 (Pascals -> kPa)
        var shearModulusKPa = Math.Round((density * Math.Pow(cs, 2)) / 1000.0, 2);

        // Young's modulus E = 2 * (1 + nu) * G approx 3 * G for nearly incompressible soft tissue (nu ~ 0.495)
        var youngsModulusKPa = Math.Round(3.0 * shearModulusKPa, 2);

        // Phase-resolved displacement sensitivity delta_z = (lambda0 / (4 * pi * n)) * sigma_phi
        // Tissue refractive index n ~ 1.38. Scaled to picometers.
        const double refractiveIndex = 1.38;
        var wavelengthNm = Math.Max(600.0, telemetry.OpticalWavelengthNanometers);
        var sigmaPhi = Math.Max(0.001, telemetry.PhaseStabilityStandardDeviationRadians);
        var displacementSensitivityPm = Math.Round(((wavelengthNm * 1000.0) / (4.0 * Math.PI * refractiveIndex)) * sigmaPhi, 1);

        string regime;
        string recommendation;
        bool isClear;

        if (telemetry.PhaseStabilityStandardDeviationRadians > 0.22)
        {
            regime = "PHASE_NOISE_DECORRELATION_WARNING";
            recommendation = "Excessive optical phase decorrelation. Engage real-time cardiac/respiratory motion gate and increase A-scan line rate.";
            isClear = false;
        }
        else if (youngsModulusKPa > 38.0)
        {
            regime = "MALIGNANT_TUMOR_BOUNDARY_DETECTED";
            recommendation = "High tissue elasticity gradient detected (E > 38 kPa). Desmoplastic stromal invasion suspected; resect wider surgical margin.";
            isClear = false;
        }
        else if (string.Equals(telemetry.AnatomicalSite, "CornealStroma", StringComparison.OrdinalIgnoreCase) && youngsModulusKPa < 12.0)
        {
            regime = "CORNEAL_ECTASIA_SOFTENING_RISK";
            recommendation = "Corneal stromal shear modulus below 4 kPa. Initiate UV-A/riboflavin collagen cross-linking (CXL) protocol.";
            isClear = true;
        }
        else
        {
            regime = "NORMAL_COMPLIANT_STROMA";
            recommendation = "Tissue elasticity within normal physiological viscoelastic baseline. Negative surgical margin confirmed with micro-scale elastographic clarity.";
            isClear = true;
        }

        return new OceScanAnalysisResult(
            ScanId: telemetry.ScanId,
            ShearModulusKiloPascals: shearModulusKPa,
            YoungsModulusKiloPascals: youngsModulusKPa,
            DisplacementSensitivityPicometers: displacementSensitivityPm,
            TissueStiffnessRegime: regime,
            SurgicalGuidanceRecommendation: recommendation,
            IsMarginClear: isClear
        );
    }
}
