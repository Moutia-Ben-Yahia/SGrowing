using System;

namespace SGrowing.Api.Modules.Finance.ZkDarkPoolVdfBatching;

public sealed record DarkPoolBatchTelemetry(
    string BatchEpochId,
    string AssetPair,
    double OracleReferencePriceUsd,
    double TotalShieldedBuyVolume,
    double TotalShieldedSellVolume,
    int SubmittedZkProofCount,
    int ValidZkProofCount,
    int NullifierCollisionCount,
    long VdfEvaluationIterations,
    double ActualVdfElapsedSeconds,
    double MinimumExpectedVdfDelaySeconds,
    bool IsVdfProofCryptographicallyValid);

public sealed record DarkPoolBatchAnalysisResult(
    string BatchEpochId,
    string AssetPair,
    double UniformClearingPriceUsd,
    double ExecutableMatchedVolume,
    double UnmatchedResidualVolume,
    double PriceSlippagePercent,
    string BatchSettlementStatus,
    string RecommendedArbitrationAction,
    bool IsBatchExecuted,
    DateTimeOffset Timestamp);
