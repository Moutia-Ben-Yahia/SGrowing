using System.Text;
using SGrowing.Api.Modules.Documents;
using Xunit;

namespace SGrowing.Tests.Documents;

public class DocumentPdfGeneratorTests
{
    private readonly DocumentPdfGeneratorService _service = new();

    [Fact]
    public void GenerateCertificatePdf_ReturnsValidPdfBytesWithSignerAndChecksums()
    {
        // Arrange
        var docId = Guid.NewGuid();
        var docTitle = "Master SaaS Agreement 2026";
        var signerName = "Omar Bennani";
        var signerEmail = "omar@atlasretail.ma";
        var signerIp = "196.200.14.88";
        var signedAt = DateTime.UtcNow;
        var docHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
        var sigHash = "9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08";
        var audit = new List<AuditTrailEventDto>
        {
            new("Document Created", signedAt.AddHours(-2), "Admin", "127.0.0.1"),
            new("Signed Digitally", signedAt, signerName, signerIp)
        };

        // Act
        var pdfBytes = _service.GenerateCertificatePdf(
            docId, docTitle, signerName, signerEmail, signerIp, signedAt, docHash, sigHash, audit
        );

        // Assert
        Assert.NotNull(pdfBytes);
        Assert.True(pdfBytes.Length > 200);

        var pdfString = Encoding.ASCII.GetString(pdfBytes);
        Assert.StartsWith("%PDF-1.4", pdfString);
        Assert.Contains("%%EOF", pdfString);
        Assert.Contains("Omar Bennani", pdfString);
        Assert.Contains(docHash, pdfString);
    }

    [Fact]
    public void GenerateInvoicePdf_ReturnsValidPdfBytesWithLineItemsAndTotals()
    {
        // Arrange
        var invoiceId = Guid.NewGuid();
        var invoiceNum = "INV-2026-9042";
        var seller = "Inspire SaaS Corp";
        var buyer = "Atlas Logistics SARL";
        var items = new List<InvoiceLineItemDto>
        {
            new("Inspire Enterprise Subscription", 1, 1000m, 1000m),
            new("WhatsApp Cloud Add-on", 2, 100m, 200m)
        };

        // Act
        var pdfBytes = _service.GenerateInvoicePdf(
            invoiceId, invoiceNum, seller, buyer, 1200m, 240m, 1440m, "USD", items
        );

        // Assert
        Assert.NotNull(pdfBytes);
        Assert.True(pdfBytes.Length > 200);

        var pdfString = Encoding.ASCII.GetString(pdfBytes);
        Assert.StartsWith("%PDF-1.4", pdfString);
        Assert.Contains("COMMERCIAL INVOICE", pdfString);
        Assert.Contains("INV-2026-9042", pdfString);
        Assert.Contains("1440.00 USD", pdfString);
    }

    [Fact]
    public void GenerateCertificatePdf_EscapesParenthesesAndSpecialCharacters()
    {
        var docId = Guid.NewGuid();
        var docTitle = "Contract (High Priority) & Special Notes";
        var signerName = "Dr. Jane (Smith) Doe";
        var signerEmail = "jane@smith.org";
        var signedAt = DateTime.UtcNow;

        var pdfBytes = _service.GenerateCertificatePdf(
            docId,
            docTitle,
            signerName,
            signerEmail,
            "10.0.0.1",
            signedAt,
            "sha256_mock_hash",
            "sig_mock_hash",
            new List<AuditTrailEventDto>()
        );

        var pdfString = Encoding.ASCII.GetString(pdfBytes);
        Assert.NotNull(pdfBytes);
        Assert.Contains(@"\(High Priority\)", pdfString);
        Assert.Contains(@"\(Smith\)", pdfString);
    }

    [Fact]
    public void GenerateInvoicePdf_HandlesEmptyLineItemsGracefully()
    {
        var invoiceId = Guid.NewGuid();
        var pdfBytes = _service.GenerateInvoicePdf(
            invoiceId,
            "INV-EMPTY-001",
            "Zero Seller",
            "Zero Buyer",
            0m,
            0m,
            0m,
            "EUR",
            new List<InvoiceLineItemDto>()
        );

        Assert.NotNull(pdfBytes);
        Assert.True(pdfBytes.Length > 200);
        var pdfString = Encoding.ASCII.GetString(pdfBytes);
        Assert.Contains("INV-EMPTY-001", pdfString);
        Assert.Contains("0.00 EUR", pdfString);
    }

    [Fact]
    public void GenerateCertificatePdf_RendersAuditTrailEventsUpToLimit()
    {
        var docId = Guid.NewGuid();
        var audit = new List<AuditTrailEventDto>();
        for (int i = 1; i <= 12; i++)
        {
            audit.Add(new AuditTrailEventDto($"Audit Event {i}", DateTime.UtcNow.AddMinutes(-i), "System", "127.0.0.1"));
        }

        var pdfBytes = _service.GenerateCertificatePdf(
            docId,
            "Audit Heavy Contract",
            "User",
            "user@corp.com",
            "127.0.0.1",
            DateTime.UtcNow,
            "doc_hash",
            "sig_hash",
            audit
        );

        Assert.NotNull(pdfBytes);
        var pdfString = Encoding.ASCII.GetString(pdfBytes);
        Assert.Contains("Audit Event 1", pdfString);
        Assert.Contains("Audit Event 8", pdfString);
        // Generator takes Take(8), so 9th should not be present
        Assert.DoesNotContain("Audit Event 9", pdfString);
    }

    [Fact]
    public void GenerateCertificatePdf_ContainsVerificationFooterNotice()
    {
        var docId = Guid.NewGuid();
        var pdfBytes = _service.GenerateCertificatePdf(
            docId,
            "Legal Binding Document",
            "Signer Legal",
            "signer@legal.com",
            "192.168.1.1",
            DateTime.UtcNow,
            "hash_1",
            "hash_2",
            new List<AuditTrailEventDto>()
        );

        var pdfString = Encoding.ASCII.GetString(pdfBytes);
        Assert.Contains("Documenso & Papermark Compliant Architecture", pdfString);
        Assert.Contains("Cryptographically Sealed, Tamper-Evident", pdfString);
    }

    [Fact]
    public void GenerateInvoicePdf_FormatsCurrenciesAndMultipleLineItems()
    {
        var invoiceId = Guid.NewGuid();
        var items = new List<InvoiceLineItemDto>
        {
            new("Consulting Retainer", 1, 3500.00m, 3500.00m),
            new("Dedicated SLA 24/7", 1, 1200.00m, 1200.00m),
            new("On-premise Gateway Node", 2, 450.00m, 900.00m)
        };

        var pdfBytes = _service.GenerateInvoicePdf(
            invoiceId,
            "INV-TND-9921",
            "SGrowing North Africa",
            "Banque Internationale",
            5600.00m,
            1064.00m,
            6664.00m,
            "TND",
            items
        );

        Assert.NotNull(pdfBytes);
        var pdfString = Encoding.ASCII.GetString(pdfBytes);
        Assert.Contains("INV-TND-9921", pdfString);
        Assert.Contains("Consulting Retainer", pdfString);
        Assert.Contains("Dedicated SLA 24/7", pdfString);
        Assert.Contains("On-premise Gateway Node", pdfString);
        Assert.Contains("6664.00 TND", pdfString);
    }

    [Fact]
    public void GenerateInvoicePdf_IncludesPaymentTermsAndFactoringEligibleNotice()
    {
        var invoiceId = Guid.NewGuid();
        var pdfBytes = _service.GenerateInvoicePdf(
            invoiceId,
            "INV-TERMS-001",
            "FinOps Corp",
            "Enterprise Client",
            100m,
            19m,
            119m,
            "USD",
            new List<InvoiceLineItemDto> { new("Item", 1, 100m, 100m) }
        );

        var pdfString = Encoding.ASCII.GetString(pdfBytes);
        Assert.Contains("Payment Terms: Net 30 Days | Factoring Eligible", pdfString);
    }
}
