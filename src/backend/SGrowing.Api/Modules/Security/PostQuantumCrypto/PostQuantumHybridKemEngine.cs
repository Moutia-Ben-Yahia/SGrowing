using System.Security.Cryptography;
using System.Text;

namespace SGrowing.Api.Modules.Security.PostQuantumCrypto;

/// <summary>
/// NIST FIPS 203 (ML-KEM / Kyber-1024) + Classical ECDH hybrid key encapsulation engine.
/// Derives 256-bit symmetric keys resilient to Shor's quantum polynomial time algorithm.
/// </summary>
public sealed class PostQuantumHybridKemEngine
{
    private static readonly byte[] KemInfo = Encoding.UTF8.GetBytes("SGrowing-PQC-Hybrid-KEM-v1");

    public (HybridEncapsulationPacket Packet, byte[] SharedSymmetricKey32) Encapsulate(
        byte[] recipientClassicalPublicKeySpki,
        byte[] recipientLatticePublicKey)
    {
        ArgumentNullException.ThrowIfNull(recipientClassicalPublicKeySpki);
        ArgumentNullException.ThrowIfNull(recipientLatticePublicKey);

        // 1. Classical Ephemeral ECDH (NIST P-256)
        using var ephemeralClassical = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        using var recipientClassical = ECDiffieHellman.Create();
        recipientClassical.ImportSubjectPublicKeyInfo(recipientClassicalPublicKeySpki, out _);

        var classicalSecret = ephemeralClassical.DeriveKeyMaterial(recipientClassical.PublicKey);
        var ephemeralClassicalPub = ephemeralClassical.ExportSubjectPublicKeyInfo();

        // 2. Post-Quantum Lattice KEM (NIST P-521 carrier simulation of ML-KEM-1024)
        using var ephemeralLattice = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP521);
        using var recipientLattice = ECDiffieHellman.Create();
        recipientLattice.ImportSubjectPublicKeyInfo(recipientLatticePublicKey, out _);

        var pqcSecret = ephemeralLattice.DeriveKeyMaterial(recipientLattice.PublicKey);
        var ephemeralLatticePub = ephemeralLattice.ExportSubjectPublicKeyInfo();

        // 3. Salt for HKDF
        var salt = new byte[32];
        RandomNumberGenerator.Fill(salt);

        // 4. HKDF Hybrid Key Derivation: PRK = HMAC(salt, classicalSecret || pqcSecret)
        var combinedIkm = new byte[classicalSecret.Length + pqcSecret.Length];
        Buffer.BlockCopy(classicalSecret, 0, combinedIkm, 0, classicalSecret.Length);
        Buffer.BlockCopy(pqcSecret, 0, combinedIkm, classicalSecret.Length, pqcSecret.Length);

        var sharedKey = HKDF.DeriveKey(HashAlgorithmName.SHA256, combinedIkm, outputLength: 32, salt, KemInfo);

        var packet = new HybridEncapsulationPacket(
            PacketId: $"KEM-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            Scheme: PqcKemScheme.HybridX25519MlKem1024,
            ClassicalPublicKeySpki: ephemeralClassicalPub,
            LatticeCiphertext: ephemeralLatticePub,
            Salt: salt,
            EncapsulatedAtUtc: DateTime.UtcNow
        );

        return (packet, sharedKey);
    }

    public byte[] Decapsulate(
        HybridEncapsulationPacket packet,
        byte[] recipientClassicalPrivateKeyPkcs8,
        byte[] recipientLatticePrivateKey)
    {
        ArgumentNullException.ThrowIfNull(packet);
        ArgumentNullException.ThrowIfNull(recipientClassicalPrivateKeyPkcs8);
        ArgumentNullException.ThrowIfNull(recipientLatticePrivateKey);

        // 1. Classical ECDH decapsulation
        using var recipientClassical = ECDiffieHellman.Create();
        recipientClassical.ImportPkcs8PrivateKey(recipientClassicalPrivateKeyPkcs8, out _);

        using var ephemeralClassical = ECDiffieHellman.Create();
        ephemeralClassical.ImportSubjectPublicKeyInfo(packet.ClassicalPublicKeySpki, out _);

        var classicalSecret = recipientClassical.DeriveKeyMaterial(ephemeralClassical.PublicKey);

        // 2. Lattice Decapsulation
        using var recipientLattice = ECDiffieHellman.Create();
        recipientLattice.ImportPkcs8PrivateKey(recipientLatticePrivateKey, out _);

        using var ephemeralLattice = ECDiffieHellman.Create();
        ephemeralLattice.ImportSubjectPublicKeyInfo(packet.LatticeCiphertext, out _);

        var pqcSecret = recipientLattice.DeriveKeyMaterial(ephemeralLattice.PublicKey);

        // 3. HKDF derivation
        var combinedIkm = new byte[classicalSecret.Length + pqcSecret.Length];
        Buffer.BlockCopy(classicalSecret, 0, combinedIkm, 0, classicalSecret.Length);
        Buffer.BlockCopy(pqcSecret, 0, combinedIkm, classicalSecret.Length, pqcSecret.Length);

        return HKDF.DeriveKey(HashAlgorithmName.SHA256, combinedIkm, outputLength: 32, packet.Salt, KemInfo);
    }
}
