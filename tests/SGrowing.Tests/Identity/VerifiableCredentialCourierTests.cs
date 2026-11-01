using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SGrowing.Api.Modules.Security.VerifiableIdentity;
using Xunit;

namespace SGrowing.Tests.Identity;

/// <summary>
/// Comprehensive unit tests for Phase 34: Decentralized Identity (DID),
/// W3C Verifiable Credentials & Biometric Courier Onboarding.
/// </summary>
public class VerifiableCredentialCourierTests
{
    private const string TestIssuerSecret = "SGrowing-AlgeriaPost-TestMasterSecret-2026-SecureKey!";
    private const string AuthoritySecret = "SGrowing-CustodyHandover-AuthoritySecret-2026";
    private const double AlgiersLat = 36.7538;
    private const double AlgiersLon = 3.0588;

    private readonly VerifiableCredentialEngine _engine = new();
    private readonly CourierOnboardingAttestationService _courierService;
    private readonly BiometricCustodyUnlockService _custodyService;

    public VerifiableCredentialCourierTests()
    {
        _courierService = new CourierOnboardingAttestationService(_engine);
        _custodyService = new BiometricCustodyUnlockService(_courierService);
    }

    // =========================================================================
    // 1. W3C DID Generation & Key Derivation Tests
    // =========================================================================

    [Fact]
    public void CreateDid_Ed25519_GeneratesValidW3CDidKeyDocument()
    {
        // Act
        var doc = _engine.CreateDid("Ed25519");

        // Assert
        doc.Should().NotBeNull();
        doc.Id.Should().StartWith("did:key:z6M");
        doc.VerificationMethod.Should().HaveCount(1);

        var vm = doc.VerificationMethod[0];
        vm.Id.Should().StartWith(doc.Id);
        vm.Controller.Should().Be(doc.Id);
        vm.Type.Should().Be("Ed25519VerificationKey2020");
        vm.PublicKeyHex.Should().HaveLength(64);
        vm.PublicKeyMultibase.Should().StartWith("z6M");

        doc.Authentication.Should().Contain(vm.Id);
        doc.AssertionMethod.Should().Contain(vm.Id);
        doc.Created.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void CreateDid_Secp256k1_GeneratesValidW3CDidKeyDocument()
    {
        // Act
        var doc = _engine.CreateDid("Secp256k1");

        // Assert
        doc.Should().NotBeNull();
        doc.Id.Should().StartWith("did:key:z");
        doc.VerificationMethod[0].Type.Should().Be("EcdsaSecp256k1VerificationKey2019");
    }

    [Fact]
    public void CreateDid_MultipleCalls_ProduceUniqueIdentifiers()
    {
        // Act
        var doc1 = _engine.CreateDid();
        var doc2 = _engine.CreateDid();

        // Assert
        doc1.Id.Should().NotBe(doc2.Id);
        doc1.VerificationMethod[0].PublicKeyHex.Should().NotBe(doc2.VerificationMethod[0].PublicKeyHex);
    }

    [Fact]
    public void ResolveDid_WhenExists_ReturnsDocument()
    {
        // Arrange
        var created = _engine.CreateDid();

        // Act
        var resolved = _engine.ResolveDid(created.Id);

        // Assert
        resolved.Should().NotBeNull();
        resolved!.Id.Should().Be(created.Id);
    }

    [Fact]
    public void ResolveDid_WhenDoesNotExist_ReturnsNull()
    {
        // Act
        var resolved = _engine.ResolveDid("did:key:zNonExistent99999");

        // Assert
        resolved.Should().BeNull();
    }

    [Fact]
    public void ResolveDid_NullOrEmpty_ReturnsNull()
    {
        _engine.ResolveDid("").Should().BeNull();
        _engine.ResolveDid("   ").Should().BeNull();
    }

    // =========================================================================
    // 2. W3C Verifiable Credential Issuance & Verification Tests
    // =========================================================================

    [Fact]
    public void IssueCredential_ValidParameters_ProducesSignedVerifiableCredential()
    {
        // Arrange
        var issuerDoc = _engine.CreateDid();
        var subjectDoc = _engine.CreateDid();
        var claims = new Dictionary<string, object>
        {
            ["courierTier"] = "Tier2",
            ["rating"] = 4.95,
            ["completedDeliveries"] = 1250
        };

        // Act
        var vc = _engine.IssueCredential(
            issuerDid: issuerDoc.Id,
            subjectDid: subjectDoc.Id,
            credentialType: "CourierDeliveryBadgeCredential",
            claims: claims,
            secretKey: TestIssuerSecret,
            validityPeriod: TimeSpan.FromDays(30));

        // Assert
        vc.Should().NotBeNull();
        vc.Id.Should().StartWith("urn:uuid:");
        vc.Issuer.Should().Be(issuerDoc.Id);
        vc.Type.Should().Contain("VerifiableCredential");
        vc.Type.Should().Contain("CourierDeliveryBadgeCredential");
        vc.ExpirationDate.Should().NotBeNull();
        vc.CredentialSubject["id"].Should().Be(subjectDoc.Id);
        vc.CredentialProof.Should().NotBeNull();
        vc.CredentialProof!.SignatureHex.Should().NotBeNullOrWhiteSpace();
        vc.CredentialProof.Jws.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void VerifyCredential_ValidCredential_ReturnsValidResult()
    {
        // Arrange
        var issuer = _engine.CreateDid();
        var subject = _engine.CreateDid();
        var claims = new Dictionary<string, object>
        {
            ["driverLicenseNumber"] = "DZ-DL-987654321",
            ["carrierId"] = "YALIDINE-EXP-01"
        };

        var vc = _engine.IssueCredential(issuer.Id, subject.Id, "CourierBadge", claims, TestIssuerSecret);

        // Act
        var result = _engine.VerifyCredential(vc, TestIssuerSecret);

        // Assert
        result.IsValid.Should().BeTrue();
        result.IsExpired.Should().BeFalse();
        result.IsRevoked.Should().BeFalse();
        result.IsSignatureValid.Should().BeTrue();
        result.ValidationErrors.Should().BeEmpty();
    }

    [Fact]
    public void VerifyCredential_TamperedClaim_FailsSignatureVerification()
    {
        // Arrange
        var issuer = _engine.CreateDid();
        var subject = _engine.CreateDid();
        var claims = new Dictionary<string, object>
        {
            ["rating"] = 4.5,
            ["role"] = "Courier"
        };

        var vc = _engine.IssueCredential(issuer.Id, subject.Id, "CourierBadge", claims, TestIssuerSecret);

        // Tamper with claim
        vc.CredentialSubject["rating"] = 5.0;

        // Act
        var result = _engine.VerifyCredential(vc, TestIssuerSecret);

        // Assert
        result.IsValid.Should().BeFalse();
        result.IsSignatureValid.Should().BeFalse();
        result.ValidationErrors.Should().Contain(e => e.Contains("Cryptographic signature verification failed"));
    }

    [Fact]
    public void VerifyCredential_AddedClaim_FailsSignatureVerification()
    {
        // Arrange
        var issuer = _engine.CreateDid();
        var subject = _engine.CreateDid();
        var claims = new Dictionary<string, object> { ["role"] = "Courier" };
        var vc = _engine.IssueCredential(issuer.Id, subject.Id, "CourierBadge", claims, TestIssuerSecret);

        // Inject unauthorized claim
        vc.CredentialSubject["adminRole"] = true;

        // Act
        var result = _engine.VerifyCredential(vc, TestIssuerSecret);

        // Assert
        result.IsValid.Should().BeFalse();
        result.IsSignatureValid.Should().BeFalse();
    }

    [Fact]
    public void VerifyCredential_RemovedClaim_FailsSignatureVerification()
    {
        // Arrange
        var issuer = _engine.CreateDid();
        var subject = _engine.CreateDid();
        var claims = new Dictionary<string, object>
        {
            ["role"] = "Courier",
            ["zone"] = "Algiers-Centre"
        };
        var vc = _engine.IssueCredential(issuer.Id, subject.Id, "CourierBadge", claims, TestIssuerSecret);

        // Remove a claim
        vc.CredentialSubject.Remove("zone");

        // Act
        var result = _engine.VerifyCredential(vc, TestIssuerSecret);

        // Assert
        result.IsValid.Should().BeFalse();
        result.IsSignatureValid.Should().BeFalse();
    }

    [Fact]
    public void VerifyCredential_TamperedSignatureHex_FailsVerification()
    {
        // Arrange
        var issuer = _engine.CreateDid();
        var subject = _engine.CreateDid();
        var claims = new Dictionary<string, object> { ["key"] = "val" };
        var vc = _engine.IssueCredential(issuer.Id, subject.Id, "Badge", claims, TestIssuerSecret);

        // Alter signature hex
        var proof = vc.CredentialProof! with { SignatureHex = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef" };
        var tamperedVc = vc with { CredentialProof = proof };

        // Act
        var result = _engine.VerifyCredential(tamperedVc, TestIssuerSecret);

        // Assert
        result.IsValid.Should().BeFalse();
        result.IsSignatureValid.Should().BeFalse();
    }

    [Fact]
    public void VerifyCredential_WrongSecretKey_FailsSignatureVerification()
    {
        // Arrange
        var issuer = _engine.CreateDid();
        var subject = _engine.CreateDid();
        var claims = new Dictionary<string, object> { ["prop"] = "test" };
        var vc = _engine.IssueCredential(issuer.Id, subject.Id, "Badge", claims, TestIssuerSecret);

        // Act
        var result = _engine.VerifyCredential(vc, "Wrong-Secret-Key-1234567890!");

        // Assert
        result.IsValid.Should().BeFalse();
        result.IsSignatureValid.Should().BeFalse();
    }

    [Fact]
    public void VerifyCredential_MissingProof_ReturnsInvalid()
    {
        // Arrange
        var vc = new VerifiableCredential
        {
            Id = "urn:uuid:test",
            Issuer = "did:key:z123",
            CredentialProof = null
        };

        // Act
        var result = _engine.VerifyCredential(vc, TestIssuerSecret);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ValidationErrors.Should().Contain(e => e.Contains("Missing cryptographic credential proof"));
    }

    [Fact]
    public void VerifyCredential_ExpiredCredential_FailsVerificationWithExpiredFlag()
    {
        // Arrange
        var issuer = _engine.CreateDid();
        var subject = _engine.CreateDid();
        var claims = new Dictionary<string, object> { ["status"] = "Active" };

        // Issued in the past with negative validity period
        var vc = _engine.IssueCredential(
            issuer.Id, subject.Id, "Badge", claims, TestIssuerSecret,
            validityPeriod: TimeSpan.FromSeconds(-10));

        // Act
        var result = _engine.VerifyCredential(vc, TestIssuerSecret);

        // Assert
        result.IsValid.Should().BeFalse();
        result.IsExpired.Should().BeTrue();
        result.ValidationErrors.Should().Contain(e => e.Contains("expired"));
    }

    [Fact]
    public void VerifyCredential_FutureExpiration_PassesVerification()
    {
        // Arrange
        var issuer = _engine.CreateDid();
        var subject = _engine.CreateDid();
        var claims = new Dictionary<string, object> { ["status"] = "Active" };

        var vc = _engine.IssueCredential(
            issuer.Id, subject.Id, "Badge", claims, TestIssuerSecret,
            validityPeriod: TimeSpan.FromDays(365));

        // Act
        var result = _engine.VerifyCredential(vc, TestIssuerSecret);

        // Assert
        result.IsValid.Should().BeTrue();
        result.IsExpired.Should().BeFalse();
    }

    // =========================================================================
    // 3. Credential Revocation Registry Tests
    // =========================================================================

    [Fact]
    public void RevokeCredential_ValidCredentialId_RegistersRevocationRecord()
    {
        // Arrange
        const string credentialId = "urn:uuid:revocation-test-001";
        const string reason = "Courier contract terminated by partner.";

        // Act
        var record = _engine.RevokeCredential(credentialId, reason);

        // Assert
        record.Should().NotBeNull();
        record.CredentialId.Should().Be(credentialId);
        record.Reason.Should().Be(reason);
        record.RevocationDate.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));

        _engine.IsCredentialRevoked(credentialId).Should().BeTrue();
        var retrieved = _engine.GetRevocationRecord(credentialId);
        retrieved.Should().NotBeNull();
        retrieved!.Reason.Should().Be(reason);
    }

    [Fact]
    public void VerifyCredential_RevokedCredential_FailsVerificationWithRevokedFlag()
    {
        // Arrange
        var issuer = _engine.CreateDid();
        var subject = _engine.CreateDid();
        var claims = new Dictionary<string, object> { ["badge"] = "Gold" };
        var vc = _engine.IssueCredential(issuer.Id, subject.Id, "CourierBadge", claims, TestIssuerSecret);

        // First verification should pass
        _engine.VerifyCredential(vc, TestIssuerSecret).IsValid.Should().BeTrue();

        // Revoke the credential
        _engine.RevokeCredential(vc.Id, "Suspicious activity detected in field.");

        // Act
        var result = _engine.VerifyCredential(vc, TestIssuerSecret);

        // Assert
        result.IsValid.Should().BeFalse();
        result.IsRevoked.Should().BeTrue();
        result.ValidationErrors.Should().Contain(e => e.Contains("revoked"));
    }

    [Fact]
    public void IsCredentialRevoked_NonRevokedCredential_ReturnsFalse()
    {
        _engine.IsCredentialRevoked("urn:uuid:unrevoked-credential-123").Should().BeFalse();
        _engine.GetRevocationRecord("urn:uuid:unrevoked-credential-123").Should().BeNull();
    }

    // =========================================================================
    // 4. Courier Onboarding & Attestation Service Tests
    // =========================================================================

    [Fact]
    public void OnboardCourier_ValidAlgerianNINAndDriverLicense_SuccessfullyOnboardsCourier()
    {
        // Arrange
        var request = new CourierOnboardingRequest
        {
            FullName = "Amine Benali",
            NationalIdNumber = "199416010012345678", // Exactly 18 numeric digits
            DriverLicenseNumber = "DL-ALG-2018-87654", // >= 8 chars
            VehiclePlateNumber = "00123-116-16",
            VehicleType = "Motorcycle",
            Phone = "+213550123456",
            CarrierPartnerId = "PARTNER-YALIDINE-01",
            RequestedCashFloatTier = CashFloatLimitTier.Tier2_Standard_250000DZD
        };

        // Act
        var result = _courierService.OnboardCourier(request, TestIssuerSecret);

        // Assert
        result.Should().NotBeNull();
        result.Badge.Should().NotBeNull();
        result.Badge.CourierDid.Should().StartWith("did:key:z6M");
        result.Badge.FullName.Should().Be("Amine Benali");
        result.Badge.CashFloatTier.Should().Be(CashFloatLimitTier.Tier2_Standard_250000DZD);
        result.Badge.MaxCashFloatDzd.Should().Be(250_000m);
        result.Badge.IsActive.Should().BeTrue();
        result.Badge.LicenseAttestationHash.Should().HaveLength(64);

        result.Credential.Should().NotBeNull();
        result.Credential.Type.Should().Contain("CourierDeliveryBadgeCredential");
        result.Credential.CredentialSubject["carrierPartnerId"].Should().Be("PARTNER-YALIDINE-01");
        result.Credential.CredentialSubject["criminalRecordClearanceRef"].ToString().Should().StartWith("CR-DZ-");
        result.Credential.CredentialSubject["vehicleInsuranceRef"].ToString().Should().StartWith("INS-DZ-");

        // Verify the issued credential cryptographically
        var verifyResult = _engine.VerifyCredential(result.Credential, TestIssuerSecret);
        verifyResult.IsValid.Should().BeTrue();
    }

    [Fact]
    public void OnboardCourier_Deconstruct_SupportsTupleSyntax()
    {
        // Arrange
        var request = CreateValidCourierRequest("Karim Ziane", "199216010098765432", "DL-ALG-99887766");

        // Act
        var (badge, credential) = _courierService.OnboardCourier(request, TestIssuerSecret);

        // Assert
        badge.Should().NotBeNull();
        credential.Should().NotBeNull();
        badge.FullName.Should().Be("Karim Ziane");
    }

    [Theory]
    [InlineData("123456789")] // 9 digits (too short)
    [InlineData("19941601001234567")] // 17 digits (too short)
    [InlineData("1994160100123456789")] // 19 digits (too long)
    [InlineData("19941601001234567A")] // non-numeric
    [InlineData("")]
    public void OnboardCourier_InvalidAlgerianNIN_ThrowsArgumentException(string invalidNin)
    {
        // Arrange
        var request = CreateValidCourierRequest("Test Courier", invalidNin, "DL-12345678");

        // Act
        var act = () => _courierService.OnboardCourier(request, TestIssuerSecret);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*NIN*");
    }

    [Theory]
    [InlineData("DL123")] // 5 chars (< 8)
    [InlineData("1234567")] // 7 chars (< 8)
    [InlineData("")]
    [InlineData("       ")]
    public void OnboardCourier_InvalidDriverLicense_TooShort_ThrowsArgumentException(string invalidLicense)
    {
        // Arrange
        var request = CreateValidCourierRequest("Test Courier", "199416010012345678", invalidLicense);

        // Act
        var act = () => _courierService.OnboardCourier(request, TestIssuerSecret);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Driver license*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("A")] // < 2 chars
    public void OnboardCourier_InvalidFullName_ThrowsArgumentException(string invalidName)
    {
        var request = new CourierOnboardingRequest
        {
            FullName = invalidName,
            NationalIdNumber = "199416010012345678",
            DriverLicenseNumber = "DL-2022-998877",
            VehiclePlateNumber = "01234-116-16",
            VehicleType = "Van",
            Phone = "+213550123456",
            CarrierPartnerId = "PARTNER-1",
            RequestedCashFloatTier = CashFloatLimitTier.Tier1_LowRisk_50000DZD
        };

        var act = () => _courierService.OnboardCourier(request, TestIssuerSecret);
        act.Should().Throw<ArgumentException>().WithMessage("*Full name*");
    }

    [Fact]
    public void OnboardCourier_InvalidVehiclePlate_ThrowsArgumentException()
    {
        var request = new CourierOnboardingRequest
        {
            FullName = "Amine Z",
            NationalIdNumber = "199416010012345678",
            DriverLicenseNumber = "DL-2022-998877",
            VehiclePlateNumber = "12", // < 4 chars
            VehicleType = "Van",
            Phone = "+213550123456",
            CarrierPartnerId = "PARTNER-1",
            RequestedCashFloatTier = CashFloatLimitTier.Tier1_LowRisk_50000DZD
        };

        var act = () => _courierService.OnboardCourier(request, TestIssuerSecret);
        act.Should().Throw<ArgumentException>().WithMessage("*Vehicle plate*");
    }

    // =========================================================================
    // 5. Cash Float Limits & Tiering Tests
    // =========================================================================

    [Theory]
    [InlineData(CashFloatLimitTier.Tier1_LowRisk_50000DZD, 50000)]
    [InlineData(CashFloatLimitTier.Tier2_Standard_250000DZD, 250000)]
    [InlineData(CashFloatLimitTier.Tier3_HighValue_1000000DZD, 1000000)]
    public void CourierOnboarding_CashFloatTiers_MapToExpectedDzdAmounts(CashFloatLimitTier tier, decimal expectedAmount)
    {
        // Arrange
        var request = CreateValidCourierRequest("Tiered Courier", "199516010011223344", "DL-VALID-123456");
        var tieredRequest = request with { RequestedCashFloatTier = tier };

        // Act
        var result = _courierService.OnboardCourier(tieredRequest, TestIssuerSecret);

        // Assert
        result.Badge.CashFloatTier.Should().Be(tier);
        result.Badge.MaxCashFloatDzd.Should().Be(expectedAmount);
    }

    [Fact]
    public void UpdateCashFloatLimit_ValidCourier_UpdatesTierAndMaxFloat()
    {
        // Arrange
        var request = CreateValidCourierRequest("Amine TierTest", "199616010011223344", "DL-VALID-654321");
        var (badge, _) = _courierService.OnboardCourier(request, TestIssuerSecret);
        badge.MaxCashFloatDzd.Should().Be(50_000m);

        // Act - Upgrade to Tier 3
        var updated = _courierService.UpdateCashFloatLimit(badge.CourierDid, CashFloatLimitTier.Tier3_HighValue_1000000DZD);

        // Assert
        updated.CashFloatTier.Should().Be(CashFloatLimitTier.Tier3_HighValue_1000000DZD);
        updated.MaxCashFloatDzd.Should().Be(1_000_000m);

        // Retrieve badge again and assert persistence
        var retrieved = _courierService.GetCourierBadge(badge.CourierDid);
        retrieved.Should().NotBeNull();
        retrieved!.MaxCashFloatDzd.Should().Be(1_000_000m);
    }

    [Fact]
    public void UpdateCashFloatLimit_NonExistentCourier_ThrowsKeyNotFoundException()
    {
        var act = () => _courierService.UpdateCashFloatLimit("did:key:nonexistent", CashFloatLimitTier.Tier2_Standard_250000DZD);
        act.Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void GetCourierBadge_NonExistentCourierDid_ReturnsNull()
    {
        _courierService.GetCourierBadge("did:key:z6MNonExistent").Should().BeNull();
    }

    // =========================================================================
    // 6. Biometric Challenge Generation & Liveness Gating Tests
    // =========================================================================

    [Fact]
    public void GenerateLivenessChallenge_ValidCourier_ReturnsDynamicChallengeWith3MinTtl()
    {
        // Arrange
        const string courierDid = "did:key:z6MkuCourierDid12345";

        // Act
        var challenge = _custodyService.GenerateLivenessChallenge(courierDid);

        // Assert
        challenge.Should().NotBeNull();
        challenge.ChallengeId.Should().StartWith("CHAL-");
        challenge.CourierDid.Should().Be(courierDid);
        challenge.Nonce.Should().HaveLength(32); // 16 bytes hex = 32 hex chars
        challenge.ChallengeAction.Should().BeOneOf("BlinkAndSmile", "HeadTurnLeft", "NodTwice");
        challenge.ExpiresAt.Should().BeCloseTo(challenge.CreatedAt.AddMinutes(3), TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void GenerateLivenessChallenge_EmptyCourierDid_ThrowsArgumentException()
    {
        var act = () => _custodyService.GenerateLivenessChallenge("");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AuthorizeCustodyUnlock_NonExistentChallenge_Fails()
    {
        // Arrange
        var request = CreateValidCustodyRequest("CHAL-NONEXISTENT", "did:key:courier1", AlgiersLat, AlgiersLon, AlgiersLat, AlgiersLon);

        // Act
        var result = _custodyService.AuthorizeCustodyUnlock(request, AuthoritySecret);

        // Assert
        result.IsAuthorized.Should().BeFalse();
        result.RejectionReason.Should().Contain("challenge not found");
    }

    [Fact]
    public void AuthorizeCustodyUnlock_CourierDidMismatch_Fails()
    {
        // Arrange
        var challenge = _custodyService.GenerateLivenessChallenge("did:key:courierCorrect");
        var request = CreateValidCustodyRequest(challenge.ChallengeId, "did:key:courierImpostor", AlgiersLat, AlgiersLon, AlgiersLat, AlgiersLon);

        // Act
        var result = _custodyService.AuthorizeCustodyUnlock(request, AuthoritySecret);

        // Assert
        result.IsAuthorized.Should().BeFalse();
        result.RejectionReason.Should().Contain("mismatch");
    }

    [Fact]
    public void AuthorizeCustodyUnlock_ChallengeSingleUse_CannotBeReused()
    {
        // Arrange
        var courier = OnboardSampleCourier();
        var challenge = _custodyService.GenerateLivenessChallenge(courier.Badge.CourierDid);
        var request = CreateValidCustodyRequest(
            challenge.ChallengeId,
            courier.Badge.CourierDid,
            AlgiersLat, AlgiersLon,
            AlgiersLat, AlgiersLon);

        // Act 1: First unlock succeeds
        var firstResult = _custodyService.AuthorizeCustodyUnlock(request, AuthoritySecret);
        firstResult.IsAuthorized.Should().BeTrue();

        // Act 2: Attempting to reuse the exact same challenge fails immediately
        var secondResult = _custodyService.AuthorizeCustodyUnlock(request, AuthoritySecret);
        secondResult.IsAuthorized.Should().BeFalse();
        secondResult.RejectionReason.Should().Contain("consumed");
    }

    [Theory]
    [InlineData(0.849)] // Just below threshold
    [InlineData(0.50)]  // Poor liveness
    [InlineData(0.12)]  // Static photo spoof
    public void AuthorizeCustodyUnlock_LivenessScoreBelowThreshold_FailsAntiSpoofing(double lowScore)
    {
        // Arrange
        var courier = OnboardSampleCourier();
        var challenge = _custodyService.GenerateLivenessChallenge(courier.Badge.CourierDid);
        var request = CreateValidCustodyRequest(
            challenge.ChallengeId,
            courier.Badge.CourierDid,
            AlgiersLat, AlgiersLon,
            AlgiersLat, AlgiersLon) with
        {
            BiometricLivenessScore = lowScore
        };

        // Act
        var result = _custodyService.AuthorizeCustodyUnlock(request, AuthoritySecret);

        // Assert
        result.IsAuthorized.Should().BeFalse();
        result.RejectionReason.Should().Contain("presentation attack");
    }

    [Theory]
    [InlineData(0.85)]  // Exactly at threshold
    [InlineData(0.92)]  // High confidence
    [InlineData(0.999)] // Excellent liveness
    public void AuthorizeCustodyUnlock_LivenessScoreAboveOrEqualThreshold_Succeeds(double passingScore)
    {
        // Arrange
        var courier = OnboardSampleCourier();
        var challenge = _custodyService.GenerateLivenessChallenge(courier.Badge.CourierDid);
        var request = CreateValidCustodyRequest(
            challenge.ChallengeId,
            courier.Badge.CourierDid,
            AlgiersLat, AlgiersLon,
            AlgiersLat, AlgiersLon) with
        {
            BiometricLivenessScore = passingScore
        };

        // Act
        var result = _custodyService.AuthorizeCustodyUnlock(request, AuthoritySecret);

        // Assert
        result.IsAuthorized.Should().BeTrue();
    }

    // =========================================================================
    // 7. Geofence Distance Calculation & Validation Tests
    // =========================================================================

    [Fact]
    public void AuthorizeCustodyUnlock_ExactLocation_CalculatesZeroDistanceAndSucceeds()
    {
        // Arrange
        var courier = OnboardSampleCourier();
        var challenge = _custodyService.GenerateLivenessChallenge(courier.Badge.CourierDid);
        var request = CreateValidCustodyRequest(
            challenge.ChallengeId,
            courier.Badge.CourierDid,
            AlgiersLat, AlgiersLon,
            AlgiersLat, AlgiersLon);

        // Act
        var result = _custodyService.AuthorizeCustodyUnlock(request, AuthoritySecret);

        // Assert
        result.IsAuthorized.Should().BeTrue();
        result.CustodyCertificateId.Should().NotBeNull();
    }

    [Fact]
    public void AuthorizeCustodyUnlock_Within50MetersGeofence_Succeeds()
    {
        // Arrange
        // ~22 meters difference in latitude (0.0002 deg lat ~ 22.2 meters)
        double courierLat = AlgiersLat + 0.0002;
        double courierLon = AlgiersLon;

        var courier = OnboardSampleCourier();
        var challenge = _custodyService.GenerateLivenessChallenge(courier.Badge.CourierDid);
        var request = CreateValidCustodyRequest(
            challenge.ChallengeId,
            courier.Badge.CourierDid,
            courierLat, courierLon,
            AlgiersLat, AlgiersLon);

        // Act
        var result = _custodyService.AuthorizeCustodyUnlock(request, AuthoritySecret);

        // Assert
        result.IsAuthorized.Should().BeTrue();
    }

    [Fact]
    public void AuthorizeCustodyUnlock_Exceeding50MetersGeofence_FailsWithViolation()
    {
        // Arrange
        // ~111 meters difference in latitude (0.0010 deg lat ~ 111 meters)
        double courierLat = AlgiersLat + 0.0010;
        double courierLon = AlgiersLon;

        var courier = OnboardSampleCourier();
        var challenge = _custodyService.GenerateLivenessChallenge(courier.Badge.CourierDid);
        var request = CreateValidCustodyRequest(
            challenge.ChallengeId,
            courier.Badge.CourierDid,
            courierLat, courierLon,
            AlgiersLat, AlgiersLon);

        // Act
        var result = _custodyService.AuthorizeCustodyUnlock(request, AuthoritySecret);

        // Assert
        result.IsAuthorized.Should().BeFalse();
        result.RejectionReason.Should().Contain("Geofence violation");
        result.RejectionReason.Should().Contain("exceeding maximum allowed 50 meters");
    }

    [Fact]
    public void CalculateHaversineDistanceMeters_KnownCoordinates_ComputesExpectedDistance()
    {
        // Known distance between Algiers (36.7538, 3.0588) and Oran (35.6987, -0.6349) is ~360 km
        double distance = BiometricCustodyUnlockService.CalculateHaversineDistanceMeters(
            36.7538, 3.0588,
            35.6987, -0.6349);

        distance.Should().BeInRange(350_000, 420_000);
    }

    [Fact]
    public void CalculateHaversineDistanceMeters_ZeroDistance_ReturnsZero()
    {
        double distance = BiometricCustodyUnlockService.CalculateHaversineDistanceMeters(
            36.7538, 3.0588,
            36.7538, 3.0588);

        distance.Should().BeApproximately(0.0, 0.0001);
    }

    // =========================================================================
    // 8. Cash Limits & Badge Status Custody Gating Tests
    // =========================================================================

    [Fact]
    public void AuthorizeCustodyUnlock_CashCollectedExceedsCourierFloatLimit_Fails()
    {
        // Arrange: Courier has Tier 1 limit (50,000 DZD)
        var courier = OnboardSampleCourier(CashFloatLimitTier.Tier1_LowRisk_50000DZD);
        var challenge = _custodyService.GenerateLivenessChallenge(courier.Badge.CourierDid);

        var request = CreateValidCustodyRequest(
            challenge.ChallengeId,
            courier.Badge.CourierDid,
            AlgiersLat, AlgiersLon,
            AlgiersLat, AlgiersLon) with
        {
            CashCollectedDzd = 75_000m // Exceeds 50,000 DZD limit
        };

        // Act
        var result = _custodyService.AuthorizeCustodyUnlock(request, AuthoritySecret);

        // Assert
        result.IsAuthorized.Should().BeFalse();
        result.RejectionReason.Should().Contain("exceeds authorized cash float limit");
    }

    [Fact]
    public void AuthorizeCustodyUnlock_CashCollectedWithinCourierFloatLimit_Succeeds()
    {
        // Arrange: Courier has Tier 1 limit (50,000 DZD)
        var courier = OnboardSampleCourier(CashFloatLimitTier.Tier1_LowRisk_50000DZD);
        var challenge = _custodyService.GenerateLivenessChallenge(courier.Badge.CourierDid);

        var request = CreateValidCustodyRequest(
            challenge.ChallengeId,
            courier.Badge.CourierDid,
            AlgiersLat, AlgiersLon,
            AlgiersLat, AlgiersLon) with
        {
            CashCollectedDzd = 42_500m // Within limit
        };

        // Act
        var result = _custodyService.AuthorizeCustodyUnlock(request, AuthoritySecret);

        // Assert
        result.IsAuthorized.Should().BeTrue();
    }

    [Fact]
    public void AuthorizeCustodyUnlock_MissingFacialTemplateHash_Fails()
    {
        var courier = OnboardSampleCourier();
        var challenge = _custodyService.GenerateLivenessChallenge(courier.Badge.CourierDid);
        var request = CreateValidCustodyRequest(challenge.ChallengeId, courier.Badge.CourierDid, AlgiersLat, AlgiersLon, AlgiersLat, AlgiersLon) with
        {
            FacialTemplateHash = ""
        };

        var result = _custodyService.AuthorizeCustodyUnlock(request, AuthoritySecret);
        result.IsAuthorized.Should().BeFalse();
        result.RejectionReason.Should().ContainEquivalentOf("facial template hash");
    }

    // =========================================================================
    // 9. Custody Handover Certificate Integrity Tests
    // =========================================================================

    [Fact]
    public void AuthorizeCustodyUnlock_GeneratesTamperProofCertificateWithValidCryptographicSignature()
    {
        // Arrange
        var courier = OnboardSampleCourier();
        var challenge = _custodyService.GenerateLivenessChallenge(courier.Badge.CourierDid);
        var request = CreateValidCustodyRequest(
            challenge.ChallengeId,
            courier.Badge.CourierDid,
            AlgiersLat, AlgiersLon,
            AlgiersLat, AlgiersLon);

        // Act
        var unlockResult = _custodyService.AuthorizeCustodyUnlock(request, AuthoritySecret);

        // Assert
        unlockResult.IsAuthorized.Should().BeTrue();
        unlockResult.CustodyCertificateId.Should().NotBeNull();
        unlockResult.AuthorizationCode.Should().StartWith("AUTH-");

        var cert = _custodyService.GetHandoverCertificate(unlockResult.CustodyCertificateId!);
        cert.Should().NotBeNull();
        cert!.CertificateId.Should().Be(unlockResult.CustodyCertificateId);
        cert.CourierDid.Should().Be(courier.Badge.CourierDid);
        cert.RecipientDid.Should().Be(request.RecipientDid);
        cert.WaybillNumber.Should().Be(request.ConsignmentWaybill);
        cert.CashAmountDzd.Should().Be(request.CashCollectedDzd);
        cert.CryptographicSignature.Should().NotBeNullOrWhiteSpace();

        // Verify certificate signature
        bool isSigValid = BiometricCustodyUnlockService.VerifyCertificateSignature(cert, AuthoritySecret);
        isSigValid.Should().BeTrue();
    }

    [Fact]
    public void VerifyCertificateSignature_TamperedWaybill_ReturnsFalse()
    {
        // Arrange
        var courier = OnboardSampleCourier();
        var challenge = _custodyService.GenerateLivenessChallenge(courier.Badge.CourierDid);
        var request = CreateValidCustodyRequest(
            challenge.ChallengeId,
            courier.Badge.CourierDid,
            AlgiersLat, AlgiersLon,
            AlgiersLat, AlgiersLon);

        var unlockResult = _custodyService.AuthorizeCustodyUnlock(request, AuthoritySecret);
        var cert = _custodyService.GetHandoverCertificate(unlockResult.CustodyCertificateId!)!;

        // Tamper with waybill
        var tamperedCert = cert with { WaybillNumber = "TAMPERED-WAYBILL-666" };

        // Act & Assert
        BiometricCustodyUnlockService.VerifyCertificateSignature(tamperedCert, AuthoritySecret).Should().BeFalse();
    }

    [Fact]
    public void VerifyCertificateSignature_TamperedCashAmount_ReturnsFalse()
    {
        // Arrange
        var courier = OnboardSampleCourier();
        var challenge = _custodyService.GenerateLivenessChallenge(courier.Badge.CourierDid);
        var request = CreateValidCustodyRequest(
            challenge.ChallengeId,
            courier.Badge.CourierDid,
            AlgiersLat, AlgiersLon,
            AlgiersLat, AlgiersLon);

        var unlockResult = _custodyService.AuthorizeCustodyUnlock(request, AuthoritySecret);
        var cert = _custodyService.GetHandoverCertificate(unlockResult.CustodyCertificateId!)!;

        // Tamper with cash amount
        var tamperedCert = cert with { CashAmountDzd = 999_999m };

        // Act & Assert
        BiometricCustodyUnlockService.VerifyCertificateSignature(tamperedCert, AuthoritySecret).Should().BeFalse();
    }

    [Fact]
    public void GetHandoverCertificate_NonExistentId_ReturnsNull()
    {
        _custodyService.GetHandoverCertificate("CERT-NONEXISTENT-999").Should().BeNull();
    }

    // =========================================================================
    // 10. Concurrency & Thread-Safety Tests
    // =========================================================================

    [Fact]
    public async Task ConcurrentCustodyUnlock_ThreadSafety_HandlesMultipleHandoverRequests()
    {
        // Arrange
        const int concurrentCouriers = 20;
        var courierList = new List<CourierOnboardingResult>();
        for (int i = 0; i < concurrentCouriers; i++)
        {
            string nin = $"1990160100{i:D8}";
            string license = $"DL-THREAD-{i:D8}";
            var req = CreateValidCourierRequest($"Courier #{i}", nin, license);
            courierList.Add(_courierService.OnboardCourier(req, TestIssuerSecret));
        }

        // Act: Parallel challenges and custody unlocks
        var tasks = courierList.Select(async c =>
        {
            await Task.Yield();
            var challenge = _custodyService.GenerateLivenessChallenge(c.Badge.CourierDid);
            var unlockReq = CreateValidCustodyRequest(
                challenge.ChallengeId,
                c.Badge.CourierDid,
                AlgiersLat, AlgiersLon,
                AlgiersLat, AlgiersLon);
            return _custodyService.AuthorizeCustodyUnlock(unlockReq, AuthoritySecret);
        });

        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().HaveCount(concurrentCouriers);
        results.All(r => r.IsAuthorized).Should().BeTrue();
        results.All(r => r.CustodyCertificateId != null).Should().BeTrue();

        // Verify all generated certificates exist and are validly signed
        foreach (var r in results)
        {
            var cert = _custodyService.GetHandoverCertificate(r.CustodyCertificateId!);
            cert.Should().NotBeNull();
            BiometricCustodyUnlockService.VerifyCertificateSignature(cert!, AuthoritySecret).Should().BeTrue();
        }
    }

    [Fact]
    public async Task ConcurrentRevocations_ThreadSafety_MaintainsConsistentState()
    {
        // Arrange
        const int credentialCount = 25;
        var credIds = Enumerable.Range(1, credentialCount)
            .Select(i => $"urn:uuid:concurrent-cred-{i:D4}")
            .ToList();

        // Act: Concurrently revoke all credentials
        var tasks = credIds.Select(async id =>
        {
            await Task.Yield();
            return _engine.RevokeCredential(id, $"Revocation reason for {id}");
        });

        var records = await Task.WhenAll(tasks);

        // Assert
        records.Should().HaveCount(credentialCount);
        foreach (var id in credIds)
        {
            _engine.IsCredentialRevoked(id).Should().BeTrue();
            _engine.GetRevocationRecord(id).Should().NotBeNull();
        }
    }

    // =========================================================================
    // 11. Dependency Injection ServiceCollection Descriptor Assertions
    // =========================================================================

    [Fact]
    public void AddVerifiableIdentityServices_RegistersExpectedSingletonsInServiceCollection()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act: Call extension method without calling BuildServiceProvider()
        services.AddVerifiableIdentityServices();

        // Assert: Assert directly on ServiceCollection descriptors (Rule 5)
        services.Should().Contain(d =>
            d.ServiceType == typeof(IVerifiableCredentialEngine) &&
            d.ImplementationType == typeof(VerifiableCredentialEngine) &&
            d.Lifetime == ServiceLifetime.Singleton);

        services.Should().Contain(d =>
            d.ServiceType == typeof(ICourierOnboardingAttestationService) &&
            d.ImplementationType == typeof(CourierOnboardingAttestationService) &&
            d.Lifetime == ServiceLifetime.Singleton);

        services.Should().Contain(d =>
            d.ServiceType == typeof(IBiometricCustodyUnlockService) &&
            d.ImplementationType == typeof(BiometricCustodyUnlockService) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    // =========================================================================
    // Helper Methods
    // =========================================================================

    private static CourierOnboardingRequest CreateValidCourierRequest(
        string name,
        string nin,
        string license,
        CashFloatLimitTier tier = CashFloatLimitTier.Tier1_LowRisk_50000DZD)
    {
        return new CourierOnboardingRequest
        {
            FullName = name,
            NationalIdNumber = nin,
            DriverLicenseNumber = license,
            VehiclePlateNumber = "00999-116-16",
            VehicleType = "Van",
            Phone = "+213550998877",
            CarrierPartnerId = "PARTNER-EXPRESS-01",
            RequestedCashFloatTier = tier
        };
    }

    private static long _sampleCounter = 1000;

    private CourierOnboardingResult OnboardSampleCourier(CashFloatLimitTier tier = CashFloatLimitTier.Tier2_Standard_250000DZD)
    {
        long num = Interlocked.Increment(ref _sampleCounter);
        string nin = $"19901601{num:D10}";
        string license = $"DL-TEST-{num:D8}";
        var req = CreateValidCourierRequest($"Courier-{num}", nin, license, tier);
        return _courierService.OnboardCourier(req, TestIssuerSecret);
    }

    private static CustodyUnlockRequest CreateValidCustodyRequest(
        string challengeId,
        string courierDid,
        double courierLat,
        double courierLon,
        double destLat,
        double destLon)
    {
        return new CustodyUnlockRequest
        {
            ChallengeId = challengeId,
            CourierDid = courierDid,
            RecipientDid = "did:key:z6MkuRecipientConsumerIdentity998877",
            ConsignmentWaybill = $"WB-DZ-{Guid.NewGuid():N}"[..18],
            CashCollectedDzd = 15_000m,
            BiometricLivenessScore = 0.94,
            FacialTemplateHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            Latitude = courierLat,
            Longitude = courierLon,
            DestinationLatitude = destLat,
            DestinationLongitude = destLon
        };
    }
}
