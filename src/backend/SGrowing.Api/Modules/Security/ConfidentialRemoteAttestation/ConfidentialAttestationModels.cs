namespace SGrowing.Api.Modules.Security.ConfidentialRemoteAttestation;

public record EnclaveMeasurement(
    string EnclaveId,
    string ExpectedMrenclaveSha256,
    string ExpectedMrSignerSha256,
    int MinimumTcbVersion);

public record HardwareAttestationReport(
    string ReportId,
    string EnclaveId,
    string PlatformArchitecture,
    string ActualMrenclaveSha256,
    string ActualMrSignerSha256,
    int ReportedTcbVersion,
    string ChipmakerCertChainFingerprint,
    byte[] EnclavePublicKeyBytes,
    byte[] ChipmakerSignature);

public record EnclaveAttestationRequest(
    List<EnclaveMeasurement> RegisteredPolicies,
    List<HardwareAttestationReport> ReportsToVerify,
    string ExpectedPlatformArch = "AMD_SEV_SNP");

public record AttestationVerificationRecord(
    string ReportId,
    string EnclaveId,
    bool IsAttestationValid,
    bool IsTcbCompliant,
    string TrustReason);

public record AttestationMeshResult(
    int TotalReportsAudited,
    int ValidEnclaves,
    int RevokedEnclaves,
    List<AttestationVerificationRecord> Verifications,
    bool WorkloadMigrationPermitted,
    string SovereigntyVerdict);
