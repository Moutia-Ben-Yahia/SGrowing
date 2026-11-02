namespace SGrowing.Api.Modules.Quantum.GaussianBosonSampling;

public sealed record GbsTelemetry(
    string InterferometerId,
    int OpticalModeCount,
    double MeanSqueezingParameterR,
    double PhotonIndistinguishabilityVisibility,
    double OverallOpticalTransmissionEfficiency,
    int DetectedOutputPhotonCount,
    double ClassicalMcmcSampleSimilarityScore,
    double InterferometerPhaseStabilityRmsRadians
);

public sealed record GbsAnalysisResult(
    string InterferometerId,
    int TorontonianMatrixDimension,
    double Log10ClassicalHafnianSimulationComplexity,
    double QuantumComputationalSupremacyMargin,
    string SamplingRegime,
    string OpticalMeshCalibrationAction,
    bool IsQuantumAdvantageCertified
);

public interface IGaussianBosonSamplingEngine
{
    GbsAnalysisResult AnalyzeGaussianBosonSampling(GbsTelemetry telemetry);
}
