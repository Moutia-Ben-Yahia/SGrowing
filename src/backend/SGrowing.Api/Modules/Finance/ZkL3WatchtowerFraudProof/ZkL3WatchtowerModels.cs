namespace SGrowing.Api.Modules.Finance.ZkL3WatchtowerFraudProof;

public record L3BlockProposal(
    string HyperchainId,
    long L3BlockNumber,
    string ProposedStateRootHex,
    string PreviousStateRootHex,
    string SequencerAddress,
    string PolynomialCommitmentKzgHex,
    int TransactionCount,
    double SequencerBondEth = 32.0
);

public record WatchtowerVerificationConfig(
    long DisputeWindowBlocksRemaining = 450,
    double MinRequiredSequencerBondEth = 25.0
);

public record L3WatchtowerEvaluationRequest(
    L3BlockProposal Proposal,
    WatchtowerVerificationConfig Config
);

public record L3WatchtowerVerificationResult(
    string HyperchainId,
    long L3BlockNumber,
    bool StateRootConsistent,
    bool PolynomialCommitmentValid,
    bool FraudulentProposalDetected,
    bool SlashingTriggered,
    double SlashedBondAmountEth,
    string WatchtowerVerdictStatus,
    string OnChainDisputeDirective
);
