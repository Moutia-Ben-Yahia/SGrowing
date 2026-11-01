using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SGrowing.Api.Modules.Documents.Idp;
using Xunit;

namespace SGrowing.Tests.Documents;

/// <summary>
/// Unit test suite for Phase 24: Intelligent Document Processing (IDP),
/// Multi-Modal OCR &amp; 3-Way Invoice Reconciliation.
/// </summary>
public class IdpThreeWayMatchTests
{
    private readonly DocumentIntelligenceParserService _parserService = new();
    private readonly ThreeWayReconciliationService _reconciliationService = new();
    private readonly PhysicalPodVerificationService _podVerifier = new();

    public IdpThreeWayMatchTests()
    {
        _reconciliationService.ClearRegisteredInvoices();
    }

    #region 1. OCR Parsing, Spatial Token Extraction & Tabular Grid Tests

    [Fact]
    public async Task ParseInvoiceDocument_WithValidBoundingBoxes_ExtractsAllMandatoryFields()
    {
        List<OcrBoundingBox> tokens =
        [
            new(50, 40, 150, 20, "SARL MAGHREB INDUSTRIAL SUPPLY", 0.98),
            new(50, 70, 80, 15, "Fournisseur: SARL MAGHREB INDUSTRIAL SUPPLY", 0.97),
            new(50, 90, 80, 15, "NIF: 001916012345678", 0.99),
            new(200, 90, 80, 15, "RC: 16/00-0987654B16", 0.99),
            new(350, 90, 80, 15, "Facture N°: INV-2026-8891", 0.99),
            new(350, 110, 80, 15, "Date: 2026-11-01", 0.98),
            new(50, 130, 80, 15, "RIB: 00200123456789012345", 0.96),
            // Table Header
            new(50, 180, 60, 15, "SKU", 0.99),
            new(130, 180, 120, 15, "Description", 0.99),
            new(270, 180, 50, 15, "Quantity", 0.99),
            new(340, 180, 50, 15, "Price", 0.99),
            new(410, 180, 60, 15, "Total", 0.99),
            // Row 1
            new(50, 205, 60, 15, "SKU-VALVE-01", 0.97),
            new(130, 205, 120, 15, "High Pressure Hydraulic Valve", 0.97),
            new(270, 205, 50, 15, "10", 0.99),
            new(340, 205, 50, 15, "150.00", 0.99),
            new(410, 205, 60, 15, "1500.00", 0.99),
            // Row 2
            new(50, 225, 60, 15, "SKU-SEAL-02", 0.97),
            new(130, 225, 120, 15, "EPDM Gasket Ring 50mm", 0.97),
            new(270, 225, 50, 15, "20", 0.99),
            new(340, 225, 50, 15, "25.00", 0.99),
            new(410, 225, 60, 15, "500.00", 0.99),
            // Totals
            new(300, 280, 80, 15, "Total HT: 2000.00 DZD", 0.99),
            new(300, 300, 80, 15, "Total TVA (19%): 380.00 DZD", 0.99),
            new(300, 320, 80, 15, "Total TTC: 2380.00 DZD", 0.99)
        ];

        var req = new ParseDocumentRequest(
            DocumentId: "DOC-001",
            DocumentType: IdpDocumentType.SupplierInvoice,
            Tokens: tokens);

        var response = await _parserService.ParseInvoiceDocumentAsync(req);

        response.Success.Should().BeTrue();
        response.ExtractedData.Should().NotBeNull();

        var inv = response.ExtractedData!;
        inv.VendorName.Should().Contain("MAGHREB INDUSTRIAL SUPPLY");
        inv.VendorTaxId.Should().Contain("001916012345678");
        inv.InvoiceNumber.Should().Be("INV-2026-8891");
        inv.Currency.Should().Be("DZD");
        inv.BankRib.Should().Be("00200123456789012345");
        inv.Subtotal.Should().Be(2000.00m);
        inv.TaxAmount.Should().Be(380.00m);
        inv.TotalAmount.Should().Be(2380.00m);
        inv.LineItems.Should().HaveCount(2);
        inv.ConfidenceScore.Should().BeGreaterThan(0.85);
        inv.Sha256Hash.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ParseInvoiceDocument_WithNifAndRcTokens_ExtractsCompoundTaxIdentifier()
    {
        var rawText = "Fournisseur: Algiers Logistics Co\nNIF: 099812345678901\nRC: 16/00-1122334B16\nFacture N°: INV-9900\nTotal TTC: 1000.00 DZD";
        var req = new ParseDocumentRequest(
            DocumentId: "DOC-COMPOUND-TAX",
            DocumentType: IdpDocumentType.SupplierInvoice,
            RawText: rawText);

        var response = await _parserService.ParseInvoiceDocumentAsync(req);

        response.Success.Should().BeTrue();
        response.ExtractedData!.VendorTaxId.Should().Contain("099812345678901");
        response.ExtractedData.VendorTaxId.Should().Contain("16/00-1122334B16");
    }

    [Theory]
    [InlineData("Facture N°: 101\nTotal TTC: 500.00 DZD", "DZD")]
    [InlineData("Invoice #: 102\nTotal Amount: 500.00 EUR", "EUR")]
    [InlineData("Invoice #: 103\nTotal Amount: $500.00", "USD")]
    public async Task ParseInvoiceDocument_WithDifferentCurrencies_CorrectlyIdentifiesCurrency(string text, string expectedCurrency)
    {
        var req = new ParseDocumentRequest("DOC-CURR", IdpDocumentType.SupplierInvoice, RawText: text);
        var response = await _parserService.ParseInvoiceDocumentAsync(req);

        response.Success.Should().BeTrue();
        response.ExtractedData!.Currency.Should().Be(expectedCurrency);
    }

    [Fact]
    public async Task ParseInvoiceDocument_WithFrenchAmountFormatting_ParsesDecimalsCorrectly()
    {
        var text = "Facture N°: FR-123\nFournisseur: France Composants\nTotal HT: 1.250,50 EUR\nTotal TVA: 250,10 EUR\nTotal TTC: 1.500,60 EUR";
        var req = new ParseDocumentRequest("DOC-FR", IdpDocumentType.SupplierInvoice, RawText: text);

        var response = await _parserService.ParseInvoiceDocumentAsync(req);

        response.Success.Should().BeTrue();
        response.ExtractedData!.Subtotal.Should().Be(1250.50m);
        response.ExtractedData.TaxAmount.Should().Be(250.10m);
        response.ExtractedData.TotalAmount.Should().Be(1500.60m);
    }

    [Fact]
    public void ExtractTabularLineItems_WithSpatialRows_CorrectlyAlignsColumns()
    {
        List<OcrBoundingBox> tokens =
        [
            // Header
            new(50, 100, 40, 10, "SKU", 0.99),
            new(100, 100, 80, 10, "Description", 0.99),
            new(200, 100, 30, 10, "Qty", 0.99),
            new(250, 100, 30, 10, "Price", 0.99),
            new(300, 100, 40, 10, "Total", 0.99),
            // Item 1
            new(50, 120, 40, 10, "SKU-001", 0.98),
            new(100, 120, 80, 10, "Steel Bolt M8", 0.98),
            new(200, 120, 30, 10, "100", 0.99),
            new(250, 120, 30, 10, "1.50", 0.99),
            new(300, 120, 40, 10, "150.00", 0.99),
            // Item 2
            new(50, 140, 40, 10, "SKU-002", 0.98),
            new(100, 140, 80, 10, "Steel Nut M8", 0.98),
            new(200, 140, 30, 10, "100", 0.99),
            new(250, 140, 30, 10, "0.50", 0.99),
            new(300, 140, 40, 10, "50.00", 0.99)
        ];

        var items = _parserService.ExtractTabularLineItems(tokens);

        items.Should().HaveCount(2);
        items[0].Sku.Should().Be("SKU-001");
        items[0].Quantity.Should().Be(100m);
        items[0].UnitPrice.Should().Be(1.50m);
        items[0].LineTotal.Should().Be(150.00m);

        items[1].Sku.Should().Be("SKU-002");
        items[1].Quantity.Should().Be(100m);
        items[1].UnitPrice.Should().Be(0.50m);
        items[1].LineTotal.Should().Be(50.00m);
    }

    [Fact]
    public void ExtractTabularLineItems_WithHeaderAndFooter_ExcludesNonTableTokens()
    {
        List<OcrBoundingBox> tokens =
        [
            new(50, 20, 100, 10, "HEADER INFO", 0.9),
            // Header
            new(50, 100, 40, 10, "SKU", 0.99),
            new(100, 100, 80, 10, "Description", 0.99),
            new(200, 100, 30, 10, "Qty", 0.99),
            new(250, 100, 30, 10, "Price", 0.99),
            // Row
            new(50, 120, 40, 10, "SKU-100", 0.98),
            new(100, 120, 80, 10, "Industrial Bearing", 0.98),
            new(200, 120, 30, 10, "5", 0.99),
            new(250, 120, 30, 10, "100.00", 0.99),
            // Footer
            new(50, 200, 100, 10, "Total HT: 500.00", 0.99)
        ];

        var items = _parserService.ExtractTabularLineItems(tokens);

        items.Should().HaveCount(1);
        items[0].Sku.Should().Be("SKU-100");
        items[0].LineTotal.Should().Be(500.00m);
    }

    [Fact]
    public void ComputeDocumentSha256_ComputesConsistentLowercaseHexDigest()
    {
        var text = "Facture N° 12345 - Test Sha256";
        var hash1 = _parserService.ComputeDocumentSha256(text);
        var hash2 = _parserService.ComputeDocumentSha256(text);

        hash1.Should().Be(hash2);
        hash1.Should().MatchRegex("^[a-f0-9]{64}$");
    }

    [Fact]
    public async Task ParseInvoiceDocument_WithEmptyTokens_HandlesGracefullyWithoutThrowing()
    {
        var req = new ParseDocumentRequest("DOC-EMPTY", IdpDocumentType.SupplierInvoice, Tokens: []);
        var response = await _parserService.ParseInvoiceDocumentAsync(req);

        response.Success.Should().BeTrue();
        response.ExtractedData.Should().NotBeNull();
        response.ExtractedData!.LineItems.Should().BeEmpty();
    }

    [Fact]
    public async Task ParseInvoiceDocument_ExtractsBankRibAccurately()
    {
        var text = "Fournisseur: ACME\nFacture N°: 9988\nRIB: 001 002 1234567890 99\nTotal TTC: 100.00 DZD";
        var req = new ParseDocumentRequest("DOC-RIB", IdpDocumentType.SupplierInvoice, RawText: text);
        var response = await _parserService.ParseInvoiceDocumentAsync(req);

        response.Success.Should().BeTrue();
        response.ExtractedData!.BankRib.Should().Be("001002123456789099");
    }

    [Fact]
    public async Task CalculateConfidenceScore_WithCompleteInvoice_ScoresHighConfidence()
    {
        var text = "Fournisseur: Tech Corp\nNIF: 1234567890\nFacture N°: INV-77\nDate: 2026-11-01\nTotal TTC: 100.00 DZD";
        var req = new ParseDocumentRequest("DOC-HIGH-CONF", IdpDocumentType.SupplierInvoice, RawText: text);
        var response = await _parserService.ParseInvoiceDocumentAsync(req);

        response.ExtractedData!.ConfidenceScore.Should().BeGreaterThanOrEqualTo(0.80);
    }

    #endregion

    #region 2. 3-Way Reconciliation Core & Tolerance Tests

    [Fact]
    public async Task ReconcileAsync_WithExactMatchAcrossPoGrnAndInvoice_AutoApprovesSuccessfully()
    {
        var po = new PurchaseOrderRecord(
            PoId: "PO-2026-001",
            VendorName: "Maghreb Industrial Supply",
            VendorTaxId: "NIF-123456789",
            Currency: "DZD",
            LineItems:
            [
                new("SKU-001", "Hydraulic Pump", 10m, 100m, 1000m, 0.19m)
            ],
            TotalAmount: 1190m,
            BankRib: "00200123456789012345",
            ExpectedTaxAmount: 190m);

        var grn = new GoodsReceiptNoteRecord(
            GrnId: "GRN-2026-001",
            PoId: "PO-2026-001",
            ReceivedDateUtc: DateTime.UtcNow,
            ReceivedItems:
            [
                new("SKU-001", AcceptedQuantity: 10m)
            ]);

        var inv = new ExtractedInvoiceData(
            DocumentId: "DOC-INV-001",
            VendorName: "Maghreb Industrial Supply",
            VendorTaxId: "NIF-123456789",
            InvoiceNumber: "INV-9001",
            InvoiceDate: DateTime.UtcNow,
            Currency: "DZD",
            LineItems:
            [
                new(1, "SKU-001", "Hydraulic Pump", 10m, 100m, 1000m, 0.19m)
            ],
            Subtotal: 1000m,
            TaxAmount: 190m,
            TotalAmount: 1190m,
            BankRib: "00200123456789012345",
            ConfidenceScore: 0.98,
            Sha256Hash: "hash-001");

        var response = await _reconciliationService.ReconcileAsync(new ThreeWayMatchRequest(po, grn, inv));

        response.Success.Should().BeTrue();
        response.Result.AutoApproved.Should().BeTrue();
        response.Result.MatchStatus.Should().Be(ThreeWayMatchStatus.FullMatch);
        response.Result.PriceVariancePercent.Should().Be(0m);
        response.Result.QuantityVariance.Should().Be(0m);
        response.Result.DiscrepancyNotes.Should().BeEmpty();
    }

    [Fact]
    public async Task ReconcileAsync_WithPriceVarianceWithinTolerance_AutoApproves()
    {
        // Allowed tolerance: 0.5%. Price delta: 1190 to 1193 (+0.25%)
        var po = new PurchaseOrderRecord(
            PoId: "PO-TOL-001",
            VendorName: "Vendor A",
            VendorTaxId: "TAX-1",
            Currency: "DZD",
            LineItems: [new("SKU-A", "Item A", 10m, 100m, 1000m, 0.19m)],
            TotalAmount: 1190m,
            ExpectedTaxAmount: 190m);

        var grn = new GoodsReceiptNoteRecord("GRN-1", "PO-TOL-001", DateTime.UtcNow, [new("SKU-A", 10m)]);

        var inv = new ExtractedInvoiceData(
            DocumentId: "DOC-1",
            VendorName: "Vendor A",
            VendorTaxId: "TAX-1",
            InvoiceNumber: "INV-TOL-01",
            InvoiceDate: DateTime.UtcNow,
            Currency: "DZD",
            LineItems: [new(1, "SKU-A", "Item A", 10m, 100.25m, 1002.50m, 0.19m)],
            Subtotal: 1002.50m,
            TaxAmount: 190.48m,
            TotalAmount: 1192.98m, // ~+0.25% variance <= 0.5%
            BankRib: "N/A",
            ConfidenceScore: 0.95,
            Sha256Hash: "hash-tol-1");

        var response = await _reconciliationService.ReconcileAsync(
            new ThreeWayMatchRequest(po, grn, inv, new ToleranceOptions(MaxPriceVariancePercent: 0.5m, TaxTolerance: 1.0m)));

        response.Success.Should().BeTrue();
        response.Result.AutoApproved.Should().BeTrue();
        response.Result.MatchStatus.Should().Be(ThreeWayMatchStatus.FullMatch);
    }

    [Fact]
    public async Task ReconcileAsync_WithPriceVarianceExceedingTolerance_GeneratesPriceVarianceDispute()
    {
        // PO: 1000.00, Invoice: 1020.00 (+2.0% > 0.5%)
        var po = new PurchaseOrderRecord(
            PoId: "PO-DISPUTE-PRICE",
            VendorName: "Vendor B",
            VendorTaxId: "TAX-2",
            Currency: "DZD",
            LineItems: [new("SKU-B", "Item B", 10m, 100m, 1000m, 0m)],
            TotalAmount: 1000m);

        var grn = new GoodsReceiptNoteRecord("GRN-2", "PO-DISPUTE-PRICE", DateTime.UtcNow, [new("SKU-B", 10m)]);

        var inv = new ExtractedInvoiceData(
            DocumentId: "DOC-2",
            VendorName: "Vendor B",
            VendorTaxId: "TAX-2",
            InvoiceNumber: "INV-HIGH-PRICE",
            InvoiceDate: DateTime.UtcNow,
            Currency: "DZD",
            LineItems: [new(1, "SKU-B", "Item B", 10m, 102m, 1020m, 0m)],
            Subtotal: 1020m,
            TaxAmount: 0m,
            TotalAmount: 1020m,
            BankRib: "N/A",
            ConfidenceScore: 0.95,
            Sha256Hash: "hash-price-2");

        var response = await _reconciliationService.ReconcileAsync(new ThreeWayMatchRequest(po, grn, inv));

        response.Success.Should().BeFalse();
        response.Result.AutoApproved.Should().BeFalse();
        response.Result.MatchStatus.Should().Be(ThreeWayMatchStatus.PriceVarianceDispute);
        response.Result.PriceVariancePercent.Should().Be(2.0m);
        response.Result.DiscrepancyNotes.Should().Contain(n => n.Contains("Price variance exceeds tolerance"));
    }

    [Fact]
    public async Task ReconcileAsync_WithNegativePriceVariance_ExceedingTolerance_GeneratesPriceVarianceDispute()
    {
        // Invoice is 2% lower than PO: 980 vs 1000 (-2.0% > 0.5% absolute)
        var po = new PurchaseOrderRecord(
            PoId: "PO-UNDER-BILL",
            VendorName: "Vendor C",
            VendorTaxId: "TAX-3",
            Currency: "DZD",
            LineItems: [new("SKU-C", "Item C", 10m, 100m, 1000m, 0m)],
            TotalAmount: 1000m);

        var grn = new GoodsReceiptNoteRecord("GRN-3", "PO-UNDER-BILL", DateTime.UtcNow, [new("SKU-C", 10m)]);

        var inv = new ExtractedInvoiceData(
            DocumentId: "DOC-3",
            VendorName: "Vendor C",
            VendorTaxId: "TAX-3",
            InvoiceNumber: "INV-UNDER",
            InvoiceDate: DateTime.UtcNow,
            Currency: "DZD",
            LineItems: [new(1, "SKU-C", "Item C", 10m, 98m, 980m, 0m)],
            Subtotal: 980m,
            TaxAmount: 0m,
            TotalAmount: 980m,
            BankRib: "N/A",
            ConfidenceScore: 0.95,
            Sha256Hash: "hash-under");

        var response = await _reconciliationService.ReconcileAsync(new ThreeWayMatchRequest(po, grn, inv));

        response.Success.Should().BeFalse();
        response.Result.MatchStatus.Should().Be(ThreeWayMatchStatus.PriceVarianceDispute);
        response.Result.PriceVariancePercent.Should().Be(-2.0m);
    }

    [Fact]
    public async Task ReconcileAsync_WithLineItemPriceCreep_FlagsDisputeEvenIfTotalIsAdjusted()
    {
        // Line unit price increased from 100 to 110 (+10%), even if quantity or other items shifted
        var po = new PurchaseOrderRecord(
            PoId: "PO-LINE-CREEP",
            VendorName: "Vendor D",
            VendorTaxId: "TAX-4",
            Currency: "EUR",
            LineItems: [new("SKU-CREEP", "Creep Item", 10m, 100m, 1000m, 0m)],
            TotalAmount: 1000m);

        var grn = new GoodsReceiptNoteRecord("GRN-4", "PO-LINE-CREEP", DateTime.UtcNow, [new("SKU-CREEP", 10m)]);

        var inv = new ExtractedInvoiceData(
            DocumentId: "DOC-4",
            VendorName: "Vendor D",
            VendorTaxId: "TAX-4",
            InvoiceNumber: "INV-CREEP",
            InvoiceDate: DateTime.UtcNow,
            Currency: "EUR",
            LineItems: [new(1, "SKU-CREEP", "Creep Item", 10m, 110m, 1100m, 0m)],
            Subtotal: 1100m,
            TaxAmount: 0m,
            TotalAmount: 1100m,
            BankRib: "N/A",
            ConfidenceScore: 0.95,
            Sha256Hash: "hash-creep");

        var response = await _reconciliationService.ReconcileAsync(new ThreeWayMatchRequest(po, grn, inv));

        response.Success.Should().BeFalse();
        response.Result.MatchStatus.Should().Be(ThreeWayMatchStatus.PriceVarianceDispute);
        response.Result.DiscrepancyNotes.Should().Contain(n => n.Contains("Unit price creep on SKU 'SKU-CREEP'"));
    }

    [Fact]
    public async Task ReconcileAsync_WithQuantityShortage_GeneratesQuantityVarianceDispute()
    {
        // PO ordered 50, GRN accepted 40 (10 damaged/short), Invoice billed 50
        var po = new PurchaseOrderRecord(
            PoId: "PO-SHORTAGE",
            VendorName: "Vendor E",
            VendorTaxId: "TAX-5",
            Currency: "DZD",
            LineItems: [new("SKU-SHORT", "Widget", 50m, 10m, 500m, 0m)],
            TotalAmount: 500m);

        var grn = new GoodsReceiptNoteRecord(
            "GRN-SHORT",
            "PO-SHORTAGE",
            DateTime.UtcNow,
            [new("SKU-SHORT", AcceptedQuantity: 40m, RejectedQuantity: 10m, RejectionReason: "Damaged in transit")]);

        var inv = new ExtractedInvoiceData(
            DocumentId: "DOC-SHORT",
            VendorName: "Vendor E",
            VendorTaxId: "TAX-5",
            InvoiceNumber: "INV-SHORT",
            InvoiceDate: DateTime.UtcNow,
            Currency: "DZD",
            LineItems: [new(1, "SKU-SHORT", "Widget", 50m, 10m, 500m, 0m)],
            Subtotal: 500m,
            TaxAmount: 0m,
            TotalAmount: 500m,
            BankRib: "N/A",
            ConfidenceScore: 0.95,
            Sha256Hash: "hash-short");

        var response = await _reconciliationService.ReconcileAsync(new ThreeWayMatchRequest(po, grn, inv));

        response.Success.Should().BeFalse();
        response.Result.AutoApproved.Should().BeFalse();
        response.Result.MatchStatus.Should().Be(ThreeWayMatchStatus.QuantityVarianceDispute);
        response.Result.QuantityVariance.Should().Be(10m);
        response.Result.DiscrepancyNotes.Should().Contain(n => n.Contains("Quantity shortage for SKU 'SKU-SHORT'"));
    }

    [Fact]
    public async Task ReconcileAsync_WithBilledSkuMissingFromGrn_GeneratesQuantityVarianceDispute()
    {
        var po = new PurchaseOrderRecord(
            PoId: "PO-MISSING-SKU",
            VendorName: "Vendor F",
            VendorTaxId: "TAX-6",
            Currency: "DZD",
            LineItems: [new("SKU-GHOST", "Ghost Product", 5m, 100m, 500m, 0m)],
            TotalAmount: 500m);

        // GRN has a different SKU or empty
        var grn = new GoodsReceiptNoteRecord("GRN-GHOST", "PO-MISSING-SKU", DateTime.UtcNow, []);

        var inv = new ExtractedInvoiceData(
            DocumentId: "DOC-GHOST",
            VendorName: "Vendor F",
            VendorTaxId: "TAX-6",
            InvoiceNumber: "INV-GHOST",
            InvoiceDate: DateTime.UtcNow,
            Currency: "DZD",
            LineItems: [new(1, "SKU-GHOST", "Ghost Product", 5m, 100m, 500m, 0m)],
            Subtotal: 500m,
            TaxAmount: 0m,
            TotalAmount: 500m,
            BankRib: "N/A",
            ConfidenceScore: 0.95,
            Sha256Hash: "hash-ghost");

        var response = await _reconciliationService.ReconcileAsync(new ThreeWayMatchRequest(po, grn, inv));

        response.Success.Should().BeFalse();
        response.Result.MatchStatus.Should().Be(ThreeWayMatchStatus.QuantityVarianceDispute);
        response.Result.DiscrepancyNotes.Should().Contain(n => n.Contains("was not received on GRN"));
    }

    [Fact]
    public async Task ReconcileAsync_WithMultipleQuantityShortages_AggregatesTotalQuantityVariance()
    {
        var po = new PurchaseOrderRecord(
            PoId: "PO-MULTI-SHORT",
            VendorName: "Vendor G",
            VendorTaxId: "TAX-7",
            Currency: "DZD",
            LineItems:
            [
                new("SKU-M1", "Item 1", 20m, 10m, 200m),
                new("SKU-M2", "Item 2", 30m, 10m, 300m)
            ],
            TotalAmount: 500m);

        var grn = new GoodsReceiptNoteRecord("GRN-MULTI", "PO-MULTI-SHORT", DateTime.UtcNow,
        [
            new("SKU-M1", AcceptedQuantity: 15m), // Short 5
            new("SKU-M2", AcceptedQuantity: 20m)  // Short 10
        ]);

        var inv = new ExtractedInvoiceData(
            DocumentId: "DOC-MULTI",
            VendorName: "Vendor G",
            VendorTaxId: "TAX-7",
            InvoiceNumber: "INV-MULTI",
            InvoiceDate: DateTime.UtcNow,
            Currency: "DZD",
            LineItems:
            [
                new(1, "SKU-M1", "Item 1", 20m, 10m, 200m, 0m),
                new(2, "SKU-M2", "Item 2", 30m, 10m, 300m, 0m)
            ],
            Subtotal: 500m,
            TaxAmount: 0m,
            TotalAmount: 500m,
            BankRib: "N/A",
            ConfidenceScore: 0.95,
            Sha256Hash: "hash-multi");

        var response = await _reconciliationService.ReconcileAsync(new ThreeWayMatchRequest(po, grn, inv));

        response.Result.MatchStatus.Should().Be(ThreeWayMatchStatus.QuantityVarianceDispute);
        response.Result.QuantityVariance.Should().Be(15m); // 5 + 10
        response.Result.DiscrepancyNotes.Should().HaveCount(2);
    }

    [Fact]
    public async Task ReconcileAsync_WithTaxDifferenceExceedingOneCent_GeneratesTaxDiscrepancy()
    {
        var po = new PurchaseOrderRecord(
            PoId: "PO-TAX-DISC",
            VendorName: "Vendor H",
            VendorTaxId: "TAX-8",
            Currency: "EUR",
            LineItems: [new("SKU-TAX", "Taxable Goods", 10m, 100m, 1000m, 0.20m)],
            TotalAmount: 1200m,
            ExpectedTaxAmount: 200.00m);

        var grn = new GoodsReceiptNoteRecord("GRN-TAX", "PO-TAX-DISC", DateTime.UtcNow, [new("SKU-TAX", 10m)]);

        var inv = new ExtractedInvoiceData(
            DocumentId: "DOC-TAX",
            VendorName: "Vendor H",
            VendorTaxId: "TAX-8",
            InvoiceNumber: "INV-TAX-ERR",
            InvoiceDate: DateTime.UtcNow,
            Currency: "EUR",
            LineItems: [new(1, "SKU-TAX", "Taxable Goods", 10m, 100m, 1000m, 0.20m)],
            Subtotal: 1000m,
            TaxAmount: 205.00m, // Difference of 5.00 EUR > 0.01 tolerance
            TotalAmount: 1205m,
            BankRib: "N/A",
            ConfidenceScore: 0.95,
            Sha256Hash: "hash-tax");

        var response = await _reconciliationService.ReconcileAsync(new ThreeWayMatchRequest(po, grn, inv));

        response.Success.Should().BeFalse();
        response.Result.DiscrepancyNotes.Should().Contain(n => n.Contains("Tax amount discrepancy"));
    }

    [Fact]
    public async Task ReconcileAsync_WithTaxDifferenceWithinOneCent_PassesTaxReconciliation()
    {
        var po = new PurchaseOrderRecord(
            PoId: "PO-TAX-OK",
            VendorName: "Vendor H",
            VendorTaxId: "TAX-8",
            Currency: "EUR",
            LineItems: [new("SKU-TAX", "Taxable Goods", 10m, 100m, 1000m, 0.20m)],
            TotalAmount: 1200.01m,
            ExpectedTaxAmount: 200.00m);

        var grn = new GoodsReceiptNoteRecord("GRN-TAX-OK", "PO-TAX-OK", DateTime.UtcNow, [new("SKU-TAX", 10m)]);

        var inv = new ExtractedInvoiceData(
            DocumentId: "DOC-TAX-OK",
            VendorName: "Vendor H",
            VendorTaxId: "TAX-8",
            InvoiceNumber: "INV-TAX-OK",
            InvoiceDate: DateTime.UtcNow,
            Currency: "EUR",
            LineItems: [new(1, "SKU-TAX", "Taxable Goods", 10m, 100m, 1000m, 0.20m)],
            Subtotal: 1000m,
            TaxAmount: 200.01m, // Within 0.01 cent tolerance
            TotalAmount: 1200.01m,
            BankRib: "N/A",
            ConfidenceScore: 0.95,
            Sha256Hash: "hash-tax-ok");

        var response = await _reconciliationService.ReconcileAsync(new ThreeWayMatchRequest(po, grn, inv));

        response.Success.Should().BeTrue();
        response.Result.MatchStatus.Should().Be(ThreeWayMatchStatus.FullMatch);
    }

    [Fact]
    public async Task ReconcileAsync_WithUnmatchedPoReference_RejectsImmediately()
    {
        var po = new PurchaseOrderRecord("PO-REAL-123", "Vendor X", "TAX-X", "DZD", [], 500m);
        var grn = new GoodsReceiptNoteRecord("GRN-WRONG", "PO-DIFFERENT-456", DateTime.UtcNow, []);
        var inv = new ExtractedInvoiceData("DOC", "Vendor X", "TAX-X", "INV-1", DateTime.UtcNow, "DZD", [], 500m, 0m, 500m, "N/A", 0.9, "h1");

        var response = await _reconciliationService.ReconcileAsync(new ThreeWayMatchRequest(po, grn, inv));

        response.Success.Should().BeFalse();
        response.Result.MatchStatus.Should().Be(ThreeWayMatchStatus.UnmatchedPo);
        response.Result.DiscrepancyNotes.Should().Contain(n => n.Contains("PO reference mismatch"));
    }

    [Fact]
    public async Task ReconcileAsync_WithCustomTolerances_RespectsConfiguredThresholds()
    {
        // Custom loose tolerance of 5% price variance
        var po = new PurchaseOrderRecord("PO-CUSTOM", "Vendor", "TAX", "DZD", [new("SKU", "Item", 1m, 100m, 100m)], 100m);
        var grn = new GoodsReceiptNoteRecord("GRN-CUSTOM", "PO-CUSTOM", DateTime.UtcNow, [new("SKU", 1m)]);
        var inv = new ExtractedInvoiceData("DOC", "Vendor", "TAX", "INV-CUSTOM", DateTime.UtcNow, "DZD", [new(1, "SKU", "Item", 1m, 103m, 103m, 0m)], 103m, 0m, 103m, "N/A", 0.9, "hcustom");

        var looseTolerance = new ToleranceOptions(MaxPriceVariancePercent: 5.0m);
        var response = await _reconciliationService.ReconcileAsync(new ThreeWayMatchRequest(po, grn, inv, looseTolerance));

        response.Success.Should().BeTrue();
        response.Result.AutoApproved.Should().BeTrue();
    }

    #endregion

    #region 3. Security Screening & Duplicate Prevention Tests

    [Fact]
    public async Task ReconcileAsync_WithDuplicateInvoiceNumber_BlocksDuplicateAndGeneratesDispute()
    {
        var po = new PurchaseOrderRecord("PO-DUP-1", "Vendor DUP", "TAX-DUP", "DZD", [new("SKU", "Item", 1m, 100m, 100m)], 100m);
        var grn = new GoodsReceiptNoteRecord("GRN-DUP-1", "PO-DUP-1", DateTime.UtcNow, [new("SKU", 1m)]);
        var inv = new ExtractedInvoiceData("DOC-DUP-1", "Vendor DUP", "TAX-DUP", "INV-DUP-100", DateTime.UtcNow, "DZD", [new(1, "SKU", "Item", 1m, 100m, 100m, 0m)], 100m, 0m, 100m, "N/A", 0.95, "hash-first");

        // 1st submission: should succeed and auto-approve
        var firstResponse = await _reconciliationService.ReconcileAsync(new ThreeWayMatchRequest(po, grn, inv));
        firstResponse.Success.Should().BeTrue();

        // 2nd submission with same invoice number: must be blocked as duplicate!
        var duplicateInv = inv with { Sha256Hash = "hash-different" };
        var secondResponse = await _reconciliationService.ReconcileAsync(new ThreeWayMatchRequest(po, grn, duplicateInv));

        secondResponse.Success.Should().BeFalse();
        secondResponse.Result.MatchStatus.Should().Be(ThreeWayMatchStatus.DuplicateInvoiceBlocked);
        secondResponse.Result.DiscrepancyNotes.Should().Contain(n => n.Contains("Duplicate invoice detected"));
    }

    [Fact]
    public async Task ReconcileAsync_WithDuplicateSha256Hash_BlocksDuplicateSubmission()
    {
        var po = new PurchaseOrderRecord("PO-DUP-HASH", "Vendor DUP2", "TAX-DUP2", "DZD", [new("SKU", "Item", 1m, 50m, 50m)], 50m);
        var grn = new GoodsReceiptNoteRecord("GRN-DUP-HASH", "PO-DUP-HASH", DateTime.UtcNow, [new("SKU", 1m)]);
        var inv1 = new ExtractedInvoiceData("DOC-1", "Vendor DUP2", "TAX-DUP2", "INV-A", DateTime.UtcNow, "DZD", [new(1, "SKU", "Item", 1m, 50m, 50m, 0m)], 50m, 0m, 50m, "N/A", 0.95, "exact-file-hash-999");

        await _reconciliationService.ReconcileAsync(new ThreeWayMatchRequest(po, grn, inv1));

        // Submitting same file with different invoice number
        var inv2 = inv1 with { InvoiceNumber = "INV-B-RENAMED", Sha256Hash = "exact-file-hash-999" };
        var dupResponse = await _reconciliationService.ReconcileAsync(new ThreeWayMatchRequest(po, grn, inv2));

        dupResponse.Success.Should().BeFalse();
        dupResponse.Result.MatchStatus.Should().Be(ThreeWayMatchStatus.DuplicateInvoiceBlocked);
    }

    [Fact]
    public async Task ReconcileAsync_WithBankRibMismatch_FlagsFraudulentDiversionRiskAndDeniesAutoApproval()
    {
        // PO master specifies vendor's verified RIB, but Invoice specifies a different attacker bank account
        var po = new PurchaseOrderRecord(
            PoId: "PO-RIB-CHECK",
            VendorName: "Vendor Safe",
            VendorTaxId: "TAX-SAFE",
            Currency: "DZD",
            LineItems: [new("SKU", "Item", 1m, 100m, 100m)],
            TotalAmount: 100m,
            BankRib: "00200111111111111111");

        var grn = new GoodsReceiptNoteRecord("GRN-RIB", "PO-RIB-CHECK", DateTime.UtcNow, [new("SKU", 1m)]);

        var inv = new ExtractedInvoiceData(
            DocumentId: "DOC-SUSP-RIB",
            VendorName: "Vendor Safe",
            VendorTaxId: "TAX-SAFE",
            InvoiceNumber: "INV-RIB-1",
            InvoiceDate: DateTime.UtcNow,
            Currency: "DZD",
            LineItems: [new(1, "SKU", "Item", 1m, 100m, 100m, 0m)],
            Subtotal: 100m,
            TaxAmount: 0m,
            TotalAmount: 100m,
            BankRib: "00599988888888888888", // Hijacked bank account!
            ConfidenceScore: 0.95,
            Sha256Hash: "hash-rib-mismatch");

        var response = await _reconciliationService.ReconcileAsync(new ThreeWayMatchRequest(po, grn, inv));

        response.Success.Should().BeFalse();
        response.Result.AutoApproved.Should().BeFalse();
        response.Result.DiscrepancyNotes.Should().Contain(n => n.Contains("Bank account (RIB) discrepancy") && n.Contains("Potential fraudulent bank alteration risk"));
    }

    [Fact]
    public async Task ReconcileAsync_WithMatchingBankRib_AutoApproves()
    {
        var po = new PurchaseOrderRecord(
            PoId: "PO-RIB-MATCH",
            VendorName: "Vendor Safe",
            VendorTaxId: "TAX-SAFE-2",
            Currency: "DZD",
            LineItems: [new("SKU", "Item", 1m, 100m, 100m)],
            TotalAmount: 100m,
            BankRib: "002 001 111111111111 11");

        var grn = new GoodsReceiptNoteRecord("GRN-RIB-MATCH", "PO-RIB-MATCH", DateTime.UtcNow, [new("SKU", 1m)]);

        var inv = new ExtractedInvoiceData(
            DocumentId: "DOC-OK-RIB",
            VendorName: "Vendor Safe",
            VendorTaxId: "TAX-SAFE-2",
            InvoiceNumber: "INV-RIB-OK",
            InvoiceDate: DateTime.UtcNow,
            Currency: "DZD",
            LineItems: [new(1, "SKU", "Item", 1m, 100m, 100m, 0m)],
            Subtotal: 100m,
            TaxAmount: 0m,
            TotalAmount: 100m,
            BankRib: "00200111111111111111", // Same normalized digits
            ConfidenceScore: 0.95,
            Sha256Hash: "hash-rib-ok");

        var response = await _reconciliationService.ReconcileAsync(new ThreeWayMatchRequest(po, grn, inv));

        response.Success.Should().BeTrue();
        response.Result.AutoApproved.Should().BeTrue();
    }

    [Fact]
    public void IsDuplicateInvoice_WhenNotRegistered_ReturnsFalse()
    {
        var isDup = _reconciliationService.IsDuplicateInvoice("TAX-NON-EXIST", "INV-NONE", "hash-none");
        isDup.Should().BeFalse();
    }

    [Fact]
    public void ClearRegisteredInvoices_AllowsReSubmissionAfterReset()
    {
        _reconciliationService.RegisterProcessedInvoice("TAX-TEST", "INV-TEST", "hash-test");
        _reconciliationService.IsDuplicateInvoice("TAX-TEST", "INV-TEST", "hash-test").Should().BeTrue();

        _reconciliationService.ClearRegisteredInvoices();
        _reconciliationService.IsDuplicateInvoice("TAX-TEST", "INV-TEST", "hash-test").Should().BeFalse();
    }

    #endregion

    #region 4. Physical Paper POD Delivery Slip Verification Tests

    [Fact]
    public async Task VerifyPaperPodAsync_WithHighConfidenceSignatureAndStamp_ReturnsVerified()
    {
        List<OcrBoundingBox> tokens =
        [
            new(50, 50, 200, 20, "BON DE LIVRAISON N° BL-2026-441", 0.99),
            new(50, 80, 150, 15, "Client: Entreprise Nationale", 0.95),
            // Hand-drawn signature
            new(100, 600, 120, 40, "[Signature]", 0.92, BlockType: "Signature", IsHandwritten: true),
            // Merchant stamp
            new(300, 600, 80, 80, "SARL TRANS-MAGHREB CACHET COMMERCIAL", 0.88, BlockType: "Stamp", IsStamp: true)
        ];

        var req = new VerifyPaperPodRequest("POD-DOC-001", Tokens: tokens);
        var response = await _podVerifier.VerifyPaperPodAsync(req);

        response.Success.Should().BeTrue();
        response.Result.StampStatus.Should().Be(PodStampStatus.Verified);
        response.Result.RecipientSignatureDetected.Should().BeTrue();
        response.Result.OfficialStampDetected.Should().BeTrue();
        response.Result.OverallConfidence.Should().BeGreaterThanOrEqualTo(0.85);
    }

    [Fact]
    public async Task VerifyPaperPodAsync_WithStampOnly_ReturnsMissingSignature()
    {
        List<OcrBoundingBox> tokens =
        [
            new(50, 50, 200, 20, "BON DE LIVRAISON BL-100", 0.99),
            // Only official stamp, no handwriting
            new(300, 600, 80, 80, "CACHET CLIENT SARL", 0.90, BlockType: "Stamp", IsStamp: true)
        ];

        var req = new VerifyPaperPodRequest("POD-NO-SIG", Tokens: tokens);
        var response = await _podVerifier.VerifyPaperPodAsync(req);

        response.Success.Should().BeFalse();
        response.Result.StampStatus.Should().Be(PodStampStatus.MissingSignature);
        response.Result.RecipientSignatureDetected.Should().BeFalse();
        response.Result.OfficialStampDetected.Should().BeTrue();
    }

    [Fact]
    public async Task VerifyPaperPodAsync_WithSignatureOnly_ReturnsMissingStamp()
    {
        List<OcrBoundingBox> tokens =
        [
            new(50, 50, 200, 20, "BON DE LIVRAISON BL-101", 0.99),
            // Signature only, no company stamp
            new(100, 600, 120, 40, "Signature Reçu", 0.93, BlockType: "Signature", IsHandwritten: true)
        ];

        var req = new VerifyPaperPodRequest("POD-NO-STAMP", Tokens: tokens);
        var response = await _podVerifier.VerifyPaperPodAsync(req);

        response.Success.Should().BeFalse();
        response.Result.StampStatus.Should().Be(PodStampStatus.MissingStamp);
        response.Result.RecipientSignatureDetected.Should().BeTrue();
        response.Result.OfficialStampDetected.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyPaperPodAsync_WithLowConfidenceSignature_ReturnsMissingSignature()
    {
        List<OcrBoundingBox> tokens =
        [
            // Confidence 0.70 < 0.85 threshold
            new(100, 600, 120, 40, "[Faint scribble]", 0.70, BlockType: "Signature", IsHandwritten: true),
            new(300, 600, 80, 80, "CACHET SARL", 0.90, BlockType: "Stamp", IsStamp: true)
        ];

        var req = new VerifyPaperPodRequest("POD-LOW-SIG", Tokens: tokens, MinSignatureConfidence: 0.85);
        var response = await _podVerifier.VerifyPaperPodAsync(req);

        response.Success.Should().BeFalse();
        response.Result.StampStatus.Should().Be(PodStampStatus.MissingSignature);
    }

    [Fact]
    public async Task VerifyPaperPodAsync_WithLowConfidenceStamp_ReturnsMissingStamp()
    {
        List<OcrBoundingBox> tokens =
        [
            new(100, 600, 120, 40, "[Signature]", 0.90, BlockType: "Signature", IsHandwritten: true),
            // Stamp confidence 0.72 < 0.80 threshold
            new(300, 600, 80, 80, "CACHET", 0.72, BlockType: "Stamp", IsStamp: true)
        ];

        var req = new VerifyPaperPodRequest("POD-LOW-STAMP", Tokens: tokens, MinStampConfidence: 0.80);
        var response = await _podVerifier.VerifyPaperPodAsync(req);

        response.Success.Should().BeFalse();
        response.Result.StampStatus.Should().Be(PodStampStatus.MissingStamp);
    }

    [Fact]
    public async Task VerifyPaperPodAsync_WithExplicitForgeryFlag_ReturnsSuspectedForgery()
    {
        var req = new VerifyPaperPodRequest("POD-FORGED-1", HasForgeryMarkers: true);
        var response = await _podVerifier.VerifyPaperPodAsync(req);

        response.Success.Should().BeFalse();
        response.Result.StampStatus.Should().Be(PodStampStatus.SuspectedForgery);
        response.Result.Notes.Should().Contain("forgery flagged");
    }

    [Fact]
    public async Task VerifyPaperPodAsync_WithFacsimileTokenMarker_ReturnsSuspectedForgery()
    {
        List<OcrBoundingBox> tokens =
        [
            new(50, 50, 100, 20, "BON DE LIVRAISON", 0.99),
            new(200, 200, 150, 30, "COPIE NON CONFORME / FACSIMILE", 0.95),
            new(100, 600, 120, 40, "[Signature]", 0.90, BlockType: "Signature", IsHandwritten: true),
            new(300, 600, 80, 80, "CACHET", 0.90, BlockType: "Stamp", IsStamp: true)
        ];

        var req = new VerifyPaperPodRequest("POD-FORGED-2", Tokens: tokens);
        var response = await _podVerifier.VerifyPaperPodAsync(req);

        response.Success.Should().BeFalse();
        response.Result.StampStatus.Should().Be(PodStampStatus.SuspectedForgery);
    }

    [Fact]
    public async Task VerifyPaperPodAsync_WithMissingBoth_FlagsMissingSignature()
    {
        List<OcrBoundingBox> tokens =
        [
            new(50, 50, 100, 20, "Delivery Slip Blank", 0.99)
        ];

        var req = new VerifyPaperPodRequest("POD-EMPTY", Tokens: tokens);
        var response = await _podVerifier.VerifyPaperPodAsync(req);

        response.Success.Should().BeFalse();
        response.Result.RecipientSignatureDetected.Should().BeFalse();
        response.Result.OfficialStampDetected.Should().BeFalse();
    }

    #endregion

    #region 5. Dependency Injection Registration & Architectural Verification

    [Fact]
    public void AddIdpServices_RegistersAllCoreServicesAsSingletons_WithoutCallingBuildServiceProvider()
    {
        // Explicit requirement: Direct ServiceDescriptor assertion on ServiceCollection without BuildServiceProvider()
        var services = new ServiceCollection();

        services.AddIdpServices();

        services.Should().Contain(d =>
            d.ServiceType == typeof(IDocumentIntelligenceParserService) &&
            d.ImplementationType == typeof(DocumentIntelligenceParserService) &&
            d.Lifetime == ServiceLifetime.Singleton);

        services.Should().Contain(d =>
            d.ServiceType == typeof(IThreeWayReconciliationService) &&
            d.ImplementationType == typeof(ThreeWayReconciliationService) &&
            d.Lifetime == ServiceLifetime.Singleton);

        services.Should().Contain(d =>
            d.ServiceType == typeof(IPhysicalPodVerificationService) &&
            d.ImplementationType == typeof(PhysicalPodVerificationService) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddIdpServices_RegistersParserServiceWithCorrectInterface()
    {
        var services = new ServiceCollection();
        services.AddIdpServices();

        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IDocumentIntelligenceParserService));
        descriptor.Should().NotBeNull();
        descriptor!.ImplementationType.Should().Be(typeof(DocumentIntelligenceParserService));
    }

    [Fact]
    public void AddIdpServices_RegistersPodVerificationServiceWithCorrectInterface()
    {
        var services = new ServiceCollection();
        services.AddIdpServices();

        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IPhysicalPodVerificationService));
        descriptor.Should().NotBeNull();
        descriptor!.ImplementationType.Should().Be(typeof(PhysicalPodVerificationService));
    }

    #endregion
}
