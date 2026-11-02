namespace SGrowing.Api.Modules.Security.VerifiableCredentials;

public enum CredentialComplianceType
{
    Iso9001QualityManagement,
    HalalCertification,
    PhytosanitaryExportPermit,
    AeoCustomsAuthorizedEconomicOperator,
    EsgCarbonNeutralityAudit,
    CommercialTaxRegistration
}

public enum VerificationOutcome
{
    Valid,
    InvalidSignature,
    Expired,
    Revoked,
    IssuerUntrusted,
    Malformed
}

public enum VendorComplianceStatus
{
    Compliant,
    ConditionalApproval,
    NonCompliant
}

public sealed record DidVerificationMethod(
    string Id,
    string Type,
    string Controller,
    string PublicKeyMultibase
);

public sealed record DidDocument(
    string Id,
    IReadOnlyList<string> Context,
    IReadOnlyList<DidVerificationMethod> VerificationMethod,
    IReadOnlyList<string> Authentication,
    IReadOnlyList<string> AssertionMethod
);

public sealed record CredentialSubject(
    string Id, // Subject DID (e.g., did:key:...)
    string VendorLegalName,
    string TaxRegistrationNumber,
    string CountryOfIncorporation,
    CredentialComplianceType CertificationType,
    string CertificationAuthority,
    IReadOnlyDictionary<string, string> Attributes
);

public sealed record CredentialStatus(
    string Id,
    string Type, // "RevocationList2020Status"
    int RevocationListIndex,
    string RevocationListCredentialUri
);

public sealed record ProofObject(
    string Type, // "Ed25519Signature2020"
    DateTime CreatedUtc,
    string VerificationMethod,
    string ProofPurpose, // "assertionMethod"
    string ProofValueHex
);

public sealed record VerifiableCredential(
    string Id,
    IReadOnlyList<string> Context,
    IReadOnlyList<string> Type,
    string IssuerDid,
    DateTime IssuanceDateUtc,
    DateTime? ExpirationDateUtc,
    CredentialSubject CredentialSubject,
    CredentialStatus? Status,
    ProofObject Proof
);

public sealed record IssueCredentialRequest(
    string IssuerDid,
    byte[] IssuerPrivateKeySeed,
    string SubjectDid,
    string VendorLegalName,
    string TaxRegistrationNumber,
    string CountryOfIncorporation,
    CredentialComplianceType CertificationType,
    string CertificationAuthority,
    IReadOnlyDictionary<string, string>? Attributes = null,
    TimeSpan? ValidityDuration = null,
    int? RevocationListIndex = null
);

public sealed record CredentialVerificationResult(
    string CredentialId,
    bool IsValid,
    VerificationOutcome Outcome,
    string Message,
    DateTime VerifiedAtUtc
);

public sealed record VendorComplianceReport(
    string VendorDid,
    string VendorLegalName,
    VendorComplianceStatus Status,
    IReadOnlyList<CredentialComplianceType> VerifiedCertifications,
    IReadOnlyList<CredentialComplianceType> MissingCertifications,
    IReadOnlyList<CredentialVerificationResult> AuditResults,
    DateTime EvaluatedAtUtc
);
