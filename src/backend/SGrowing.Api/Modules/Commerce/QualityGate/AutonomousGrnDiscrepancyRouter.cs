using System.Security.Cryptography;
using System.Text;

namespace SGrowing.Api.Modules.Commerce.QualityGate;

/// <summary>
/// Autonomous thread-safe Goods Receipt Note (GRN) discrepancy routing engine.
/// Reconciles purchase orders, allocates warehouse quarantine bins, and generates cryptographically signed debit memos in DZD.
/// </summary>
public sealed class AutonomousGrnDiscrepancyRouter : IAutonomousGrnDiscrepancyRouter
{
    private static readonly byte[] HmacSecretKey = "SGrowing-QualityGate-CryptographicAuditHmacKey-v1"u8.ToArray();

    private readonly System.Threading.Lock _lock = new();
    private int _binAllocationCounter;
    private readonly Dictionary<string, GrnDiscrepancyDebitMemo> _memosById = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<GrnDiscrepancyDebitMemo> _memos = [];

    /// <inheritdoc />
    public GrnDiscrepancyDebitMemo ProcessInspectionDiscrepancy(
        string poId,
        string supplierId,
        PackagingInspectionResult inspection,
        decimal unitPriceDzd)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(poId);
        ArgumentException.ThrowIfNullOrWhiteSpace(supplierId);
        ArgumentNullException.ThrowIfNull(inspection);

        if (unitPriceDzd < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(unitPriceDzd), "Unit price in DZD cannot be negative.");
        }

        int defectiveCount = inspection.TotalDefectsFound > 0
            ? inspection.TotalDefectsFound
            : (inspection.Disposition != InspectionDisposition.AcceptedDirectToStock
                ? (inspection.Findings.Length > 0 ? inspection.Findings.Length : 1)
                : 0);

        decimal totalDebitAmountDzd = Math.Round(defectiveCount * unitPriceDzd, 2);

        string quarantineBinLocation = defectiveCount > 0
            ? AllocateQuarantineBin(poId)
            : "NONE-DIRECT-TO-STOCK";

        string memoId = $"MEMO-GRN-{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}";
        DateTime generatedAtUtc = DateTime.UtcNow;

        string signature = GenerateSignature(
            memoId,
            poId,
            supplierId,
            defectiveCount,
            totalDebitAmountDzd,
            quarantineBinLocation,
            generatedAtUtc);

        var memo = new GrnDiscrepancyDebitMemo(
            MemoId: memoId,
            PurchaseOrderId: poId,
            SupplierId: supplierId,
            DefectiveItemsCount: defectiveCount,
            TotalDebitAmountDzd: totalDebitAmountDzd,
            QuarantineBinLocation: quarantineBinLocation,
            GeneratedAtUtc: generatedAtUtc,
            CryptographicAuditSignature: signature);

        lock (_lock)
        {
            _memosById[memo.MemoId] = memo;
            _memos.Add(memo);
        }

        return memo;
    }

    /// <inheritdoc />
    public GrnDiscrepancyDebitMemo? GetDebitMemo(string memoId)
    {
        if (string.IsNullOrWhiteSpace(memoId))
        {
            return null;
        }

        lock (_lock)
        {
            return _memosById.GetValueOrDefault(memoId);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<GrnDiscrepancyDebitMemo> ListQuarantinedMemos()
    {
        lock (_lock)
        {
            return [.. _memos.Where(m => m.QuarantineBinLocation.StartsWith("QUAR-ZONE-", StringComparison.OrdinalIgnoreCase))];
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<GrnDiscrepancyDebitMemo> GetMemosByPurchaseOrder(string poId)
    {
        if (string.IsNullOrWhiteSpace(poId))
        {
            return [];
        }

        lock (_lock)
        {
            return [.. _memos.Where(m => string.Equals(m.PurchaseOrderId, poId, StringComparison.OrdinalIgnoreCase))];
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<GrnDiscrepancyDebitMemo> GetMemosBySupplier(string supplierId)
    {
        if (string.IsNullOrWhiteSpace(supplierId))
        {
            return [];
        }

        lock (_lock)
        {
            return [.. _memos.Where(m => string.Equals(m.SupplierId, supplierId, StringComparison.OrdinalIgnoreCase))];
        }
    }

    /// <inheritdoc />
    public bool VerifyDebitMemoSignature(GrnDiscrepancyDebitMemo memo)
    {
        ArgumentNullException.ThrowIfNull(memo);

        string expectedSignature = GenerateSignature(
            memo.MemoId,
            memo.PurchaseOrderId,
            memo.SupplierId,
            memo.DefectiveItemsCount,
            memo.TotalDebitAmountDzd,
            memo.QuarantineBinLocation,
            memo.GeneratedAtUtc);

        byte[] expectedBytes = Encoding.UTF8.GetBytes(expectedSignature);
        byte[] actualBytes = Encoding.UTF8.GetBytes(memo.CryptographicAuditSignature);

        return CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }

    private string AllocateQuarantineBin(string poId)
    {
        int index = Interlocked.Increment(ref _binAllocationCounter);
        int floor = ((index - 1) % 4) + 1; // Floors 1 to 4
        char bayLetter = (char)('A' + (((index - 1) / 4) % 26));
        int bayNumber = (((index - 1) / 104) % 99) + 1;

        return $"QUAR-ZONE-{floor:D2}-{bayLetter}{bayNumber:D2}";
    }

    private static string GenerateSignature(
        string memoId,
        string poId,
        string supplierId,
        int defectiveCount,
        decimal totalDebitAmountDzd,
        string quarantineBinLocation,
        DateTime generatedAtUtc)
    {
        string canonicalPayload = FormattableString.Invariant(
            $"{memoId}:{poId}:{supplierId}:{defectiveCount}:{totalDebitAmountDzd:F2}:{quarantineBinLocation}:{generatedAtUtc:O}");

        using var hmac = new HMACSHA256(HmacSecretKey);
        byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(canonicalPayload));
        return Convert.ToHexString(hash);
    }
}
