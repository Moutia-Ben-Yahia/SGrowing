namespace SGrowing.Tests.Infrastructure;

using FluentAssertions;
using SGrowing.Api.Modules.Infrastructure.DePinWorkerArbiter;
using Xunit;

public class DePinWorkerArbiterTests
{
    private readonly DePinWorkerArbiterEngine _engine = new();

    [Fact]
    public void ArbitrateBatch_ValidProofWithinSla_RewardsWorkerAndIncreasesReputation()
    {
        var worker = new EdgeWorkerNode("NODE-GPU-01", "0x1111AAA", "NVIDIA RTX 4090", 82.5, 5000m, 0.90, true);
        var task = new ComputeBatchTask("TASK-LLM-01", "QuantizedInference", 10.0, 500.0, 25.0m, "CHALLENGE-HASH-1", "EXPECTED-HASH-ABC");
        var proof = new WorkerExecutionProof("NODE-GPU-01", "TASK-LLM-01", 320.0, "EXPECTED-HASH-ABC", 62.5);

        var request = new DePinArbitrationRequest([worker], [task], [proof]);
        var result = _engine.ArbitrateBatch(request);

        result.VerifiedProofs.Should().Be(1);
        result.SlashedWorkers.Should().Be(0);
        result.TotalRewardsDisbursed.Should().Be(25.0m);
        result.TotalTokensSlashed.Should().Be(0m);
        result.NetworkIntegrityScore.Should().Be(100.0);

        var settlement = result.Settlements.Should().ContainSingle().Subject;
        settlement.IsVerified.Should().BeTrue();
        settlement.RewardEarnedTokens.Should().Be(25.0m);
        settlement.UpdatedReputation.Should().Be(0.95);
    }

    [Fact]
    public void ArbitrateBatch_ByzantineHashMismatch_SlashesCollateralAndReducesReputation()
    {
        var worker = new EdgeWorkerNode("NODE-MALICIOUS-02", "0x2222BBB", "Spoofed RTX 3080", 30.0, 4000m, 0.70, true);
        var task = new ComputeBatchTask("TASK-RENDER-02", "RayTracingBatch", 15.0, 1000.0, 50.0m, "CHALLENGE-HASH-2", "HASH-EXPECTED-GENUINE");
        var proof = new WorkerExecutionProof("NODE-MALICIOUS-02", "TASK-RENDER-02", 450.0, "HASH-TAMPERED-FRAUD", 55.0);

        var request = new DePinArbitrationRequest([worker], [task], [proof], SlashingFraction: 0.25m);
        var result = _engine.ArbitrateBatch(request);

        result.VerifiedProofs.Should().Be(0);
        result.SlashedWorkers.Should().Be(1);
        result.TotalRewardsDisbursed.Should().Be(0m);
        result.TotalTokensSlashed.Should().Be(1000m); // 25% of 4000
        result.NetworkIntegrityScore.Should().Be(0.0);

        var settlement = result.Settlements.Should().ContainSingle().Subject;
        settlement.IsVerified.Should().BeFalse();
        settlement.SlashedTokens.Should().Be(1000m);
        settlement.UpdatedReputation.Should().Be(0.20); // 0.70 - 0.50
        settlement.VerificationNote.Should().Contain("Byzantine");
    }

    [Fact]
    public void ArbitrateBatch_LatencySlaBreached_ZeroRewardAndNoSlashing()
    {
        var worker = new EdgeWorkerNode("NODE-SLOW-03", "0x3333CCC", "Apple M3 Max", 40.0, 2000m, 0.85, true);
        var task = new ComputeBatchTask("TASK-AUDIO-03", "WhisperTranscription", 5.0, 300.0, 15.0m, "CHALLENGE-HASH-3", "HASH-MATCH");
        var proof = new WorkerExecutionProof("NODE-SLOW-03", "TASK-AUDIO-03", 650.0, "HASH-MATCH", 70.0); // 650ms > 300ms SLA

        var request = new DePinArbitrationRequest([worker], [task], [proof]);
        var result = _engine.ArbitrateBatch(request);

        result.VerifiedProofs.Should().Be(0);
        result.SlashedWorkers.Should().Be(0);
        result.TotalRewardsDisbursed.Should().Be(0m);
        result.TotalTokensSlashed.Should().Be(0m);

        var settlement = result.Settlements.Should().ContainSingle().Subject;
        settlement.IsVerified.Should().BeFalse();
        settlement.RewardEarnedTokens.Should().Be(0m);
        settlement.SlashedTokens.Should().Be(0m);
        settlement.UpdatedReputation.Should().Be(0.75); // 0.85 - 0.10
        settlement.VerificationNote.Should().Contain("Latency SLA breached");
    }

    [Fact]
    public void ArbitrateBatch_EmptyProofs_ReturnsZeroEvaluatedAnd100Integrity()
    {
        var request = new DePinArbitrationRequest([], [], []);
        var result = _engine.ArbitrateBatch(request);

        result.TotalTasksEvaluated.Should().Be(0);
        result.VerifiedProofs.Should().Be(0);
        result.SlashedWorkers.Should().Be(0);
        result.NetworkIntegrityScore.Should().Be(100.0);
    }
}
