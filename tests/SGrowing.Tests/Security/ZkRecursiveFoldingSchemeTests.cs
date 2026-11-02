using FluentAssertions;
using SGrowing.Api.Modules.Security.ZkRecursiveFoldingScheme;
using Xunit;

namespace SGrowing.Tests.Security;

public sealed class ZkRecursiveFoldingSchemeTests
{
    private readonly ZkRecursiveFoldingSchemeEngine _engine = new();

    [Fact]
    public void AnalyzeFoldingSession_NominalFolding_ReturnsIncrementalFoldingOptimal()
    {
        var telemetry = new ZkFoldingTelemetry(
            ProofSessionId: "NOVA-ROLLUP-01",
            CurrentFoldedStepCount: 128,
            TargetDeciderRecursionThreshold: 512,
            ConstraintCountPerStep: 65536,
            CrossTermCommitmentLatencyMs: 14.5,
            RandomChallengeEntropyBits: 256.0,
            AccumulatedErrorVectorNorm: 0.005,
            SpartanDeciderSnarkWrapTriggered: false
        );

        var result = _engine.AnalyzeFoldingSession(telemetry);

        result.FoldingSchemeRegime.Should().Be("INCREMENTAL_FOLDING_OPTIMAL");
        result.IsProofAccumulationValid.Should().BeTrue();
        result.TotalFoldedConstraints.Should().Be(128 * 65536);
        result.AmortizedProverTimePerStepMs.Should().BeGreaterThan(10.0);
    }

    [Fact]
    public void AnalyzeFoldingSession_LowEntropyChallenge_ReturnsEntropyCollapseRisk()
    {
        var telemetry = new ZkFoldingTelemetry(
            ProofSessionId: "NOVA-WEAK-HASH-02",
            CurrentFoldedStepCount: 64,
            TargetDeciderRecursionThreshold: 512,
            ConstraintCountPerStep: 32768,
            CrossTermCommitmentLatencyMs: 12.0,
            RandomChallengeEntropyBits: 80.0, // Low entropy < 128 bits
            AccumulatedErrorVectorNorm: 0.002,
            SpartanDeciderSnarkWrapTriggered: false
        );

        var result = _engine.AnalyzeFoldingSession(telemetry);

        result.FoldingSchemeRegime.Should().Be("FIAT_SHAMIR_ENTROPY_COLLAPSE_RISK");
        result.IsProofAccumulationValid.Should().BeFalse();
        result.ProverOptimizationAction.Should().Contain("Poseidon2 state domain separation");
    }

    [Fact]
    public void AnalyzeFoldingSession_LargeErrorSlack_ReturnsSlackOverflowWarning()
    {
        var telemetry = new ZkFoldingTelemetry(
            ProofSessionId: "NOVA-OVERFLOW-03",
            CurrentFoldedStepCount: 200,
            TargetDeciderRecursionThreshold: 512,
            ConstraintCountPerStep: 65536,
            CrossTermCommitmentLatencyMs: 15.0,
            RandomChallengeEntropyBits: 256.0,
            AccumulatedErrorVectorNorm: 0.92, // High error vector norm > 0.85
            SpartanDeciderSnarkWrapTriggered: false
        );

        var result = _engine.AnalyzeFoldingSession(telemetry);

        result.FoldingSchemeRegime.Should().Be("RELAXED_R1CS_SLACK_OVERFLOW_WARNING");
        result.IsProofAccumulationValid.Should().BeFalse();
        result.ProverOptimizationAction.Should().Contain("Scale witness vectors");
    }

    [Fact]
    public void AnalyzeFoldingSession_ThresholdReached_ReturnsDeciderWrapRequired()
    {
        var telemetry = new ZkFoldingTelemetry(
            ProofSessionId: "NOVA-COMPLETE-04",
            CurrentFoldedStepCount: 512, // Reached threshold 512
            TargetDeciderRecursionThreshold: 512,
            ConstraintCountPerStep: 65536,
            CrossTermCommitmentLatencyMs: 16.0,
            RandomChallengeEntropyBits: 256.0,
            AccumulatedErrorVectorNorm: 0.012,
            SpartanDeciderSnarkWrapTriggered: true
        );

        var result = _engine.AnalyzeFoldingSession(telemetry);

        result.FoldingSchemeRegime.Should().Be("DECIDER_SNARK_WRAP_REQUIRED");
        result.IsProofAccumulationValid.Should().BeTrue();
        result.ProverOptimizationAction.Should().Contain("Spartan/Groth16 decider circuit");
    }
}
