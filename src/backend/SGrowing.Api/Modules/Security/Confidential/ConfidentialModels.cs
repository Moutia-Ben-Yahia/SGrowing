namespace SGrowing.Api.Modules.Security.Confidential;

/// <summary>
/// Public key for the Paillier cryptosystem used for additive homomorphic encryption.
/// </summary>
public record PaillierPublicKey
{
    public required string ModulusN { get; init; }
    public required string ModulusNSquared { get; init; }
    public required string GeneratorG { get; init; }
    public required int KeySizeBits { get; init; }
}

/// <summary>
/// Private key for the Paillier cryptosystem used for decryption of homomorphic ciphertexts.
/// </summary>
public record PaillierPrivateKey
{
    public required string Lambda { get; init; }
    public required string Mu { get; init; }
    public required PaillierPublicKey PublicKey { get; init; }
}

/// <summary>
/// Complete Paillier keypair containing both public and private keys.
/// </summary>
public record PaillierKeyPair
{
    public required PaillierPublicKey PublicKey { get; init; }
    public required PaillierPrivateKey PrivateKey { get; init; }
}

/// <summary>
/// Represents a homomorphically encrypted numerical value.
/// </summary>
public record EncryptedValue
{
    public required string CiphertextHex { get; init; }
    public required string ModulusNSquaredHex { get; init; }
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Blinded candidate item submitted by a consortium participant for private set intersection.
/// </summary>
public record PsiCandidateItem
{
    public required string BlindedHashHex { get; init; }
    public required string PartitionKey { get; init; }
}

/// <summary>
/// Result of a Private Set Intersection (PSI) computation across consortium participants.
/// </summary>
public record PsiIntersectionResult
{
    public required int MatchedCount { get; init; }
    public required IReadOnlyList<string> MatchedHashes { get; init; } = [];
    public required double ExecutionTimeMs { get; init; }
    public required IReadOnlyList<string> ConsortiumParticipantIds { get; init; } = [];
}

/// <summary>
/// Non-repudiable audit attestation token signed by the confidential computation mesh.
/// </summary>
public record ConfidentialAttestationToken
{
    public required string AttestationId { get; init; }
    public required string ConsortiumId { get; init; }
    public required string ComputationType { get; init; }
    public required string InputHash { get; init; }
    public required string ResultCommitment { get; init; }
    public DateTime IssuedAtUtc { get; init; } = DateTime.UtcNow;
    public required string CryptographicSignature { get; init; }
}

// ==========================================
// API DTOs (Requests & Responses)
// ==========================================

public record GenerateKeysRequest
{
    public int KeySizeBits { get; init; } = 512;
}

public record GenerateKeysResponse
{
    public required PaillierKeyPair KeyPair { get; init; }
}

public record EncryptValueRequest
{
    public required long Plaintext { get; init; }
    public required PaillierPublicKey PublicKey { get; init; }
}

public record EncryptValueResponse
{
    public required EncryptedValue Encrypted { get; init; }
}

public record HomomorphicAddRequest
{
    public required string Ciphertext1Hex { get; init; }
    public required string Ciphertext2Hex { get; init; }
    public required PaillierPublicKey PublicKey { get; init; }
}

public record HomomorphicAddResponse
{
    public required string ResultCiphertextHex { get; init; }
    public required PaillierPublicKey PublicKey { get; init; }
}

public record HomomorphicScalarMultiplyRequest
{
    public required string CiphertextHex { get; init; }
    public required long Scalar { get; init; }
    public required PaillierPublicKey PublicKey { get; init; }
}

public record HomomorphicScalarMultiplyResponse
{
    public required string ResultCiphertextHex { get; init; }
    public required PaillierPublicKey PublicKey { get; init; }
}

public record DecryptValueRequest
{
    public required string CiphertextHex { get; init; }
    public required PaillierPrivateKey PrivateKey { get; init; }
}

public record DecryptValueResponse
{
    public required long Plaintext { get; init; }
}

public record ComputePsiRequest
{
    public required IReadOnlyList<PsiCandidateItem> SetA { get; init; } = [];
    public required IReadOnlyList<PsiCandidateItem> SetB { get; init; } = [];
    public required string ConsortiumSessionId { get; init; }
    public IReadOnlyList<string>? ParticipantIds { get; init; }
}

public record ComputePsiResponse
{
    public required PsiIntersectionResult Result { get; init; }
}

public record IssueAttestationRequest
{
    public required string ConsortiumId { get; init; }
    public required string ComputationType { get; init; }
    public required string InputHash { get; init; }
    public required string ResultCommitment { get; init; }
    public IReadOnlyList<string>? ParticipantIds { get; init; }
}

public record VerifyAttestationRequest
{
    public required ConfidentialAttestationToken Token { get; init; }
    public required string ConsortiumSecretKey { get; init; }
}

public record VerifyAttestationResponse
{
    public required bool IsValid { get; init; }
    public required string AttestationId { get; init; }
    public string? Reason { get; init; }
}
