namespace SGrowing.Api.Modules.Security.ZeroKnowledge;

/// <summary>
/// Service managing confidential B2B invoices using zero-knowledge commitments.
/// Validates invoice consistency against purchase orders and payment settlements
/// without disclosing buyer identity, supplier identity, margins, or bank RIBs.
/// </summary>
public interface IZkPrivateInvoiceService
{
    /// <summary>
    /// Creates a private B2B invoice with blinded commitments for merchant, supplier, amount, and due date.
    /// </summary>
    ZkPrivateInvoiceRecord CreatePrivateInvoice(CreatePrivateInvoiceRequest request);

    /// <summary>
    /// Verifies that a private invoice matches an authorized purchase order commitment
    /// without disclosing cleartext invoice amounts or supplier pricing.
    /// </summary>
    VerifyPrivateInvoiceResponse VerifyPrivateInvoice(VerifyPrivateInvoiceRequest request);

    /// <summary>
    /// Settles a private invoice using a zero-knowledge settlement payment proof.
    /// Conceals bank RIB and transaction routes from third-party auditors.
    /// </summary>
    bool SettleInvoice(SettlePrivateInvoiceRequest request);

    /// <summary>
    /// Retrieves a private invoice record by its deterministic cryptographic hash.
    /// </summary>
    ZkPrivateInvoiceRecord? GetInvoiceByHash(string invoiceHash);

    /// <summary>
    /// Returns all registered confidential invoices.
    /// </summary>
    IReadOnlyList<ZkPrivateInvoiceRecord> GetAllInvoices();

    /// <summary>
    /// Resets in-memory invoice records (testing utility).
    /// </summary>
    void Clear();
}
