using FluentAssertions;
using SGrowing.Api.Modules.Documents.Idp;
using Xunit;

namespace SGrowing.Tests.Documents;

public class DocumentIdpEngineTests
{
    private readonly DocumentIntelligenceParserService _parser = new();
    private readonly PhysicalPodVerificationService _podVerifier = new();
    private readonly ThreeWayReconciliationService _reconciliation = new();

    #region 1. Document Intelligence Parser Service Tests

    [Fact]
    public void ComputeDocumentSha256_ProducesDeterministicLowerHexDigest()
    {
        var text = "Invoice-INV-2026-001;Vendor=Acme;Total=1500.00";
        var hash1 = _parser.ComputeDocumentSha256(text);
        var hash2 = _parser.ComputeDocumentSha256(text);

        hash1.Should().NotBeNullOrWhiteSpace();
        hash1.Should().Be(hash2);
        hash1.Should().MatchRegex("^[a-f0-9]{64}$");

        var byteHash = _parser.ComputeDocumentSha256(System.Text.Encoding.UTF8.GetBytes(text));
        byteHash.Should().Be(hash1);
    }

    [Fact]
    public async Task ParseInvoiceDocumentAsync_ExtractsMetadataAndLineItemsFromText()
    {
        var rawText = @"
Fournisseur: Carthage High-Tech Distribution
NIF: 001234567890123
Facture N°: FAC-2026-9042
Date de facture: 2026-10-15
Compte Bancaire RIB: 08000000000000000099
Total HT: 1000.00
Total TVA: 190.00
Total TTC: 1190.00
";

        var request = new ParseDocumentRequest(
            DocumentId: "doc_inv_9042",
            DocumentType: IdpDocumentType.SupplierInvoice,
            RawText: rawText
        );

        var response = await _parser.ParseInvoiceDocumentAsync(request);

        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.ExtractedData.Should().NotBeNull();

        var data = response.ExtractedData!;
        data.VendorName.Should().Contain("Carthage High-Tech");
        data.VendorTaxId.Should().Be("001234567890123");
        data.InvoiceNumber.Should().Be("FAC-2026-9042");
        data.Subtotal.Should().Be(1000.00m);
        data.TaxAmount.Should().Be(190.00m);
        data.TotalAmount.Should().Be(1190.00m);
        data.BankRib.Should().Contain("08000000000000000099");
        data.Sha256Hash.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ParseInvoiceDocumentAsync_NullRequest_ThrowsArgumentNullException()
    {
        var act = async () => await _parser.ParseInvoiceDocumentAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void ExtractTabularLineItems_GroupsTokensByRowTolerance()
    {
        // 2 lines of tokens
        var tokens = new List<OcrBoundingBox>
        {
            // Row 1: Y around 100
            new(X: 10, Y: 100, Width: 40, Height: 10, Text: "SKU-001", Confidence: 0.95),
            new(X: 60, Y: 102, Width: 80, Height: 10, Text: "Fiber Router", Confidence: 0.96),
            new(X: 150, Y: 101, Width: 20, Height: 10, Text: "2", Confidence: 0.98),
            new(X: 180, Y: 100, Width: 40, Height: 10, Text: "150.00", Confidence: 0.97),
            new(X: 230, Y: 101, Width: 40, Height: 10, Text: "300.00", Confidence: 0.99),

            // Row 2: Y around 130
            new(X: 10, Y: 130, Width: 40, Height: 10, Text: "SKU-002", Confidence: 0.95),
            new(X: 60, Y: 131, Width: 80, Height: 10, Text: "Cat6 Cable 50m", Confidence: 0.94),
            new(X: 150, Y: 129, Width: 20, Height: 10, Text: "5", Confidence: 0.99),
            new(X: 180, Y: 130, Width: 40, Height: 10, Text: "20.00", Confidence: 0.96),
            new(X: 230, Y: 130, Width: 40, Height: 10, Text: "100.00", Confidence: 0.99)
        };

        var items = _parser.ExtractTabularLineItems(tokens, rowToleranceY: 10.0);

        items.Should().NotBeNull();
        items.Should().HaveCount(2);
        items[0].LineNumber.Should().Be(1);
        items[1].LineNumber.Should().Be(2);
    }

    #endregion

    #region 2. Physical POD Verification Service Tests

    [Fact]
    public async Task VerifyPaperPodAsync_ValidSignatureAndStamp_ReturnsVerified()
    {
        var tokens = new List<OcrBoundingBox>
        {
            new(X: 50, Y: 400, Width: 120, Height: 40, Text: "Signature Recipient", Confidence: 0.92, IsHandwritten: true),
            new(X: 250, Y: 400, Width: 80, Height: 80, Text: "Cachet Société SARL", Confidence: 0.88, IsStamp: true)
        };

        var request = new VerifyPaperPodRequest(
            DocumentId: "pod-receipt-8841",
            Tokens: tokens,
            MinSignatureConfidence: 0.85,
            MinStampConfidence: 0.80
        );

        var response = await _podVerifier.VerifyPaperPodAsync(request);

        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Result.StampStatus.Should().Be(PodStampStatus.Verified);
        response.Result.RecipientSignatureDetected.Should().BeTrue();
        response.Result.OfficialStampDetected.Should().BeTrue();
    }

    [Fact]
    public async Task VerifyPaperPodAsync_MissingSignature_ReturnsMissingSignatureWarning()
    {
        var tokens = new List<OcrBoundingBox>
        {
            // Only stamp, no signature
            new(X: 250, Y: 400, Width: 80, Height: 80, Text: "Cachet Commercial", Confidence: 0.89, IsStamp: true)
        };

        var request = new VerifyPaperPodRequest(
            DocumentId: "pod-receipt-8842",
            Tokens: tokens
        );

        var response = await _podVerifier.VerifyPaperPodAsync(request);

        response.Success.Should().BeFalse();
        response.Result.StampStatus.Should().Be(PodStampStatus.MissingSignature);
        response.Result.OfficialStampDetected.Should().BeTrue();
        response.Result.RecipientSignatureDetected.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyPaperPodAsync_MissingStamp_ReturnsMissingStampWarning()
    {
        var tokens = new List<OcrBoundingBox>
        {
            // Only signature, no stamp
            new(X: 50, Y: 400, Width: 100, Height: 35, Text: "John Driver", Confidence: 0.90, IsHandwritten: true)
        };

        var request = new VerifyPaperPodRequest(
            DocumentId: "pod-receipt-8843",
            Tokens: tokens
        );

        var response = await _podVerifier.VerifyPaperPodAsync(request);

        response.Success.Should().BeFalse();
        response.Result.StampStatus.Should().Be(PodStampStatus.MissingStamp);
        response.Result.RecipientSignatureDetected.Should().BeTrue();
        response.Result.OfficialStampDetected.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyPaperPodAsync_HasForgeryMarkers_FlagsSuspectedForgery()
    {
        var request = new VerifyPaperPodRequest(
            DocumentId: "pod-fake-9999",
            HasForgeryMarkers: true
        );

        var response = await _podVerifier.VerifyPaperPodAsync(request);

        response.Success.Should().BeFalse();
        response.Result.StampStatus.Should().Be(PodStampStatus.SuspectedForgery);
        response.Message.Should().Contain("Suspected synthetic forgery");
    }

    #endregion

    #region 3. Three-Way Reconciliation Service Tests

    [Fact]
    public async Task ReconcileAsync_PerfectMatch_ReturnsFullMatchAutoApproved()
    {
        _reconciliation.ClearRegisteredInvoices();

        var po = new PurchaseOrderRecord(
            PoId: "PO-2026-100",
            VendorName: "Atlas Components",
            VendorTaxId: "MF-778899",
            Currency: "TND",
            LineItems: new List<PoLineItem>
            {
                new("SKU-1", "Widget A", 10m, 100.00m, 1000.00m, 19m)
            },
            TotalAmount: 1190.00m,
            ExpectedTaxAmount: 190.00m
        );

        var grn = new GoodsReceiptNoteRecord(
            GrnId: "GRN-2026-100",
            PoId: "PO-2026-100",
            ReceivedDateUtc: DateTime.UtcNow.AddDays(-2),
            ReceivedItems: new List<GrnLineItem>
            {
                new("SKU-1", AcceptedQuantity: 10m, RejectedQuantity: 0m)
            }
        );

        var invoice = new ExtractedInvoiceData(
            DocumentId: "INV-DOC-1",
            VendorName: "Atlas Components",
            VendorTaxId: "MF-778899",
            InvoiceNumber: "FAC-2026-100",
            InvoiceDate: DateTime.UtcNow,
            Currency: "TND",
            LineItems: new List<ExtractedLineItem>
            {
                new(1, "SKU-1", "Widget A", 10m, 100.00m, 1000.00m, 19m)
            },
            Subtotal: 1000.00m,
            TaxAmount: 190.00m,
            TotalAmount: 1190.00m,
            BankRib: "08123456789012345678",
            ConfidenceScore: 0.98,
            Sha256Hash: "abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890"
        );

        var response = await _reconciliation.ReconcileAsync(new ThreeWayMatchRequest(po, grn, invoice));

        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Result.MatchStatus.Should().Be(ThreeWayMatchStatus.FullMatch);
        response.Result.AutoApproved.Should().BeTrue();
        response.Result.PriceVariancePercent.Should().Be(0m);
        response.Result.QuantityVariance.Should().Be(0m);
    }

    [Fact]
    public async Task ReconcileAsync_PriceVarianceExceedsTolerance_ReturnsPriceVarianceDispute()
    {
        _reconciliation.ClearRegisteredInvoices();

        var po = new PurchaseOrderRecord(
            PoId: "PO-2026-200",
            VendorName: "Atlas Components",
            VendorTaxId: "MF-778899",
            Currency: "TND",
            LineItems: new List<PoLineItem>
            {
                new("SKU-1", "Widget A", 10m, 100.00m, 1000.00m, 19m)
            },
            TotalAmount: 1190.00m
        );

        var grn = new GoodsReceiptNoteRecord(
            GrnId: "GRN-2026-200",
            PoId: "PO-2026-200",
            ReceivedDateUtc: DateTime.UtcNow,
            ReceivedItems: new List<GrnLineItem> { new("SKU-1", 10m) }
        );

        // Invoice charges 110.00 instead of 100.00 (10% higher, exceeding 0.5% tolerance)
        var invoice = new ExtractedInvoiceData(
            DocumentId: "INV-DOC-2",
            VendorName: "Atlas Components",
            VendorTaxId: "MF-778899",
            InvoiceNumber: "FAC-2026-200",
            InvoiceDate: DateTime.UtcNow,
            Currency: "TND",
            LineItems: new List<ExtractedLineItem>
            {
                new(1, "SKU-1", "Widget A", 10m, 110.00m, 1100.00m, 19m)
            },
            Subtotal: 1100.00m,
            TaxAmount: 209.00m,
            TotalAmount: 1309.00m,
            BankRib: "08123456789012345678",
            ConfidenceScore: 0.98,
            Sha256Hash: "2222221234567890abcdef1234567890abcdef1234567890abcdef1234567890"
        );

        var response = await _reconciliation.ReconcileAsync(new ThreeWayMatchRequest(po, grn, invoice));

        response.Success.Should().BeFalse();
        response.Result.MatchStatus.Should().Be(ThreeWayMatchStatus.PriceVarianceDispute);
        response.Result.AutoApproved.Should().BeFalse();
        response.Result.DiscrepancyNotes.Should().Contain(n => n.Contains("Price variance exceeds tolerance"));
    }

    [Fact]
    public async Task ReconcileAsync_QuantityVarianceExceedsTolerance_ReturnsQuantityVarianceDispute()
    {
        _reconciliation.ClearRegisteredInvoices();

        var po = new PurchaseOrderRecord(
            PoId: "PO-2026-300",
            VendorName: "Atlas Components",
            VendorTaxId: "MF-778899",
            Currency: "TND",
            LineItems: new List<PoLineItem>
            {
                new("SKU-1", "Widget A", 10m, 100.00m, 1000.00m, 19m)
            },
            TotalAmount: 1190.00m
        );

        // Warehouse only accepted 7 widgets (3 rejected)
        var grn = new GoodsReceiptNoteRecord(
            GrnId: "GRN-2026-300",
            PoId: "PO-2026-300",
            ReceivedDateUtc: DateTime.UtcNow,
            ReceivedItems: new List<GrnLineItem> { new("SKU-1", AcceptedQuantity: 7m, RejectedQuantity: 3m) }
        );

        // Invoice bills for the full 10 widgets
        var invoice = new ExtractedInvoiceData(
            DocumentId: "INV-DOC-3",
            VendorName: "Atlas Components",
            VendorTaxId: "MF-778899",
            InvoiceNumber: "FAC-2026-300",
            InvoiceDate: DateTime.UtcNow,
            Currency: "TND",
            LineItems: new List<ExtractedLineItem>
            {
                new(1, "SKU-1", "Widget A", 10m, 100.00m, 1000.00m, 19m)
            },
            Subtotal: 1000.00m,
            TaxAmount: 190.00m,
            TotalAmount: 1190.00m,
            BankRib: "08123456789012345678",
            ConfidenceScore: 0.98,
            Sha256Hash: "3333331234567890abcdef1234567890abcdef1234567890abcdef1234567890"
        );

        var response = await _reconciliation.ReconcileAsync(new ThreeWayMatchRequest(po, grn, invoice));

        response.Success.Should().BeFalse();
        response.Result.MatchStatus.Should().Be(ThreeWayMatchStatus.QuantityVarianceDispute);
        response.Result.AutoApproved.Should().BeFalse();
    }

    [Fact]
    public async Task ReconcileAsync_DuplicateSubmission_BlocksDuplicateInvoice()
    {
        _reconciliation.ClearRegisteredInvoices();

        var po = new PurchaseOrderRecord(
            PoId: "PO-2026-400",
            VendorName: "Atlas Components",
            VendorTaxId: "MF-778899",
            Currency: "TND",
            LineItems: new List<PoLineItem>
            {
                new("SKU-1", "Widget A", 1m, 100.00m, 100.00m)
            },
            TotalAmount: 100.00m
        );

        var grn = new GoodsReceiptNoteRecord(
            GrnId: "GRN-2026-400",
            PoId: "PO-2026-400",
            ReceivedDateUtc: DateTime.UtcNow,
            ReceivedItems: new List<GrnLineItem> { new("SKU-1", 1m) }
        );

        var hash = "4444441234567890abcdef1234567890abcdef1234567890abcdef1234567890";
        var invoice = new ExtractedInvoiceData(
            DocumentId: "INV-DOC-4",
            VendorName: "Atlas Components",
            VendorTaxId: "MF-778899",
            InvoiceNumber: "FAC-2026-400",
            InvoiceDate: DateTime.UtcNow,
            Currency: "TND",
            LineItems: new List<ExtractedLineItem>
            {
                new(1, "SKU-1", "Widget A", 1m, 100.00m, 100.00m, 0m)
            },
            Subtotal: 100.00m,
            TaxAmount: 0m,
            TotalAmount: 100.00m,
            BankRib: "08123456789012345678",
            ConfidenceScore: 0.98,
            Sha256Hash: hash
        );

        // Pre-register invoice to simulate already processed
        _reconciliation.RegisterProcessedInvoice("MF-778899", "FAC-2026-400", hash);

        var response = await _reconciliation.ReconcileAsync(new ThreeWayMatchRequest(po, grn, invoice));

        response.Success.Should().BeFalse();
        response.Result.MatchStatus.Should().Be(ThreeWayMatchStatus.DuplicateInvoiceBlocked);
        response.Result.AutoApproved.Should().BeFalse();
        response.Result.DiscrepancyNotes.Should().Contain(n => n.Contains("Duplicate invoice detected"));
    }

    [Fact]
    public async Task ReconcileAsync_UnmatchedPo_ReturnsUnmatchedPoStatus()
    {
        _reconciliation.ClearRegisteredInvoices();

        var po = new PurchaseOrderRecord(
            PoId: "PO-TARGET-999",
            VendorName: "Atlas Components",
            VendorTaxId: "MF-778899",
            Currency: "TND",
            LineItems: new List<PoLineItem>(),
            TotalAmount: 100.00m
        );

        // GRN references wrong PO
        var grn = new GoodsReceiptNoteRecord(
            GrnId: "GRN-DIFFERENT-111",
            PoId: "PO-MISMATCHED-000",
            ReceivedDateUtc: DateTime.UtcNow,
            ReceivedItems: new List<GrnLineItem>()
        );

        var invoice = new ExtractedInvoiceData(
            DocumentId: "INV-DOC-5",
            VendorName: "Atlas Components",
            VendorTaxId: "MF-778899",
            InvoiceNumber: "FAC-2026-500",
            InvoiceDate: DateTime.UtcNow,
            Currency: "TND",
            LineItems: new List<ExtractedLineItem>(),
            Subtotal: 100.00m,
            TaxAmount: 0m,
            TotalAmount: 100.00m,
            BankRib: "08123456789012345678",
            ConfidenceScore: 0.95,
            Sha256Hash: "5555551234567890abcdef1234567890abcdef1234567890abcdef1234567890"
        );

        var response = await _reconciliation.ReconcileAsync(new ThreeWayMatchRequest(po, grn, invoice));

        response.Success.Should().BeFalse();
        response.Result.MatchStatus.Should().Be(ThreeWayMatchStatus.UnmatchedPo);
    }

    #endregion
}
