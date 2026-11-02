using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SGrowing.Api.Modules.Security.PostQuantumCrypto;
using Xunit;

namespace SGrowing.Tests.Security;

public sealed class PostQuantumCryptoTests
{
    [Fact]
    public void HybridKemEngine_EncapsulateAndDecapsulate_DeriveExactSameSymmetricKey()
    {
        // Arrange
        using var recipientClassicalEcdh = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        using var recipientLatticeEcdh = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP521);

        var recipientClassicalPub = recipientClassicalEcdh.ExportSubjectPublicKeyInfo();
        var recipientClassicalPriv = recipientClassicalEcdh.ExportPkcs8PrivateKey();

        var recipientLatticePub = recipientLatticeEcdh.ExportSubjectPublicKeyInfo();
        var recipientLatticePriv = recipientLatticeEcdh.ExportPkcs8PrivateKey();

        var engine = new PostQuantumHybridKemEngine();

        // Act
        var (packet, senderSharedKey) = engine.Encapsulate(recipientClassicalPub, recipientLatticePub);
        var recipientSharedKey = engine.Decapsulate(packet, recipientClassicalPriv, recipientLatticePriv);

        // Assert
        packet.Should().NotBeNull();
        packet.PacketId.Should().StartWith("KEM-");
        packet.Scheme.Should().Be(PqcKemScheme.HybridX25519MlKem1024);
        senderSharedKey.Should().HaveCount(32);
        recipientSharedKey.Should().Equal(senderSharedKey);
    }

    [Fact]
    public void EnvelopeSealer_SealAndOpen_ReconstructsPlaintextAndValidatesHybridSignatures()
    {
        // Arrange
        using var recipientClassicalEcdh = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        using var recipientLatticeEcdh = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP521);

        var recipientClassicalPub = recipientClassicalEcdh.ExportSubjectPublicKeyInfo();
        var recipientClassicalPriv = recipientClassicalEcdh.ExportPkcs8PrivateKey();
        var recipientLatticePub = recipientLatticeEcdh.ExportSubjectPublicKeyInfo();
        var recipientLatticePriv = recipientLatticeEcdh.ExportPkcs8PrivateKey();

        using var senderClassicalEcdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var senderLatticeEcdsa = ECDsa.Create(ECCurve.NamedCurves.nistP521);

        var senderClassicalPub = senderClassicalEcdsa.ExportSubjectPublicKeyInfo();
        var senderClassicalPriv = senderClassicalEcdsa.ExportPkcs8PrivateKey();
        var senderLatticePub = senderLatticeEcdsa.ExportSubjectPublicKeyInfo();
        var senderLatticePriv = senderLatticeEcdsa.ExportPkcs8PrivateKey();

        var kemEngine = new PostQuantumHybridKemEngine();
        var sealer = new PostQuantumEnvelopeSealerService(kemEngine);

        var originalText = "TOP_SECRET_DEFENSE_SATELLITE_TELEMETRY_PAYLOAD_2026";
        var originalPlaintext = Encoding.UTF8.GetBytes(originalText);

        var sealRequest = new SealPayloadRequest(
            Plaintext: originalPlaintext,
            RecipientClassicalPublicKeySpki: recipientClassicalPub,
            RecipientLatticePublicKey: recipientLatticePub,
            SenderClassicalPrivateKeyPkcs8: senderClassicalPriv,
            SenderLatticePrivateKey: senderLatticePriv
        );

        // Act
        var envelope = sealer.SealPayload(sealRequest);

        var openRequest = new OpenEnvelopeRequest(
            Envelope: envelope,
            RecipientClassicalPrivateKeyPkcs8: recipientClassicalPriv,
            RecipientLatticePrivateKey: recipientLatticePriv,
            SenderClassicalPublicKeySpki: senderClassicalPub,
            SenderLatticePublicKey: senderLatticePub
        );

        var (recoveredPlaintext, verification) = sealer.OpenEnvelope(openRequest);

        // Assert
        envelope.Should().NotBeNull();
        envelope.EnvelopeId.Should().StartWith("PQC-");
        envelope.Ciphertext.Should().NotEqual(originalPlaintext);

        recoveredPlaintext.Should().Equal(originalPlaintext);
        Encoding.UTF8.GetString(recoveredPlaintext).Should().Be(originalText);

        verification.IsClassicalSignatureValid.Should().BeTrue();
        verification.IsPostQuantumSignatureValid.Should().BeTrue();
        verification.IsHybridSecuritySatisfied.Should().BeTrue();
        verification.CryptographicAuditNote.Should().Contain("HYBRID IMMUNITY CONFIRMED");
    }

    [Fact]
    public void EnvelopeSealer_TamperedCiphertext_FailsValidationOrThrows()
    {
        // Arrange
        using var recipientClassicalEcdh = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        using var recipientLatticeEcdh = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP521);

        var recipientClassicalPub = recipientClassicalEcdh.ExportSubjectPublicKeyInfo();
        var recipientClassicalPriv = recipientClassicalEcdh.ExportPkcs8PrivateKey();
        var recipientLatticePub = recipientLatticeEcdh.ExportSubjectPublicKeyInfo();
        var recipientLatticePriv = recipientLatticeEcdh.ExportPkcs8PrivateKey();

        using var senderClassicalEcdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var senderLatticeEcdsa = ECDsa.Create(ECCurve.NamedCurves.nistP521);

        var senderClassicalPub = senderClassicalEcdsa.ExportSubjectPublicKeyInfo();
        var senderClassicalPriv = senderClassicalEcdsa.ExportPkcs8PrivateKey();
        var senderLatticePub = senderLatticeEcdsa.ExportSubjectPublicKeyInfo();
        var senderLatticePriv = senderLatticeEcdsa.ExportPkcs8PrivateKey();

        var kemEngine = new PostQuantumHybridKemEngine();
        var sealer = new PostQuantumEnvelopeSealerService(kemEngine);

        var originalPlaintext = Encoding.UTF8.GetBytes("AUTHENTIC_MILITARY_FLIGHT_PLAN");
        var envelope = sealer.SealPayload(new SealPayloadRequest(
            originalPlaintext,
            recipientClassicalPub,
            recipientLatticePub,
            senderClassicalPriv,
            senderLatticePriv
        ));

        // Tamper with ciphertext byte
        var tamperedCiphertext = (byte[])envelope.Ciphertext.Clone();
        tamperedCiphertext[0] ^= 0xFF;

        var tamperedEnvelope = envelope with { Ciphertext = tamperedCiphertext };

        var openRequest = new OpenEnvelopeRequest(
            Envelope: tamperedEnvelope,
            RecipientClassicalPrivateKeyPkcs8: recipientClassicalPriv,
            RecipientLatticePrivateKey: recipientLatticePriv,
            SenderClassicalPublicKeySpki: senderClassicalPub,
            SenderLatticePublicKey: senderLatticePub
        );

        // Act & Assert (Either CryptographicException from AES-GCM or invalid signature)
        var act = () => sealer.OpenEnvelope(openRequest);
        act.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void AddPostQuantumCryptoServices_RegistersDependenciesCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddPostQuantumCryptoServices();

        // Assert - Rule: NEVER call services.BuildServiceProvider()
        services.Should().ContainSingle(s => s.ServiceType == typeof(PostQuantumHybridKemEngine) && s.Lifetime == ServiceLifetime.Singleton);
        services.Should().ContainSingle(s => s.ServiceType == typeof(PostQuantumEnvelopeSealerService) && s.Lifetime == ServiceLifetime.Singleton);
    }
}
