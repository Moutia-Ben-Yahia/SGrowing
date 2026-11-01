using System.Text.Json.Serialization;

namespace SGrowing.Api.Modules.Security.VerifiableIdentity;

// ==========================================
// 1. W3C DID Core Models
// ==========================================

/// <summary>
/// Cryptographic verification method embedded in a W3C DID document.
/// </summary>
public record VerificationMethod
{
    public required string Id { get; init; }
    public required string Type { get; init; }
    public required string Controller { get; init; }
    public required string PublicKeyHex { get; init; }
    public required string PublicKeyMultibase { get; init; }
}

/// <summary>
/// W3C Decentralized Identifier (DID) document conforming to W3C DID v1.0.
/// </summary>
public record DidDocument
{
    public required string Id { get; init; }
    public IReadOnlyList<VerificationMethod> VerificationMethod { get; init; } = [];
    public IReadOnlyList<string> Authentication { get; init; } = [];
    public IReadOnlyList<string> AssertionMethod { get; init; } = [];
    public DateTimeOffset Created { get; init; } = DateTimeOffset.UtcNow;
}

// ==========================================
// 2. W3C Verifiable Credential Models
// ==========================================

/// <summary>
/// Cryptographic proof attached to a W3C Verifiable Credential.
/// </summary>
public record CredentialProof
{
    public required string Type { get; init; } = "Ed25519Signature2020";
    public DateTimeOffset Created { get; init; } = DateTimeOffset.UtcNow;
    public required string VerificationMethod { get; init; }
    public required string ProofPurpose { get; init; } = "assertionMethod";
    public string? Jws { get; init; }
    public required string SignatureHex { get; init; }
}

/// <summary>
/// W3C Verifiable Credential (VC) v1.1 / v2.0 representation.
/// </summary>
public record VerifiableCredential
{
    [JsonPropertyName("@context")]
    public IReadOnlyList<string> Context { get; init; } = ["https://www.w3.org/2018/credentials/v1"];

    public required string Id { get; init; }
    public IReadOnlyList<string> Type { get; init; } = ["VerifiableCredential"];
    public required string Issuer { get; init; }
    public DateTimeOffset IssuanceDate { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ExpirationDate { get; init; }
    public IDictionary<string, object> CredentialSubject { get; init; } = new Dictionary<string, object>();
    public CredentialProof? CredentialProof { get; init; }
}

/// <summary>
/// Result of verifying a W3C Verifiable Credential.
/// </summary>
public record CredentialVerificationResult
{
    public bool IsValid { get; init; }
    public bool IsExpired { get; init; }
    public bool IsRevoked { get; init; }
    public bool IsSignatureValid { get; init; }
    public IReadOnlyList<string> ValidationErrors { get; init; } = [];
}

/// <summary>
/// Audit record of a revoked credential.
/// </summary>
public record RevocationRecord
{
    public required string CredentialId { get; init; }
    public DateTimeOffset RevocationDate { get; init; } = DateTimeOffset.UtcNow;
    public required string Reason { get; init; }
}

// ==========================================
// 3. Courier Onboarding & Badge Models
// ==========================================

/// <summary>
/// Cash float authorization tiers for couriers handling Cash-on-Delivery (COD).
/// </summary>
public enum CashFloatLimitTier
{
    Tier1_LowRisk_50000DZD = 1,
    Tier2_Standard_250000DZD = 2,
    Tier3_HighValue_1000000DZD = 3
}

/// <summary>
/// Request to onboard a new courier and issue decentralized biometric and license credentials.
/// </summary>
public record CourierOnboardingRequest
{
    public required string FullName { get; init; }
    public required string NationalIdNumber { get; init; }
    public required string DriverLicenseNumber { get; init; }
    public required string VehiclePlateNumber { get; init; }
    public required string VehicleType { get; init; }
    public required string Phone { get; init; }
    public required string CarrierPartnerId { get; init; }
    public CashFloatLimitTier RequestedCashFloatTier { get; init; } = CashFloatLimitTier.Tier1_LowRisk_50000DZD;
}

/// <summary>
/// Tamper-proof digital badge representing an active courier credential.
/// </summary>
public record CourierBadge
{
    public required string CourierDid { get; init; }
    public required string FullName { get; init; }
    public required string LicenseAttestationHash { get; init; }
    public required string VehiclePlate { get; init; }
    public required CashFloatLimitTier CashFloatTier { get; init; }
    public required decimal MaxCashFloatDzd { get; init; }
    public DateTimeOffset IssuedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; init; }
    public bool IsActive { get; init; } = true;
}

// ==========================================
// 4. Biometric Custody & Handover Models
// ==========================================

/// <summary>
/// Single-use dynamic biometric challenge for liveness verification before parcel custody unlock.
/// </summary>
public record BiometricChallenge
{
    public required string ChallengeId { get; init; }
    public required string CourierDid { get; init; }
    public required string Nonce { get; init; }
    public required string ChallengeAction { get; init; } // "BlinkAndSmile" | "HeadTurnLeft" | "NodTwice"
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; init; }
}

/// <summary>
/// Request to unlock parcel custody and authorize package handover with biometric & geofence proof.
/// </summary>
public record CustodyUnlockRequest
{
    public required string ChallengeId { get; init; }
    public required string CourierDid { get; init; }
    public required string RecipientDid { get; init; }
    public required string ConsignmentWaybill { get; init; }
    public decimal CashCollectedDzd { get; init; }
    public double BiometricLivenessScore { get; init; }
    public required string FacialTemplateHash { get; init; }
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public double DestinationLatitude { get; init; }
    public double DestinationLongitude { get; init; }
}

/// <summary>
/// Result of a custody unlock authorization request.
/// </summary>
public record CustodyUnlockResult
{
    public bool IsAuthorized { get; init; }
    public string? AuthorizationCode { get; init; }
    public string? CustodyCertificateId { get; init; }
    public required string Message { get; init; }
    public string? RejectionReason { get; init; }
}

/// <summary>
/// Immutable, cryptographically signed handover certificate confirming delivery & cash collection.
/// </summary>
public record CustodyHandoverCertificate
{
    public required string CertificateId { get; init; }
    public required string CourierDid { get; init; }
    public required string RecipientDid { get; init; }
    public required string WaybillNumber { get; init; }
    public decimal CashAmountDzd { get; init; }
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public DateTimeOffset HandoverTimestampUtc { get; init; } = DateTimeOffset.UtcNow;
    public required string CryptographicSignature { get; init; }
}

// ==========================================
// 5. API Request/Response DTOs
// ==========================================

public record CreateDidRequest
{
    public string KeyType { get; init; } = "Ed25519";
}

public record IssueCredentialRequest
{
    public required string IssuerDid { get; init; }
    public required string SubjectDid { get; init; }
    public required string CredentialType { get; init; }
    public required Dictionary<string, object> Claims { get; init; }
    public required string SecretKey { get; init; }
    public int? ValidityDays { get; init; }
}

public record RevokeCredentialRequest
{
    public required string Reason { get; init; }
}

public record OnboardCourierResponse
{
    public required CourierBadge Badge { get; init; }
    public required VerifiableCredential Credential { get; init; }
    public required DidDocument CourierDidDocument { get; init; }
}

public record GenerateChallengeRequest
{
    public required string CourierDid { get; init; }
}

public record UpdateCashFloatTierRequest
{
    public required CashFloatLimitTier NewTier { get; init; }
}
