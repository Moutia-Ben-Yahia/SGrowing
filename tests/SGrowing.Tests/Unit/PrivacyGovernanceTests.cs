using System;
using System.Collections.Generic;
using FluentAssertions;
using SGrowing.Api.Modules.Security.PrivacyGovernance;
using Xunit;

namespace SGrowing.Tests.Unit;

public class PrivacyGovernanceTests
{
    [Fact]
    public void ComputeAndVerifySignature_GeneratesDeterministicSha256_AndMatchesExpected()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var email = "sultan.almutairi@al-diriyah.sa";
        var phone = "+966551122334";
        var type = PdplConsentType.MarketingSms;
        var status = ConsentStatus.Granted;
        var basis = "PDPL Article 5: Express Consent for SMS Alerts";
        var ip = "158.140.99.12";
        var timestamp = new DateTime(2026, 11, 7, 12, 0, 0, DateTimeKind.Utc);

        // Act
        var hash = PrivacyGovernanceService.ComputeConsentSha256(
            tenantId, contactId, email, type, status, basis, ip, timestamp
        );

        // Assert
        hash.Should().NotBeNullOrWhiteSpace();
        hash.Length.Should().Be(64); // 32 bytes hex encoded = 64 chars

        var record = new PdplConsentRecord(
            ConsentId: Guid.NewGuid(),
            TenantId: tenantId,
            ContactId: contactId,
            ContactEmail: email,
            ContactPhone: phone,
            Type: type,
            Status: status,
            LegalBasisDescription: basis,
            SourceIp: ip,
            DigitalSignatureSha256: hash,
            RecordedAtUtc: timestamp,
            ExpiresAtUtc: timestamp.AddYears(1)
        );

        PrivacyGovernanceService.VerifyConsentSignature(record).Should().BeTrue();

        // Tampering test: modify status without updating hash
        var tamperedRecord = record with { Status = ConsentStatus.Revoked };
        PrivacyGovernanceService.VerifyConsentSignature(tamperedRecord).Should().BeFalse();
    }

    [Fact]
    public void RecordConsent_ValidRequest_CreatesConsentRecordWithSignature()
    {
        // Arrange
        var service = new PrivacyGovernanceService();
        var tenantId = Guid.NewGuid();
        var contactId = Guid.NewGuid();

        var request = new RecordConsentRequest(
            TenantId: tenantId,
            ContactId: contactId,
            ContactEmail: "noura.alharbi@neom.tech",
            ContactPhone: "+966549988776",
            Type: PdplConsentType.WhatsAppBroadcast,
            Status: ConsentStatus.Granted,
            LegalBasis: "Saudi PDPL Article 13: Direct WhatsApp Opt-in",
            SourceIp: "10.200.4.15"
        );

        // Act
        var created = service.RecordConsent(request);

        // Assert
        created.Should().NotBeNull();
        created.ConsentId.Should().NotBeEmpty();
        created.TenantId.Should().Be(tenantId);
        created.ContactId.Should().Be(contactId);
        created.ContactEmail.Should().Be("noura.alharbi@neom.tech");
        created.Status.Should().Be(ConsentStatus.Granted);
        created.Type.Should().Be(PdplConsentType.WhatsAppBroadcast);
        created.ExpiresAtUtc.Should().NotBeNull();
        created.ExpiresAtUtc.Should().BeAfter(created.RecordedAtUtc);

        PrivacyGovernanceService.VerifyConsentSignature(created).Should().BeTrue();

        var records = service.GetConsents(tenantId, contactId);
        records.Should().ContainSingle(c => c.ConsentId == created.ConsentId);
    }

    [Fact]
    public void ConsentRevocation_WhenNewRevocationRecorded_RevokesPreviousActiveConsent()
    {
        // Arrange
        var service = new PrivacyGovernanceService();
        var tenantId = Guid.NewGuid();
        var contactId = Guid.NewGuid();

        // 1. Grant Email marketing consent
        service.RecordConsent(new RecordConsentRequest(
            TenantId: tenantId,
            ContactId: contactId,
            ContactEmail: "yousef.alqahtani@saudi-aramco.sa",
            ContactPhone: "+966503344556",
            Type: PdplConsentType.MarketingEmail,
            Status: ConsentStatus.Granted,
            LegalBasis: "PDPL Article 5 Opt-In",
            SourceIp: "10.0.1.50"
        ));

        var initialList = service.GetConsents(tenantId, contactId);
        initialList.Should().ContainSingle(c => c.Type == PdplConsentType.MarketingEmail && c.Status == ConsentStatus.Granted);

        // 2. Revoke consent
        var revokedRecord = service.RecordConsent(new RecordConsentRequest(
            TenantId: tenantId,
            ContactId: contactId,
            ContactEmail: "yousef.alqahtani@saudi-aramco.sa",
            ContactPhone: "+966503344556",
            Type: PdplConsentType.MarketingEmail,
            Status: ConsentStatus.Revoked,
            LegalBasis: "PDPL Article 5: Subject Right of Withdrawal",
            SourceIp: "10.0.1.50"
        ));

        // Assert
        revokedRecord.Status.Should().Be(ConsentStatus.Revoked);
        var updatedList = service.GetConsents(tenantId, contactId);
        // All email records for this contact should now be Revoked
        updatedList.Where(c => c.Type == PdplConsentType.MarketingEmail)
            .Should().OnlyContain(c => c.Status == ConsentStatus.Revoked);
    }

    [Fact]
    public void SubjectAccessRequest_Resolution_GeneratesCryptographicExportUrl()
    {
        // Arrange
        var service = new PrivacyGovernanceService();
        var tenantId = Guid.NewGuid();
        var contactId = Guid.NewGuid();

        var submitted = service.SubmitPrivacyRequest(new SubmitPrivacyRequest(
            TenantId: tenantId,
            ContactId: contactId,
            RequesterEmail: "layla.alrashid@redsea.sa",
            RequestType: PrivacyRequestType.SubjectAccessRequest_Export,
            Justification: "Subject access request for all stored CRM, analytics, and messaging records per PDPL Article 22."
        ));

        submitted.Status.Should().Be(PrivacyRequestStatus.PendingDpoReview);
        submitted.ExportDownloadUrl.Should().BeNull();

        // Act
        var resolved = service.ResolvePrivacyRequest(new ResolvePrivacyRequest(
            TenantId: tenantId,
            RequestId: submitted.RequestId,
            Resolution: PrivacyRequestStatus.Completed,
            DpoNotes: "Verified identity via National IAM portal; SAR package generated."
        ));

        // Assert
        resolved.Status.Should().Be(PrivacyRequestStatus.Completed);
        resolved.ResolvedAtUtc.Should().NotBeNull();
        resolved.ExportDownloadUrl.Should().NotBeNullOrWhiteSpace();
        resolved.ExportDownloadUrl.Should().StartWith("https://storage.sa-central-1.cloud.sgrowing.net/sar-exports/");
        resolved.ExportDownloadUrl.Should().EndWith("_sar.enc");
    }

    [Fact]
    public void RightToBeForgotten_Resolution_CalculatesRedactedEntitiesCount_AndRevokesAllActiveConsents()
    {
        // Arrange
        var service = new PrivacyGovernanceService();
        var tenantId = Guid.NewGuid();
        var contactId = Guid.NewGuid();

        // Grant multiple consents for contact
        service.RecordConsent(new RecordConsentRequest(
            TenantId: tenantId,
            ContactId: contactId,
            ContactEmail: "tariq.alghamdi@riyadh.sa",
            ContactPhone: "+966567788990",
            Type: PdplConsentType.MarketingEmail,
            Status: ConsentStatus.Granted,
            LegalBasis: "Opt-in",
            SourceIp: "127.0.0.1"
        ));
        service.RecordConsent(new RecordConsentRequest(
            TenantId: tenantId,
            ContactId: contactId,
            ContactEmail: "tariq.alghamdi@riyadh.sa",
            ContactPhone: "+966567788990",
            Type: PdplConsentType.TelemarketingCall,
            Status: ConsentStatus.Granted,
            LegalBasis: "Opt-in",
            SourceIp: "127.0.0.1"
        ));

        var req = service.SubmitPrivacyRequest(new SubmitPrivacyRequest(
            TenantId: tenantId,
            ContactId: contactId,
            RequesterEmail: "tariq.alghamdi@riyadh.sa",
            RequestType: PrivacyRequestType.RightToBeForgotten_Erasure,
            Justification: "Complete deletion requested under PDPL Article 24."
        ));

        // Act
        var resolved = service.ResolvePrivacyRequest(new ResolvePrivacyRequest(
            TenantId: tenantId,
            RequestId: req.RequestId,
            Resolution: PrivacyRequestStatus.Completed,
            DpoNotes: "Right to be forgotten erasure executed across all distributed stores."
        ));

        // Assert
        resolved.Status.Should().Be(PrivacyRequestStatus.Completed);
        resolved.RedactedEntitiesCount.Should().BeGreaterThan(0);

        // Verify active consents are now revoked
        var consents = service.GetConsents(tenantId, contactId);
        consents.Should().OnlyContain(c => c.Status == ConsentStatus.Revoked);
    }

    [Fact]
    public void DataResidencyAudit_CalculatesMetrics_AndCertifiesSaudiRegulations()
    {
        // Arrange
        var service = new PrivacyGovernanceService();
        var tenantId = PrivacyGovernanceService.DefaultTenantId;

        // Act
        var audit = service.GetDataResidencyAudit(tenantId);

        // Assert
        audit.Should().NotBeNull();
        audit.TenantId.Should().Be(tenantId);
        audit.SovereignRegion.Should().Contain("sa-central-1");
        audit.SovereignRegion.Should().Contain("Riyadh");
        audit.IsSovereignEnclaveEnforced.Should().BeTrue();
        audit.TotalConsentsRecorded.Should().BeGreaterThanOrEqualTo(5);
        audit.ActiveConsents.Should().BeGreaterThanOrEqualTo(3);
        audit.PendingPrivacyRequests.Should().BeGreaterThanOrEqualTo(2);
        audit.CompletedErasuresCount.Should().BeGreaterThanOrEqualTo(1);

        audit.CertifiedRegulations.Should().Contain("Saudi Personal Data Protection Law (PDPL - Royal Decree M/19)");
        audit.CertifiedRegulations.Should().Contain("NCA Essential Cybersecurity Controls (ECC-1:2018)");
        audit.CertifiedRegulations.Should().Contain("GCC Cross-Border Data Privacy Framework");
    }

    [Fact]
    public void TenantIsolation_PreventsCrossTenantAccess()
    {
        // Arrange
        var service = new PrivacyGovernanceService();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var contactA = Guid.NewGuid();

        // 1. Tenant A records consent and submits request
        var consentA = service.RecordConsent(new RecordConsentRequest(
            TenantId: tenantA,
            ContactId: contactA,
            ContactEmail: "user.a@tenant-a.sa",
            ContactPhone: "+966512345678",
            Type: PdplConsentType.ThirdPartyAnalytics,
            Status: ConsentStatus.Granted,
            LegalBasis: "Explicit agreement",
            SourceIp: "10.10.1.1"
        ));

        var reqA = service.SubmitPrivacyRequest(new SubmitPrivacyRequest(
            TenantId: tenantA,
            ContactId: contactA,
            RequesterEmail: "user.a@tenant-a.sa",
            RequestType: PrivacyRequestType.DataRectification,
            Justification: "Correct typo in profile"
        ));

        // Assert Tenant B cannot see Tenant A's consents or requests
        var consentsB = service.GetConsents(tenantB, contactA);
        consentsB.Should().BeEmpty();

        var allConsentsB = service.GetAllConsents(tenantB);
        allConsentsB.Should().NotContain(c => c.ConsentId == consentA.ConsentId);

        var requestsB = service.GetPrivacyRequests(tenantB);
        requestsB.Should().NotContain(r => r.RequestId == reqA.RequestId);

        // Assert Tenant B attempting to resolve Tenant A's request throws KeyNotFoundException
        var resolveAction = () => service.ResolvePrivacyRequest(new ResolvePrivacyRequest(
            TenantId: tenantB,
            RequestId: reqA.RequestId,
            Resolution: PrivacyRequestStatus.Completed,
            DpoNotes: "Illegitimate cross-tenant resolution attempt"
        ));

        resolveAction.Should().Throw<KeyNotFoundException>()
            .WithMessage("*Cross-tenant access denied*");
    }
}
