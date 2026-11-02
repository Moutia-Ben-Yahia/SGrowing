using FluentAssertions;
using SGrowing.Api.Modules.Finance.ZkStateRelayAggregator;
using System.Text;
using Xunit;

namespace SGrowing.Tests.Finance;

public class ZkStateRelayAggregatorTests
{
    private readonly ZkStateRelayAggregatorEngine _engine = new();

    [Fact]
    public void AggregateProofs_ValidSubproofBatch_ComputesRecursiveProofAndGasSavings()
    {
        var rawProofBytes1 = Convert.ToBase64String(Encoding.UTF8.GetBytes("GROTH16-CIRCUIT-BATCH-01-PROOF-BYTES-SECURE"));
        var rawProofBytes2 = Convert.ToBase64String(Encoding.UTF8.GetBytes("GROTH16-CIRCUIT-BATCH-02-PROOF-BYTES-SECURE"));

        var subProofs = new List<ZkSubProof>
        {
            new("BATCH-001", 19500000, "0xabcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890", "Groth16_BN254", rawProofBytes1, 2, 450),
            new("BATCH-002", 19500001, "0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef", "Groth16_BN254", rawProofBytes2, 2, 620)
        };

        var request = new ZkStateRelayAggregationRequest("RELAY-SESSION-99", "ETHEREUM_MAINNET", subProofs);
        var result = _engine.AggregateProofs(request);

        result.RelaySessionId.Should().Be("RELAY-SESSION-99");
        result.TargetChainId.Should().Be("ETHEREUM_MAINNET");
        result.AggregatedProofCount.Should().Be(2);
        result.TotalAggregatedTransactions.Should().Be(1070);
        result.AggregationValid.Should().BeTrue();
        result.RelayVerificationStatus.Should().Be("AGGREGATION_VERIFIED_SUCCINCT");
        result.CalldataCompressionRatioPercent.Should().BeGreaterThan(50.0);
        result.AggregatedProofHex.Should().StartWith("0x");
        result.AggregatedStateRootCommitmentHex.Should().StartWith("0x");
        result.DispatchDirective.Should().Contain("DISPATCH_RELAY");
    }

    [Fact]
    public void AggregateProofs_CorruptedBase64_RejectsWithDecodingFault()
    {
        var subProofs = new List<ZkSubProof>
        {
            new("BATCH-CORRUPT", 19500000, "0xabcdef1234567890", "Halo2_KZG", "THIS_IS_NOT_VALID_BASE64!@#$", 4, 100)
        };

        var request = new ZkStateRelayAggregationRequest("RELAY-CORRUPT", "ARBITRUM_ONE", subProofs);
        var result = _engine.AggregateProofs(request);

        result.AggregationValid.Should().BeFalse();
        result.RelayVerificationStatus.Should().Be("BASE64_DECODING_FAULT");
        result.DispatchDirective.Should().Contain("REJECT");
    }

    [Fact]
    public void AggregateProofs_EmptySubproofList_ReturnsEmptyBatchRejection()
    {
        var request = new ZkStateRelayAggregationRequest("RELAY-EMPTY", "SOVEREIGN_ROLLUP_ALGERIA", []);
        var result = _engine.AggregateProofs(request);

        result.AggregationValid.Should().BeFalse();
        result.RelayVerificationStatus.Should().Be("EMPTY_SUBPROOF_BATCH");
        result.TotalAggregatedTransactions.Should().Be(0);
    }

    [Fact]
    public void AggregateProofs_InsufficientProofBytes_RejectsSecurityRisk()
    {
        // 4 bytes is less than the 8-byte security minimum
        var shortProof = Convert.ToBase64String(new byte[] { 0x01, 0x02, 0x03 });
        var subProofs = new List<ZkSubProof>
        {
            new("BATCH-TINY", 19500000, "0xabcdef1234567890", "Plonky2", shortProof, 1, 10)
        };

        var request = new ZkStateRelayAggregationRequest("RELAY-TINY", "ETHEREUM_MAINNET", subProofs);
        var result = _engine.AggregateProofs(request);

        result.AggregationValid.Should().BeFalse();
        result.RelayVerificationStatus.Should().Be("INSUFFICIENT_PROOF_BYTES");
    }
}
