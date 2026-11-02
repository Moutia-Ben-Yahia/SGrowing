namespace SGrowing.Api.Modules.Security.ZkIdentityVerification;

public enum AttributeClaimType
{
    AgeAtLeast21,
    AgeAtLeast18,
    AccreditedInvestorNetWorthEur,
    SecurityClearanceLevel
}

public sealed record BlindedAttributeCredential(
    string CredentialId,
    string SubjectDid,
    AttributeClaimType ClaimType,
    string CommitmentHex,
    DateTime IssuedAtUtc
);

public sealed record ZkRangeProof(
    string ProofId,
    AttributeClaimType ClaimType,
    long ThresholdValue,
    string CommitmentHex,
    string EphemeralAHex,
    string ChallengeHex,
    string ResponseZ1Hex,
    string ResponseZ2Hex,
    DateTime ProvedAtUtc
);

public sealed record ZkProofVerificationResult(
    string ProofId,
    AttributeClaimType ClaimType,
    long ThresholdValue,
    bool IsProofMathematicallyValid,
    string AuditSummary,
    DateTime VerifiedAtUtc
);

public sealed record GenerateProofRequest(
    long ActualAttributeValue,
    long RequiredThreshold,
    AttributeClaimType ClaimType
);
