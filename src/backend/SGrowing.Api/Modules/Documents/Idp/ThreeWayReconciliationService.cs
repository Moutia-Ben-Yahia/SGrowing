using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Documents.Idp;

/// <summary>
/// Service contract for 3-way reconciliation across Purchase Orders (PO),
/// Goods Receipt Notes (GRN), and Supplier Invoices.
/// </summary>
public interface IThreeWayReconciliationService
{
    /// <summary>
    /// Executes automated 3-way matching, enforces price/quantity/tax tolerance rules,
    /// blocks duplicate submissions, verifies bank details, and returns match or dispute records.
    /// </summary>
    Task<ThreeWayMatchResponse> ReconcileAsync(
        ThreeWayMatchRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether an invoice has already been registered as processed/reconciled.
    /// </summary>
    bool IsDuplicateInvoice(string vendorTaxId, string invoiceNumber, string sha256Hash);

    /// <summary>
    /// Registers an approved or reconciled invoice to prevent duplicate payment attacks.
    /// </summary>
    void RegisterProcessedInvoice(string vendorTaxId, string invoiceNumber, string sha256Hash);

    /// <summary>
    /// Resets the internal processed invoices registry (primarily used for test isolation).
    /// </summary>
    void ClearRegisteredInvoices();
}

/// <summary>
/// Enterprise 3-way invoice reconciliation engine with milligram-exact quantity matching,
/// configurable percentage price creep tolerance, cent-exact tax cross-verification,
/// and bank account RIB security screening.
/// </summary>
public class ThreeWayReconciliationService : IThreeWayReconciliationService
{
    // In-memory thread-safe registry: key = VendorTaxId:InvoiceNumber or Hash:sha256
    private readonly ConcurrentDictionary<string, DateTime> _registeredInvoices = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public Task<ThreeWayMatchResponse> ReconcileAsync(
        ThreeWayMatchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var po = request.PurchaseOrder;
        var grn = request.GoodsReceiptNote;
        var invoice = request.Invoice;
        var tolerance = request.Tolerance ?? new ToleranceOptions();

        var notes = new List<string>();

        // 1. Validate Unmatched PO / GRN Cross-Referencing
        if (string.IsNullOrWhiteSpace(po.PoId) ||
            !string.Equals(po.PoId, grn.PoId, StringComparison.OrdinalIgnoreCase))
        {
            notes.Add($"PO reference mismatch: PO ID '{po.PoId}' does not match GRN referenced PO ID '{grn.PoId}'.");
            var unmatchedResult = new ThreeWayMatchResult(
                MatchStatus: ThreeWayMatchStatus.UnmatchedPo,
                PoId: po.PoId,
                GrnId: grn.GrnId,
                InvoiceId: invoice.InvoiceNumber,
                TotalPoAmount: po.TotalAmount,
                TotalGrnAmount: 0m,
                TotalInvoiceAmount: invoice.TotalAmount,
                PriceVariancePercent: 100m,
                QuantityVariance: 0m,
                DiscrepancyNotes: notes,
                AutoApproved: false,
                ReconciledAtUtc: DateTime.UtcNow);

            return Task.FromResult(new ThreeWayMatchResponse(
                Success: false,
                Result: unmatchedResult,
                Message: "Unmatched Purchase Order reference. Reconciliation rejected."));
        }

        // 2. Check for Duplicate Invoice Submission
        var vendorKey = NormalizeTaxId(invoice.VendorTaxId);
        if (IsDuplicateInvoice(vendorKey, invoice.InvoiceNumber, invoice.Sha256Hash))
        {
            notes.Add($"Duplicate invoice detected: Invoice '{invoice.InvoiceNumber}' (Vendor '{invoice.VendorTaxId}') or SHA-256 hash '{invoice.Sha256Hash}' was previously submitted and processed.");
            var duplicateResult = new ThreeWayMatchResult(
                MatchStatus: ThreeWayMatchStatus.DuplicateInvoiceBlocked,
                PoId: po.PoId,
                GrnId: grn.GrnId,
                InvoiceId: invoice.InvoiceNumber,
                TotalPoAmount: po.TotalAmount,
                TotalGrnAmount: 0m,
                TotalInvoiceAmount: invoice.TotalAmount,
                PriceVariancePercent: 0m,
                QuantityVariance: 0m,
                DiscrepancyNotes: notes,
                AutoApproved: false,
                ReconciledAtUtc: DateTime.UtcNow);

            return Task.FromResult(new ThreeWayMatchResponse(
                Success: false,
                Result: duplicateResult,
                Message: "Duplicate invoice blocked to prevent double-payment."));
        }

        // 3. Bank Account (RIB/IBAN) Screening
        if (!string.IsNullOrWhiteSpace(po.BankRib) && po.BankRib != "N/A")
        {
            var normalizedPoRib = NormalizeRib(po.BankRib);
            var normalizedInvRib = NormalizeRib(invoice.BankRib);

            if (!string.Equals(normalizedPoRib, normalizedInvRib, StringComparison.OrdinalIgnoreCase))
            {
                notes.Add($"Bank account (RIB) discrepancy: Invoice specifies RIB '{invoice.BankRib}' (normalized: '{normalizedInvRib}') but PO master records require registered RIB '{po.BankRib}' (normalized: '{normalizedPoRib}'). Potential fraudulent bank alteration risk.");
            }
        }

        // 4. Quantity Reconciliation (Invoice billed quantity vs GRN accepted quantity)
        decimal totalQuantityVariance = 0m;
        bool hasQuantityDiscrepancy = false;

        var grnItemMap = grn.ReceivedItems
            .GroupBy(g => g.Sku.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.AcceptedQuantity), StringComparer.OrdinalIgnoreCase);

        // Check each invoice line item against GRN received quantities
        foreach (var invItem in invoice.LineItems)
        {
            var skuKey = invItem.Sku.Trim();
            if (grnItemMap.TryGetValue(skuKey, out var acceptedQty))
            {
                var diff = invItem.Quantity - acceptedQty;
                if (diff > tolerance.QuantityTolerance)
                {
                    hasQuantityDiscrepancy = true;
                    totalQuantityVariance += Math.Abs(diff);
                    notes.Add($"Quantity shortage for SKU '{skuKey}': Invoiced billed quantity ({invItem.Quantity}) exceeds GRN accepted quantity ({acceptedQty}) by {diff}.");
                }
            }
            else
            {
                // SKU missing from GRN intake
                hasQuantityDiscrepancy = true;
                totalQuantityVariance += invItem.Quantity;
                notes.Add($"SKU '{skuKey}' billed on invoice was not received on GRN '{grn.GrnId}'. Invoiced quantity: {invItem.Quantity}.");
            }
        }

        // Also check if GRN has items not on invoice or partially invoiced
        var totalGrnAmount = 0m;
        foreach (var grnItem in grn.ReceivedItems)
        {
            var matchingPoLine = po.LineItems.FirstOrDefault(p => string.Equals(p.Sku.Trim(), grnItem.Sku.Trim(), StringComparison.OrdinalIgnoreCase));
            if (matchingPoLine is not null)
            {
                totalGrnAmount += grnItem.AcceptedQuantity * matchingPoLine.UnitPrice;
            }
        }

        // 5. Price & Total Reconciliation
        // Calculate price variance against PO total
        decimal priceVariancePercent = 0m;
        if (po.TotalAmount > 0m)
        {
            var delta = invoice.TotalAmount - po.TotalAmount;
            priceVariancePercent = Math.Round((delta / po.TotalAmount) * 100m, 4);
        }

        bool hasPriceDiscrepancy = false;
        if (Math.Abs(priceVariancePercent) > tolerance.MaxPriceVariancePercent)
        {
            hasPriceDiscrepancy = true;
            notes.Add($"Price variance exceeds tolerance: Invoice total {invoice.TotalAmount:N2} {invoice.Currency} vs PO total {po.TotalAmount:N2} {po.Currency} is {priceVariancePercent:+0.00;-0.00}% (Allowed: ±{tolerance.MaxPriceVariancePercent}%).");
        }

        // Check individual line item price creep
        foreach (var invItem in invoice.LineItems)
        {
            var poItem = po.LineItems.FirstOrDefault(p => string.Equals(p.Sku.Trim(), invItem.Sku.Trim(), StringComparison.OrdinalIgnoreCase));
            if (poItem is not null && poItem.UnitPrice > 0m)
            {
                var lineUnitPriceDiff = invItem.UnitPrice - poItem.UnitPrice;
                var lineUnitPriceVariancePercent = (lineUnitPriceDiff / poItem.UnitPrice) * 100m;
                if (lineUnitPriceVariancePercent > tolerance.MaxPriceVariancePercent)
                {
                    hasPriceDiscrepancy = true;
                    notes.Add($"Unit price creep on SKU '{invItem.Sku}': Invoiced unit price {invItem.UnitPrice:N2} exceeds PO price {poItem.UnitPrice:N2} by {lineUnitPriceVariancePercent:+0.00;-0.00}%.");
                }
            }
        }

        // 6. Tax Discrepancy Reconciliation
        bool hasTaxDiscrepancy = false;
        decimal expectedTax = po.ExpectedTaxAmount;
        if (expectedTax <= 0m && po.LineItems.Count > 0)
        {
            expectedTax = po.LineItems.Sum(p =>
            {
                var rate = p.TaxRate > 1.0m ? p.TaxRate / 100.0m : p.TaxRate;
                return Math.Round(p.LineTotal * rate, 2);
            });
        }

        if (expectedTax > 0m)
        {
            var taxDiff = Math.Abs(invoice.TaxAmount - expectedTax);
            if (taxDiff > tolerance.TaxTolerance)
            {
                hasTaxDiscrepancy = true;
                notes.Add($"Tax amount discrepancy: Invoice tax {invoice.TaxAmount:N2} differs from expected tax {expectedTax:N2} by {taxDiff:N2} (Tolerance: {tolerance.TaxTolerance}).");
            }
        }

        // 7. Determine Final Match Status
        ThreeWayMatchStatus status;
        if (hasQuantityDiscrepancy)
        {
            status = ThreeWayMatchStatus.QuantityVarianceDispute;
        }
        else if (hasPriceDiscrepancy)
        {
            status = ThreeWayMatchStatus.PriceVarianceDispute;
        }
        else if (hasTaxDiscrepancy)
        {
            status = ThreeWayMatchStatus.TaxDiscrepancy;
        }
        else
        {
            status = ThreeWayMatchStatus.FullMatch;
        }

        // Bank RIB discrepancy prevents auto-approval even if math matches
        bool bankMismatch = notes.Any(n => n.Contains("Bank account (RIB) discrepancy", StringComparison.OrdinalIgnoreCase));
        bool autoApproved = status == ThreeWayMatchStatus.FullMatch && !bankMismatch;

        // If auto-approved, register invoice in processed registry
        if (autoApproved)
        {
            RegisterProcessedInvoice(vendorKey, invoice.InvoiceNumber, invoice.Sha256Hash);
        }

        var result = new ThreeWayMatchResult(
            MatchStatus: status,
            PoId: po.PoId,
            GrnId: grn.GrnId,
            InvoiceId: invoice.InvoiceNumber,
            TotalPoAmount: po.TotalAmount,
            TotalGrnAmount: totalGrnAmount > 0m ? totalGrnAmount : po.TotalAmount,
            TotalInvoiceAmount: invoice.TotalAmount,
            PriceVariancePercent: priceVariancePercent,
            QuantityVariance: totalQuantityVariance,
            DiscrepancyNotes: notes,
            AutoApproved: autoApproved,
            ReconciledAtUtc: DateTime.UtcNow);

        return Task.FromResult(new ThreeWayMatchResponse(
            Success: autoApproved,
            Result: result,
            Message: autoApproved
                ? "3-way match verified successfully. Auto-approved for disbursement."
                : $"Reconciliation dispute flagged: {status}."));
    }

    /// <inheritdoc />
    public bool IsDuplicateInvoice(string vendorTaxId, string invoiceNumber, string sha256Hash)
    {
        var key1 = BuildInvoiceKey(vendorTaxId, invoiceNumber);
        if (_registeredInvoices.ContainsKey(key1)) return true;

        if (!string.IsNullOrWhiteSpace(sha256Hash) &&
            _registeredInvoices.ContainsKey($"HASH:{sha256Hash.ToLowerInvariant()}"))
        {
            return true;
        }

        return false;
    }

    /// <inheritdoc />
    public void RegisterProcessedInvoice(string vendorTaxId, string invoiceNumber, string sha256Hash)
    {
        var key = BuildInvoiceKey(vendorTaxId, invoiceNumber);
        _registeredInvoices[key] = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(sha256Hash))
        {
            _registeredInvoices[$"HASH:{sha256Hash.ToLowerInvariant()}"] = DateTime.UtcNow;
        }
    }

    /// <inheritdoc />
    public void ClearRegisteredInvoices()
    {
        _registeredInvoices.Clear();
    }

    #region Private Helpers

    private static string BuildInvoiceKey(string vendorTaxId, string invoiceNumber)
    {
        var v = NormalizeTaxId(vendorTaxId);
        var i = (invoiceNumber ?? "").Trim().ToUpperInvariant();
        return $"{v}:{i}";
    }

    private static string NormalizeTaxId(string taxId)
    {
        if (string.IsNullOrWhiteSpace(taxId)) return "DEFAULT";
        return taxId.Replace(" ", "").Replace("-", "").Replace("/", "").ToUpperInvariant();
    }

    private static string NormalizeRib(string rib)
    {
        if (string.IsNullOrWhiteSpace(rib)) return string.Empty;
        return rib.Replace(" ", "").Replace("-", "").ToUpperInvariant();
    }

    #endregion
}
