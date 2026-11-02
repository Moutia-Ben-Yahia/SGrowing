namespace SGrowing.Tests.Finance;

using System.Security.Cryptography;
using SGrowing.Api.Modules.Finance.PqcCrossChainHtlc;
using Xunit;

public class PqcCrossChainHtlcTests
{
    private readonly PqcCrossChainHtlcEngine _engine = new();

    private static string ComputeExpectedHash(byte[] preimage)
    {
        byte[] hash = SHA3_256.IsSupported ? SHA3_256.HashData(preimage) : SHA256.HashData(preimage);
        return "0x" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    [Fact]
    public void EvaluateSwap_SafeTimelockParameters_ReportsAtomicSwapReady()
    {
        // Source = 7200s (2h), Target = 3600s (1h) -> delta = 3600s
        // Finality: src = 60s, tgt = 15s -> max = 60s
        // Required = 2 * 60 + 300 = 420s -> Delta 3600s is safe with margin +3180s
        var telemetry = new PqcCrossChainHtlcTelemetry(
            SwapId: "SWAP-ETH-SOL-001",
            SourceChainId: "Ethereum-Mainnet",
            TargetChainId: "Solana-Mainnet",
            SourceAmountUsd: 10000.00m,
            TargetAmountUsd: 10000.00m,
            Sha3HashLockHex: "0x1122334455667788990011223344556677889900112233445566778899001122",
            SourceTimelockDurationSeconds: 7200,
            TargetTimelockDurationSeconds: 3600,
            SourceBlockFinalityLatencySeconds: 60,
            TargetBlockFinalityLatencySeconds: 15,
            ClaimPreimageHex: null,
            ZkPreimageProofHex: null);

        var result = _engine.EvaluateSwap(telemetry);

        Assert.Equal("SWAP-ETH-SOL-001", result.SwapId);
        Assert.True(result.IsTimelockSafetyCompliant);
        Assert.Equal(3180, result.SafetyMarginSeconds);
        Assert.Equal("ATOMIC_SWAP_READY", result.ExecutionState);
        Assert.Equal("PROCEED_WITH_CONTRACT_DEPLOYMENT", result.RecommendedAction);
    }

    [Fact]
    public void EvaluateSwap_UnsafeTimelockDifferential_FlagsTimelockSafetyViolation()
    {
        // Source = 3700s, Target = 3600s -> delta = 100s
        // Finality: 60s -> Required = 120 + 300 = 420s > 100s -> UNSAFE!
        var telemetry = new PqcCrossChainHtlcTelemetry(
            SwapId: "SWAP-UNSAFE-002",
            SourceChainId: "Ethereum-Mainnet",
            TargetChainId: "Solana-Mainnet",
            SourceAmountUsd: 50000.00m,
            TargetAmountUsd: 50000.00m,
            Sha3HashLockHex: "0x1122334455667788990011223344556677889900112233445566778899001122",
            SourceTimelockDurationSeconds: 3700,
            TargetTimelockDurationSeconds: 3600,
            SourceBlockFinalityLatencySeconds: 60,
            TargetBlockFinalityLatencySeconds: 15,
            ClaimPreimageHex: null,
            ZkPreimageProofHex: null);

        var result = _engine.EvaluateSwap(telemetry);

        Assert.False(result.IsTimelockSafetyCompliant);
        Assert.True(result.SafetyMarginSeconds < 0);
        Assert.Equal("TIMELOCK_SAFETY_VIOLATION", result.ExecutionState);
        Assert.Equal("INCREASE_SOURCE_TIMELOCK_DELTA", result.RecommendedAction);
        Assert.Contains("CROSS-CHAIN RACE RISK", result.AuditSummary);
    }

    [Fact]
    public void EvaluateSwap_ValidPreimageAndZkProof_SettlesClaim()
    {
        byte[] preimageBytes = [0xde, 0xad, 0xbe, 0xef, 0xca, 0xfe, 0xba, 0xbe];
        string expectedHash = ComputeExpectedHash(preimageBytes);

        var telemetry = new PqcCrossChainHtlcTelemetry(
            SwapId: "SWAP-CLAIM-003",
            SourceChainId: "Ethereum-Mainnet",
            TargetChainId: "Arbitrum-One",
            SourceAmountUsd: 25000.00m,
            TargetAmountUsd: 25000.00m,
            Sha3HashLockHex: expectedHash,
            SourceTimelockDurationSeconds: 7200,
            TargetTimelockDurationSeconds: 3600,
            SourceBlockFinalityLatencySeconds: 12,
            TargetBlockFinalityLatencySeconds: 2,
            ClaimPreimageHex: "0x" + Convert.ToHexString(preimageBytes),
            ZkPreimageProofHex: "0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef");

        var result = _engine.EvaluateSwap(telemetry);

        Assert.True(result.IsTimelockSafetyCompliant);
        Assert.True(result.IsPreimageValid);
        Assert.True(result.IsZkProofValid);
        Assert.Equal("PREIMAGE_CLAIM_SETTLED", result.ExecutionState);
        Assert.Equal("EXTRACT_AND_EXECUTE_COUNTERPARTY_CLAIM", result.RecommendedAction);
    }

    [Fact]
    public void EvaluateSwap_ZeroAmount_ThrowsArgumentOutOfRangeException()
    {
        var invalid = new PqcCrossChainHtlcTelemetry(
            SwapId: "ERR-001",
            SourceChainId: "ChainA",
            TargetChainId: "ChainB",
            SourceAmountUsd: 0m,
            TargetAmountUsd: 100m,
            Sha3HashLockHex: "0x1234",
            SourceTimelockDurationSeconds: 1000,
            TargetTimelockDurationSeconds: 500,
            SourceBlockFinalityLatencySeconds: 10,
            TargetBlockFinalityLatencySeconds: 10,
            ClaimPreimageHex: null,
            ZkPreimageProofHex: null);

        Assert.Throws<ArgumentOutOfRangeException>(() => _engine.EvaluateSwap(invalid));
    }
}
