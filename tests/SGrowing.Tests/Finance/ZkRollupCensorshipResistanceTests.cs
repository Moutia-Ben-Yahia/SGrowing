using FluentAssertions;
using SGrowing.Api.Modules.Finance.ZkRollupCensorshipResistance;
using Xunit;

namespace SGrowing.Tests.Finance;

public sealed class ZkRollupCensorshipResistanceTests
{
    private readonly ZkRollupCensorshipResistanceEngine _engine = new();

    [Fact]
    public void AnalyzeRollupSequencerState_NominalDecentralized_ReturnsFairSequencing()
    {
        var telemetry = new ZkRollupCensorshipTelemetry(
            RollupBridgeId: "ROLLUP-ZK-SYNC-01",
            TotalActiveSequencers: 16,
            PendingForcedL1TransactionsCount: 2,
            OldestForcedTxWaitTimeHours: 2.5,
            SequencerStakeBondEth: 64.0,
            MevExtractionEstimateGwei: 45.0,
            CurrentL1GasBaseFeeGwei: 22.0,
            ThresholdEncryptionMempoolActive: true
        );

        var result = _engine.AnalyzeRollupSequencerState(telemetry);

        result.CensorshipPostureRegime.Should().Be("DECENTRALIZED_SEQUENCING_FAIR");
        result.IsSequencerNetworkHonest.Should().BeTrue();
        result.SlashPenaltyEth.Should().Be(0.0);
        result.ChallengeWindowRemainingHours.Should().BeGreaterThan(20.0);
    }

    [Fact]
    public void AnalyzeRollupSequencerState_TimeoutExceeded_ReturnsEscapeHatchTriggered()
    {
        var telemetry = new ZkRollupCensorshipTelemetry(
            RollupBridgeId: "ROLLUP-CENSORED-02",
            TotalActiveSequencers: 8,
            PendingForcedL1TransactionsCount: 4,
            OldestForcedTxWaitTimeHours: 25.0, // Exceeds 24h challenge window
            SequencerStakeBondEth: 64.0,
            MevExtractionEstimateGwei: 150.0,
            CurrentL1GasBaseFeeGwei: 25.0,
            ThresholdEncryptionMempoolActive: true
        );

        var result = _engine.AnalyzeRollupSequencerState(telemetry);

        result.CensorshipPostureRegime.Should().Be("FORCED_L1_ESCAPE_HATCH_TRIGGERED");
        result.IsSequencerNetworkHonest.Should().BeFalse();
        result.SlashPenaltyEth.Should().Be(64.0); // 100% slash
        result.ArbiterAction.Should().Contain("Confiscate sequencer stake");
    }

    [Fact]
    public void AnalyzeRollupSequencerState_HighMevUnencrypted_ReturnsCollusionSuspected()
    {
        var telemetry = new ZkRollupCensorshipTelemetry(
            RollupBridgeId: "ROLLUP-TOXIC-MEV-03",
            TotalActiveSequencers: 12,
            PendingForcedL1TransactionsCount: 1,
            OldestForcedTxWaitTimeHours: 4.0,
            SequencerStakeBondEth: 80.0,
            MevExtractionEstimateGwei: 650.0, // High MEV > 450 Gwei
            CurrentL1GasBaseFeeGwei: 30.0,
            ThresholdEncryptionMempoolActive: false // Unencrypted mempool
        );

        var result = _engine.AnalyzeRollupSequencerState(telemetry);

        result.CensorshipPostureRegime.Should().Be("SEQUENCER_MEV_COLLUSION_SUSPECTED");
        result.IsSequencerNetworkHonest.Should().BeFalse();
        result.SlashPenaltyEth.Should().BeGreaterThan(0.0);
        result.ArbiterAction.Should().Contain("DKG threshold decryption");
    }

    [Fact]
    public void AnalyzeRollupSequencerState_ApproachingDeadline_ReturnsPendingWarning()
    {
        var telemetry = new ZkRollupCensorshipTelemetry(
            RollupBridgeId: "ROLLUP-DELAYED-04",
            TotalActiveSequencers: 16,
            PendingForcedL1TransactionsCount: 3,
            OldestForcedTxWaitTimeHours: 19.5, // 19.5h > 16h
            SequencerStakeBondEth: 64.0,
            MevExtractionEstimateGwei: 80.0,
            CurrentL1GasBaseFeeGwei: 28.0,
            ThresholdEncryptionMempoolActive: true
        );

        var result = _engine.AnalyzeRollupSequencerState(telemetry);

        result.CensorshipPostureRegime.Should().Be("PENDING_INCLUSION_WARNING");
        result.IsSequencerNetworkHonest.Should().BeTrue();
        result.SlashPenaltyEth.Should().Be(6.4);
        result.ChallengeWindowRemainingHours.Should().BeLessThan(5.0);
    }
}
