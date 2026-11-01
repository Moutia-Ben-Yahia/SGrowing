namespace SGrowing.Api.Modules.Security.ConfidentialEnclave;

/// <summary>
/// Hardware confidential computing isolation platforms.
/// </summary>
public enum EnclavePlatform
{
    AmdSevSnp = 1,       // AMD Secure Encrypted Virtualization-Secure Nested Paging
    IntelSgx = 2,        // Intel Software Guard Extensions
    AwsNitroEnclaves = 3,// AWS Nitro Enclaves
    ArmRealm = 4         // ARM Confidential Compute Architecture (CCA) Realm
}

/// <summary>
/// State of a cryptographic key within the HSM lifecycle.
/// </summary>
public enum HsmKeyStatus
{
    Active = 1,
    Rotated = 2,
    Compromised = 3,
    Zeroized = 4
}

/// <summary>
/// Cryptographic key algorithm classes managed by the HSM.
/// </summary>
public enum HsmKeyType
{
    Aes256Gcm = 1,
    Rsa4096 = 2,
    EcSecp256r1 = 3,
    Kyber1024Pqc = 4
}

/// <summary>
/// Launch measurement and hardware identity registers of a confidential micro-VM.
/// </summary>
public sealed record EnclaveLaunchMeasurement(
    EnclavePlatform Platform,
    string MeasurementSha384Hex, // PCR / launch digest
    string ImageFamilyId,
    int SvnVersion,              // Security Version Number
    string HostPlatformId
);

/// <summary>
/// Cryptographic attestation report issued by the secure processor/enclave hardware.
/// </summary>
public sealed record EnclaveAttestationReport(
    string ReportId,
    EnclavePlatform Platform,
    EnclaveLaunchMeasurement Measurement,
    string ReportDataHex,        // Nonce or public key digest
    string ChipId,
    string HardwareSignatureHex,
    string CertificateChainFingerprint,
    DateTime IssuedAtUtc
);

/// <summary>
/// Sovereign compliance policy defining acceptable hardware measurements and SVN levels.
/// </summary>
public sealed record AttestationPolicy(
    string PolicyId,
    IReadOnlyList<EnclavePlatform> AllowedPlatforms,
    IReadOnlyList<string> GoldenMeasurementHashes,
    int MinimumSvn,
    bool RequireHardwareSignatureValidation = true
);

/// <summary>
/// Outcome of cryptographic remote attestation evaluation.
/// </summary>
public sealed record AttestationVerificationResult(
    string ReportId,
    EnclavePlatform Platform,
    bool Passed,
    string? FailureReason,
    string? EnclaveSessionToken,
    DateTime VerifiedAtUtc
);

/// <summary>
/// Metadata describing an HSM-backed key.
/// </summary>
public sealed record HsmKeyMetadata(
    string KeyId,
    HsmKeyType KeyType,
    string KeyAlias,
    int Version,
    HsmKeyStatus Status,
    DateTime CreatedAtUtc,
    DateTime NextRotationUtc,
    DateTime? RotatedAtUtc = null
);

/// <summary>
/// A discrete slice in Shamir's M-of-N threshold secret sharing scheme.
/// </summary>
public sealed record ShamirSecretShare(
    int ShareIndex,
    int ThresholdM,
    int TotalSharesN,
    string ShareDataHex,
    string ShareChecksumHex
);

/// <summary>
/// Mutually attested confidential mesh channel between two micro-VMs.
/// </summary>
public sealed record ConfidentialMeshChannel(
    string ChannelId,
    string SourceEnclaveId,
    string TargetEnclaveId,
    bool MutualAttestationVerified,
    DateTime EstablishedAtUtc,
    long MessagesTransferred,
    bool IsActive
);

/// <summary>
/// Attestation verification request payload.
/// </summary>
public sealed record VerifyAttestationRequest(
    EnclaveAttestationReport Report,
    string ExpectedNonceHex
);

/// <summary>
/// Request to split a secret master key using Shamir's M-of-N secret sharing.
/// </summary>
public sealed record ShamirSplitRequest(
    string SecretDataHex,
    int ThresholdM,
    int TotalSharesN
);

/// <summary>
/// Request to reconstruct a secret from a threshold subset of Shamir shares.
/// </summary>
public sealed record ShamirReconstructRequest(
    IReadOnlyList<ShamirSecretShare> Shares
);

/// <summary>
/// Request to establish an mATLS confidential mesh channel between micro-VMs.
/// </summary>
public sealed record EstablishMeshChannelRequest(
    string SourceEnclaveId,
    string TargetEnclaveId,
    EnclaveAttestationReport SourceReport,
    EnclaveAttestationReport TargetReport,
    string SharedChallengeNonceHex
);
