namespace SGrowing.Api.Modules.Security.ZeroKnowledge;

/// <summary>
/// Represents a cryptographic Pedersen commitment C = g^v * h^r (mod p).
/// Perfectly hiding and computationally binding over a high-security prime field.
/// </summary>
public record PedersenCommitment
{
    public required string CommitmentHex { get; init; }
    public required string GeneratorG { get; init; }
    public required string GeneratorH { get; init; }
    public required string BlindingFactorHex { get; init; }
    public bool CommittedValueHidden { get; init; } = true;
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Zero-knowledge Bulletproof-style range proof proving v in [LowerBound, UpperBound]
/// without revealing the secret numerical value v.
/// </summary>
public record BulletproofRangeProof
{
    public required string ProofId { get; init; }
    public required string CommitmentHex { get; init; }
    public required decimal LowerBound { get; init; }
    public required decimal UpperBound { get; init; }
    public required string ProofBytesBase64 { get; init; }
    public required string VerificationHash { get; init; }
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Certified zero-knowledge solvency statement for a tenant proving Assets >= Liabilities * ReserveRatio
/// strictly over cryptographic commitments without disclosing balance amounts.
/// </summary>
public record ZkSolvencyStatement
{
    public required string TenantId { get; init; }
    public required string TotalAssetsCommitment { get; init; }
    public required string TotalLiabilitiesCommitment { get; init; }
    public double RequiredReserveRatio { get; init; } = 1.0;
    public required string ProofOfSolvencyHex { get; init; }
    public bool IsVerified { get; init; }
    public DateTime CertifiedAtUtc { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Confidential B2B invoice record concealing merchant identity, supplier identity,
/// monetary value, and payment credentials using zero-knowledge commitments.
/// </summary>
public record ZkPrivateInvoiceRecord
{
    public required string InvoiceHash { get; init; }
    public required string MerchantCommitment { get; init; }
    public required string SupplierCommitment { get; init; }
    public required string AmountCommitment { get; init; }
    public required string DueDateCommitment { get; init; }
    public required string PaymentProofHex { get; init; }
    public bool IsSettled { get; init; }
    public string? PurchaseOrderCommitment { get; init; }
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
    public DateTime? SettledAtUtc { get; init; }
}

/// <summary>
/// Merkle inclusion audit proof proving a tenant's solvency commitment is included in
/// the global reserve tree without revealing other tenants' liabilities or commitments.
/// </summary>
public record MerkleSolvencyProof
{
    public required string RootHash { get; init; }
    public required string LeafHash { get; init; }
    public required int AuditIndex { get; init; }
    public required IReadOnlyList<string> MerklePath { get; init; }
    public bool IsValid { get; init; }
}

// ==========================================
// API Request & Response Contracts
// ==========================================

public record CreateCommitmentRequest
{
    public required decimal Value { get; init; }
    public string? BlindingFactorHex { get; init; }
    public string? Purpose { get; init; }
}

public record CreateCommitmentResponse
{
    public required PedersenCommitment Commitment { get; init; }
    public string? Note { get; init; }
}

public record VerifyCommitmentOpeningRequest
{
    public required string CommitmentHex { get; init; }
    public required decimal Value { get; init; }
    public required string BlindingFactorHex { get; init; }
}

public record GenerateRangeProofRequest
{
    public required decimal Value { get; init; }
    public required string BlindingFactorHex { get; init; }
    public required decimal LowerBound { get; init; }
    public required decimal UpperBound { get; init; }
}

public record VerifyRangeProofRequest
{
    public required BulletproofRangeProof Proof { get; init; }
}

public record GenerateSolvencyProofRequest
{
    public required string TenantId { get; init; }
    public required decimal TotalAssets { get; init; }
    public required decimal TotalLiabilities { get; init; }
    public double RequiredReserveRatio { get; init; } = 1.0;
    public string? AssetsBlindingHex { get; init; }
    public string? LiabilitiesBlindingHex { get; init; }
}

public record VerifySolvencyProofRequest
{
    public required ZkSolvencyStatement Statement { get; init; }
}

public record VerifySolvencyProofResponse
{
    public bool IsVerified { get; init; }
    public required string TenantId { get; init; }
    public string? Reason { get; init; }
    public DateTime VerifiedAtUtc { get; init; } = DateTime.UtcNow;
}

public record CreatePrivateInvoiceRequest
{
    public required string MerchantId { get; init; }
    public required string SupplierId { get; init; }
    public required decimal Amount { get; init; }
    public required DateTime DueDateUtc { get; init; }
    public required string PurchaseOrderRef { get; init; }
    public required decimal POAmount { get; init; }
}

public record VerifyPrivateInvoiceRequest
{
    public required string InvoiceHash { get; init; }
    public required string PurchaseOrderCommitmentHex { get; init; }
    public string? ProofHex { get; init; }
}

public record VerifyPrivateInvoiceResponse
{
    public bool IsValid { get; init; }
    public required string Reason { get; init; }
    public required string InvoiceHash { get; init; }
    public bool IsSettled { get; init; }
    public bool MatchesPurchaseOrder { get; init; }
}

public record SettlePrivateInvoiceRequest
{
    public required string InvoiceHash { get; init; }
    public required string PaymentProofHex { get; init; }
    public required decimal SettledAmount { get; init; }
    public string? MaskedBankRib { get; init; }
}

public record MerkleSolvencyRootResponse
{
    public required string RootHash { get; init; }
    public int TotalTenants { get; init; }
    public DateTime GeneratedAtUtc { get; init; } = DateTime.UtcNow;
    public required IReadOnlyList<string> TenantIds { get; init; }
    public MerkleSolvencyProof? InclusionProof { get; init; }
}

public record VerifyMerkleProofRequest
{
    public required MerkleSolvencyProof Proof { get; init; }
}

public record ZkEngineHealthResponse
{
    public required string Status { get; init; }
    public required string PrimeGroup { get; init; }
    public int SecurityBits { get; init; }
    public bool HardwareAccelerationAvailable { get; init; }
    public long TotalProofsGenerated { get; init; }
    public long TotalCommitmentsCreated { get; init; }
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
}
