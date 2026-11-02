namespace SGrowing.Api.Modules.Physics.ColdAtomGravimeterInterferometry;

public sealed record ColdAtomGravimeterTelemetry(
    string GravimeterId,
    double Rb87AtomCloudCount,
    double InterrogationTimeTSeconds,
    double RamanLaserWavelengthNanometers,
    double RamanLaserChirpRateMhzPerSec,
    double ResidualSeismicVibrationRmsMicroG,
    double InterferometerInterferenceFringeContrastPercent,
    double EffectiveGravitationalAccelerationMps2
);

public sealed record ColdAtomGravimeterAnalysisResult(
    string GravimeterId,
    double CalculatedGravitationalPhaseShiftRadians,
    double GravityMeasurementResolutionMicroGal,
    double ChirpRateDopplerErrorHertz,
    string GravimetryRegime,
    string FeedforwardVibrationControlGuidance,
    bool IsMetrologyValid
);

public interface IColdAtomGravimeterInterferometryEngine
{
    ColdAtomGravimeterAnalysisResult AnalyzeGravimeter(ColdAtomGravimeterTelemetry telemetry);
}
