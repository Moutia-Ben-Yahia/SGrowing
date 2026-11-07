using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Security.PrivacyGovernance;

/// <summary>
/// Consent categories defined under Saudi Personal Data Protection Law (PDPL) and GCC Data Privacy Regulations.
/// </summary>
public enum PdplConsentType
{
    MarketingEmail,
    MarketingSms,
    WhatsAppBroadcast,
    ThirdPartyAnalytics,
    TelemarketingCall,
    CrossBorderTransfer
}

/// <summary>
/// State of a given consent authorization.
/// </summary>
public enum ConsentStatus
{
    Granted,
    Revoked,
    Expired
}

/// <summary>
/// Regulatory privacy subject rights requests under Saudi PDPL (Arts. 22-24) & GCC Frameworks.
/// </summary>
public enum PrivacyRequestType
{
    RightToBeForgotten_Erasure,
    SubjectAccessRequest_Export,
    DataRectification,
    ConsentWithdrawal
}

/// <summary>
/// Lifecycle status of a Data Protection Officer (DPO) reviewed privacy request.
/// </summary>
public enum PrivacyRequestStatus
{
    PendingDpoReview,
    Processing,
    Completed,
    Rejected
}

/// <summary>
/// Immutable cryptographically signed consent ledger record complying with Saudi PDPL Article 5 & 13.
/// </summary>
public record PdplConsentRecord(
    Guid ConsentId,
    Guid TenantId,
    Guid ContactId,
    string ContactEmail,
    string ContactPhone,
    PdplConsentType Type,
    ConsentStatus Status,
    string LegalBasisDescription,
    string SourceIp,
    string DigitalSignatureSha256,
    DateTime RecordedAtUtc,
    DateTime? ExpiresAtUtc
);

/// <summary>
/// Formal regulatory privacy subject access/erasure inquiry managed by the DPO.
/// </summary>
public record PrivacySubjectRequest(
    Guid RequestId,
    Guid TenantId,
    Guid ContactId,
    string RequesterEmail,
    PrivacyRequestType RequestType,
    PrivacyRequestStatus Status,
    string Justification,
    string DpoNotes,
    DateTime SubmittedAtUtc,
    DateTime? ResolvedAtUtc,
    string? ExportDownloadUrl,
    int RedactedEntitiesCount
);

/// <summary>
/// Real-time sovereign data residency and PDPL/NCA audit report.
/// </summary>
public record DataResidencyComplianceReport(
    Guid TenantId,
    string SovereignRegion,
    bool IsSovereignEnclaveEnforced,
    int TotalConsentsRecorded,
    int ActiveConsents,
    int PendingPrivacyRequests,
    int CompletedErasuresCount,
    DateTime LastAuditedAtUtc,
    List<string> CertifiedRegulations
);

/// <summary>
/// Payload to record or revoke an explicit consent item.
/// </summary>
public record RecordConsentRequest(
    Guid TenantId,
    Guid ContactId,
    string ContactEmail,
    string ContactPhone,
    PdplConsentType Type,
    ConsentStatus Status,
    string LegalBasis,
    string SourceIp
);

/// <summary>
/// Payload to submit a privacy subject rights request.
/// </summary>
public record SubmitPrivacyRequest(
    Guid TenantId,
    Guid ContactId,
    string RequesterEmail,
    PrivacyRequestType RequestType,
    string Justification
);

/// <summary>
/// Payload for DPO resolution of a privacy subject rights request.
/// </summary>
public record ResolvePrivacyRequest(
    Guid TenantId,
    Guid RequestId,
    PrivacyRequestStatus Resolution,
    string DpoNotes
);

/// <summary>
/// Request DTO for resolving a request via route parameters.
/// </summary>
public record ResolvePrivacyRequestDto(
    PrivacyRequestStatus Resolution,
    string DpoNotes
);
