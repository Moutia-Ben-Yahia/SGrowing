namespace SGrowing.Api.Modules.BioTech.OpticalCoherenceElastographyBiomechanics;

public sealed record OceScanTelemetry(
    string ScanId,
    string AnatomicalSite,
    double OpticalWavelengthNanometers,
    double MeasuredShearWaveVelocityMetersPerSecond,
    double TissueDensityKgPerM3,
    double PhaseStabilityStandardDeviationRadians,
    double ShearWaveAttenuationCoefficientNpPerMm,
    bool AcousticRadiationForceTrackingActive
);

public sealed record OceScanAnalysisResult(
    string ScanId,
    double ShearModulusKiloPascals,
    double YoungsModulusKiloPascals,
    double DisplacementSensitivityPicometers,
    string TissueStiffnessRegime,
    string SurgicalGuidanceRecommendation,
    bool IsMarginClear
);

public interface IOpticalCoherenceElastographyBiomechanicsEngine
{
    OceScanAnalysisResult AnalyzeTissueBiomechanics(OceScanTelemetry telemetry);
}
