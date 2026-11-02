using FluentAssertions;
using SGrowing.Api.Modules.Security.VerifiableCredentials;
using Xunit;

namespace SGrowing.Tests.Security;

public sealed class VerifiableCredentialsTests
{
    private readonly DidDocumentResolverEngine _didResolver = new();
    private readonly VerifiableCredentialIssuerAndValidator _vcService = new();
    private readonly VendorComplianceVerificationGateway _gateway;

    public VerifiableCredentialsTests()
    {
        _gateway = new VendorComplianceVerificationGateway(_vcService);
    }

    #region DID Document Resolution Tests

    [Fact]
    public void GenerateDidKey_ReturnsValidDidDocument()
    {
        var (did, privKey, pubKey) = _didResolver.GenerateDidKey();

        did.Should().StartWith("did:key:z");
        privKey.Should().NotBeEmpty();
        pubKey.Should().NotBeEmpty();

        var doc = _didResolver.Resolve(did);
        doc.Should().NotBeNull();
        doc.Id.Should().Be(did);
        doc.VerificationMethod.Should().ContainSingle();
        doc.VerificationMethod[0].Controller.Should().Be(did);
        doc.Authentication.Should().ContainSingle();
    }

    [Fact]
    public void Resolve_InvalidScheme_ThrowsNotSupportedException()
    {
        var act = () => _didResolver.Resolve("did:ion:EiD3487fhs");
        act.Should().Throw<NotSupportedException>()
            .WithMessage("*Only 'did:key' scheme is natively resolved*");
    }

    #endregion

    #region Verifiable Credential Issuance & Verification Tests

    [Fact]
    public void IssueCredential_ValidSignature_VerifiesSuccessfully()
    {
        var (issuerDid, issuerPrivKey, _) = _didResolver.GenerateDidKey();
        var (subjectDid, _, _) = _didResolver.GenerateDidKey();

        var req = new IssueCredentialRequest(
            IssuerDid: issuerDid,
            IssuerPrivateKeySeed: issuerPrivKey,
            SubjectDid: subjectDid,
            VendorLegalName: "Atlas Maghreb Agro Export SARL",
            TaxRegistrationNumber: "DZ-NIF-0998374829",
            CountryOfIncorporation: "DZA",
            CertificationType: CredentialComplianceType.Iso9001QualityManagement,
            CertificationAuthority: "Afnor International Certification",
            ValidityDuration: TimeSpan.FromDays(365),
            RevocationListIndex: 12
        );

        var vc = _vcService.IssueCredential(req);

        vc.Should().NotBeNull();
        vc.IssuerDid.Should().Be(issuerDid);
        vc.CredentialSubject.Id.Should().Be(subjectDid);
        vc.Proof.ProofValueHex.Should().NotBeNullOrWhiteSpace();

        // Verify
        var verification = _vcService.VerifyCredential(vc);
        verification.IsValid.Should().BeTrue();
        verification.Outcome.Should().Be(VerificationOutcome.Valid);
    }

    [Fact]
    public void VerifyCredential_TamperedSubject_FailsSignature()
    {
        var (issuerDid, issuerPrivKey, _) = _didResolver.GenerateDidKey();
        var (subjectDid, _, _) = _didResolver.GenerateDidKey();

        var vc = _vcService.IssueCredential(new IssueCredentialRequest(
            IssuerDid: issuerDid,
            IssuerPrivateKeySeed: issuerPrivKey,
            SubjectDid: subjectDid,
            VendorLegalName: "Atlas Logistics SPA",
            TaxRegistrationNumber: "DZ-NIF-11223344",
            CountryOfIncorporation: "DZA",
            CertificationType: CredentialComplianceType.CommercialTaxRegistration,
            CertificationAuthority: "Ministry of Finance"
        ));

        // Tamper with legal name without re-signing
        var tamperedSubject = vc.CredentialSubject with { VendorLegalName = "Fraudulent Clone Corp" };
        var tamperedVc = vc with { CredentialSubject = tamperedSubject };

        var verification = _vcService.VerifyCredential(tamperedVc);
        verification.IsValid.Should().BeFalse();
        verification.Outcome.Should().Be(VerificationOutcome.InvalidSignature);
    }

    [Fact]
    public void VerifyCredential_ExpiredCredential_ReturnsExpiredOutcome()
    {
        var (issuerDid, issuerPrivKey, _) = _didResolver.GenerateDidKey();
        var (subjectDid, _, _) = _didResolver.GenerateDidKey();

        var vc = _vcService.IssueCredential(new IssueCredentialRequest(
            IssuerDid: issuerDid,
            IssuerPrivateKeySeed: issuerPrivKey,
            SubjectDid: subjectDid,
            VendorLegalName: "Dated Dates Co",
            TaxRegistrationNumber: "DZ-NIF-556677",
            CountryOfIncorporation: "DZA",
            CertificationType: CredentialComplianceType.PhytosanitaryExportPermit,
            CertificationAuthority: "Ministry of Agriculture",
            ValidityDuration: TimeSpan.FromSeconds(-10) // Expired 10 seconds ago
        ));

        var verification = _vcService.VerifyCredential(vc);
        verification.IsValid.Should().BeFalse();
        verification.Outcome.Should().Be(VerificationOutcome.Expired);
    }

    [Fact]
    public void VerifyCredential_RevokedBitstring_ReturnsRevokedOutcome()
    {
        var (issuerDid, issuerPrivKey, _) = _didResolver.GenerateDidKey();
        var (subjectDid, _, _) = _didResolver.GenerateDidKey();

        var vc = _vcService.IssueCredential(new IssueCredentialRequest(
            IssuerDid: issuerDid,
            IssuerPrivateKeySeed: issuerPrivKey,
            SubjectDid: subjectDid,
            VendorLegalName: "Food Corp",
            TaxRegistrationNumber: "DZ-NIF-998877",
            CountryOfIncorporation: "DZA",
            CertificationType: CredentialComplianceType.HalalCertification,
            CertificationAuthority: "Algerian Halal Institute",
            RevocationListIndex: 42
        ));

        // Initially valid
        var initialCheck = _vcService.VerifyCredential(vc);
        initialCheck.IsValid.Should().BeTrue();

        // Revoke credential at index 42
        _vcService.RevokeCredential(vc.Status!.RevocationListCredentialUri, 42);

        // Verification must now fail
        var revokedCheck = _vcService.VerifyCredential(vc);
        revokedCheck.IsValid.Should().BeFalse();
        revokedCheck.Outcome.Should().Be(VerificationOutcome.Revoked);
    }

    #endregion

    #region Vendor Compliance Gateway Tests

    [Fact]
    public void AssessVendorCompliance_AllCertificationsPresent_ReturnsCompliant()
    {
        var (issuerDid, issuerPrivKey, _) = _didResolver.GenerateDidKey();
        var (vendorDid, _, _) = _didResolver.GenerateDidKey();

        var taxCred = _vcService.IssueCredential(new IssueCredentialRequest(
            IssuerDid: issuerDid,
            IssuerPrivateKeySeed: issuerPrivKey,
            SubjectDid: vendorDid,
            VendorLegalName: "Premium Olive Oil Ltd",
            TaxRegistrationNumber: "DZ-TAX-1010",
            CountryOfIncorporation: "DZA",
            CertificationType: CredentialComplianceType.CommercialTaxRegistration,
            CertificationAuthority: "Tax Authority"
        ));

        var halalCred = _vcService.IssueCredential(new IssueCredentialRequest(
            IssuerDid: issuerDid,
            IssuerPrivateKeySeed: issuerPrivKey,
            SubjectDid: vendorDid,
            VendorLegalName: "Premium Olive Oil Ltd",
            TaxRegistrationNumber: "DZ-TAX-1010",
            CountryOfIncorporation: "DZA",
            CertificationType: CredentialComplianceType.HalalCertification,
            CertificationAuthority: "Halal Board"
        ));

        var report = _gateway.AssessVendorCompliance(
            vendorDid: vendorDid,
            vendorLegalName: "Premium Olive Oil Ltd",
            requiredCertifications: [CredentialComplianceType.CommercialTaxRegistration, CredentialComplianceType.HalalCertification],
            presentedCredentials: [taxCred, halalCred]
        );

        report.Status.Should().Be(VendorComplianceStatus.Compliant);
        report.MissingCertifications.Should().BeEmpty();
        report.VerifiedCertifications.Should().HaveCount(2);
    }

    [Fact]
    public void AssessVendorCompliance_MissingRequiredCert_ReturnsConditionalOrNonCompliant()
    {
        var (issuerDid, issuerPrivKey, _) = _didResolver.GenerateDidKey();
        var (vendorDid, _, _) = _didResolver.GenerateDidKey();

        var taxCred = _vcService.IssueCredential(new IssueCredentialRequest(
            IssuerDid: issuerDid,
            IssuerPrivateKeySeed: issuerPrivKey,
            SubjectDid: vendorDid,
            VendorLegalName: "Mediterranean Logistics",
            TaxRegistrationNumber: "DZ-TAX-5544",
            CountryOfIncorporation: "DZA",
            CertificationType: CredentialComplianceType.CommercialTaxRegistration,
            CertificationAuthority: "Tax Authority"
        ));

        // Requires AEO Customs certification which is missing
        var report = _gateway.AssessVendorCompliance(
            vendorDid: vendorDid,
            vendorLegalName: "Mediterranean Logistics",
            requiredCertifications: [CredentialComplianceType.CommercialTaxRegistration, CredentialComplianceType.AeoCustomsAuthorizedEconomicOperator],
            presentedCredentials: [taxCred]
        );

        // Has tax cert, but missing AEO -> ConditionalApproval
        report.Status.Should().Be(VendorComplianceStatus.ConditionalApproval);
        report.MissingCertifications.Should().Contain(CredentialComplianceType.AeoCustomsAuthorizedEconomicOperator);
    }

    #endregion
}
