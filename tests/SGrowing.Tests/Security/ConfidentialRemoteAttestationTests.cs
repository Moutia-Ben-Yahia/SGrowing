namespace SGrowing.Tests.Security;

using FluentAssertions;
using SGrowing.Api.Modules.Security.ConfidentialRemoteAttestation;
using Xunit;

public class ConfidentialRemoteAttestationTests
{
    private readonly ConfidentialRemoteAttestationEngine _engine = new();

    [Fact]
    public void VerifyAttestationMesh_GenuineAmdSevSnpReport_ConfirmsAttestationAndEnablesMigration()
    {
        var policy = new EnclaveMeasurement(
            EnclaveId: "ENC-LEDGER-01",
            ExpectedMrenclaveSha256: "E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855",
            ExpectedMrSignerSha256: "SIGNER-CORP-ROOT-KEY-HASH-9988",
            MinimumTcbVersion: 104
        );

        var report = new HardwareAttestationReport(
            ReportId: "REP-AMD-001",
            EnclaveId: "ENC-LEDGER-01",
            PlatformArchitecture: "AMD_SEV_SNP",
            ActualMrenclaveSha256: "E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855",
            ActualMrSignerSha256: "SIGNER-CORP-ROOT-KEY-HASH-9988",
            ReportedTcbVersion: 106, // Compliant (> 104)
            ChipmakerCertChainFingerprint: "CERT-AMD-MILAN-ROOT-CA-1",
            EnclavePublicKeyBytes: new byte[32],
            ChipmakerSignature: new byte[64]
        );

        var request = new EnclaveAttestationRequest([policy], [report]);
        var result = _engine.VerifyAttestationMesh(request);

        result.TotalReportsAudited.Should().Be(1);
        result.ValidEnclaves.Should().Be(1);
        result.RevokedEnclaves.Should().Be(0);
        result.WorkloadMigrationPermitted.Should().BeTrue();
        result.SovereigntyVerdict.Should().Contain("Sovereign Attestation Confirmed");

        var record = result.Verifications.Should().ContainSingle().Subject;
        record.IsAttestationValid.Should().BeTrue();
        record.IsTcbCompliant.Should().BeTrue();
        record.TrustReason.Should().Contain("Memory encryption key exchange approved");
    }

    [Fact]
    public void VerifyAttestationMesh_TamperedMrenclaveHash_RevokesEnclaveAndHaltsMigration()
    {
        var policy = new EnclaveMeasurement("ENC-PAYMENT-02", "GENUINE-HASH-AAA", "SIGNER-KEY", 100);

        var tamperedReport = new HardwareAttestationReport(
            ReportId: "REP-TAMPER-002",
            EnclaveId: "ENC-PAYMENT-02",
            PlatformArchitecture: "AMD_SEV_SNP",
            ActualMrenclaveSha256: "INJECTED-MALICIOUS-PAYLOAD-HASH", // Tampered!
            ActualMrSignerSha256: "SIGNER-KEY",
            ReportedTcbVersion: 105,
            ChipmakerCertChainFingerprint: "CERT-AMD-GENOA-CA",
            EnclavePublicKeyBytes: new byte[32],
            ChipmakerSignature: new byte[64]
        );

        var request = new EnclaveAttestationRequest([policy], [tamperedReport]);
        var result = _engine.VerifyAttestationMesh(request);

        result.ValidEnclaves.Should().Be(0);
        result.RevokedEnclaves.Should().Be(1);
        result.WorkloadMigrationPermitted.Should().BeFalse();
        result.SovereigntyVerdict.Should().Contain("Security Alert");

        var record = result.Verifications.Should().ContainSingle().Subject;
        record.IsAttestationValid.Should().BeFalse();
        record.TrustReason.Should().Contain("MRENCLAVE Measurement Tampered");
    }

    [Fact]
    public void VerifyAttestationMesh_OutdatedMicrocodeTcb_FlagsTcbNonCompliance()
    {
        var policy = new EnclaveMeasurement("ENC-DATABASE-03", "HASH-MATCH", "SIGNER-KEY", MinimumTcbVersion: 120);

        var outdatedReport = new HardwareAttestationReport(
            ReportId: "REP-OUTDATED-003",
            EnclaveId: "ENC-DATABASE-03",
            PlatformArchitecture: "AMD_SEV_SNP",
            ActualMrenclaveSha256: "HASH-MATCH",
            ActualMrSignerSha256: "SIGNER-KEY",
            ReportedTcbVersion: 108, // Outdated! (108 < 120)
            ChipmakerCertChainFingerprint: "CERT-AMD-EPYC-CA",
            EnclavePublicKeyBytes: new byte[32],
            ChipmakerSignature: new byte[64]
        );

        var request = new EnclaveAttestationRequest([policy], [outdatedReport]);
        var result = _engine.VerifyAttestationMesh(request);

        result.WorkloadMigrationPermitted.Should().BeFalse();
        var record = result.Verifications.Should().ContainSingle().Subject;
        record.IsTcbCompliant.Should().BeFalse();
        record.TrustReason.Should().Contain("Outdated Microcode TCB");
    }

    [Fact]
    public void VerifyAttestationMesh_EmptyReportsList_ReturnsZeroAudited()
    {
        var request = new EnclaveAttestationRequest([], []);
        var result = _engine.VerifyAttestationMesh(request);

        result.TotalReportsAudited.Should().Be(0);
        result.WorkloadMigrationPermitted.Should().BeFalse();
        result.SovereigntyVerdict.Should().Contain("Zero Reports");
    }
}
