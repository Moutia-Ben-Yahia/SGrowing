namespace SGrowing.Api.Modules.Infrastructure.DePinWorkerArbiter;

public record EdgeWorkerNode(
    string WorkerId,
    string OperatorAddress,
    string HardwareSpec,
    double AvailableTflops,
    decimal StakedCollateralTokens,
    double HistoricalReputationScore,
    bool IsActive);

public record ComputeBatchTask(
    string TaskId,
    string JobType,
    double RequiredTflops,
    double MaxAllowedLatencyMs,
    decimal RewardTokens,
    string ChallengeHash,
    string ExpectedResultHash);

public record WorkerExecutionProof(
    string WorkerId,
    string TaskId,
    double ExecutionDurationMs,
    string ProducedResultHash,
    double TemperatureCelsius);

public record DePinArbitrationRequest(
    List<EdgeWorkerNode> RegisteredNodes,
    List<ComputeBatchTask> Tasks,
    List<WorkerExecutionProof> SubmittedProofs,
    decimal SlashingFraction = 0.25m);

public record WorkerSettlementRecord(
    string WorkerId,
    string TaskId,
    bool IsVerified,
    decimal RewardEarnedTokens,
    decimal SlashedTokens,
    double UpdatedReputation,
    string VerificationNote);

public record DePinArbitrationResult(
    int TotalTasksEvaluated,
    int VerifiedProofs,
    int SlashedWorkers,
    decimal TotalRewardsDisbursed,
    decimal TotalTokensSlashed,
    List<WorkerSettlementRecord> Settlements,
    double NetworkIntegrityScore);
