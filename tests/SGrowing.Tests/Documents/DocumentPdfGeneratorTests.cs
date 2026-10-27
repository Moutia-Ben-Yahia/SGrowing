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
}
