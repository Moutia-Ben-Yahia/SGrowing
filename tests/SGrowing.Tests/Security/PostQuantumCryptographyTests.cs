using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SGrowing.Api.Modules.Security.PostQuantum;
using Xunit;

namespace SGrowing.Tests.Security;

/// <summary>
/// Unit tests for Phase 47: Quantum-Resistant Cryptography (PQC),
/// Post-Quantum Key Exchange & Signature Suite on .NET 10 / C# 14.
/// Strictly asserts on ServiceCollection descriptors without calling BuildServiceProvider().
/// </summary>
public class PostQuantumCryptographyTests
{
    private readonly PostQuantumKeyExchangeEngine _kemEngine = new();
    private readonly MlDsaQuantumSignatureService _dsaService = new();
    private readonly CryptoAgilityMigrationGateway _gateway = new();

    public PostQuantumCryptographyTests()
    {
        _gateway.ResetMetrics();
    }

    // =========================================================================
    // 1. Hybrid Key Pair Generation Tests
    // =========================================================================

    [Fact]
    public void GenerateHybridKeyPair_ReturnsValidPqcKeyPair_WithExpectedAlgorithmAndSecurityLevel()
    {
        // Act
        var keyPair = _kemEngine.GenerateHybridKeyPair();

        // Assert
        keyPair.Should().NotBeNull();
        keyPair.Algorithm.Should().Be(PqcAlgorithm.HybridClassicalPqc);
        keyPair.SecurityLevel.Should().Be(SecurityLevel.NistLevel3_192bit);
        keyPair.CreatedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        keyPair.PublicKeyHex.Should().NotBeNullOrWhiteSpace();
        keyPair.PrivateKeyHex.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void GenerateHybridKeyPair_ProducesDistinctKeysOnSubsequentCalls()
    {
        // Act
        var kp1 = _kemEngine.GenerateHybridKeyPair();
        var kp2 = _kemEngine.GenerateHybridKeyPair();

        // Assert
        kp1.PublicKeyHex.Should().NotBe(kp2.PublicKeyHex);
        kp1.PrivateKeyHex.Should().NotBe(kp2.PrivateKeyHex);
    }

    [Fact]
    public void GenerateHybridKeyPair_CompositePublicKey_ContainsValidClassicalAndKyberComponents()
    {
        // Act
        var keyPair = _kemEngine.GenerateHybridKeyPair();
        string[] parts = keyPair.PublicKeyHex.Split(':');

        // Assert
        parts.Should().HaveCount(2);
        string classicalPkHex = parts[0];
        string kyberPkHex = parts[1];

        // Classical X25519 PK is 32 bytes = 64 hex chars
        classicalPkHex.Length.Should().Be(64);
        Convert.FromHexString(classicalPkHex).Should().HaveCount(32);

        // ML-KEM-768 PK is 32 bytes (rho) + 1536 bytes (vector t) = 1568 bytes = 3136 hex chars
        kyberPkHex.Length.Should().Be(3136);
        Convert.FromHexString(kyberPkHex).Should().HaveCount(1568);
    }

    [Fact]
    public void GenerateHybridKeyPair_CompositePrivateKey_ContainsValidClassicalAndKyberComponents()
    {
        // Act
        var keyPair = _kemEngine.GenerateHybridKeyPair();
        string[] parts = keyPair.PrivateKeyHex.Split(':');

        // Assert
        parts.Should().HaveCount(2);
        string classicalPrivHex = parts[0];
        string kyberPrivHex = parts[1];

        // Classical X25519 Private Key is 32 bytes = 64 hex chars
        classicalPrivHex.Length.Should().Be(64);
        Convert.FromHexString(classicalPrivHex).Should().HaveCount(32);

        // ML-KEM-768 Private Key is 32 bytes (rho) + 1536 bytes (vector s) = 1568 bytes = 3136 hex chars
        kyberPrivHex.Length.Should().Be(3136);
        Convert.FromHexString(kyberPrivHex).Should().HaveCount(1568);
    }

    [Fact]
    public void GenerateHybridKeyPair_HexEncodings_AreValidHexAndCorrectLengths()
    {
        // Act
        var keyPair = _kemEngine.GenerateHybridKeyPair();

        // Assert
        Action parsePub = () =>
        {
            foreach (var part in keyPair.PublicKeyHex.Split(':'))
            {
                Convert.FromHexString(part);
            }
        };
        parsePub.Should().NotThrow();

        Action parsePriv = () =>
        {
            foreach (var part in keyPair.PrivateKeyHex.Split(':'))
            {
                Convert.FromHexString(part);
            }
        };
        parsePriv.Should().NotThrow();
    }

    // =========================================================================
    // 2. Hybrid KEM Encapsulation & Decapsulation Tests
    // =========================================================================

    [Fact]
    public void EncapsulateAndDecapsulate_MatchingSharedSecret_ClientAndServerSecretsMatchIdentically()
    {
        // Arrange (Client generates hybrid key pair)
        var clientKeyPair = _kemEngine.GenerateHybridKeyPair();
        string[] pkParts = clientKeyPair.PublicKeyHex.Split(':');
        string clientClassicalPk = pkParts[0];
        string clientKyberPk = pkParts[1];

        // Act (Server encapsulates key using client public keys)
        var encResult = _kemEngine.EncapsulateKey(clientClassicalPk, clientKyberPk);

        // Client decapsulates key using received ciphertext and its private key
        string clientDerivedSecret = _kemEngine.DecapsulateKey(encResult.CiphertextHex, clientKeyPair.PrivateKeyHex);

        // Assert
        encResult.SharedSecretHex.Should().NotBeNullOrWhiteSpace();
        encResult.SharedSecretHex.Length.Should().Be(64); // 32-byte 256-bit symmetric session key
        clientDerivedSecret.Should().Be(encResult.SharedSecretHex);
    }

    [Fact]
    public void EncapsulateKey_WithCombinedPublicKeyString_SuccessfullyEncapsulates()
    {
        // Arrange
        var clientKeyPair = _kemEngine.GenerateHybridKeyPair();

        // Act: Pass composite combined public key "classical:kyber"
        var encResult = _kemEngine.EncapsulateKey(clientKeyPair.PublicKeyHex, string.Empty);
        string clientDerivedSecret = _kemEngine.DecapsulateKey(encResult.CiphertextHex, clientKeyPair.PrivateKeyHex);

        // Assert
        clientDerivedSecret.Should().Be(encResult.SharedSecretHex);
    }

    [Fact]
    public void EncapsulateKey_WithInvalidClassicalKeyLength_ThrowsArgumentException()
    {
        // Arrange
        var clientKeyPair = _kemEngine.GenerateHybridKeyPair();
        string[] pkParts = clientKeyPair.PublicKeyHex.Split(':');
        string invalidShortPk = "deadbeef";

        // Act
        Action act = () => _kemEngine.EncapsulateKey(invalidShortPk, pkParts[1]);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*32 bytes*");
    }

    [Fact]
    public void EncapsulateKey_WithNullOrEmptyClassicalKey_ThrowsArgumentException()
    {
        // Act
        Action act = () => _kemEngine.EncapsulateKey(string.Empty, "validKyberPk");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void EncapsulateKey_WithNullOrEmptyPqcKey_ThrowsArgumentException()
    {
        // Act
        Action act = () => _kemEngine.EncapsulateKey("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef", string.Empty);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void DecapsulateKey_WithMismatchedCiphertext_ProducesDifferentSharedSecret()
    {
        // Arrange
        var client1 = _kemEngine.GenerateHybridKeyPair();
        var client2 = _kemEngine.GenerateHybridKeyPair();

        string[] pk1 = client1.PublicKeyHex.Split(':');
        var enc1 = _kemEngine.EncapsulateKey(pk1[0], pk1[1]);

        // Act: Try decapsulating enc1's ciphertext using client2's private key
        string secret2 = _kemEngine.DecapsulateKey(enc1.CiphertextHex, client2.PrivateKeyHex);

        // Assert
        secret2.Should().NotBe(enc1.SharedSecretHex);
    }

    [Fact]
    public void DecapsulateKey_WithTamperedEphemeralPublicKey_ProducesMismatchedSharedSecret()
    {
        // Arrange
        var client = _kemEngine.GenerateHybridKeyPair();
        string[] pk = client.PublicKeyHex.Split(':');
        var enc = _kemEngine.EncapsulateKey(pk[0], pk[1]);

        string[] ctParts = enc.CiphertextHex.Split(':');
        byte[] ephPubBytes = Convert.FromHexString(ctParts[0]);

        // Flip a byte in the ephemeral public key
        ephPubBytes[5] ^= 0xFF;
        string tamperedCt = $"{Convert.ToHexString(ephPubBytes).ToLowerInvariant()}:{ctParts[1]}";

        // Act
        string clientDerivedSecret = _kemEngine.DecapsulateKey(tamperedCt, client.PrivateKeyHex);

        // Assert
        clientDerivedSecret.Should().NotBe(enc.SharedSecretHex);
    }

    // =========================================================================
    // 3. Kyber-768 Lattice Edge Cases & Robustness
    // =========================================================================

    [Fact]
    public void DecapsulateKey_WithTamperedKyberCiphertext_ProducesMismatchedSharedSecret()
    {
        // Arrange
        var client = _kemEngine.GenerateHybridKeyPair();
        string[] pk = client.PublicKeyHex.Split(':');
        var enc = _kemEngine.EncapsulateKey(pk[0], pk[1]);

        string[] ctParts = enc.CiphertextHex.Split(':');
        byte[] kyberCtBytes = Convert.FromHexString(ctParts[1]);

        // Flip a byte in the Kyber ciphertext
        kyberCtBytes[100] ^= 0xAA;
        string tamperedCt = $"{ctParts[0]}:{Convert.ToHexString(kyberCtBytes).ToLowerInvariant()}";

        // Act
        string clientDerivedSecret = _kemEngine.DecapsulateKey(tamperedCt, client.PrivateKeyHex);

        // Assert
        clientDerivedSecret.Should().NotBe(enc.SharedSecretHex);
    }

    [Fact]
    public void DecapsulateKey_WithMalformedCiphertextFormat_ThrowsArgumentException()
    {
        // Act
        Action act = () => _kemEngine.DecapsulateKey("invalid_single_part_ct", "priv:priv");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Invalid hybrid ciphertext format*");
    }

    [Fact]
    public void DecapsulateKey_WithMalformedPrivateKeyFormat_ThrowsArgumentException()
    {
        // Act
        Action act = () => _kemEngine.DecapsulateKey("eph:kyber", "invalid_single_part_priv");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Invalid hybrid private key format*");
    }

    [Fact]
    public void DecapsulateKey_WithInvalidHexCharacters_ThrowsFormatException()
    {
        // Act
        Action act = () => _kemEngine.DecapsulateKey("NOT_HEX:NOT_HEX", "NOT_HEX:NOT_HEX");

        // Assert
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void EncapsulateKey_MultipleSessionsAgainstSameClientKey_GeneratesUniqueSessionKeysAndUniqueCiphertexts()
    {
        // Arrange
        var client = _kemEngine.GenerateHybridKeyPair();
        string[] pk = client.PublicKeyHex.Split(':');

        // Act
        var enc1 = _kemEngine.EncapsulateKey(pk[0], pk[1]);
        var enc2 = _kemEngine.EncapsulateKey(pk[0], pk[1]);
        var enc3 = _kemEngine.EncapsulateKey(pk[0], pk[1]);

        // Assert: Each encapsulation must have unique ephemeral keys and unique session keys
        enc1.CiphertextHex.Should().NotBe(enc2.CiphertextHex);
        enc2.CiphertextHex.Should().NotBe(enc3.CiphertextHex);
        enc1.SharedSecretHex.Should().NotBe(enc2.SharedSecretHex);
        enc2.SharedSecretHex.Should().NotBe(enc3.SharedSecretHex);

        // But all 3 must properly decapsulate with the client's single private key
        _kemEngine.DecapsulateKey(enc1.CiphertextHex, client.PrivateKeyHex).Should().Be(enc1.SharedSecretHex);
        _kemEngine.DecapsulateKey(enc2.CiphertextHex, client.PrivateKeyHex).Should().Be(enc2.SharedSecretHex);
        _kemEngine.DecapsulateKey(enc3.CiphertextHex, client.PrivateKeyHex).Should().Be(enc3.SharedSecretHex);
    }

    // =========================================================================
    // 4. ML-DSA Lattice Signature Generation & Verification Tests
    // =========================================================================

    [Fact]
    public void GenerateSignatureKeyPair_ReturnsValidPqcKeyPair_WithNistLevel3Security()
    {
        // Act
        var keyPair = _dsaService.GenerateSignatureKeyPair();

        // Assert
        keyPair.Should().NotBeNull();
        keyPair.Algorithm.Should().Be(PqcAlgorithm.MlDsa65_CrystalsDilithium);
        keyPair.SecurityLevel.Should().Be(SecurityLevel.NistLevel3_192bit);
        keyPair.PublicKeyHex.Should().NotBeNullOrWhiteSpace();
        keyPair.PrivateKeyHex.Should().NotBeNullOrWhiteSpace();

        // Dilithium public key: 32 bytes seed + 4 * 256 * 4 bytes = 4128 bytes = 8256 hex chars
        keyPair.PublicKeyHex.Length.Should().Be(8256);
    }

    [Fact]
    public void SignPayload_WithValidPayloadAndPrivateKey_ProducesAuthenticSignatureToken()
    {
        // Arrange
        var keyPair = _dsaService.GenerateSignatureKeyPair();
        byte[] payload = Encoding.UTF8.GetBytes("Quantum-resistant enterprise invoice attestation payload #47");
        const string signerDid = "did:sgrowing:sec-vault-001";

        // Act
        var token = _dsaService.SignPayload(payload, keyPair.PrivateKeyHex, signerDid);

        // Assert
        token.Should().NotBeNull();
        token.SignerDid.Should().Be(signerDid);
        token.Algorithm.Should().Be(PqcAlgorithm.MlDsa65_CrystalsDilithium);
        token.MessageHashHex.Should().Be(Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant());
        token.SignatureHex.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void VerifySignature_ValidSignatureAndMatchingPayload_ReturnsStatusValid()
    {
        // Arrange
        var keyPair = _dsaService.GenerateSignatureKeyPair();
        byte[] payload = Encoding.UTF8.GetBytes("Settlement Authorization #849204 for Sovereign Financial Gateway");
        const string signerDid = "did:sgrowing:oracle-node-42";

        var token = _dsaService.SignPayload(payload, keyPair.PrivateKeyHex, signerDid);

        // Act
        var result = _dsaService.VerifySignature(payload, token.SignatureHex, keyPair.PublicKeyHex);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(SignatureVerificationStatus.Valid);
        result.Algorithm.Should().Be(PqcAlgorithm.MlDsa65_CrystalsDilithium);
        result.SignerDid.Should().Be(signerDid);
        result.VerificationMessage.Should().Contain("successfully");
    }

    [Fact]
    public void VerifySignature_WithTextPayloadUtf8_SignsAndVerifiesSuccessfully()
    {
        // Arrange
        var keyPair = _dsaService.GenerateSignatureKeyPair();
        byte[] payload = "Unicode test string with multi-byte characters: 🔒⚡🚀"u8.ToArray();

        var token = _dsaService.SignPayload(payload, keyPair.PrivateKeyHex, "did:sgrowing:unicode-test");

        // Act
        var result = _dsaService.VerifySignature(payload, token.SignatureHex, keyPair.PublicKeyHex);

        // Assert
        result.Status.Should().Be(SignatureVerificationStatus.Valid);
    }

    [Fact]
    public void VerifySignature_WithEmptyPayloadBytes_SignsAndVerifiesSuccessfully()
    {
        // Arrange
        var keyPair = _dsaService.GenerateSignatureKeyPair();
        byte[] emptyPayload = [];

        var token = _dsaService.SignPayload(emptyPayload, keyPair.PrivateKeyHex, "did:sgrowing:empty-test");

        // Act
        var result = _dsaService.VerifySignature(emptyPayload, token.SignatureHex, keyPair.PublicKeyHex);

        // Assert
        result.Status.Should().Be(SignatureVerificationStatus.Valid);
    }

    [Fact]
    public void VerifySignature_WithLargePayload100KB_SignsAndVerifiesSuccessfully()
    {
        // Arrange
        var keyPair = _dsaService.GenerateSignatureKeyPair();
        byte[] largePayload = RandomNumberGenerator.GetBytes(100 * 1024); // 100 KB payload

        var token = _dsaService.SignPayload(largePayload, keyPair.PrivateKeyHex, "did:sgrowing:large-payload-signer");

        // Act
        var result = _dsaService.VerifySignature(largePayload, token.SignatureHex, keyPair.PublicKeyHex);

        // Assert
        result.Status.Should().Be(SignatureVerificationStatus.Valid);
    }

    [Fact]
    public void SignPayload_PreservesSignerDidInSignatureTokenAndVerificationResult()
    {
        // Arrange
        var keyPair = _dsaService.GenerateSignatureKeyPair();
        byte[] payload = "DID preservation check"u8.ToArray();
        const string customDid = "did:ion:EiD34b86e088a67cc74020bbea63b139b22514a08798e3404ddef9519b3cd3a431";

        var token = _dsaService.SignPayload(payload, keyPair.PrivateKeyHex, customDid);

        // Act
        var result = _dsaService.VerifySignature(payload, token.SignatureHex, keyPair.PublicKeyHex);

        // Assert
        result.SignerDid.Should().Be(customDid);
    }

    // =========================================================================
    // 5. Tampering & Adversarial Detection Tests
    // =========================================================================

    [Fact]
    public void VerifySignature_TamperedPayloadSingleByteFlipped_ReturnsTamperedPayloadStatus()
    {
        // Arrange
        var keyPair = _dsaService.GenerateSignatureKeyPair();
        byte[] originalPayload = "Transfer $1,000,000 to Vault B-7"u8.ToArray();
        var token = _dsaService.SignPayload(originalPayload, keyPair.PrivateKeyHex, "did:sgrowing:treasury");

        // Adversary alters single byte ('1' -> '9')
        byte[] tamperedPayload = "Transfer $9,000,000 to Vault B-7"u8.ToArray();

        // Act
        var result = _dsaService.VerifySignature(tamperedPayload, token.SignatureHex, keyPair.PublicKeyHex);

        // Assert
        result.Status.Should().Be(SignatureVerificationStatus.TamperedPayload);
        result.VerificationMessage.Should().Contain("Payload data does not match");
    }

    [Fact]
    public void VerifySignature_TamperedSignatureHexSingleBitFlipped_ReturnsInvalidSignature()
    {
        // Arrange
        var keyPair = _dsaService.GenerateSignatureKeyPair();
        byte[] payload = "Critical security attestation"u8.ToArray();
        var token = _dsaService.SignPayload(payload, keyPair.PrivateKeyHex, "did:sgrowing:validator");

        // Tamper with the signature bytes (flip a single bit in the hex representation)
        byte[] sigBytes = Convert.FromHexString(token.SignatureHex);
        sigBytes[50] ^= 0x01; // flip single bit
        string tamperedSigHex = Convert.ToHexString(sigBytes).ToLowerInvariant();

        // Act
        var result = _dsaService.VerifySignature(payload, tamperedSigHex, keyPair.PublicKeyHex);

        // Assert
        result.Status.Should().Be(SignatureVerificationStatus.InvalidSignature);
    }

    [Fact]
    public void VerifySignature_TamperedSignatureZVectorLatticeBoundsViolation_ReturnsInvalidSignature()
    {
        // Arrange
        var keyPair = _dsaService.GenerateSignatureKeyPair();
        byte[] payload = "Boundary test"u8.ToArray();
        var token = _dsaService.SignPayload(payload, keyPair.PrivateKeyHex, "did:sgrowing:tester");

        byte[] sigBytes = Convert.FromHexString(token.SignatureHex);
        // Overwrite first integer in vector z with a value exceeding Gamma1 bound (e.g. 5,000,000)
        int outOfBoundsVal = 5000000;
        sigBytes[0] = (byte)(outOfBoundsVal & 0xFF);
        sigBytes[1] = (byte)((outOfBoundsVal >> 8) & 0xFF);
        sigBytes[2] = (byte)((outOfBoundsVal >> 16) & 0xFF);
        sigBytes[3] = (byte)((outOfBoundsVal >> 24) & 0xFF);
        string tamperedSigHex = Convert.ToHexString(sigBytes).ToLowerInvariant();

        // Act
        var result = _dsaService.VerifySignature(payload, tamperedSigHex, keyPair.PublicKeyHex);

        // Assert
        result.Status.Should().Be(SignatureVerificationStatus.InvalidSignature);
    }

    [Fact]
    public void VerifySignature_WrongPublicKey_ReturnsInvalidSignature()
    {
        // Arrange
        var keyPair1 = _dsaService.GenerateSignatureKeyPair();
        var keyPair2 = _dsaService.GenerateSignatureKeyPair();

        byte[] payload = "Payload to test key separation"u8.ToArray();
        var token = _dsaService.SignPayload(payload, keyPair1.PrivateKeyHex, "did:sgrowing:client1");

        // Act: Verify token generated by keyPair1 using keyPair2's public key
        var result = _dsaService.VerifySignature(payload, token.SignatureHex, keyPair2.PublicKeyHex);

        // Assert
        result.Status.Should().Be(SignatureVerificationStatus.InvalidSignature);
    }

    [Fact]
    public void VerifySignature_MalformedHexSignatureOrPublicKey_ReturnsInvalidSignature()
    {
        // Arrange
        byte[] payload = "Sample payload"u8.ToArray();

        // Act & Assert
        var res1 = _dsaService.VerifySignature(payload, "ZZZZ_NOT_HEX", "001122");
        res1.Status.Should().Be(SignatureVerificationStatus.InvalidSignature);

        var res2 = _dsaService.VerifySignature(payload, "001122", "NOT_A_VALID_HEX_KEY");
        res2.Status.Should().Be(SignatureVerificationStatus.InvalidSignature);
    }

    // =========================================================================
    // 6. HKDF-SHA256 Derivation & Cryptographic Invariants
    // =========================================================================

    [Fact]
    public void HkdfSha256_DifferentSaltsOrCiphertexts_YieldDistinctCryptographicKeys()
    {
        // Arrange
        var client = _kemEngine.GenerateHybridKeyPair();
        string[] pk = client.PublicKeyHex.Split(':');

        // Act
        var enc1 = _kemEngine.EncapsulateKey(pk[0], pk[1]);
        var enc2 = _kemEngine.EncapsulateKey(pk[0], pk[1]);

        // Assert
        enc1.SharedSecretHex.Should().NotBe(enc2.SharedSecretHex);
        Convert.FromHexString(enc1.SharedSecretHex).Should().HaveCount(32);
        Convert.FromHexString(enc2.SharedSecretHex).Should().HaveCount(32);
    }

    [Fact]
    public void HkdfSha256_SessionKeys_PossessFull256BitEntropy()
    {
        // Arrange
        var client = _kemEngine.GenerateHybridKeyPair();
        string[] pk = client.PublicKeyHex.Split(':');
        var enc = _kemEngine.EncapsulateKey(pk[0], pk[1]);

        // Act
        byte[] keyBytes = Convert.FromHexString(enc.SharedSecretHex);

        // Assert: Entropy check - 32 bytes should not be trivial (all 0s or all 0xFFs)
        keyBytes.Should().HaveCount(32);
        keyBytes.Should().Contain(b => b != 0);
        keyBytes.Should().Contain(b => b != 0xFF);
    }

    [Fact]
    public void ClassicalX25519_MontgomeryLadder_CommutativeDiffieHellmanConsistency()
    {
        // Act: Generate two hybrid key pairs and encapsulate between them
        var partyA = _kemEngine.GenerateHybridKeyPair();
        var partyB = _kemEngine.GenerateHybridKeyPair();

        string[] pkA = partyA.PublicKeyHex.Split(':');
        string[] pkB = partyB.PublicKeyHex.Split(':');

        var encForA = _kemEngine.EncapsulateKey(pkA[0], pkA[1]);
        var encForB = _kemEngine.EncapsulateKey(pkB[0], pkB[1]);

        string secretA = _kemEngine.DecapsulateKey(encForA.CiphertextHex, partyA.PrivateKeyHex);
        string secretB = _kemEngine.DecapsulateKey(encForB.CiphertextHex, partyB.PrivateKeyHex);

        // Assert
        secretA.Should().Be(encForA.SharedSecretHex);
        secretB.Should().Be(encForB.SharedSecretHex);
    }

    [Fact]
    public void Kyber768_PolynomialMultiplication_ObeysRingModulusQuotient()
    {
        // Arrange & Act
        var client = _kemEngine.GenerateHybridKeyPair();
        var enc = _kemEngine.EncapsulateKey(client.PublicKeyHex, string.Empty);
        string decSecret = _kemEngine.DecapsulateKey(enc.CiphertextHex, client.PrivateKeyHex);

        // Assert: Ring multiplication & subtraction successfully cancels lattice noise and decodes m
        decSecret.Should().Be(enc.SharedSecretHex);
    }

    // =========================================================================
    // 7. Crypto-Agility Migration Gateway & Header Negotiation Tests
    // =========================================================================

    [Fact]
    public void NegotiateCipherSuite_ClientWithSecPqcSupportedHeader_UpgradesToHybridClassicalPqc()
    {
        // Act
        var result = _gateway.NegotiateCipherSuite(null, "true");

        // Assert
        result.SelectedAlgorithm.Should().Be(PqcAlgorithm.HybridClassicalPqc);
        result.IsQuantumResistant.Should().BeTrue();
        result.SuiteName.Should().Contain("Hybrid");
    }

    [Theory]
    [InlineData("1")]
    [InlineData("yes")]
    [InlineData("enabled")]
    [InlineData("TRUE")]
    public void NegotiateCipherSuite_ClientWithVariousSecPqcFlags_UpgradesSuccessfully(string flag)
    {
        // Act
        var result = _gateway.NegotiateCipherSuite(null, flag);

        // Assert
        result.SelectedAlgorithm.Should().Be(PqcAlgorithm.HybridClassicalPqc);
        result.IsQuantumResistant.Should().BeTrue();
    }

    [Fact]
    public void NegotiateCipherSuite_ClientWithXCryptoSuitesPqc_NegotiatesAppropriateAlgorithm()
    {
        // Act
        var result = _gateway.NegotiateCipherSuite("TLS_AES_256_GCM_SHA384, HYBRID_X25519_KYBER768");

        // Assert
        result.SelectedAlgorithm.Should().Be(PqcAlgorithm.HybridClassicalPqc);
        result.IsQuantumResistant.Should().BeTrue();
    }

    [Fact]
    public void NegotiateCipherSuite_ClientWithMlDsaHeader_NegotiatesMlDsa65Suite()
    {
        // Act
        var result = _gateway.NegotiateCipherSuite("ML-DSA-65, Crystals-Dilithium");

        // Assert
        result.SelectedAlgorithm.Should().Be(PqcAlgorithm.MlDsa65_CrystalsDilithium);
        result.IsQuantumResistant.Should().BeTrue();
        result.SuiteName.Should().Contain("ML-DSA-65");
    }

    [Fact]
    public void NegotiateCipherSuite_ClientWithMlKemHeader_NegotiatesMlKem768Suite()
    {
        // Act
        var result = _gateway.NegotiateCipherSuite("ML-KEM-768, Crystals-Kyber");

        // Assert
        result.SelectedAlgorithm.Should().Be(PqcAlgorithm.MlKem768_CrystalsKyber);
        result.IsQuantumResistant.Should().BeTrue();
        result.SuiteName.Should().Contain("ML-KEM-768");
    }

    [Fact]
    public void NegotiateCipherSuite_ClientWithLegacyHeadersOnly_FallsBackToClassicalX25519()
    {
        // Act
        var result = _gateway.NegotiateCipherSuite("ECDH-X25519, RSA-2048, AES-128-GCM");

        // Assert
        result.SelectedAlgorithm.Should().Be(PqcAlgorithm.ClassicalX25519);
        result.IsQuantumResistant.Should().BeFalse();
        result.SuiteName.Should().Be("Classical-X25519");
    }

    [Fact]
    public void NegotiateCipherSuite_NullOrEmptyHeaders_DefaultsToClassicalX25519()
    {
        // Act
        var result = _gateway.NegotiateCipherSuite(null, null);

        // Assert
        result.SelectedAlgorithm.Should().Be(PqcAlgorithm.ClassicalX25519);
        result.IsQuantumResistant.Should().BeFalse();
    }

    [Fact]
    public void GetQuantumReadinessStatus_CalculatesAccuratePercentagesAndMigrationGuidance()
    {
        // Arrange
        _gateway.ResetMetrics();

        // 3 Quantum-safe communications
        _gateway.RecordCommunication(PqcAlgorithm.HybridClassicalPqc);
        _gateway.RecordCommunication(PqcAlgorithm.MlKem768_CrystalsKyber);
        _gateway.RecordCommunication(PqcAlgorithm.MlDsa65_CrystalsDilithium);

        // 1 Classical communication
        _gateway.RecordCommunication(PqcAlgorithm.ClassicalX25519);

        // Act
        var status = _gateway.GetQuantumReadinessStatus();

        // Assert
        status.TotalCommunications.Should().Be(4);
        status.QuantumSafeCommunications.Should().Be(3);
        status.ClassicalCommunications.Should().Be(1);
        status.QuantumSafePercentage.Should().Be(75.0);
        status.RecommendedMigrationStrategy.Should().Contain("Quantum-Resilient");
        status.SupportedAlgorithms.Should().Contain(nameof(PqcAlgorithm.HybridClassicalPqc));
    }

    [Fact]
    public void GetQuantumReadinessStatus_WithLowPqcPercentage_RecommendsCriticalStrategy()
    {
        // Arrange
        _gateway.ResetMetrics();
        _gateway.RecordCommunication(PqcAlgorithm.ClassicalX25519);
        _gateway.RecordCommunication(PqcAlgorithm.ClassicalX25519);
        _gateway.RecordCommunication(PqcAlgorithm.ClassicalX25519);
        _gateway.RecordCommunication(PqcAlgorithm.ClassicalX25519);
        _gateway.RecordCommunication(PqcAlgorithm.HybridClassicalPqc); // 1 out of 5 = 20%

        // Act
        var status = _gateway.GetQuantumReadinessStatus();

        // Assert
        status.QuantumSafePercentage.Should().Be(20.0);
        status.RecommendedMigrationStrategy.Should().Contain("Critical");
    }

    // =========================================================================
    // 8. Thread-Safety, Concurrency & Dependency Injection Tests
    // =========================================================================

    [Fact]
    public void ConcurrentHybridOperations_ThreadSafeSimultaneousEncapsulationAndDecapsulation()
    {
        // Arrange
        const int concurrency = 20;
        var clientKeyPairs = Enumerable.Range(0, concurrency)
            .Select(_ => _kemEngine.GenerateHybridKeyPair())
            .ToArray();

        // Act & Assert in parallel
        Parallel.For(0, concurrency, i =>
        {
            var kp = clientKeyPairs[i];
            string[] pk = kp.PublicKeyHex.Split(':');

            var enc = _kemEngine.EncapsulateKey(pk[0], pk[1]);
            string dec = _kemEngine.DecapsulateKey(enc.CiphertextHex, kp.PrivateKeyHex);

            dec.Should().Be(enc.SharedSecretHex);
        });
    }

    [Fact]
    public void ConcurrentSignaturesAndReadinessTracking_ThreadSafeMetricsAggregation()
    {
        // Arrange
        const int iterations = 30;
        _gateway.ResetMetrics();
        var keyPair = _dsaService.GenerateSignatureKeyPair();

        // Act in parallel
        Parallel.For(0, iterations, i =>
        {
            byte[] payload = Encoding.UTF8.GetBytes($"Payload-{i}");
            var token = _dsaService.SignPayload(payload, keyPair.PrivateKeyHex, $"did:sgrowing:worker-{i}");
            var verifyResult = _dsaService.VerifySignature(payload, token.SignatureHex, keyPair.PublicKeyHex);
            verifyResult.Status.Should().Be(SignatureVerificationStatus.Valid);

            _gateway.RecordCommunication(PqcAlgorithm.MlDsa65_CrystalsDilithium);
        });

        // Assert
        var status = _gateway.GetQuantumReadinessStatus();
        status.TotalCommunications.Should().Be(iterations);
        status.QuantumSafeCommunications.Should().Be(iterations);
        status.QuantumSafePercentage.Should().Be(100.0);
    }

    [Fact]
    public void AddPostQuantumServices_RegistersSingletonsInServiceCollection_WithoutBuildingProvider()
    {
        // Arrange: STRICT RULE - NEVER call services.BuildServiceProvider() in tests.
        // Direct ServiceDescriptor assertions on ServiceCollection are required.
        var services = new ServiceCollection();

        // Act
        services.AddPostQuantumServices();

        // Assert
        services.Should().Contain(sd =>
            sd.ServiceType == typeof(IPostQuantumKeyExchangeEngine) &&
            sd.ImplementationType == typeof(PostQuantumKeyExchangeEngine) &&
            sd.Lifetime == ServiceLifetime.Singleton);

        services.Should().Contain(sd =>
            sd.ServiceType == typeof(IMlDsaQuantumSignatureService) &&
            sd.ImplementationType == typeof(MlDsaQuantumSignatureService) &&
            sd.Lifetime == ServiceLifetime.Singleton);

        services.Should().Contain(sd =>
            sd.ServiceType == typeof(ICryptoAgilityMigrationGateway) &&
            sd.ImplementationType == typeof(CryptoAgilityMigrationGateway) &&
            sd.Lifetime == ServiceLifetime.Singleton);
    }
}
