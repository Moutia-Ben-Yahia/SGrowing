namespace SGrowing.Api.Modules.Finance.ZkSolvencyMerkleSumAudit;

public sealed record ZkSolvencyTelemetry(
    string AuditSessionId,
    int TotalVerifiedAccountsCount,
    double TotalCommittedLiabilitiesEth,
    double TotalProvenOnChainAssetsEth,
    int NegativeBalanceLeafProofFailures,
    double MultiSigAttestationQuorumRatio,
    double MerkleSumTreeRootHashVerificationMs,
    bool ZkSnarkRangeProofVerificationSucceeded
);

public sealed record ZkSolvencyAnalysisResult(
    string AuditSessionId,
    double SolvencyRatioPercent,
    double ExcessReserveSurplusEth,
    string InstitutionalSolvencyRegime,
    string SolvencyAuditAction,
    bool IsSolvencyAuditPassed
);

public interface IZkSolvencyMerkleSumAuditEngine
{
    ZkSolvencyAnalysisResult AnalyzeProofOfSolvency(ZkSolvencyTelemetry telemetry);
}
