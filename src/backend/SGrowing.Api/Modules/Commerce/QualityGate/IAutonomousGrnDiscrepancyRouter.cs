namespace SGrowing.Api.Modules.Commerce.QualityGate;

/// <summary>
/// Interface for autonomous Goods Receipt Note (GRN) discrepancy routing and debit memo issuance.
/// </summary>
public interface IAutonomousGrnDiscrepancyRouter
{
    /// <summary>
    /// Reconciles purchase order expectations with physical packaging inspection findings,
    /// routes defective items to quarantine bins, and issues a cryptographically signed debit memo in DZD.
    /// </summary>
    GrnDiscrepancyDebitMemo ProcessInspectionDiscrepancy(
        string poId,
        string supplierId,
        PackagingInspectionResult inspection,
        decimal unitPriceDzd);

    /// <summary>
    /// Retrieves a generated discrepancy debit memo by its unique ID.
    /// </summary>
    GrnDiscrepancyDebitMemo? GetDebitMemo(string memoId);

    /// <summary>
    /// Lists all debit memos where items were routed to quarantine storage.
    /// </summary>
    IReadOnlyList<GrnDiscrepancyDebitMemo> ListQuarantinedMemos();

    /// <summary>
    /// Retrieves all debit memos associated with a specific Purchase Order.
    /// </summary>
    IReadOnlyList<GrnDiscrepancyDebitMemo> GetMemosByPurchaseOrder(string poId);

    /// <summary>
    /// Retrieves all debit memos issued against a specific Supplier.
    /// </summary>
    IReadOnlyList<GrnDiscrepancyDebitMemo> GetMemosBySupplier(string supplierId);

    /// <summary>
    /// Cryptographically validates the HMAC-SHA256 audit signature of a discrepancy debit memo.
    /// </summary>
    bool VerifyDebitMemoSignature(GrnDiscrepancyDebitMemo memo);
}
