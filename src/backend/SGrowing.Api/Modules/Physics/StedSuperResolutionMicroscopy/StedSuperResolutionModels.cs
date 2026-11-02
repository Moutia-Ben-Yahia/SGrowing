namespace SGrowing.Api.Modules.Physics.StedSuperResolutionMicroscopy;

public sealed record StedMicroscopyTelemetry(
    string MicroscopeId,
    double ExcitationWavelengthNanometers,
    double StedDepletionWavelengthNanometers,
    double ObjectiveNumericalAperture,
    double StedDoughnutLaserPowerMilliwatts,
    double SaturationIntensityMegawattsPerSquareCm,
    double SpecimenDepthMicrons,
    double PhotobleachingDecayRatePercentPerFrame,
    double AdaptiveOpticsWavefrontRmsErrorWavelengths
);

public sealed record StedMicroscopyAnalysisResult(
    string MicroscopeId,
    double AbbeDiffractionLimitNanometers,
    double EffectiveStedResolutionNanometers,
    double ResolutionImprovementFactor,
    double PhototoxicityRiskIndex,
    string MicroscopyRegime,
    string LaserPowerAndOpticsGuidance,
    bool IsImagingOptimal
);

public interface IStedSuperResolutionMicroscopyEngine
{
    StedMicroscopyAnalysisResult AnalyzeStedMicroscopy(StedMicroscopyTelemetry telemetry);
}
