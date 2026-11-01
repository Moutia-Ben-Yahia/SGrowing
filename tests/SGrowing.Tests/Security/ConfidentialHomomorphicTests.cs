using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SGrowing.Api.Modules.Security.Confidential;
using Xunit;

namespace SGrowing.Tests.Security;

/// <summary>
/// Unit tests for Phase 37: Multi-Party Confidential Computing & Homomorphic Data Collaboration Mesh.
/// Strictly asserts on ServiceCollection descriptors without calling BuildServiceProvider().
/// </summary>
public class ConfidentialHomomorphicTests
{
    private readonly PaillierHomomorphicEncryptionEngine _engine = new();
    private readonly PrivateSetIntersectionService _psiService = new();
    private readonly ConfidentialAuditFederationService _auditService = new();

    private readonly PaillierKeyPair _testKeyPair;

    public ConfidentialHomomorphicTests()
    {
        // 256-bit modulus for ultra-fast, deterministic test execution
        _testKeyPair = _engine.GenerateKeyPair(256);
    }

    // ==========================================
    // 1. Paillier Key Generation & Structural Tests
    // ==========================================

    [Fact]
    public void GenerateKeyPair_DefaultBitLength_ProducesValidKeys()
    {
        // Act
        var keyPair = _engine.GenerateKeyPair(256);

        // Assert
        keyPair.Should().NotBeNull();
        keyPair.PublicKey.Should().NotBeNull();
        keyPair.PrivateKey.Should().NotBeNull();
        keyPair.PublicKey.KeySizeBits.Should().Be(256);
        keyPair.PublicKey.ModulusN.Should().NotBeNullOrWhiteSpace();
        keyPair.PublicKey.ModulusNSquared.Should().NotBeNullOrWhiteSpace();
        keyPair.PublicKey.GeneratorG.Should().NotBeNullOrWhiteSpace();
        keyPair.PrivateKey.Lambda.Should().NotBeNullOrWhiteSpace();
        keyPair.PrivateKey.Mu.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData(128)]
    [InlineData(256)]
    public void GenerateKeyPair_VariousBitLengths_MatchesRequestedModulusBits(int bitLength)
    {
        // Act
        var keyPair = _engine.GenerateKeyPair(bitLength);

        // Assert
        keyPair.PublicKey.KeySizeBits.Should().Be(bitLength);
        var n = PaillierHomomorphicEncryptionEngine.FromHex(keyPair.PublicKey.ModulusN);
        n.GetBitLength().Should().BeInRange(bitLength - 2, bitLength + 1);
    }

    [Fact]
    public void GenerateKeyPair_TooSmallBitLength_ThrowsArgumentOutOfRangeException()
    {
        // Act
        Action act = () => _engine.GenerateKeyPair(32);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("bitLength");
    }

    [Fact]
    public void GenerateKeyPair_ModulusNSquared_EqualsNSquared()
    {
        // Arrange
        var keyPair = _testKeyPair;
        var n = PaillierHomomorphicEncryptionEngine.FromHex(keyPair.PublicKey.ModulusN);
        var nSquared = PaillierHomomorphicEncryptionEngine.FromHex(keyPair.PublicKey.ModulusNSquared);

        // Assert
        nSquared.Should().Be(n * n);
    }

    [Fact]
    public void GenerateKeyPair_GeneratorG_EqualsNPlusOne()
    {
        // Arrange
        var keyPair = _testKeyPair;
        var n = PaillierHomomorphicEncryptionEngine.FromHex(keyPair.PublicKey.ModulusN);
        var g = PaillierHomomorphicEncryptionEngine.FromHex(keyPair.PublicKey.GeneratorG);

        // Assert
        g.Should().Be(n + BigInteger.One);
    }

    // ==========================================
    // 2. Encryption and Decryption Tests
    // ==========================================

    [Fact]
    public void Encrypt_And_Decrypt_SmallPositiveInteger_ReturnsOriginalValue()
    {
        // Arrange
        const long plaintext = 42L;

        // Act
        var encrypted = _engine.Encrypt(plaintext, _testKeyPair.PublicKey);
        long decrypted = _engine.Decrypt(encrypted.CiphertextHex, _testKeyPair.PrivateKey);

        // Assert
        decrypted.Should().Be(plaintext);
    }

    [Fact]
    public void Encrypt_And_Decrypt_LargePositiveInteger_ReturnsOriginalValue()
    {
        // Arrange
        const long plaintext = 987_654_321L;

        // Act
        var encrypted = _engine.Encrypt(plaintext, _testKeyPair.PublicKey);
        long decrypted = _engine.Decrypt(encrypted.CiphertextHex, _testKeyPair.PrivateKey);

        // Assert
        decrypted.Should().Be(plaintext);
    }

    [Fact]
    public void Encrypt_And_Decrypt_Zero_ReturnsZero()
    {
        // Arrange
        const long plaintext = 0L;

        // Act
        var encrypted = _engine.Encrypt(plaintext, _testKeyPair.PublicKey);
        long decrypted = _engine.Decrypt(encrypted.CiphertextHex, _testKeyPair.PrivateKey);

        // Assert
        decrypted.Should().Be(0L);
    }

    [Fact]
    public void Encrypt_And_Decrypt_NegativeInteger_ReturnsNegativeValue()
    {
        // Arrange
        const long plaintext = -1_250L;

        // Act
        var encrypted = _engine.Encrypt(plaintext, _testKeyPair.PublicKey);
        long decrypted = _engine.Decrypt(encrypted.CiphertextHex, _testKeyPair.PrivateKey);

        // Assert
        decrypted.Should().Be(plaintext);
    }

    [Fact]
    public void Encrypt_SamePlaintextTwice_ProducesDifferentCiphertexts_SemanticSecurity()
    {
        // Arrange
        const long plaintext = 100L;

        // Act
        var c1 = _engine.Encrypt(plaintext, _testKeyPair.PublicKey);
        var c2 = _engine.Encrypt(plaintext, _testKeyPair.PublicKey);

        // Assert: Probabilistic encryption ensures different ciphertexts for identical plaintexts
        c1.CiphertextHex.Should().NotBe(c2.CiphertextHex);
        _engine.Decrypt(c1.CiphertextHex, _testKeyPair.PrivateKey).Should().Be(plaintext);
        _engine.Decrypt(c2.CiphertextHex, _testKeyPair.PrivateKey).Should().Be(plaintext);
    }

    [Fact]
    public void Encrypt_BigInteger_Beyond64Bits_DecryptsCorrectly()
    {
        // Arrange
        var largeVal = BigInteger.Parse("1234567890123456789012345678901234567890");

        // Act
        var encrypted = _engine.Encrypt(largeVal, _testKeyPair.PublicKey);
        var decrypted = _engine.DecryptBigInteger(encrypted.CiphertextHex, _testKeyPair.PrivateKey);

        // Assert
        decrypted.Should().Be(largeVal);
    }

    // ==========================================
    // 3. Homomorphic Addition Tests
    // ==========================================

    [Fact]
    public void AddEncrypted_TwoPositiveNumbers_DecryptionEqualsSum()
    {
        // Arrange: 500 + 750 == 1250
        const long m1 = 500L;
        const long m2 = 750L;

        var c1 = _engine.Encrypt(m1, _testKeyPair.PublicKey);
        var c2 = _engine.Encrypt(m2, _testKeyPair.PublicKey);

        // Act
        string cSumHex = _engine.AddEncrypted(c1.CiphertextHex, c2.CiphertextHex, _testKeyPair.PublicKey);
        long decryptedSum = _engine.Decrypt(cSumHex, _testKeyPair.PrivateKey);

        // Assert
        decryptedSum.Should().Be(1250L);
    }

    [Fact]
    public void AddEncrypted_WithZero_LeavesOriginalValueUnchanged()
    {
        // Arrange: 450 + 0 == 450
        const long m1 = 450L;
        const long m2 = 0L;

        var c1 = _engine.Encrypt(m1, _testKeyPair.PublicKey);
        var c2 = _engine.Encrypt(m2, _testKeyPair.PublicKey);

        // Act
        string cSumHex = _engine.AddEncrypted(c1.CiphertextHex, c2.CiphertextHex, _testKeyPair.PublicKey);
        long decryptedSum = _engine.Decrypt(cSumHex, _testKeyPair.PrivateKey);

        // Assert
        decryptedSum.Should().Be(450L);
    }

    [Fact]
    public void AddEncrypted_PositiveAndNegative_DecryptionEqualsNetSum()
    {
        // Arrange: 1000 + (-350) == 650
        const long m1 = 1000L;
        const long m2 = -350L;

        var c1 = _engine.Encrypt(m1, _testKeyPair.PublicKey);
        var c2 = _engine.Encrypt(m2, _testKeyPair.PublicKey);

        // Act
        string cSumHex = _engine.AddEncrypted(c1.CiphertextHex, c2.CiphertextHex, _testKeyPair.PublicKey);
        long decryptedSum = _engine.Decrypt(cSumHex, _testKeyPair.PrivateKey);

        // Assert
        decryptedSum.Should().Be(650L);
    }

    [Fact]
    public void AddEncrypted_TwoNegativeNumbers_DecryptionEqualsNegativeSum()
    {
        // Arrange: (-200) + (-300) == -500
        const long m1 = -200L;
        const long m2 = -300L;

        var c1 = _engine.Encrypt(m1, _testKeyPair.PublicKey);
        var c2 = _engine.Encrypt(m2, _testKeyPair.PublicKey);

        // Act
        string cSumHex = _engine.AddEncrypted(c1.CiphertextHex, c2.CiphertextHex, _testKeyPair.PublicKey);
        long decryptedSum = _engine.Decrypt(cSumHex, _testKeyPair.PrivateKey);

        // Assert
        decryptedSum.Should().Be(-500L);
    }

    // ==========================================
    // 4. Homomorphic Subtraction Tests
    // ==========================================

    [Fact]
    public void SubtractEncrypted_PositiveResult_DecryptionEqualsDifference()
    {
        // Arrange: 800 - 300 == 500
        const long m1 = 800L;
        const long m2 = 300L;

        var c1 = _engine.Encrypt(m1, _testKeyPair.PublicKey);
        var c2 = _engine.Encrypt(m2, _testKeyPair.PublicKey);

        // Act
        string cDiffHex = _engine.SubtractEncrypted(c1.CiphertextHex, c2.CiphertextHex, _testKeyPair.PublicKey);
        long decryptedDiff = _engine.Decrypt(cDiffHex, _testKeyPair.PrivateKey);

        // Assert
        decryptedDiff.Should().Be(500L);
    }

    [Fact]
    public void SubtractEncrypted_NegativeResult_DecryptionYieldsNegativeNumber()
    {
        // Arrange: 200 - 650 == -450
        const long m1 = 200L;
        const long m2 = 650L;

        var c1 = _engine.Encrypt(m1, _testKeyPair.PublicKey);
        var c2 = _engine.Encrypt(m2, _testKeyPair.PublicKey);

        // Act
        string cDiffHex = _engine.SubtractEncrypted(c1.CiphertextHex, c2.CiphertextHex, _testKeyPair.PublicKey);
        long decryptedDiff = _engine.Decrypt(cDiffHex, _testKeyPair.PrivateKey);

        // Assert
        decryptedDiff.Should().Be(-450L);
    }

    [Fact]
    public void SubtractEncrypted_SelfSubtraction_DecryptionYieldsZero()
    {
        // Arrange: c - c == 0
        const long m = 999L;
        var c = _engine.Encrypt(m, _testKeyPair.PublicKey);

        // Act
        string cDiffHex = _engine.SubtractEncrypted(c.CiphertextHex, c.CiphertextHex, _testKeyPair.PublicKey);
        long decryptedDiff = _engine.Decrypt(cDiffHex, _testKeyPair.PrivateKey);

        // Assert
        decryptedDiff.Should().Be(0L);
    }

    // ==========================================
    // 5. Homomorphic Scalar Multiplication Tests
    // ==========================================

    [Fact]
    public void MultiplyScalar_PositiveScalar_DecryptionEqualsProduct()
    {
        // Arrange: 250 * 4 == 1000
        const long m = 250L;
        const long k = 4L;
        var c = _engine.Encrypt(m, _testKeyPair.PublicKey);

        // Act
        string cProdHex = _engine.MultiplyScalar(c.CiphertextHex, k, _testKeyPair.PublicKey);
        long decryptedProd = _engine.Decrypt(cProdHex, _testKeyPair.PrivateKey);

        // Assert
        decryptedProd.Should().Be(1000L);
    }

    [Fact]
    public void MultiplyScalar_ByZero_DecryptionEqualsZero()
    {
        // Arrange: 500 * 0 == 0
        const long m = 500L;
        var c = _engine.Encrypt(m, _testKeyPair.PublicKey);

        // Act
        string cProdHex = _engine.MultiplyScalar(c.CiphertextHex, 0L, _testKeyPair.PublicKey);
        long decryptedProd = _engine.Decrypt(cProdHex, _testKeyPair.PrivateKey);

        // Assert
        decryptedProd.Should().Be(0L);
    }

    [Fact]
    public void MultiplyScalar_ByOne_DecryptionLeavesValueUnchanged()
    {
        // Arrange: 777 * 1 == 777
        const long m = 777L;
        var c = _engine.Encrypt(m, _testKeyPair.PublicKey);

        // Act
        string cProdHex = _engine.MultiplyScalar(c.CiphertextHex, 1L, _testKeyPair.PublicKey);
        long decryptedProd = _engine.Decrypt(cProdHex, _testKeyPair.PrivateKey);

        // Assert
        decryptedProd.Should().Be(777L);
    }

    [Fact]
    public void MultiplyScalar_ByNegativeScalar_DecryptionYieldsNegativeProduct()
    {
        // Arrange: 150 * (-3) == -450
        const long m = 150L;
        const long k = -3L;
        var c = _engine.Encrypt(m, _testKeyPair.PublicKey);

        // Act
        string cProdHex = _engine.MultiplyScalar(c.CiphertextHex, k, _testKeyPair.PublicKey);
        long decryptedProd = _engine.Decrypt(cProdHex, _testKeyPair.PrivateKey);

        // Assert
        decryptedProd.Should().Be(-450L);
    }

    // ==========================================
    // 6. Chained Operations & Re-randomization Tests
    // ==========================================

    [Fact]
    public void ChainedHomomorphicSum_FiveEncryptedBalances_CalculatesTotal()
    {
        // Arrange: [100, 250, 400, 150, 300] => sum = 1200
        long[] balances = [100L, 250L, 400L, 150L, 300L];
        var encryptedList = balances.Select(b => _engine.Encrypt(b, _testKeyPair.PublicKey).CiphertextHex).ToList();

        // Act: Accumulate homomorphic sum across all 5 ciphertexts
        string currentSumHex = encryptedList[0];
        for (int i = 1; i < encryptedList.Count; i++)
        {
            currentSumHex = _engine.AddEncrypted(currentSumHex, encryptedList[i], _testKeyPair.PublicKey);
        }

        long totalDecrypted = _engine.Decrypt(currentSumHex, _testKeyPair.PrivateKey);

        // Assert
        totalDecrypted.Should().Be(1200L);
    }

    [Fact]
    public void CombinedHomomorphicLinearCombination_ComputesCorrectly()
    {
        // Arrange: 2 * c1 + 3 * c2 - c3
        // m1 = 10, m2 = 20, m3 = 15 => 2(10) + 3(20) - 15 = 20 + 60 - 15 = 65
        var c1 = _engine.Encrypt(10L, _testKeyPair.PublicKey);
        var c2 = _engine.Encrypt(20L, _testKeyPair.PublicKey);
        var c3 = _engine.Encrypt(15L, _testKeyPair.PublicKey);

        // Act
        string term1 = _engine.MultiplyScalar(c1.CiphertextHex, 2L, _testKeyPair.PublicKey);
        string term2 = _engine.MultiplyScalar(c2.CiphertextHex, 3L, _testKeyPair.PublicKey);
        string sumTerms = _engine.AddEncrypted(term1, term2, _testKeyPair.PublicKey);
        string finalCiphertext = _engine.SubtractEncrypted(sumTerms, c3.CiphertextHex, _testKeyPair.PublicKey);

        long result = _engine.Decrypt(finalCiphertext, _testKeyPair.PrivateKey);

        // Assert
        result.Should().Be(65L);
    }

    [Fact]
    public void RandomizeCiphertext_AltersCiphertextHex_WithoutChangingDecryptedValue()
    {
        // Arrange
        const long plaintext = 555L;
        var original = _engine.Encrypt(plaintext, _testKeyPair.PublicKey);

        // Act
        string randomizedHex = _engine.RandomizeCiphertext(original.CiphertextHex, _testKeyPair.PublicKey);

        // Assert: Hex changed, but underlying plaintext preserved
        randomizedHex.Should().NotBe(original.CiphertextHex);
        _engine.Decrypt(randomizedHex, _testKeyPair.PrivateKey).Should().Be(plaintext);
    }

    // ==========================================
    // 7. Mathematical & Serialization Helper Tests
    // ==========================================

    [Fact]
    public void ModInverse_CoprimeNumbers_SatisfiesModularIdentity()
    {
        // Arrange: a = 17, m = 3120
        var a = new BigInteger(17);
        var m = new BigInteger(3120);

        // Act
        var inv = PaillierHomomorphicEncryptionEngine.ModInverse(a, m);

        // Assert: (a * inv) mod m == 1
        PaillierHomomorphicEncryptionEngine.Mod(a * inv, m).Should().Be(BigInteger.One);
    }

    [Fact]
    public void ModInverse_NonCoprimeNumbers_ThrowsInvalidOperationException()
    {
        // Arrange: 6 and 9 share factor 3
        var a = new BigInteger(6);
        var m = new BigInteger(9);

        // Act
        Action act = () => PaillierHomomorphicEncryptionEngine.ModInverse(a, m);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*gcd*!= 1*");
    }

    [Fact]
    public void HexConversion_Roundtrip_PreservesBigInteger()
    {
        // Arrange
        var val = new BigInteger(9876543210987654321);

        // Act
        string hex = PaillierHomomorphicEncryptionEngine.ToHex(val);
        var parsed = PaillierHomomorphicEncryptionEngine.FromHex(hex);

        // Assert
        parsed.Should().Be(val);
    }

    [Fact]
    public void FromHex_OddLengthHex_PadsAndParsesSuccessfully()
    {
        // Arrange: "F" is odd length
        const string hex = "F";

        // Act
        var val = PaillierHomomorphicEncryptionEngine.FromHex(hex);

        // Assert
        val.Should().Be(15);
    }

    // ==========================================
    // 8. Private Set Intersection (PSI) Tests
    // ==========================================

    [Fact]
    public void ComputeIntersection_WithOverlappingElements_FindsExactMatches()
    {
        // Arrange
        const string sessionSecret = "Consortium-Session-Secret-123";
        const string sessionId = "sess_001";

        var itemA1 = _psiService.BlindItem("user1@example.com", "MerchantA", sessionSecret);
        var itemA2 = _psiService.BlindItem("fraudster@bad.com", "MerchantA", sessionSecret);
        var itemA3 = _psiService.BlindItem("user3@example.com", "MerchantA", sessionSecret);

        var itemB1 = _psiService.BlindItem("fraudster@bad.com", "MerchantB", sessionSecret);
        var itemB2 = _psiService.BlindItem("user4@example.com", "MerchantB", sessionSecret);

        List<PsiCandidateItem> setA = [itemA1, itemA2, itemA3];
        List<PsiCandidateItem> setB = [itemB1, itemB2];

        // Act
        var result = _psiService.ComputeIntersection(setA, setB, sessionId);

        // Assert
        result.Should().NotBeNull();
        result.MatchedCount.Should().Be(1);
        result.MatchedHashes.Should().ContainSingle()
            .Which.Should().Be(itemA2.BlindedHashHex);
        result.ExecutionTimeMs.Should().BeGreaterThanOrEqualTo(0);
        result.ConsortiumParticipantIds.Should().Contain("MerchantA");
        result.ConsortiumParticipantIds.Should().Contain("MerchantB");
    }

    [Fact]
    public void ComputeIntersection_DisjointSets_ReturnsZeroMatches()
    {
        // Arrange
        const string sessionSecret = "Consortium-Session-Secret-456";
        const string sessionId = "sess_002";

        var setA = _psiService.BlindItems([("alice@bank.com", "BankA"), ("bob@bank.com", "BankA")], sessionSecret);
        var setB = _psiService.BlindItems([("charlie@shop.com", "ShopB"), ("david@shop.com", "ShopB")], sessionSecret);

        // Act
        var result = _psiService.ComputeIntersection(setA, setB, sessionId);

        // Assert
        result.MatchedCount.Should().Be(0);
        result.MatchedHashes.Should().BeEmpty();
    }

    [Fact]
    public void ComputeIntersection_FullOverlap_ReturnsAllElements()
    {
        // Arrange
        const string sessionSecret = "Consortium-Session-Secret-789";
        const string sessionId = "sess_003";

        var setA = _psiService.BlindItems([("dup1", "P1"), ("dup2", "P1")], sessionSecret);
        var setB = _psiService.BlindItems([("dup1", "P2"), ("dup2", "P2")], sessionSecret);

        // Act
        var result = _psiService.ComputeIntersection(setA, setB, sessionId);

        // Assert
        result.MatchedCount.Should().Be(2);
        result.MatchedHashes.Count.Should().Be(2);
    }

    [Fact]
    public void BlindItem_SameSecret_ProducesDeterministicHash()
    {
        // Arrange
        const string secret = "SharedConsortiumKey";
        const string raw = "card_fingerprint_xyz";

        // Act
        var b1 = _psiService.BlindItem(raw, "M1", secret);
        var b2 = _psiService.BlindItem(raw, "M2", secret);

        // Assert: Hashes match regardless of partition key
        b1.BlindedHashHex.Should().Be(b2.BlindedHashHex);
    }

    [Fact]
    public void BlindItem_DifferentSecrets_ProducesDifferentHashes()
    {
        // Arrange
        const string raw = "card_fingerprint_xyz";

        // Act
        var b1 = _psiService.BlindItem(raw, "M1", "Secret-1");
        var b2 = _psiService.BlindItem(raw, "M1", "Secret-2");

        // Assert
        b1.BlindedHashHex.Should().NotBe(b2.BlindedHashHex);
    }

    [Fact]
    public void ComputeMultiPartyIntersection_ThreeParties_FindsCommonIntersect()
    {
        // Arrange
        const string secret = "3-Party-Consortium-Secret";
        const string common = "shared_fraudster_id";

        var setA = _psiService.BlindItems([(common, "Bank1"), ("exclusive_a", "Bank1")], secret);
        var setB = _psiService.BlindItems([(common, "Bank2"), ("exclusive_b", "Bank2")], secret);
        var setC = _psiService.BlindItems([(common, "Bank3"), ("exclusive_c", "Bank3")], secret);

        Dictionary<string, IEnumerable<PsiCandidateItem>> participantSets = new()
        {
            ["Bank1"] = setA,
            ["Bank2"] = setB,
            ["Bank3"] = setC
        };

        // Act
        var result = _psiService.ComputeMultiPartyIntersection(participantSets, "3p_session");

        // Assert
        result.MatchedCount.Should().Be(1);
        result.ConsortiumParticipantIds.Should().BeEquivalentTo(["Bank1", "Bank2", "Bank3"]);
    }

    // ==========================================
    // 9. Confidential Audit Federation & Attestation Tests
    // ==========================================

    [Fact]
    public void IssueAttestation_ValidRequest_CreatesSignedToken()
    {
        // Arrange
        const string secretKey = "ConsortiumRootSecretKey2026";
        var request = new IssueAttestationRequest
        {
            ConsortiumId = "consortium_emea_01",
            ComputationType = "HomomorphicAddition",
            InputHash = "A1B2C3D4E5F6",
            ResultCommitment = "F6E5D4C3B2A1"
        };

        // Act
        var token = _auditService.IssueAttestation(request, secretKey);

        // Assert
        token.Should().NotBeNull();
        token.AttestationId.Should().NotBeNullOrWhiteSpace();
        token.ConsortiumId.Should().Be(request.ConsortiumId);
        token.ComputationType.Should().Be(request.ComputationType);
        token.InputHash.Should().Be(request.InputHash);
        token.ResultCommitment.Should().Be(request.ResultCommitment);
        token.CryptographicSignature.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void VerifyAttestation_UntamperedToken_ReturnsTrue()
    {
        // Arrange
        const string secretKey = "ConsortiumRootSecretKey2026";
        var request = new IssueAttestationRequest
        {
            ConsortiumId = "consortium_latam_02",
            ComputationType = "PrivateSetIntersection",
            InputHash = "DEADBEEF1234",
            ResultCommitment = "CAFEBABE5678"
        };

        var token = _auditService.IssueAttestation(request, secretKey);

        // Act
        bool isValid = _auditService.VerifyAttestation(token, secretKey);

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public void VerifyAttestation_WrongSecretKey_ReturnsFalse()
    {
        // Arrange
        const string secretKey = "ConsortiumSecretKey";
        const string wrongSecretKey = "AttackerSecretKey";
        var request = new IssueAttestationRequest
        {
            ConsortiumId = "c1",
            ComputationType = "HomomorphicAddition",
            InputHash = "hash1",
            ResultCommitment = "comm1"
        };

        var token = _auditService.IssueAttestation(request, secretKey);

        // Act
        bool isValid = _auditService.VerifyAttestation(token, wrongSecretKey);

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void VerifyAttestation_TamperedPayload_ReturnsFalse()
    {
        // Arrange
        const string secretKey = "ConsortiumSecretKey";
        var request = new IssueAttestationRequest
        {
            ConsortiumId = "c1",
            ComputationType = "HomomorphicAddition",
            InputHash = "hash1",
            ResultCommitment = "comm1"
        };

        var originalToken = _auditService.IssueAttestation(request, secretKey);

        // Tamper with ResultCommitment
        var tamperedToken = originalToken with
        {
            ResultCommitment = "tampered_result_commitment"
        };

        // Act
        bool isValid = _auditService.VerifyAttestation(tamperedToken, secretKey);

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void VerifyAttestation_MalformedSignatureHex_ReturnsFalseWithoutThrowing()
    {
        // Arrange
        const string secretKey = "ConsortiumSecretKey";
        var request = new IssueAttestationRequest
        {
            ConsortiumId = "c1",
            ComputationType = "HomomorphicAddition",
            InputHash = "hash1",
            ResultCommitment = "comm1"
        };

        var token = _auditService.IssueAttestation(request, secretKey);
        var malformedToken = token with { CryptographicSignature = "NOT_A_HEX_STRING!@#$" };

        // Act
        bool isValid = _auditService.VerifyAttestation(malformedToken, secretKey);

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void GetAttestation_AfterIssuance_CanBeRetrievedById()
    {
        // Arrange
        const string secret = "TestSecret";
        var token = _auditService.IssueAttestation(new IssueAttestationRequest
        {
            ConsortiumId = "consort_x",
            ComputationType = "HomomorphicAddition",
            InputHash = "h1",
            ResultCommitment = "rc1"
        }, secret);

        // Act
        var retrieved = _auditService.GetAttestation(token.AttestationId);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.AttestationId.Should().Be(token.AttestationId);
    }

    // ==========================================
    // 10. Concurrency & Thread-Safety Tests
    // ==========================================

    [Fact]
    public void ConcurrentHomomorphicMath_ExecutesSafelyAcrossMultipleThreads()
    {
        // Arrange
        const int threadCount = 8;
        const int iterationsPerThread = 20;

        // Act
        Parallel.For(0, threadCount, t =>
        {
            for (int i = 0; i < iterationsPerThread; i++)
            {
                long val1 = (t + 1) * 10;
                long val2 = (i + 1) * 5;

                var c1 = _engine.Encrypt(val1, _testKeyPair.PublicKey);
                var c2 = _engine.Encrypt(val2, _testKeyPair.PublicKey);

                string sumHex = _engine.AddEncrypted(c1.CiphertextHex, c2.CiphertextHex, _testKeyPair.PublicKey);
                long dec = _engine.Decrypt(sumHex, _testKeyPair.PrivateKey);

                dec.Should().Be(val1 + val2);
            }
        });
    }

    [Fact]
    public void ConcurrentAttestationIssuance_MaintainsRegistryIntegrity()
    {
        // Arrange
        const int count = 50;
        const string secret = "ThreadSafetyConsortiumSecret";

        // Act
        Parallel.For(0, count, i =>
        {
            _auditService.IssueAttestation(new IssueAttestationRequest
            {
                ConsortiumId = $"consortium_{i}",
                ComputationType = "HomomorphicAddition",
                InputHash = $"hash_{i}",
                ResultCommitment = $"commit_{i}"
            }, secret);
        });

        // Assert
        var recent = _auditService.GetRecentAttestations(count + 10);
        recent.Count.Should().BeGreaterThanOrEqualTo(count);
    }

    // ==========================================
    // 11. Dependency Injection Registration Tests
    // ==========================================

    [Fact]
    public void AddConfidentialServices_RegistersSingletonsInServiceCollection_WithoutBuildingProvider()
    {
        // Arrange: STRICT RULE: Do not call services.BuildServiceProvider() in tests.
        // Direct ServiceDescriptor assertion on ServiceCollection is required.
        var services = new ServiceCollection();

        // Act
        services.AddConfidentialServices();

        // Assert
        services.Should().Contain(sd => sd.ServiceType == typeof(IPaillierHomomorphicEncryptionEngine) &&
                                        sd.ImplementationType == typeof(PaillierHomomorphicEncryptionEngine) &&
                                        sd.Lifetime == ServiceLifetime.Singleton);

        services.Should().Contain(sd => sd.ServiceType == typeof(IPrivateSetIntersectionService) &&
                                        sd.ImplementationType == typeof(PrivateSetIntersectionService) &&
                                        sd.Lifetime == ServiceLifetime.Singleton);

        services.Should().Contain(sd => sd.ServiceType == typeof(IConfidentialAuditFederationService) &&
                                        sd.ImplementationType == typeof(ConfidentialAuditFederationService) &&
                                        sd.Lifetime == ServiceLifetime.Singleton);
    }
}
