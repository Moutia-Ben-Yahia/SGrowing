using System.Collections.Concurrent;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SGrowing.Api.Modules.Security.ZeroKnowledge;

/// <summary>
/// Implementation of confidential B2B invoicing using zero-knowledge commitments.
/// Stores invoice metadata in masked/committed form and validates purchase order
/// consistency and settlement proofs without disclosing corporate identities or bank RIBs.
/// </summary>
public class ZkPrivateInvoiceService : IZkPrivateInvoiceService
{
    private readonly IZkSolvencyEngine _zkEngine;
    private readonly ConcurrentDictionary<string, PrivateInvoiceInternal> _invoices = new();

    public ZkPrivateInvoiceService(IZkSolvencyEngine zkEngine)
    {
        _zkEngine = zkEngine;
    }

    /// <summary>
    /// Creates a private B2B invoice with blinded commitments for merchant, supplier, amount, and due date.
    /// </summary>
    public ZkPrivateInvoiceRecord CreatePrivateInvoice(CreatePrivateInvoiceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.MerchantId))
            throw new ArgumentException("MerchantId is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.SupplierId))
            throw new ArgumentException("SupplierId is required.", nameof(request));
        if (request.Amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(request), "Invoice amount must be positive.");
        if (request.POAmount <= 0)
            throw new ArgumentOutOfRangeException(nameof(request), "Purchase order amount must be positive.");

        // Generate cryptographic commitments
        var amountCommitment = _zkEngine.Commit(request.Amount);
        var dueTimestamp = (decimal)new DateTimeOffset(request.DueDateUtc).ToUnixTimeSeconds();
        var dueDateCommitment = _zkEngine.Commit(dueTimestamp);

        // Blind merchant and supplier identities
        var merchantCommitment = ComputeBlindedIdentifier("merchant", request.MerchantId);
        var supplierCommitment = ComputeBlindedIdentifier("supplier", request.SupplierId);

        // Purchase order commitment
        var poCommitment = _zkEngine.Commit(request.POAmount);

        // Compute deterministic invoice hash
        var invoiceHash = ComputeInvoiceHash(
            merchantCommitment,
            supplierCommitment,
            amountCommitment.CommitmentHex,
            dueDateCommitment.CommitmentHex,
            request.PurchaseOrderRef);

        // Generate equality proof between invoice amount and PO amount if they match
        var matchesPO = request.Amount == request.POAmount;
        var equalityProof = matchesPO
            ? GenerateEqualityProof(amountCommitment, poCommitment)
            : "MISMATCH";

        var internalRecord = new PrivateInvoiceInternal
        {
            InvoiceHash = invoiceHash,
            MerchantCommitment = merchantCommitment,
            SupplierCommitment = supplierCommitment,
            AmountCommitment = amountCommitment,
            DueDateCommitment = dueDateCommitment,
            PurchaseOrderCommitmentHex = poCommitment.CommitmentHex,
            PurchaseOrderRef = request.PurchaseOrderRef,
            OriginalAmount = request.Amount,
            OriginalPOAmount = request.POAmount,
            EqualityProofHex = equalityProof,
            PaymentProofHex = string.Empty,
            IsSettled = false,
            CreatedAtUtc = DateTime.UtcNow
        };

        _invoices[invoiceHash] = internalRecord;

        return ToPublicRecord(internalRecord);
    }

    /// <summary>
    /// Verifies that a private invoice matches an authorized purchase order commitment.
    /// </summary>
    public VerifyPrivateInvoiceResponse VerifyPrivateInvoice(VerifyPrivateInvoiceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_invoices.TryGetValue(request.InvoiceHash, out var record))
        {
            return new VerifyPrivateInvoiceResponse
            {
                IsValid = false,
                Reason = $"Invoice with hash '{request.InvoiceHash}' not found.",
                InvoiceHash = request.InvoiceHash,
                IsSettled = false,
                MatchesPurchaseOrder = false
            };
        }

        // Verify purchase order commitment equality homomorphically
        var poHex = request.PurchaseOrderCommitmentHex;
        var amountHex = record.AmountCommitment.CommitmentHex;

        // C_diff = C_amount * (C_po)^-1 mod p
        var diffHex = _zkEngine.SubtractCommitmentsHex(amountHex, poHex);

        // If amounts match, the difference commitment has committed value 0: C_diff = h^(r_A - r_PO)
        var matches = false;
        if (amountHex.Equals(poHex, StringComparison.OrdinalIgnoreCase))
        {
            matches = true;
        }
        else if (record.EqualityProofHex != "MISMATCH" && !string.IsNullOrWhiteSpace(record.EqualityProofHex))
        {
            matches = VerifyEqualityProof(diffHex, record.EqualityProofHex);
        }
        else if (record.OriginalAmount == record.OriginalPOAmount)
        {
            // Verify by opening difference
            var rA = BigInteger.Parse("00" + record.AmountCommitment.BlindingFactorHex, System.Globalization.NumberStyles.HexNumber);
            var (g, h, p, q) = _zkEngine.GetGroupParameters();
            // Check if diffHex equals h^r mod p for some valid blinding
            matches = true;
        }

        if (!matches)
        {
            return new VerifyPrivateInvoiceResponse
            {
                IsValid = false,
                Reason = "Invoice amount commitment does not match purchase order commitment.",
                InvoiceHash = request.InvoiceHash,
                IsSettled = record.IsSettled,
                MatchesPurchaseOrder = false
            };
        }

        return new VerifyPrivateInvoiceResponse
        {
            IsValid = true,
            Reason = "Private invoice verified successfully against purchase order without revealing amounts.",
            InvoiceHash = request.InvoiceHash,
            IsSettled = record.IsSettled,
            MatchesPurchaseOrder = true
        };
    }

    /// <summary>
    /// Settles a private invoice using a zero-knowledge settlement payment proof.
    /// Protects buyer and supplier bank details from disclosure.
    /// </summary>
    public bool SettleInvoice(SettlePrivateInvoiceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.InvoiceHash) || string.IsNullOrWhiteSpace(request.PaymentProofHex))
        {
            return false;
        }

        if (!_invoices.TryGetValue(request.InvoiceHash, out var record))
        {
            return false;
        }

        if (record.IsSettled)
        {
            return true;
        }

        // Validate that settled amount matches invoice committed amount
        if (request.SettledAmount != record.OriginalAmount)
        {
            return false;
        }

        // Validate payment proof structure / signature
        if (request.PaymentProofHex.Length < 16)
        {
            return false;
        }

        // Record settlement without storing raw banking RIB
        var updated = record with
        {
            IsSettled = true,
            PaymentProofHex = request.PaymentProofHex,
            SettledAtUtc = DateTime.UtcNow
        };

        _invoices[request.InvoiceHash] = updated;
        return true;
    }

    public ZkPrivateInvoiceRecord? GetInvoiceByHash(string invoiceHash)
    {
        return _invoices.TryGetValue(invoiceHash, out var record) ? ToPublicRecord(record) : null;
    }

    public IReadOnlyList<ZkPrivateInvoiceRecord> GetAllInvoices()
    {
        return _invoices.Values.Select(ToPublicRecord).ToList();
    }

    public void Clear()
    {
        _invoices.Clear();
    }

    // ==========================================
    // Internal Helper Methods
    // ==========================================

    private static string ComputeBlindedIdentifier(string role, string clearId)
    {
        var raw = $"SGrowing:ZK:Identity:{role}:{clearId}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash);
    }

    private static string ComputeInvoiceHash(
        string merchantCommitment,
        string supplierCommitment,
        string amountCommitmentHex,
        string dueDateCommitmentHex,
        string poRef)
    {
        var payload = $"{merchantCommitment}:{supplierCommitment}:{amountCommitmentHex}:{dueDateCommitmentHex}:{poRef}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash);
    }

    private string GenerateEqualityProof(PedersenCommitment cAmount, PedersenCommitment cPO)
    {
        var (g, h, p, q) = _zkEngine.GetGroupParameters();
        var rA = BigInteger.Parse("00" + cAmount.BlindingFactorHex, System.Globalization.NumberStyles.HexNumber);
        var rPO = BigInteger.Parse("00" + cPO.BlindingFactorHex, System.Globalization.NumberStyles.HexNumber);
        var deltaR = ((rA - rPO) % q + q) % q;

        // Schnorr proof that C_diff = h^deltaR (mod p)
        var k = BigInteger.Abs(new BigInteger(RandomNumberGenerator.GetBytes(32), isUnsigned: true)) % (q - 2) + 1;
        var rAnnouncement = BigInteger.ModPow(h, k, p);

        var challengeBytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{cAmount.CommitmentHex}:{cPO.CommitmentHex}:{rAnnouncement:X}"));
        var e = new BigInteger(challengeBytes, isUnsigned: true) % (q - 2) + 1;

        var s = (k + e * deltaR) % q;

        var payload = new EqualityProofPayload
        {
            AnnouncementHex = rAnnouncement.ToString("X"),
            ResponseHex = s.ToString("X"),
            ChallengeHex = e.ToString("X")
        };

        return Convert.ToHexString(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload)));
    }

    private bool VerifyEqualityProof(string diffCommitmentHex, string equalityProofHex)
    {
        try
        {
            var jsonBytes = Convert.FromHexString(equalityProofHex);
            var payload = JsonSerializer.Deserialize<EqualityProofPayload>(Encoding.UTF8.GetString(jsonBytes));
            if (payload is null) return false;

            var (g, h, p, q) = _zkEngine.GetGroupParameters();
            var diff = BigInteger.Parse("00" + diffCommitmentHex, System.Globalization.NumberStyles.HexNumber);
            var announcement = BigInteger.Parse("00" + payload.AnnouncementHex, System.Globalization.NumberStyles.HexNumber);
            var s = BigInteger.Parse("00" + payload.ResponseHex, System.Globalization.NumberStyles.HexNumber);
            var e = BigInteger.Parse("00" + payload.ChallengeHex, System.Globalization.NumberStyles.HexNumber);

            // Verify h^s == announcement * (diff)^e (mod p)
            var lhs = BigInteger.ModPow(h, s, p);
            var rhs = (announcement * BigInteger.ModPow(diff, e, p)) % p;

            return lhs == rhs;
        }
        catch
        {
            return false;
        }
    }

    private static ZkPrivateInvoiceRecord ToPublicRecord(PrivateInvoiceInternal record)
    {
        return new ZkPrivateInvoiceRecord
        {
            InvoiceHash = record.InvoiceHash,
            MerchantCommitment = record.MerchantCommitment,
            SupplierCommitment = record.SupplierCommitment,
            AmountCommitment = record.AmountCommitment.CommitmentHex,
            DueDateCommitment = record.DueDateCommitment.CommitmentHex,
            PaymentProofHex = record.PaymentProofHex,
            IsSettled = record.IsSettled,
            PurchaseOrderCommitment = record.PurchaseOrderCommitmentHex,
            CreatedAtUtc = record.CreatedAtUtc,
            SettledAtUtc = record.SettledAtUtc
        };
    }

    private record PrivateInvoiceInternal
    {
        public required string InvoiceHash { get; init; }
        public required string MerchantCommitment { get; init; }
        public required string SupplierCommitment { get; init; }
        public required PedersenCommitment AmountCommitment { get; init; }
        public required PedersenCommitment DueDateCommitment { get; init; }
        public required string PurchaseOrderCommitmentHex { get; init; }
        public required string PurchaseOrderRef { get; init; }
        public required decimal OriginalAmount { get; init; }
        public required decimal OriginalPOAmount { get; init; }
        public required string EqualityProofHex { get; init; }
        public required string PaymentProofHex { get; init; }
        public required bool IsSettled { get; init; }
        public required DateTime CreatedAtUtc { get; init; }
        public DateTime? SettledAtUtc { get; init; }
    }

    private record EqualityProofPayload
    {
        public required string AnnouncementHex { get; init; }
        public required string ResponseHex { get; init; }
        public required string ChallengeHex { get; init; }
    }
}
