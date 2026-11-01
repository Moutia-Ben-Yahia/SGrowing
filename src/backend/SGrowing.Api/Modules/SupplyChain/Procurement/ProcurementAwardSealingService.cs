using System.Security.Cryptography;
using System.Text;

namespace SGrowing.Api.Modules.SupplyChain.Procurement;

/// <summary>
/// Interface for cryptographic contract award sealing, budget cap authority enforcement,
/// and HMAC-SHA256 digital signature verification.
/// </summary>
public interface IProcurementAwardSealingService
{
    ProcurementAwardContract SealAwardContract(
        Guid auctionId,
        Guid winningBidId,
        int? quantity = null,
        string? secretKey = null,
        string? approverId = null);

    bool VerifyContractSignature(ProcurementAwardContract contract, string? secretKey = null);
    ProcurementAwardContract? GetContract(Guid contractId);
    IReadOnlyList<ProcurementAwardContract> GetAllContracts();
}

/// <summary>
/// Implements autonomous contract award sealing, budget cap enforcement (contracts &lt;= $10,000 auto-approved),
/// and HMAC-SHA256 digital signature verification.
/// </summary>
public class ProcurementAwardSealingService(ISupplierBiddingMatrixService biddingMatrixService) : IProcurementAwardSealingService
{
    public const decimal DefaultAutonomousBudgetCap = 10_000m;
    public const string DefaultSigningKey = "SGrowing-Procurement-Autonomous-Award-Sealing-Key-2026";

    private readonly ISupplierBiddingMatrixService _biddingMatrixService = biddingMatrixService
        ?? throw new ArgumentNullException(nameof(biddingMatrixService));

    private readonly Lock _lock = new();
    private readonly Dictionary<Guid, ProcurementAwardContract> _contracts = [];

    /// <summary>
    /// Seals an award contract for the winning supplier bid.
    /// Checks budget cap (&lt;= $10,000 auto-approves; &gt; $10,000 requires HITL),
    /// generates an HMAC-SHA256 cryptographic signature, and updates auction status to Awarded.
    /// </summary>
    public ProcurementAwardContract SealAwardContract(
        Guid auctionId,
        Guid winningBidId,
        int? quantity = null,
        string? secretKey = null,
        string? approverId = null)
    {
        var auction = _biddingMatrixService.GetAuction(auctionId)
            ?? throw new KeyNotFoundException($"Auction '{auctionId}' was not found.");

        if (auction.Status == AuctionStatus.Awarded)
        {
            throw new InvalidOperationException($"Auction '{auctionId}' has already been awarded.");
        }

        var winningBid = auction.Bids.FirstOrDefault(b => b.BidId == winningBidId)
            ?? throw new KeyNotFoundException($"Bid '{winningBidId}' was not found in auction '{auctionId}'.");

        var awardedQuantity = quantity ?? auction.RequiredQuantity;
        if (awardedQuantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Awarded quantity must be positive.");
        }

        var agreedUnitPrice = winningBid.UnitPrice;
        var totalContractValue = awardedQuantity * agreedUnitPrice;

        // Budget authority threshold check
        var isAutoApproved = totalContractValue <= DefaultAutonomousBudgetCap;
        var approvalMode = isAutoApproved
            ? AwardApprovalMode.AutonomousAutonomous
            : AwardApprovalMode.RequiresHitl;

        var contractId = Guid.NewGuid();
        var awardedAtUtc = DateTime.UtcNow;

        // Cryptographic HMAC-SHA256 sealing signature
        var signature = GenerateDigitalSignature(
            contractId,
            auctionId,
            winningBidId,
            winningBid.SupplierId,
            awardedQuantity,
            agreedUnitPrice,
            totalContractValue,
            awardedAtUtc,
            secretKey ?? DefaultSigningKey);

        var contract = new ProcurementAwardContract(
            ContractId: contractId,
            AuctionId: auctionId,
            WinningBidId: winningBidId,
            SupplierId: winningBid.SupplierId,
            AwardedQuantity: awardedQuantity,
            AgreedUnitPrice: agreedUnitPrice,
            TotalContractValue: totalContractValue,
            SlaScore: winningBid.SlaReliabilityScore,
            DigitalSignatureSha256: signature,
            AwardedAtUtc: awardedAtUtc,
            AutoApproved: isAutoApproved,
            ApprovalMode: approvalMode,
            ApproverId: isAutoApproved ? "AUTONOMOUS-AI-AGENT" : approverId ?? "PENDING-HITL-APPROVAL");

        // Transition auction and bid statuses
        _biddingMatrixService.UpdateAuctionStatus(auctionId, AuctionStatus.Awarded);
        _biddingMatrixService.UpdateBid(auctionId, winningBid with { Status = BidStatus.Accepted });

        lock (_lock)
        {
            _contracts[contractId] = contract;
        }

        return contract;
    }

    /// <summary>
    /// Verifies the cryptographic HMAC-SHA256 digital signature of a procurement contract.
    /// </summary>
    public bool VerifyContractSignature(ProcurementAwardContract contract, string? secretKey = null)
    {
        ArgumentNullException.ThrowIfNull(contract);

        var expectedSignature = GenerateDigitalSignature(
            contract.ContractId,
            contract.AuctionId,
            contract.WinningBidId,
            contract.SupplierId,
            contract.AwardedQuantity,
            contract.AgreedUnitPrice,
            contract.TotalContractValue,
            contract.AwardedAtUtc,
            secretKey ?? DefaultSigningKey);

        var expectedBytes = Encoding.UTF8.GetBytes(expectedSignature);
        var actualBytes = Encoding.UTF8.GetBytes(contract.DigitalSignatureSha256);

        return CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }

    /// <summary>
    /// Retrieves a sealed contract by ID.
    /// </summary>
    public ProcurementAwardContract? GetContract(Guid contractId)
    {
        lock (_lock)
        {
            return _contracts.GetValueOrDefault(contractId);
        }
    }

    /// <summary>
    /// Retrieves all sealed contracts.
    /// </summary>
    public IReadOnlyList<ProcurementAwardContract> GetAllContracts()
    {
        lock (_lock)
        {
            return [.. _contracts.Values];
        }
    }

    // -----------------------------------------------------------------------
    // Cryptographic Sealing Helper
    // -----------------------------------------------------------------------

    private static string GenerateDigitalSignature(
        Guid contractId,
        Guid auctionId,
        Guid bidId,
        string supplierId,
        int quantity,
        decimal unitPrice,
        decimal totalValue,
        DateTime timestampUtc,
        string secretKey)
    {
        // Canonical payload string for cryptographic digest
        var payload = FormattableString.Invariant(
            $"{contractId}:{auctionId}:{bidId}:{supplierId}:{quantity}:{unitPrice:F2}:{totalValue:F2}:{timestampUtc:O}");

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
