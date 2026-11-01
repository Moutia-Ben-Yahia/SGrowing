namespace SGrowing.Api.Modules.Security.PostQuantum;

/// <summary>
/// Cryptographic engine for Hybrid Classical (X25519) + PQC (ML-KEM / Crystals-Kyber-768)
/// Key Encapsulation Mechanism (KEM) in compliance with NIST FIPS 203.
/// </summary>
public interface IPostQuantumKeyExchangeEngine
{
    /// <summary>
    /// Generates a hybrid key pair combining classical X25519 ECDH and ML-KEM-768 lattice keys.
    /// </summary>
    PqcKeyPair GenerateHybridKeyPair();

    /// <summary>
    /// Encapsulates a dual shared secret against the client's classical and PQC public keys,
    /// returning the combined ciphertext and HKDF-SHA256 derived session key.
    /// </summary>
    HybridEncapsulationResult EncapsulateKey(string clientClassicalPkHex, string clientPqcPkHex);

    /// <summary>
    /// Decapsulates a received hybrid ciphertext using the recipient's private key,
    /// reconstructing the identical symmetric shared secret.
    /// </summary>
    string DecapsulateKey(string ciphertextHex, string privateKeyHex);
}
