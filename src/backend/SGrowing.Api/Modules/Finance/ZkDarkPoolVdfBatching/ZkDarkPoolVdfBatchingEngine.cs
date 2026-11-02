using System;

namespace SGrowing.Api.Modules.Finance.ZkDarkPoolVdfBatching;

public interface IZkDarkPoolVdfBatchingEngine
{
    DarkPoolBatchAnalysisResult AnalyzeBatchSettlement(DarkPoolBatchTelemetry telemetry);
}

public sealed class ZkDarkPoolVdfBatchingEngine : IZkDarkPoolVdfBatchingEngine
{
    public DarkPoolBatchAnalysisResult AnalyzeBatchSettlement(DarkPoolBatchTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        if (telemetry.OracleReferencePriceUsd <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(telemetry), "Oracle reference price must be strictly positive.");
        }

        double totalVolume = telemetry.TotalShieldedBuyVolume + telemetry.TotalShieldedSellVolume;
        double matchedVolume = Math.Min(telemetry.TotalShieldedBuyVolume, telemetry.TotalShieldedSellVolume);
        double unmatchedVolume = Math.Abs(telemetry.TotalShieldedBuyVolume - telemetry.TotalShieldedSellVolume);

        double slippageFraction = 0.0;
        if (totalVolume > 0.0)
        {
            slippageFraction = 0.08 * ((telemetry.TotalShieldedBuyVolume - telemetry.TotalShieldedSellVolume) / totalVolume);
        }

        double uniformClearingPrice = telemetry.OracleReferencePriceUsd * (1.0 + slippageFraction);
        double priceSlippagePercent = Math.Abs(slippageFraction) * 100.0;

        string settlementStatus;
        string action;
        bool isBatchExecuted;

        if (!telemetry.IsVdfProofCryptographicallyValid || telemetry.ActualVdfElapsedSeconds < (telemetry.MinimumExpectedVdfDelaySeconds * 0.90))
        {
            settlementStatus = "MEV_TAMPER_DETECTED";
            action = "REJECT_BATCH_AND_SLASH_SEQUENCER_COLLATERAL";
            isBatchExecuted = false;
        }
        else if (telemetry.NullifierCollisionCount > 0 || telemetry.ValidZkProofCount < telemetry.SubmittedZkProofCount)
        {
            settlementStatus = "INVALID_ZK_PROOFS_OR_DOUBLE_SPEND";
            action = "PURGE_INVALID_NOTES_AND_RECALCULATE_BATCH";
            isBatchExecuted = false;
        }
        else if (priceSlippagePercent > 5.0)
        {
            settlementStatus = "HIGH_SLIPPAGE_EXCEEDED";
            action = "ROLL_UNMATCHED_LIQUIDITY_TO_NEXT_EPOCH";
            isBatchExecuted = false;
        }
        else
        {
            settlementStatus = "VDF_BATCH_CLEARED";
            action = "EXECUTE_ATOMIC_BATCH_SETTLEMENT_AND_EMIT_ZK_PROOFS";
            isBatchExecuted = true;
        }

        return new DarkPoolBatchAnalysisResult(
            telemetry.BatchEpochId,
            telemetry.AssetPair,
            Math.Round(uniformClearingPrice, 4),
            Math.Round(matchedVolume, 4),
            Math.Round(unmatchedVolume, 4),
            Math.Round(priceSlippagePercent, 3),
            settlementStatus,
            action,
            isBatchExecuted,
            DateTimeOffset.UtcNow);
    }
}
