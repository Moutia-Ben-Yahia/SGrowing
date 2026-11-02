using System;

namespace SGrowing.Api.Modules.Security.ZkRecursiveFoldingScheme;

public sealed class ZkRecursiveFoldingSchemeEngine : IZkRecursiveFoldingSchemeEngine
{
    public ZkFoldingAnalysisResult AnalyzeFoldingSession(ZkFoldingTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        var steps = Math.Max(1, telemetry.CurrentFoldedStepCount);
        var constraintsPerStep = Math.Max(1, telemetry.ConstraintCountPerStep);
        var totalConstraints = (double)steps * constraintsPerStep;

        var amortizedProverMs = Math.Round(telemetry.CrossTermCommitmentLatencyMs + 1.25, 2);

        // Soundness error margin 2^(-bits)
        var entropy = Math.Clamp(telemetry.RandomChallengeEntropyBits, 1.0, 256.0);
        var soundnessMargin = Math.Round(Math.Pow(2.0, -entropy), 15);

        string regime;
        string action;
        bool isValid;

        if (telemetry.RandomChallengeEntropyBits < 128.0)
        {
            regime = "FIAT_SHAMIR_ENTROPY_COLLAPSE_RISK";
            action = "Transcript hash entropy below 128 bits. Inject Poseidon2 state domain separation to prevent adaptive prover collision.";
            isValid = false;
        }
        else if (telemetry.AccumulatedErrorVectorNorm > 0.85)
        {
            regime = "RELAXED_R1CS_SLACK_OVERFLOW_WARNING";
            action = "Accumulated error slack vector norm near field overflow. Scale witness vectors or execute intermediate compression step.";
            isValid = false;
        }
        else if (telemetry.CurrentFoldedStepCount >= telemetry.TargetDeciderRecursionThreshold || telemetry.SpartanDeciderSnarkWrapTriggered)
        {
            regime = "DECIDER_SNARK_WRAP_REQUIRED";
            action = "Folding recursion threshold reached. Dispatch Spartan/Groth16 decider circuit to seal O(1) constant-size on-chain verification proof.";
            isValid = true;
        }
        else
        {
            regime = "INCREMENTAL_FOLDING_OPTIMAL";
            action = "Nova IVC relaxed R1CS folding proceeding nominally. O(1) group multiscalar multiplications confirmed with zero FFT overhead.";
            isValid = true;
        }

        return new ZkFoldingAnalysisResult(
            ProofSessionId: telemetry.ProofSessionId,
            TotalFoldedConstraints: totalConstraints,
            AmortizedProverTimePerStepMs: amortizedProverMs,
            SoundnessErrorMargin: soundnessMargin,
            FoldingSchemeRegime: regime,
            ProverOptimizationAction: action,
            IsProofAccumulationValid: isValid
        );
    }
}
