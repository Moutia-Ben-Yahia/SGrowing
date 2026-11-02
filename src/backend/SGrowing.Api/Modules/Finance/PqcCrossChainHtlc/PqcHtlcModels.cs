namespace SGrowing.Api.Modules.Finance.PqcCrossChainHtlc;

public record PqcCrossChainHtlcTelemetry(
    string SwapId,
    string SourceChainId,
    string TargetChainId,
    decimal SourceAmountUsd,
    decimal TargetAmountUsd,
    string Sha3HashLockHex,
    int SourceTimelockDurationSeconds,
    int TargetTimelockDurationSeconds,
    int SourceBlockFinalityLatencySeconds,
    int TargetBlockFinalityLatencySeconds,
    string? ClaimPreimageHex,
    string? ZkPreimageProofHex);

public record PqcCrossChainHtlcAnalysisResult(
    string SwapId,
    bool IsTimelockSafetyCompliant,
    int SafetyMarginSeconds,
    bool IsPreimageValid,
    bool IsZkProofValid,
    string ExecutionState,
    string RecommendedAction,
    string AuditSummary);
