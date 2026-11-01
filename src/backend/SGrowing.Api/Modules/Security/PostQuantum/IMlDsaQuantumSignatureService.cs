namespace SGrowing.Api.Modules.Security.PostQuantum;

/// <summary>
/// NIST FIPS 204 ML-DSA (Crystals-Dilithium) post-quantum digital signature service.
/// Provides lattice-based signature generation, rejection sampling, and tampering-evident verification.
/// </summary>
public interface IMlDsaQuantumSignatureService
{
    /// <summary>
    /// Generates an ML-DSA-65 lattice signature key pair at NIST Security Level 3.
    /// </summary>
    PqcKeyPair GenerateSignatureKeyPair();

    /// <summary>
    /// Signs an arbitrary payload using ML-DSA lattice rejection sampling.
    /// </summary>
    PqcSignatureToken SignPayload(byte[] payloadBytes, string privateKeyHex, string signerDid);

    /// <summary>
    /// Verifies an ML-DSA lattice signature against a payload and public key,
    /// detecting payload tampering, signature corruption, or key mismatches.
    /// </summary>
    PqcVerificationResult VerifySignature(byte[] payloadBytes, string signatureHex, string publicKeyHex);
}
