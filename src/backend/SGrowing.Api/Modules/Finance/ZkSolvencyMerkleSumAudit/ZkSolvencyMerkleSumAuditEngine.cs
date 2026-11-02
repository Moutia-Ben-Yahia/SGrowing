using System;

namespace SGrowing.Api.Modules.Finance.ZkSolvencyMerkleSumAudit;

public sealed class ZkSolvencyMerkleSumAuditEngine : IZkSolvencyMerkleSumAuditEngine
{
    public ZkSolvencyAnalysisResult AnalyzeProofOfSolvency(ZkSolvencyTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        var liabilities = Math.Max(0.01, telemetry.TotalCommittedLiabilitiesEth);
        var assets = Math.Max(0.0, telemetry.TotalProvenOnChainAssetsEth);

        var solvencyRatio = Math.Round((assets / liabilities) * 100.0, 2);
        var surplusEth = Math.Round(assets - liabilities, 2);

        string regime;
        string action;
        bool isPassed;

        if (telemetry.NegativeBalanceLeafProofFailures > 0 || !telemetry.ZkSnarkRangeProofVerificationSucceeded)
        {
            regime = "NEGATIVE_BALANCE_FRAUD_DETECTED";
            action = "Merkle-Sum Tree negative balance leaf or ZK range proof failure. Exchange suspected of generating dummy negative accounts to deflate reported liabilities.";
            isPassed = false;
        }
        else if (solvencyRatio < 100.0)
        {
            regime = "INSOLVENT_LIABILITY_DEFICIT_CRITICAL";
            action = "Total liabilities exceed proven on-chain reserve assets. Declare fractional reserve insolvency; halt client withdrawals and engage regulatory conservator.";
            isPassed = false;
        }
        else if (telemetry.MultiSigAttestationQuorumRatio < 0.667)
        {
            regime = "MULTISIG_ATTESTATION_QUORUM_DEFICIENT";
            action = "Cold wallet multi-sig attestation failed to reach 2/3 Byzantine fault tolerance quorum. Re-authenticate hardware security modules (HSMs).";
            isPassed = false;
        }
        else
        {
            regime = "SOLVENT_OVERCOLLATERALIZED_PROVEN";
            action = "Zero-knowledge proof-of-solvency verified. Full 1:1 asset backing confirmed with positive reserve surplus cushion.";
            isPassed = true;
        }

        return new ZkSolvencyAnalysisResult(
            AuditSessionId: telemetry.AuditSessionId,
            SolvencyRatioPercent: solvencyRatio,
            ExcessReserveSurplusEth: surplusEth,
            InstitutionalSolvencyRegime: regime,
            SolvencyAuditAction: action,
            IsSolvencyAuditPassed: isPassed
        );
    }
}
