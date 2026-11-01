using System.Numerics;

namespace SGrowing.Api.Modules.Security.Confidential;

/// <summary>
/// Cryptographic engine implementing the Paillier Additive Homomorphic Cryptosystem.
/// Enables mathematical computations (addition, subtraction, scalar multiplication) directly on encrypted data.
/// </summary>
public interface IPaillierHomomorphicEncryptionEngine
{
    /// <summary>
    /// Generates a new Paillier key pair of the specified modulus bit length (e.g. 512, 1024, 2048).
    /// </summary>
    PaillierKeyPair GenerateKeyPair(int bitLength = 512);

    /// <summary>
    /// Encrypts a 64-bit integer plaintext into a Paillier ciphertext using the given public key.
    /// </summary>
    EncryptedValue Encrypt(long plaintext, PaillierPublicKey publicKey);

    /// <summary>
    /// Encrypts an arbitrary-precision BigInteger plaintext into a Paillier ciphertext.
    /// </summary>
    EncryptedValue Encrypt(BigInteger plaintext, PaillierPublicKey publicKey);

    /// <summary>
    /// Decrypts a Paillier ciphertext back to a 64-bit signed integer using the private key.
    /// </summary>
    long Decrypt(string ciphertextHex, PaillierPrivateKey privateKey);

    /// <summary>
    /// Decrypts a Paillier ciphertext back to an arbitrary-precision BigInteger using the private key.
    /// </summary>
    BigInteger DecryptBigInteger(string ciphertextHex, PaillierPrivateKey privateKey);

    /// <summary>
    /// Computes the homomorphic sum of two ciphertexts: Decrypt(c1 + c2) = m1 + m2 (mod n).
    /// </summary>
    string AddEncrypted(string ciphertext1Hex, string ciphertext2Hex, PaillierPublicKey publicKey);

    /// <summary>
    /// Computes the homomorphic subtraction of two ciphertexts: Decrypt(c1 - c2) = m1 - m2 (mod n).
    /// </summary>
    string SubtractEncrypted(string ciphertext1Hex, string ciphertext2Hex, PaillierPublicKey publicKey);

    /// <summary>
    /// Computes the homomorphic scalar multiplication of a ciphertext: Decrypt(c * k) = k * m (mod n).
    /// </summary>
    string MultiplyScalar(string ciphertextHex, long scalar, PaillierPublicKey publicKey);

    /// <summary>
    /// Re-randomizes a ciphertext without changing the underlying plaintext, defeating ciphertext linkage attacks.
    /// </summary>
    string RandomizeCiphertext(string ciphertextHex, PaillierPublicKey publicKey);
}
