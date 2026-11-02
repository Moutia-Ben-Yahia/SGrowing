namespace SGrowing.Api.Modules.Finance.ZkStateRelayAggregator;

public record ZkSubProof(
    string BatchId,
    long SourceBlockNumber,
    string SourceStateRootHash,
    string ProofSystem, // e.g., "Halo2_KZG", "Groth16_BN254", "Plonky2"
    string RawProofBytesBase64,
    int PublicInputsCount,
    int CompressedTxCount
);

public record ZkStateRelayAggregationRequest(
    string RelaySessionId,
    string TargetChainId, // e.g., "ETHEREUM_MAINNET", "ARBITRUM_ONE", "SOVEREIGN_ROLLUP_ALGERIA"
    IReadOnlyList<ZkSubProof> SubProofs,
    double MaxGasCalldataLimit = 300000.0
);

public record ZkAggregatedProofResult(
    string RelaySessionId,
    string TargetChainId,
    int AggregatedProofCount,
    int TotalAggregatedTransactions,
    string AggregatedProofHex,
    string AggregatedStateRootCommitmentHex,
    double CalldataCompressionRatioPercent,
    double EstimatedVerificationGasUnits,
    bool AggregationValid,
    string RelayVerificationStatus,
    string DispatchDirective
);
