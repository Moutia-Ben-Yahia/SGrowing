using System.Text.Json.Serialization;

namespace SGrowing.Api.Modules.SupplyChain.ZkTraceability;

/// <summary>
/// Preferential trade zones and origins.
/// </summary>
public enum TradeZone
{
    DomesticAlgeria = 1,
    AfCFTA = 2,           // African Continental Free Trade Area
    GAFTA = 3,            // Greater Arab Free Trade Area
    UMA = 4,              // Arab Maghreb Union
    EUMedAgreement = 5,   // EU-Algeria Association Agreement
    GlobalOther = 6
}

/// <summary>
/// Classification of nodes in the supply chain provenance DAG.
/// </summary>
public enum ProvenanceNodeType
{
    RawMaterialExtraction = 1,
    RefiningAndSmelting = 2,
    ComponentFabrication = 3,
    QualityInspection = 4,
    CustomsClearance = 5,
    FinalAssembly = 6,
    PackagingAndShipping = 7
}

/// <summary>
/// Result status of cryptographic zero-knowledge origin verification.
/// </summary>
public enum OriginVerificationStatus
{
    Verified = 1,
    RejectedTampered = 2,
    ThresholdNotMet = 3,
    InvalidSignature = 4
}

/// <summary>
/// Status of privacy-preserving sanction screening.
/// </summary>
public enum SanctionScreeningStatus
{
    Compliant = 1,
    SanctionViolationDetected = 2,
    StaleSanctionDatabase = 3
}

/// <summary>
/// Represents a discrete component item in a Bill of Materials.
/// </summary>
public sealed record BomComponentItem(
    string ComponentId,
    string ComponentName,
    decimal WeightKg,
    decimal UnitValueDzd,
    int Quantity,
    TradeZone OriginZone,
    string BlindedSupplierHash,
    string BlindedSanctionIdentifierHash,
    string? CertifyingBody = null
)
{
    public decimal TotalValueDzd => UnitValueDzd * Quantity;
}

/// <summary>
/// Complete Bill of Materials (BOM) manifest for a finished manufactured product.
/// </summary>
public sealed record BillOfMaterialsManifest(
    string ProductSku,
    string BatchId,
    IReadOnlyList<BomComponentItem> Items,
    DateTime CreatedAtUtc
)
{
    public decimal TotalValueDzd => Items.Sum(i => i.TotalValueDzd);

    public decimal CalculateZoneValue(TradeZone targetZone)
    {
        return Items
            .Where(i => i.OriginZone == targetZone || 
                        (targetZone == TradeZone.AfCFTA && i.OriginZone == TradeZone.DomesticAlgeria))
            .Sum(i => i.TotalValueDzd);
    }

    public decimal CalculateZoneValueRatio(TradeZone targetZone)
    {
        var total = TotalValueDzd;
        if (total <= 0m) return 0m;
        return CalculateZoneValue(targetZone) / total;
    }
}

/// <summary>
/// Cryptographic blinded commitment representation for private component value.
/// </summary>
public sealed record PedersenCommitment(
    string CommitmentHash,
    string BlindingSaltHex,
    decimal CommittedValueDzd,
    DateTime CommittedAtUtc
);

/// <summary>
/// Zero-Knowledge Origin Compliance Proof issued for customs or preferential duty claims.
/// </summary>
public sealed record OriginComplianceProof(
    string ProofId,
    string ProductSku,
    string BatchId,
    TradeZone TargetTradeZone,
    decimal MinimumRequiredRatio,
    bool ThresholdSatisfied,
    string PublicCommitmentRoot,
    string ProofChallenge,
    string ProofResponse,
    string CryptographicSignature,
    DateTime IssuedAtUtc,
    OriginVerificationStatus Status
);

/// <summary>
/// Request to generate a Zero-Knowledge Origin proof for an assembled batch.
/// </summary>
public sealed record GenerateOriginProofRequest(
    BillOfMaterialsManifest Manifest,
    TradeZone TargetZone,
    decimal MinimumRequiredRatio = 0.60m // 60% rule of origin for AfCFTA / GAFTA
);

/// <summary>
/// Request to verify an incoming Zero-Knowledge Origin proof.
/// </summary>
public sealed record VerifyOriginProofRequest(
    OriginComplianceProof Proof,
    string ExpectedPublicCommitmentRoot,
    decimal MinimumRequiredRatio = 0.60m
);

/// <summary>
/// Output of privacy-preserving anti-sanction screening.
/// </summary>
public sealed record SanctionScreeningResult(
    string ScreeningId,
    DateTime ScreenedAtUtc,
    string SanctionListVersion,
    int TotalBlindedSuppliersScreened,
    bool MatchDetected,
    IReadOnlyList<string> ViolatedSupplierHashes,
    string CryptographicAuditProof,
    SanctionScreeningStatus Status
);

/// <summary>
/// Request to screen a supplier graph or BOM against active international sanction lists.
/// </summary>
public sealed record SanctionScreeningRequest(
    IReadOnlyList<string> BlindedSanctionIdentifiers,
    string AuditorSalt
);

/// <summary>
/// Discrete node in a cryptographic provenance Directed Acyclic Graph (DAG).
/// </summary>
public sealed record ProvenanceDagNode(
    string NodeId,
    ProvenanceNodeType NodeType,
    string EntityName,
    string LocationCountryIso,
    IReadOnlyList<string> ParentNodeIds,
    string PayloadSha256,
    string NodeHash,
    DateTime TimestampUtc,
    IReadOnlyDictionary<string, string> Metadata
);

/// <summary>
/// Merkle provenance DAG encompassing the full lifecycle of a batch.
/// </summary>
public sealed record MerkleProvenanceGraph(
    string GraphId,
    string ProductSku,
    string BatchId,
    string MerkleRootHash,
    IReadOnlyDictionary<string, ProvenanceDagNode> Nodes,
    DateTime GeneratedAtUtc
);

/// <summary>
/// Merkle inclusion audit proof verifying a specific raw material or certificate belongs to the batch DAG.
/// </summary>
public sealed record MerkleInclusionProof(
    string LeafNodeId,
    string LeafHash,
    IReadOnlyList<string> AuditPathHashes,
    IReadOnlyList<bool> IsLeftNeighbor,
    string ExpectedRootHash,
    bool IsValid
);
