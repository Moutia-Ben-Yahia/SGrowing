namespace SGrowing.Api.Modules.Security.ComplianceVault;

public enum PiiCategory
{
    CreditCard,
    Ssn,
    Email,
    PhoneNumber,
    Iban,
    PhysicalAddress
}

public record RedactionMatch(
    string OriginalText,
    string SyntheticToken,
    PiiCategory Category,
    int StartIndex,
    int Length
);

public record PiiRedactionResult(
    string SanitizedText,
    List<RedactionMatch> Matches,
    int TotalPiiCount,
    double ProcessingLatencyMs
);

public record ScanPiiRequest(
    string InputText,
    bool GenerateSyntheticTokens = true
);

public record MerkleAuditLeaf(
    string LeafId,
    string ActionType,
    string ActorId,
    string TenantId,
    string PayloadSha256,
    string MerkleHash,
    DateTime TimestampUtc
);

public record MerkleRootVerification(
    string RootHash,
    int TotalLeaves,
    bool IsTamperProof,
    List<string> ProofChain
);

public record DataResidencyBoundary(
    string Jurisdiction,
    string RegionCode,
    bool IsEnforced,
    string PrimaryStorageLocation,
    List<string> PermittedSubprocessors
);
