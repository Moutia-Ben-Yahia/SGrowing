using FluentAssertions;
using SGrowing.Api.Modules.Finance.ZkL3WatchtowerFraudProof;
using Xunit;

namespace SGrowing.Tests.Finance;

public class ZkL3WatchtowerFraudProofTests
{
    private readonly ZkL3WatchtowerEngine _engine = new();

    private const string ValidStateRoot1 = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string ValidStateRoot2 = "0xbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string ValidKzgCommitment = "0x111122223333444455556666777788889999000011112222333344445555666677778888999900001111222233334444";

    [Fact]
    public void VerifyProposal_ValidStateAndKzg_AffirmsSettlement()
    {
        var proposal = new L3BlockProposal(
            HyperchainId: "HYPERCHAIN-DEFI-ALGERIA",
            L3BlockNumber: 104250,
            ProposedStateRootHex: ValidStateRoot2,
            PreviousStateRootHex: ValidStateRoot1,
            SequencerAddress: "0x1234567890123456789012345678901234567890",
            PolynomialCommitmentKzgHex: ValidKzgCommitment,
            TransactionCount: 350,
            SequencerBondEth: 32.0
        );

        var request = new L3WatchtowerEvaluationRequest(proposal, new WatchtowerVerificationConfig());
        var result = _engine.VerifyProposal(request);

        result.HyperchainId.Should().Be("HYPERCHAIN-DEFI-ALGERIA");
        result.StateRootConsistent.Should().BeTrue();
        result.PolynomialCommitmentValid.Should().BeTrue();
        result.FraudulentProposalDetected.Should().BeFalse();
        result.SlashingTriggered.Should().BeFalse();
        result.WatchtowerVerdictStatus.Should().Be("VALID_L3_STATE_ROOT_AFFIRMED");
        result.OnChainDisputeDirective.Should().Contain("CHALLENGE_CLEARED");
    }

    [Fact]
    public void VerifyProposal_StateRootEquivocation_TriggersFraudProofAndSlashing()
    {
        // Proposed root is identical to previous root despite having 200 transactions (equivocation)
        var proposal = new L3BlockProposal(
            HyperchainId: "HYPERCHAIN-EQUIVOCATION",
            L3BlockNumber: 104251,
            ProposedStateRootHex: ValidStateRoot1,
            PreviousStateRootHex: ValidStateRoot1, // Identical!
            SequencerAddress: "0x9999999999999999999999999999999999999999",
            PolynomialCommitmentKzgHex: ValidKzgCommitment,
            TransactionCount: 200,
            SequencerBondEth: 32.0
        );

        var request = new L3WatchtowerEvaluationRequest(proposal, new WatchtowerVerificationConfig());
        var result = _engine.VerifyProposal(request);

        result.FraudulentProposalDetected.Should().BeTrue();
        result.SlashingTriggered.Should().BeTrue();
        result.SlashedBondAmountEth.Should().Be(32.0);
        result.WatchtowerVerdictStatus.Should().Be("STATE_ROOT_EQUIVOCATION_FRAUD");
        result.OnChainDisputeDirective.Should().Contain("SUBMIT_FRAUD_PROOF");
    }

    [Fact]
    public void VerifyProposal_CorruptKzgCommitment_DetectsCommitmentCorruption()
    {
        // KZG commitment is too short (corrupt)
        var proposal = new L3BlockProposal(
            HyperchainId: "HYPERCHAIN-CORRUPT-KZG",
            L3BlockNumber: 104252,
            ProposedStateRootHex: ValidStateRoot2,
            PreviousStateRootHex: ValidStateRoot1,
            SequencerAddress: "0x1234567890123456789012345678901234567890",
            PolynomialCommitmentKzgHex: "0xdeadbeef", // Too short!
            TransactionCount: 150,
            SequencerBondEth: 32.0
        );

        var request = new L3WatchtowerEvaluationRequest(proposal, new WatchtowerVerificationConfig());
        var result = _engine.VerifyProposal(request);

        result.PolynomialCommitmentValid.Should().BeFalse();
        result.FraudulentProposalDetected.Should().BeTrue();
        result.WatchtowerVerdictStatus.Should().Be("POLYNOMIAL_COMMITMENT_CORRUPTION");
    }

    [Fact]
    public void VerifyProposal_UndercollateralizedBond_RejectsProposal()
    {
        var proposal = new L3BlockProposal(
            HyperchainId: "HYPERCHAIN-UNDERBONDED",
            L3BlockNumber: 104253,
            ProposedStateRootHex: ValidStateRoot2,
            PreviousStateRootHex: ValidStateRoot1,
            SequencerAddress: "0x1234567890123456789012345678901234567890",
            PolynomialCommitmentKzgHex: ValidKzgCommitment,
            TransactionCount: 100,
            SequencerBondEth: 12.0 // Below 25 ETH min
        );

        var request = new L3WatchtowerEvaluationRequest(proposal, new WatchtowerVerificationConfig(MinRequiredSequencerBondEth: 25.0));
        var result = _engine.VerifyProposal(request);

        result.FraudulentProposalDetected.Should().BeTrue();
        result.WatchtowerVerdictStatus.Should().Be("INSUFFICIENT_SEQUENCER_COLLATERAL");
    }
}
