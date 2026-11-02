using System;
using SGrowing.Api.Modules.Finance.ZkDarkPoolVdfBatching;
using Xunit;

namespace SGrowing.Tests.Finance;

public sealed class ZkDarkPoolVdfBatchingTests
{
    private readonly ZkDarkPoolVdfBatchingEngine _engine = new();

    [Fact]
    public void AnalyzeBatchSettlement_ValidBatch_ExecutesAtomicSettlement()
    {
        var telemetry = new DarkPoolBatchTelemetry(
            BatchEpochId: "EPOCH-DARKPOOL-00921",
            AssetPair: "ETH/USDC",
            OracleReferencePriceUsd: 3200.0,
            TotalShieldedBuyVolume: 120.0,
            TotalShieldedSellVolume: 100.0,
            SubmittedZkProofCount: 25,
            ValidZkProofCount: 25,
            NullifierCollisionCount: 0,
            VdfEvaluationIterations: 5000000,
            ActualVdfElapsedSeconds: 12.5,
            MinimumExpectedVdfDelaySeconds: 12.0,
            IsVdfProofCryptographicallyValid: true);

        var result = _engine.AnalyzeBatchSettlement(telemetry);

        Assert.Equal("VDF_BATCH_CLEARED", result.BatchSettlementStatus);
        Assert.Equal("EXECUTE_ATOMIC_BATCH_SETTLEMENT_AND_EMIT_ZK_PROOFS", result.RecommendedArbitrationAction);
        Assert.True(result.IsBatchExecuted);
        Assert.Equal(100.0, result.ExecutableMatchedVolume);
        Assert.Equal(20.0, result.UnmatchedResidualVolume);
        Assert.True(result.PriceSlippagePercent < 5.0);
    }

    [Fact]
    public void AnalyzeBatchSettlement_InvalidVdfOrPrematureReveal_RejectsAndSlashes()
    {
        var telemetry = new DarkPoolBatchTelemetry(
            BatchEpochId: "EPOCH-DARKPOOL-00922",
            AssetPair: "BTC/USDT",
            OracleReferencePriceUsd: 92000.0,
            TotalShieldedBuyVolume: 10.0,
            TotalShieldedSellVolume: 10.0,
            SubmittedZkProofCount: 15,
            ValidZkProofCount: 15,
            NullifierCollisionCount: 0,
            VdfEvaluationIterations: 5000000,
            ActualVdfElapsedSeconds: 5.0, // Premature reveal (< 12.0 * 0.90)
            MinimumExpectedVdfDelaySeconds: 12.0,
            IsVdfProofCryptographicallyValid: true);

        var result = _engine.AnalyzeBatchSettlement(telemetry);

        Assert.Equal("MEV_TAMPER_DETECTED", result.BatchSettlementStatus);
        Assert.Equal("REJECT_BATCH_AND_SLASH_SEQUENCER_COLLATERAL", result.RecommendedArbitrationAction);
        Assert.False(result.IsBatchExecuted);
    }

    [Fact]
    public void AnalyzeBatchSettlement_NullifierCollision_FlagsDoubleSpend()
    {
        var telemetry = new DarkPoolBatchTelemetry(
            BatchEpochId: "EPOCH-DARKPOOL-00923",
            AssetPair: "SOL/USDC",
            OracleReferencePriceUsd: 185.0,
            TotalShieldedBuyVolume: 500.0,
            TotalShieldedSellVolume: 450.0,
            SubmittedZkProofCount: 30,
            ValidZkProofCount: 30,
            NullifierCollisionCount: 1, // Double-spend attempt detected
            VdfEvaluationIterations: 5000000,
            ActualVdfElapsedSeconds: 12.4,
            MinimumExpectedVdfDelaySeconds: 12.0,
            IsVdfProofCryptographicallyValid: true);

        var result = _engine.AnalyzeBatchSettlement(telemetry);

        Assert.Equal("INVALID_ZK_PROOFS_OR_DOUBLE_SPEND", result.BatchSettlementStatus);
        Assert.Equal("PURGE_INVALID_NOTES_AND_RECALCULATE_BATCH", result.RecommendedArbitrationAction);
        Assert.False(result.IsBatchExecuted);
    }

    [Fact]
    public void AnalyzeBatchSettlement_SevereVolumeImbalance_ExceedsSlippageLimit()
    {
        // 900 buy vs 10 sell -> total = 910 -> factor = 890 / 910 = 0.978 -> slippage = 0.08 * 0.978 * 100 = 7.82% > 5.0%
        var telemetry = new DarkPoolBatchTelemetry(
            BatchEpochId: "EPOCH-DARKPOOL-00924",
            AssetPair: "ETH/USDC",
            OracleReferencePriceUsd: 3200.0,
            TotalShieldedBuyVolume: 900.0,
            TotalShieldedSellVolume: 10.0,
            SubmittedZkProofCount: 40,
            ValidZkProofCount: 40,
            NullifierCollisionCount: 0,
            VdfEvaluationIterations: 5000000,
            ActualVdfElapsedSeconds: 12.2,
            MinimumExpectedVdfDelaySeconds: 12.0,
            IsVdfProofCryptographicallyValid: true);

        var result = _engine.AnalyzeBatchSettlement(telemetry);

        Assert.Equal("HIGH_SLIPPAGE_EXCEEDED", result.BatchSettlementStatus);
        Assert.Equal("ROLL_UNMATCHED_LIQUIDITY_TO_NEXT_EPOCH", result.RecommendedArbitrationAction);
        Assert.False(result.IsBatchExecuted);
        Assert.True(result.PriceSlippagePercent > 5.0);
    }
}
