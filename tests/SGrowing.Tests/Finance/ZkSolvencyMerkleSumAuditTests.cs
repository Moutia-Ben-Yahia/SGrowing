using FluentAssertions;
using SGrowing.Api.Modules.Finance.ZkSolvencyMerkleSumAudit;
using Xunit;

namespace SGrowing.Tests.Finance;

public sealed class ZkSolvencyMerkleSumAuditTests
{
    private readonly ZkSolvencyMerkleSumAuditEngine _engine = new();

    [Fact]
    public void AnalyzeProofOfSolvency_NominalOvercollateralized_ReturnsSolventProven()
    {
        var telemetry = new ZkSolvencyTelemetry(
            AuditSessionId: "AUDIT-KRAKEN-ETH-01",
            TotalVerifiedAccountsCount: 250000,
            TotalCommittedLiabilitiesEth: 185000.0,
            TotalProvenOnChainAssetsEth: 205000.0,
            NegativeBalanceLeafProofFailures: 0,
            MultiSigAttestationQuorumRatio: 0.85,
            MerkleSumTreeRootHashVerificationMs: 32.0,
            ZkSnarkRangeProofVerificationSucceeded: true
        );

        var result = _engine.AnalyzeProofOfSolvency(telemetry);

        result.InstitutionalSolvencyRegime.Should().Be("SOLVENT_OVERCOLLATERALIZED_PROVEN");
        result.IsSolvencyAuditPassed.Should().BeTrue();
        result.SolvencyRatioPercent.Should().BeGreaterThan(100.0);
        result.ExcessReserveSurplusEth.Should().Be(20000.0);
    }

    [Fact]
    public void AnalyzeProofOfSolvency_NegativeLeafFound_ReturnsFraudDetected()
    {
        var telemetry = new ZkSolvencyTelemetry(
            AuditSessionId: "AUDIT-FRAUD-02",
            TotalVerifiedAccountsCount: 150000,
            TotalCommittedLiabilitiesEth: 120000.0,
            TotalProvenOnChainAssetsEth: 130000.0,
            NegativeBalanceLeafProofFailures: 3, // Negative balance fraud
            MultiSigAttestationQuorumRatio: 0.80,
            MerkleSumTreeRootHashVerificationMs: 45.0,
            ZkSnarkRangeProofVerificationSucceeded: false
        );

        var result = _engine.AnalyzeProofOfSolvency(telemetry);

        result.InstitutionalSolvencyRegime.Should().Be("NEGATIVE_BALANCE_FRAUD_DETECTED");
        result.IsSolvencyAuditPassed.Should().BeFalse();
        result.SolvencyAuditAction.Should().Contain("generating dummy negative accounts");
    }

    [Fact]
    public void AnalyzeProofOfSolvency_ReserveDeficit_ReturnsInsolventCritical()
    {
        var telemetry = new ZkSolvencyTelemetry(
            AuditSessionId: "AUDIT-FTX-DEFICIT-03",
            TotalVerifiedAccountsCount: 400000,
            TotalCommittedLiabilitiesEth: 250000.0,
            TotalProvenOnChainAssetsEth: 190000.0, // Deficit of 60,000 ETH
            NegativeBalanceLeafProofFailures: 0,
            MultiSigAttestationQuorumRatio: 0.90,
            MerkleSumTreeRootHashVerificationMs: 28.0,
            ZkSnarkRangeProofVerificationSucceeded: true
        );

        var result = _engine.AnalyzeProofOfSolvency(telemetry);

        result.InstitutionalSolvencyRegime.Should().Be("INSOLVENT_LIABILITY_DEFICIT_CRITICAL");
        result.IsSolvencyAuditPassed.Should().BeFalse();
        result.SolvencyRatioPercent.Should().BeLessThan(100.0);
        result.SolvencyAuditAction.Should().Contain("fractional reserve insolvency");
    }

    [Fact]
    public void AnalyzeProofOfSolvency_LowMultisigQuorum_ReturnsQuorumDeficient()
    {
        var telemetry = new ZkSolvencyTelemetry(
            AuditSessionId: "AUDIT-LOWQUORUM-04",
            TotalVerifiedAccountsCount: 100000,
            TotalCommittedLiabilitiesEth: 80000.0,
            TotalProvenOnChainAssetsEth: 90000.0,
            NegativeBalanceLeafProofFailures: 0,
            MultiSigAttestationQuorumRatio: 0.50, // Insufficient quorum < 2/3
            MerkleSumTreeRootHashVerificationMs: 25.0,
            ZkSnarkRangeProofVerificationSucceeded: true
        );

        var result = _engine.AnalyzeProofOfSolvency(telemetry);

        result.InstitutionalSolvencyRegime.Should().Be("MULTISIG_ATTESTATION_QUORUM_DEFICIENT");
        result.IsSolvencyAuditPassed.Should().BeFalse();
        result.SolvencyAuditAction.Should().Contain("2/3 Byzantine fault tolerance quorum");
    }
}
