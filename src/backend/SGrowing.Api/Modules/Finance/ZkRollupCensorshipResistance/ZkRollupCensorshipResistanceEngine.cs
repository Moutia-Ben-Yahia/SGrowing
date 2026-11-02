using System;

namespace SGrowing.Api.Modules.Finance.ZkRollupCensorshipResistance;

public sealed class ZkRollupCensorshipResistanceEngine : IZkRollupCensorshipResistanceEngine
{
    public ZkRollupCensorshipAnalysisResult AnalyzeRollupSequencerState(ZkRollupCensorshipTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        const double maxChallengeWindowHours = 24.0;
        var remainingHours = Math.Round(Math.Max(0.0, maxChallengeWindowHours - telemetry.OldestForcedTxWaitTimeHours), 1);

        var timeFraction = Math.Clamp(telemetry.OldestForcedTxWaitTimeHours / maxChallengeWindowHours, 0.0, 1.0);
        var encryptionDiscount = telemetry.ThresholdEncryptionMempoolActive ? 0.40 : 1.0;
        var riskScore = Math.Round(Math.Clamp((timeFraction * encryptionDiscount) + (telemetry.PendingForcedL1TransactionsCount * 0.04), 0.0, 1.0), 3);

        double slashEth;
        string regime;
        string action;
        bool isHonest;

        if (telemetry.OldestForcedTxWaitTimeHours >= maxChallengeWindowHours)
        {
            regime = "FORCED_L1_ESCAPE_HATCH_TRIGGERED";
            slashEth = telemetry.SequencerStakeBondEth; // 100% slash
            action = "Forced transaction timed out past 24h challenge window. Confiscate sequencer stake, freeze batch posting, and activate direct L1 emergency state withdrawal.";
            isHonest = false;
        }
        else if (!telemetry.ThresholdEncryptionMempoolActive && telemetry.MevExtractionEstimateGwei > 450.0)
        {
            regime = "SEQUENCER_MEV_COLLUSION_SUSPECTED";
            slashEth = Math.Round(telemetry.SequencerStakeBondEth * 0.15, 2);
            action = "Unencrypted mempool exhibiting toxic sandwich MEV extraction (>450 Gwei). Enforce DKG threshold decryption and rotate leader sequencer.";
            isHonest = false;
        }
        else if (telemetry.OldestForcedTxWaitTimeHours > 16.0 || telemetry.PendingForcedL1TransactionsCount > 8)
        {
            regime = "PENDING_INCLUSION_WARNING";
            slashEth = Math.Round(telemetry.SequencerStakeBondEth * 0.10, 2);
            action = "Forced L1 queue approaching challenge threshold. Prioritize forced inclusion transactions in next zk-proof batch commit.";
            isHonest = true;
        }
        else
        {
            regime = "DECENTRALIZED_SEQUENCING_FAIR";
            slashEth = 0.0;
            action = "Multi-prover sequencer queue balanced. Forced L1 transactions settling within SLA and threshold encryption preserving fair ordering.";
            isHonest = true;
        }

        return new ZkRollupCensorshipAnalysisResult(
            RollupBridgeId: telemetry.RollupBridgeId,
            CensorshipRiskScore: riskScore,
            SlashPenaltyEth: slashEth,
            ChallengeWindowRemainingHours: remainingHours,
            CensorshipPostureRegime: regime,
            ArbiterAction: action,
            IsSequencerNetworkHonest: isHonest
        );
    }
}
