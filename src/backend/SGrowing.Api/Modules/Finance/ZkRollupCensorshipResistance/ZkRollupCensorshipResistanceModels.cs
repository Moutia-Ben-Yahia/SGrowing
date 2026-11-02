namespace SGrowing.Api.Modules.Finance.ZkRollupCensorshipResistance;

public sealed record ZkRollupCensorshipTelemetry(
    string RollupBridgeId,
    int TotalActiveSequencers,
    int PendingForcedL1TransactionsCount,
    double OldestForcedTxWaitTimeHours,
    double SequencerStakeBondEth,
    double MevExtractionEstimateGwei,
    double CurrentL1GasBaseFeeGwei,
    bool ThresholdEncryptionMempoolActive
);

public sealed record ZkRollupCensorshipAnalysisResult(
    string RollupBridgeId,
    double CensorshipRiskScore,
    double SlashPenaltyEth,
    double ChallengeWindowRemainingHours,
    string CensorshipPostureRegime,
    string ArbiterAction,
    bool IsSequencerNetworkHonest
);

public interface IZkRollupCensorshipResistanceEngine
{
    ZkRollupCensorshipAnalysisResult AnalyzeRollupSequencerState(ZkRollupCensorshipTelemetry telemetry);
}
