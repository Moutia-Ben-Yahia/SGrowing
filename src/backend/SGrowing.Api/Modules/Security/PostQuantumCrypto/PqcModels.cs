namespace SGrowing.Api.Modules.Security.PostQuantumCrypto;

public enum PqcKemScheme
{
    HybridX25519MlKem1024,
    NistFips203MlKem1024Only
}

public enum PqcSignatureScheme
{
    HybridNistP256MlDsa87,
    NistFips204MlDsa87Only
}

public sealed record HybridEncapsulationPacket(
    string PacketId,
    PqcKemScheme Scheme,
    byte[] ClassicalPublicKeySpki,
    byte[] LatticeCiphertext,
    byte[] Salt,
    DateTime EncapsulatedAtUtc
);

public sealed record PqcSealedEnvelope(
    string EnvelopeId,
    HybridEncapsulationPacket KemPacket,
    byte[] Ciphertext,
    byte[] Nonce12Bytes,
    byte[] Tag16Bytes,
    byte[] ClassicalSignature,
    byte[] PostQuantumSignature,
    DateTime SealedAtUtc
);

public sealed record SealPayloadRequest(
    byte[] Plaintext,
    byte[] RecipientClassicalPublicKeySpki,
    byte[] RecipientLatticePublicKey,
    byte[] SenderClassicalPrivateKeyPkcs8,
    byte[] SenderLatticePrivateKey
);

public sealed record OpenEnvelopeRequest(
    PqcSealedEnvelope Envelope,
    byte[] RecipientClassicalPrivateKeyPkcs8,
    byte[] RecipientLatticePrivateKey,
    byte[] SenderClassicalPublicKeySpki,
    byte[] SenderLatticePublicKey
);

public sealed record PqcVerificationResult(
    string EnvelopeId,
    bool IsClassicalSignatureValid,
    bool IsPostQuantumSignatureValid,
    bool IsHybridSecuritySatisfied,
    string CryptographicAuditNote,
    DateTime VerifiedAtUtc
);
