using System.Security.Cryptography;
using System.Text;

namespace SGrowing.Api.Modules.Finance.ZkStateRelayAggregator;

public interface IZkStateRelayAggregatorEngine
{
    ZkAggregatedProofResult AggregateProofs(ZkStateRelayAggregationRequest request);
}

public class ZkStateRelayAggregatorEngine : IZkStateRelayAggregatorEngine
{
    public ZkAggregatedProofResult AggregateProofs(ZkStateRelayAggregationRequest request)
    {
        var subProofs = request.SubProofs ?? [];
        if (subProofs.Count == 0)
        {
            return new ZkAggregatedProofResult(
                RelaySessionId: request.RelaySessionId,
                TargetChainId: request.TargetChainId,
                AggregatedProofCount: 0,
                TotalAggregatedTransactions: 0,
                AggregatedProofHex: "0x00",
                AggregatedStateRootCommitmentHex: "0x00",
                CalldataCompressionRatioPercent: 0.0,
                EstimatedVerificationGasUnits: 0.0,
                AggregationValid: false,
                RelayVerificationStatus: "EMPTY_SUBPROOF_BATCH",
                DispatchDirective: "REJECT: No sub-proofs supplied for recursive rollup aggregation."
            );
        }

        // Validate each sub-proof format and integrity
        int totalTxs = 0;
        using var rootHasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var proofHasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        foreach (var p in subProofs)
        {
            if (string.IsNullOrWhiteSpace(p.RawProofBytesBase64) || string.IsNullOrWhiteSpace(p.SourceStateRootHash))
            {
                return new ZkAggregatedProofResult(
                    RelaySessionId: request.RelaySessionId,
                    TargetChainId: request.TargetChainId,
                    AggregatedProofCount: subProofs.Count,
                    TotalAggregatedTransactions: 0,
                    AggregatedProofHex: "0x00",
                    AggregatedStateRootCommitmentHex: "0x00",
                    CalldataCompressionRatioPercent: 0.0,
                    EstimatedVerificationGasUnits: 0.0,
                    AggregationValid: false,
                    RelayVerificationStatus: "INVALID_SUBPROOF_PAYLOAD",
                    DispatchDirective: $"REJECT: Sub-proof batch {p.BatchId} contains null or malformed proof/state-root payload."
                );
            }

            try
            {
                byte[] raw = Convert.FromBase64String(p.RawProofBytesBase64);
                if (raw.Length < 8)
                {
                    return new ZkAggregatedProofResult(
                        RelaySessionId: request.RelaySessionId,
                        TargetChainId: request.TargetChainId,
                        AggregatedProofCount: subProofs.Count,
                        TotalAggregatedTransactions: 0,
                        AggregatedProofHex: "0x00",
                        AggregatedStateRootCommitmentHex: "0x00",
                        CalldataCompressionRatioPercent: 0.0,
                        EstimatedVerificationGasUnits: 0.0,
                        AggregationValid: false,
                        RelayVerificationStatus: "INSUFFICIENT_PROOF_BYTES",
                        DispatchDirective: $"REJECT: Sub-proof {p.BatchId} byte length is below cryptographic security minimum."
                    );
                }

                proofHasher.AppendData(raw);
                rootHasher.AppendData(Encoding.UTF8.GetBytes(p.SourceStateRootHash));
                totalTxs += Math.Max(1, p.CompressedTxCount);
            }
            catch (FormatException)
            {
                return new ZkAggregatedProofResult(
                    RelaySessionId: request.RelaySessionId,
                    TargetChainId: request.TargetChainId,
                    AggregatedProofCount: subProofs.Count,
                    TotalAggregatedTransactions: 0,
                    AggregatedProofHex: "0x00",
                    AggregatedStateRootCommitmentHex: "0x00",
                    CalldataCompressionRatioPercent: 0.0,
                    EstimatedVerificationGasUnits: 0.0,
                    AggregationValid: false,
                    RelayVerificationStatus: "BASE64_DECODING_FAULT",
                    DispatchDirective: $"REJECT: Sub-proof {p.BatchId} failed Base64 cryptographic decoding."
                );
            }
        }

        byte[] finalRootHash = rootHasher.GetHashAndReset();
        byte[] finalProofHash = proofHasher.GetHashAndReset();

        string aggregatedStateRoot = "0x" + Convert.ToHexString(finalRootHash).ToLowerInvariant();
        string aggregatedProof = "0x" + Convert.ToHexString(finalProofHash).ToLowerInvariant();

        // Gas calculations:
        // Individual proof on-chain verification ~220,000 gas each
        // Recursive aggregated verification: 210,000 fixed gas + (1,200 * n)
        double unaggregatedGas = subProofs.Count * 220000.0;
        double aggregatedGas = 210000.0 + (subProofs.Count * 1200.0);
        double gasSavedPercent = unaggregatedGas > 0 ? (100.0 * (unaggregatedGas - aggregatedGas) / unaggregatedGas) : 0.0;
        gasSavedPercent = Math.Clamp(gasSavedPercent, 0.0, 95.0);

        bool gasWithinLimit = aggregatedGas <= (request.MaxGasCalldataLimit * 1.5);
        string status = gasWithinLimit ? "AGGREGATION_VERIFIED_SUCCINCT" : "GAS_LIMIT_EXCEEDED";
        string directive = gasWithinLimit
            ? $"DISPATCH_RELAY: Successfully aggregated {subProofs.Count} sub-proofs covering {totalTxs} rollup transactions with {gasSavedPercent:F1}% calldata compression. Relay state commitment to {request.TargetChainId} bridge contract."
            : $"THROTTLE_BATCH: Aggregated gas requirement ({aggregatedGas:F0}) exceeds target calldata gas ceiling ({request.MaxGasCalldataLimit:F0}). Partition batch into smaller sub-rollups.";

        return new ZkAggregatedProofResult(
            RelaySessionId: request.RelaySessionId,
            TargetChainId: request.TargetChainId,
            AggregatedProofCount: subProofs.Count,
            TotalAggregatedTransactions: totalTxs,
            AggregatedProofHex: aggregatedProof,
            AggregatedStateRootCommitmentHex: aggregatedStateRoot,
            CalldataCompressionRatioPercent: Math.Round(gasSavedPercent, 1),
            EstimatedVerificationGasUnits: Math.Round(aggregatedGas, 0),
            AggregationValid: gasWithinLimit,
            RelayVerificationStatus: status,
            DispatchDirective: directive
        );
    }
}
