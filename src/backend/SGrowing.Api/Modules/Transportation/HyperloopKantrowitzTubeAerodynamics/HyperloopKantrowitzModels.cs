namespace SGrowing.Api.Modules.Transportation.HyperloopKantrowitzTubeAerodynamics;

public sealed record HyperloopTubeTelemetry(
    string PodId,
    double PodVelocityMetersPerSecond,
    double TubeStaticPressurePascals,
    double TubeTemperatureKelvin,
    double PodFrontalAreaSquareMeters,
    double TubeCrossSectionalAreaSquareMeters,
    double PodDragCoefficient,
    bool CompressorBypassActive,
    double CompressorMassFlowRateKgPerSec
);

public sealed record HyperloopTubeAnalysisResult(
    string PodId,
    double PodMachNumber,
    double KantrowitzMachLimit,
    double BlockageRatio,
    double AerodynamicPistonDragKiloNewtons,
    string KantrowitzChokingRegime,
    string AerodynamicControlGuidance,
    bool IsAerodynamicallyStable
);

public interface IHyperloopKantrowitzTubeAerodynamicsEngine
{
    HyperloopTubeAnalysisResult AnalyzeTubeAerodynamics(HyperloopTubeTelemetry telemetry);
}
