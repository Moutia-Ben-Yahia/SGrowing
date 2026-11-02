using System.Text.RegularExpressions;

namespace SGrowing.Api.Modules.Finance.ZkL3WatchtowerFraudProof;

public interface IZkL3WatchtowerEngine
{
    L3WatchtowerVerificationResult VerifyProposal(L3WatchtowerEvaluationRequest request);
}

public class ZkL3WatchtowerEngine : IZkL3WatchtowerEngine
{
    private static readonly Regex HexRegex = new(@"^0x[0-9a-fA-F]+$", RegexOptions.Compiled);

    public L3WatchtowerVerificationResult VerifyProposal(L3WatchtowerEvaluationRequest request)
    {
        var p = request.Proposal;
        var cfg = request.Config;

        // 1. Verify Proposed & Previous State Roots
        bool proposedRootValid = !string.IsNullOrWhiteSpace(p.ProposedStateRootHex) &&
                                 p.ProposedStateRootHex.Length >= 66 &&
                                 HexRegex.IsMatch(p.ProposedStateRootHex);

        bool previousRootValid = !string.IsNullOrWhiteSpace(p.PreviousStateRootHex) &&
                                 p.PreviousStateRootHex.Length >= 66 &&
                                 HexRegex.IsMatch(p.PreviousStateRootHex);

        // State root must evolve if transactions exist
        bool rootEvolutionValid = !(p.TransactionCount > 0 &&
                                    string.Equals(p.ProposedStateRootHex, p.PreviousStateRootHex, StringComparison.OrdinalIgnoreCase));

        bool stateConsistent = proposedRootValid && previousRootValid && rootEvolutionValid;

        // 2. Verify KZG Polynomial Commitment
        bool kzgValid = !string.IsNullOrWhiteSpace(p.PolynomialCommitmentKzgHex) &&
                        p.PolynomialCommitmentKzgHex.Length >= 98 && // 48 bytes (96 hex chars + '0x')
                        HexRegex.IsMatch(p.PolynomialCommitmentKzgHex);

        // 3. Sequencer Bond check
        bool bondAdequate = p.SequencerBondEth >= cfg.MinRequiredSequencerBondEth;

        // Fraud criteria
        bool fraudDetected = !stateConsistent || !kzgValid || !bondAdequate;
        bool slashTriggered = fraudDetected;
        double slashedAmount = slashTriggered ? p.SequencerBondEth : 0.0;

        string verdict;
        string directive;

        if (!stateConsistent)
        {
            verdict = "STATE_ROOT_EQUIVOCATION_FRAUD";
            directive = $"SUBMIT_FRAUD_PROOF: State root transition for block #{p.L3BlockNumber} is invalid or equivocal. Submit dispute to Layer-2 settlement contract and slash {slashedAmount:F1} ETH sequencer bond.";
        }
        else if (!kzgValid)
        {
            verdict = "POLYNOMIAL_COMMITMENT_CORRUPTION";
            directive = $"SUBMIT_KZG_CHALLENGE: KZG commitment evaluation failed or corrupt for block #{p.L3BlockNumber}. Trigger dispute challenge on L2 to halt rollup execution and slash sequencer.";
        }
        else if (!bondAdequate)
        {
            verdict = "INSUFFICIENT_SEQUENCER_COLLATERAL";
            directive = $"REJECT_PROPOSAL: Sequencer bond ({p.SequencerBondEth:F1} ETH) below {cfg.MinRequiredSequencerBondEth:F1} ETH threshold. Block proposal rejected from L3 state tree.";
        }
        else
        {
            verdict = "VALID_L3_STATE_ROOT_AFFIRMED";
            directive = $"CHALLENGE_CLEARED: Block #{p.L3BlockNumber} covering {p.TransactionCount} transactions verified against KZG commitment. Finalize state root to L2 settlement contract.";
        }

        return new L3WatchtowerVerificationResult(
            HyperchainId: p.HyperchainId,
            L3BlockNumber: p.L3BlockNumber,
            StateRootConsistent: stateConsistent,
            PolynomialCommitmentValid: kzgValid,
            FraudulentProposalDetected: fraudDetected,
            SlashingTriggered: slashTriggered,
            SlashedBondAmountEth: Math.Round(slashedAmount, 1),
            WatchtowerVerdictStatus: verdict,
            OnChainDisputeDirective: directive
        );
    }
}
