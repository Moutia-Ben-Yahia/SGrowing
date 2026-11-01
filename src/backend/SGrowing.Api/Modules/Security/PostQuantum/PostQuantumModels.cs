using System.Text.Json.Serialization;

namespace SGrowing.Api.Modules.Security.PostQuantum;

/// <summary>
/// Quantum-safe and hybrid cryptographic algorithms supported by SGrowing PQC suite.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PqcAlgorithm
{
    MlKem768_CrystalsKyber,
    MlDsa65_CrystalsDilithium,
    HybridClassicalPqc,
    ClassicalX25519
}

/// <summary>
/// Post-quantum cryptographic security levels defined by NIST SP 800-208 / FIPS 203 / 204.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SecurityLevel
{
    NistLevel3_192bit,
    NistLevel5_256bit
}

/// <summary>
/// Verification status resulting from ML-DSA signature validation.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SignatureVerificationStatus
{
    Valid,
    InvalidSignature,
    TamperedPayload,
    Expired
}

/// <summary>
/// Represents a post-quantum or hybrid cryptographic key pair.
/// </summary>
public sealed record PqcKeyPair(
    string PublicKeyHex,
    string PrivateKeyHex,
    PqcAlgorithm Algorithm,
    SecurityLevel SecurityLevel,
    DateTime CreatedAtUtc);

/// <summary>
/// Encapsulation result from Hybrid Classical (X25519) + PQC (ML-KEM Crystals-Kyber) exchange.
/// </summary>
public sealed record HybridEncapsulationResult(
    string CiphertextHex,
    string SharedSecretHex,
    string ClassicalPublicKeyHex,
    string PqcPublicKeyHex);

/// <summary>
/// Signed cryptographic token produced using NIST FIPS 204 ML-DSA lattice signatures.
/// </summary>
public sealed record PqcSignatureToken(
    string MessageHashHex,
    string SignatureHex,
    string SignerDid,
    PqcAlgorithm Algorithm,
    DateTime CreatedAtUtc);

/// <summary>
/// Verification evaluation for post-quantum digital signatures.
/// </summary>
public sealed record PqcVerificationResult(
    SignatureVerificationStatus Status,
    PqcAlgorithm Algorithm,
    string SignerDid,
    string VerificationMessage);

/// <summary>
/// Request to generate a post-quantum or hybrid key pair.
/// </summary>
public sealed record GeneratePqcKeysRequest
{
    public PqcAlgorithm Algorithm { get; init; } = PqcAlgorithm.HybridClassicalPqc;
    public SecurityLevel SecurityLevel { get; init; } = SecurityLevel.NistLevel3_192bit;
}

/// <summary>
/// Request to encapsulate a shared secret for a client's public keys.
/// </summary>
public sealed record EncapsulateKeyRequest
{
    public string ClientClassicalPkHex { get; init; } = string.Empty;
    public string ClientPqcPkHex { get; init; } = string.Empty;
}

/// <summary>
/// Request to decapsulate a received hybrid ciphertext using recipient's private key.
/// </summary>
public sealed record DecapsulateKeyRequest
{
    public string CiphertextHex { get; init; } = string.Empty;
    public string PrivateKeyHex { get; init; } = string.Empty;
}

/// <summary>
/// Response payload containing the decapsulated symmetric shared secret.
/// </summary>
public sealed record DecapsulateKeyResponse(string SharedSecretHex);

/// <summary>
/// Request to sign a payload using ML-DSA lattice signature.
/// </summary>
public sealed record SignPayloadRequest
{
    public string PayloadBase64 { get; init; } = string.Empty;
    public string? PayloadText { get; init; }
    public string PrivateKeyHex { get; init; } = string.Empty;
    public string SignerDid { get; init; } = string.Empty;
}

/// <summary>
/// Response payload containing the generated signature token.
/// </summary>
public sealed record SignPayloadResponse(PqcSignatureToken Token);

/// <summary>
/// Request to verify an ML-DSA lattice signature against a payload.
/// </summary>
public sealed record VerifySignatureRequest
{
    public string PayloadBase64 { get; init; } = string.Empty;
    public string? PayloadText { get; init; }
    public string SignatureHex { get; init; } = string.Empty;
    public string PublicKeyHex { get; init; } = string.Empty;
}

/// <summary>
/// Response payload containing verification results.
/// </summary>
public sealed record VerifySignatureResponse(PqcVerificationResult Result);

/// <summary>
/// Result of dynamic cipher suite negotiation between client and server.
/// </summary>
public sealed record NegotiatedCipherSuiteResult(
    PqcAlgorithm SelectedAlgorithm,
    bool IsQuantumResistant,
    string SuiteName,
    string Description);

/// <summary>
/// Quantum readiness status and audit metrics of the enterprise platform.
/// </summary>
public sealed record QuantumReadinessResponse
{
    public double QuantumSafePercentage { get; init; }
    public long TotalCommunications { get; init; }
    public long QuantumSafeCommunications { get; init; }
    public long ClassicalCommunications { get; init; }
    public string RecommendedMigrationStrategy { get; init; } = string.Empty;
    public IReadOnlyList<string> SupportedAlgorithms { get; init; } = [];
}
