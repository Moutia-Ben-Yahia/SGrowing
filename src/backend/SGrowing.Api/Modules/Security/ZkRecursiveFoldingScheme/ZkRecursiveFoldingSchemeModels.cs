namespace SGrowing.Api.Modules.Security.ZkRecursiveFoldingScheme;

public sealed record ZkFoldingTelemetry(
    string ProofSessionId,
    int CurrentFoldedStepCount,
    int TargetDeciderRecursionThreshold,
    int ConstraintCountPerStep,
    double CrossTermCommitmentLatencyMs,
    double RandomChallengeEntropyBits,
    double AccumulatedErrorVectorNorm,
    bool SpartanDeciderSnarkWrapTriggered
);

public sealed record ZkFoldingAnalysisResult(
    string ProofSessionId,
    double TotalFoldedConstraints,
    double AmortizedProverTimePerStepMs,
    double SoundnessErrorMargin,
    string FoldingSchemeRegime,
    string ProverOptimizationAction,
    bool IsProofAccumulationValid
);

public interface IZkRecursiveFoldingSchemeEngine
{
    ZkFoldingAnalysisResult AnalyzeFoldingSession(ZkFoldingTelemetry telemetry);
}
