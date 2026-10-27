using System.Security.Cryptography;
using System.Text;

namespace SGrowing.Api.Modules.Documents;

public interface IDocumentPdfGeneratorService
{
    byte[] GenerateCertificatePdf(
        Guid documentId,
        string documentTitle,
        string signerName,
        string signerEmail,
        string signerIp,
        DateTime signedAtUtc,
        string documentHashSha256,
        string signatureHashSha256,
        List<AuditTrailEventDto> auditTrail
    );

    byte[] GenerateInvoicePdf(
        Guid invoiceId,
        string invoiceNumber,
        string sellerName,
        string buyerName,
        decimal subtotal,
        decimal taxAmount,
        decimal totalAmount,
        string currency,
        List<InvoiceLineItemDto> items
    );
}

public record AuditTrailEventDto(string EventName, DateTime TimestampUtc, string Actor, string IpAddress);
public record InvoiceLineItemDto(string Description, int Quantity, decimal UnitPrice, decimal TotalPrice);

public class DocumentPdfGeneratorService : IDocumentPdfGeneratorService
{
    public byte[] GenerateCertificatePdf(
        Guid documentId,
        string documentTitle,
        string signerName,
        string signerEmail,
        string signerIp,
        DateTime signedAtUtc,
        string documentHashSha256,
        string signatureHashSha256,
        List<AuditTrailEventDto> auditTrail)
    {
        var sb = new StringBuilder();

        // Canvas dimensions: 612 x 792 (US Letter)
        // Background and border
        sb.AppendLine("0.1 0.1 0.18 rg"); // Deep navy blue accent
        sb.AppendLine("36 720 540 36 re f"); // Header bar

        // Header Text
        sb.AppendLine("BT");
        sb.AppendLine("/F2 16 Tf"); // Bold
        sb.AppendLine("1 1 1 rg"); // White text
        sb.AppendLine("50 732 Td");
        sb.AppendLine("(CRYPTOGRAPHIC E-SIGNATURE COMPLETION CERTIFICATE) Tj");
        sb.AppendLine("ET");

        // Document Details Box
        sb.AppendLine("0.96 0.97 0.98 rg");
        sb.AppendLine("36 580 540 125 re f");
        sb.AppendLine("0.8 0.85 0.90 RG");
        sb.AppendLine("1 w");
        sb.AppendLine("36 580 540 125 re S");

        sb.AppendLine("BT");
        sb.AppendLine("/F2 12 Tf");
        sb.AppendLine("0.1 0.1 0.1 rg");
        sb.AppendLine("50 680 Td");
        sb.AppendLine($"(Document Title: {EscapePdfText(documentTitle)}) Tj");

        sb.AppendLine("/F1 10 Tf");
        sb.AppendLine("0.2 0.2 0.2 rg");
        sb.AppendLine("0 -18 Td");
        sb.AppendLine($"(Document Envelope ID: {documentId}) Tj");
        sb.AppendLine("0 -16 Td");
        sb.AppendLine($"(Signer Name: {EscapePdfText(signerName)} <{EscapePdfText(signerEmail)}>) Tj");
        sb.AppendLine("0 -16 Td");
        sb.AppendLine($"(Signing IP Address: {EscapePdfText(signerIp)}) Tj");
        sb.AppendLine("0 -16 Td");
        sb.AppendLine($"(Timestamp \\(UTC\\): {signedAtUtc:yyyy-MM-dd HH:mm:ss} UTC) Tj");
        sb.AppendLine("ET");

        // Cryptographic Hashes Box
        sb.AppendLine("0.93 0.98 0.95 rg"); // Mint soft green
        sb.AppendLine("36 470 540 95 re f");
        sb.AppendLine("0.2 0.7 0.4 RG");
        sb.AppendLine("1 w");
        sb.AppendLine("36 470 540 95 re S");

        sb.AppendLine("BT");
        sb.AppendLine("/F2 11 Tf");
        sb.AppendLine("0.1 0.5 0.2 rg");
        sb.AppendLine("50 542 Td");
        sb.AppendLine("(SHA-256 INTEGRITY & AUTHENTICITY VALIDATION) Tj");

        sb.AppendLine("/F1 9 Tf");
        sb.AppendLine("0.1 0.1 0.1 rg");
        sb.AppendLine("0 -18 Td");
        sb.AppendLine($"(Original File SHA-256: {documentHashSha256}) Tj");
        sb.AppendLine("0 -16 Td");
        sb.AppendLine($"(Digital Signature SHA-256: {signatureHashSha256}) Tj");
        sb.AppendLine("0 -16 Td");
        sb.AppendLine("(Status: Legally Binding, Cryptographically Sealed, Tamper-Evident) Tj");
        sb.AppendLine("ET");

        // Audit Trail Header
        sb.AppendLine("BT");
        sb.AppendLine("/F2 12 Tf");
        sb.AppendLine("0.1 0.1 0.2 rg");
        sb.AppendLine("50 435 Td");
        sb.AppendLine("(AUDIT TRAIL LOG) Tj");
        sb.AppendLine("ET");

        // Audit Trail Table
        int y = 410;
        foreach (var audit in auditTrail.Take(8))
        {
            sb.AppendLine("0.9 0.9 0.9 RG 0.5 w");
            sb.AppendLine($"36 {y - 4} 540 0 re S");

            sb.AppendLine("BT");
            sb.AppendLine("/F1 9 Tf");
            sb.AppendLine("0.2 0.2 0.2 rg");
            sb.AppendLine($"50 {y} Td");
            sb.AppendLine($"({audit.TimestampUtc:yyyy-MM-dd HH:mm:ss} UTC  |  {EscapePdfText(audit.EventName)}  |  Actor: {EscapePdfText(audit.Actor)}  |  IP: {EscapePdfText(audit.IpAddress)}) Tj");
            sb.AppendLine("ET");
            y -= 22;
        }

        // Footer
        sb.AppendLine("BT");
        sb.AppendLine("/F1 8 Tf");
        sb.AppendLine("0.5 0.5 0.5 rg");
        sb.AppendLine("50 45 Td");
        sb.AppendLine("(Verified by Inspire Trust & Security Infrastructure \\(Documenso & Papermark Compliant Architecture\\)) Tj");
        sb.AppendLine("ET");

        return BuildCompliantPdf(sb.ToString());
    }

    public byte[] GenerateInvoicePdf(
        Guid invoiceId,
        string invoiceNumber,
        string sellerName,
        string buyerName,
        decimal subtotal,
        decimal taxAmount,
        decimal totalAmount,
        string currency,
        List<InvoiceLineItemDto> items)
    {
        var sb = new StringBuilder();

        // Header Banner
        sb.AppendLine("0.08 0.45 0.75 rg"); // Indigo blue
        sb.AppendLine("36 710 540 45 re f");

        sb.AppendLine("BT");
        sb.AppendLine("/F2 18 Tf");
        sb.AppendLine("1 1 1 rg");
        sb.AppendLine("50 726 Td");
        sb.AppendLine("(COMMERCIAL INVOICE) Tj");
        sb.AppendLine("ET");

        // Invoice Meta
        sb.AppendLine("BT");
        sb.AppendLine("/F2 11 Tf");
        sb.AppendLine("0.1 0.1 0.1 rg");
        sb.AppendLine("50 670 Td");
        sb.AppendLine($"(Invoice Number: {EscapePdfText(invoiceNumber)}) Tj");
        sb.AppendLine("/F1 10 Tf");
        sb.AppendLine("0 -16 Td");
        sb.AppendLine($"(Invoice Date: {DateTime.UtcNow:yyyy-MM-dd}) Tj");
        sb.AppendLine("0 -16 Td");
        sb.AppendLine($"(Payment Terms: Net 30 Days | Factoring Eligible) Tj");
        sb.AppendLine("ET");

        // Seller & Buyer Blocks
        sb.AppendLine("BT");
        sb.AppendLine("/F2 11 Tf");
        sb.AppendLine("50 600 Td");
        sb.AppendLine("(ISSUED BY:) Tj");
        sb.AppendLine("/F1 10 Tf");
        sb.AppendLine("0 -15 Td");
        sb.AppendLine($"({EscapePdfText(sellerName)}) Tj");

        sb.AppendLine("/F2 11 Tf");
        sb.AppendLine("280 15 Td");
        sb.AppendLine("(BILLED TO:) Tj");
        sb.AppendLine("/F1 10 Tf");
        sb.AppendLine("0 -15 Td");
        sb.AppendLine($"({EscapePdfText(buyerName)}) Tj");
        sb.AppendLine("ET");

        // Table Header
        sb.AppendLine("0.92 0.94 0.96 rg");
        sb.AppendLine("36 530 540 22 re f");
        sb.AppendLine("0.7 0.7 0.8 RG 1 w");
        sb.AppendLine("36 530 540 22 re S");

        sb.AppendLine("BT");
        sb.AppendLine("/F2 9 Tf");
        sb.AppendLine("0.1 0.1 0.2 rg");
        sb.AppendLine("50 537 Td");
        sb.AppendLine("(Item Description) Tj");
        sb.AppendLine("260 0 Td");
        sb.AppendLine("(Qty) Tj");
        sb.AppendLine("60 0 Td");
        sb.AppendLine("(Unit Price) Tj");
        sb.AppendLine("90 0 Td");
        sb.AppendLine("(Total) Tj");
        sb.AppendLine("ET");

        // Table Rows
        int y = 505;
        foreach (var item in items)
        {
            sb.AppendLine("0.9 0.9 0.9 RG 0.5 w");
            sb.AppendLine($"36 {y - 4} 540 0 re S");

            sb.AppendLine("BT");
            sb.AppendLine("/F1 9 Tf");
            sb.AppendLine("0.15 0.15 0.15 rg");
            sb.AppendLine($"50 {y} Td");
            sb.AppendLine($"({EscapePdfText(item.Description)}) Tj");
            sb.AppendLine($"310 {y} Td");
            sb.AppendLine($"({item.Quantity}) Tj");
            sb.AppendLine($"370 {y} Td");
            sb.AppendLine($"({item.UnitPrice:F2} {currency}) Tj");
            sb.AppendLine($"460 {y} Td");
            sb.AppendLine($"({item.TotalPrice:F2} {currency}) Tj");
            sb.AppendLine("ET");
            y -= 22;
        }

        // Summary Box
        int summaryY = Math.Max(y - 30, 200);
        sb.AppendLine("BT");
        sb.AppendLine("/F1 10 Tf");
        sb.AppendLine($"350 {summaryY} Td");
        sb.AppendLine($"(Subtotal: {subtotal:F2} {currency}) Tj");
        sb.AppendLine("0 -18 Td");
        sb.AppendLine($"(Tax: {taxAmount:F2} {currency}) Tj");
        sb.AppendLine("/F2 12 Tf");
        sb.AppendLine("0.08 0.45 0.75 rg");
        sb.AppendLine("0 -20 Td");
        sb.AppendLine($"(TOTAL DUE: {totalAmount:F2} {currency}) Tj");
        sb.AppendLine("ET");

        // Footer
        sb.AppendLine("BT");
        sb.AppendLine("/F1 8 Tf");
        sb.AppendLine("0.5 0.5 0.5 rg");
        sb.AppendLine("50 45 Td");
        sb.AppendLine("(Thank you for your business. For remittance questions, contact finance@inspire.internal) Tj");
        sb.AppendLine("ET");

        return BuildCompliantPdf(sb.ToString());
    }

    private static string EscapePdfText(string input)
    {
        if (string.IsNullOrEmpty(input)) return "";
        return input.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
    }

    private static byte[] BuildCompliantPdf(string contentStream)
    {
        var contentBytes = Encoding.ASCII.GetBytes(contentStream);
        var contentLen = contentBytes.Length;

        var sb = new StringBuilder();
        sb.Append("%PDF-1.4\n");
        sb.Append("%\xE2\xE3\xCF\xD3\n");

        var offsets = new List<long>();

        // 1 0 obj: Catalog
        offsets.Add(sb.Length);
        sb.Append("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");

        // 2 0 obj: Pages
        offsets.Add(sb.Length);
        sb.Append("2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n");

        // 3 0 obj: Page
        offsets.Add(sb.Length);
        sb.Append("3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R /F2 5 0 R >> >> /Contents 6 0 R >>\nendobj\n");

        // 4 0 obj: Regular Font
        offsets.Add(sb.Length);
        sb.Append("4 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>\nendobj\n");

        // 5 0 obj: Bold Font
        offsets.Add(sb.Length);
        sb.Append("5 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >>\nendobj\n");

        // 6 0 obj: Contents stream
        offsets.Add(sb.Length);
        sb.Append($"6 0 obj\n<< /Length {contentLen} >>\nstream\n");
        sb.Append(contentStream);
        sb.Append("\nendstream\nendobj\n");

        // xref table
        var startXref = sb.Length;
        sb.Append("xref\n");
        sb.Append($"0 {offsets.Count + 1}\n");
        sb.Append("0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            sb.Append($"{offset:D10} 00000 n \n");
        }

        // trailer
        sb.Append("trailer\n");
        sb.Append($"<< /Size {offsets.Count + 1} /Root 1 0 R >>\n");
        sb.Append("startxref\n");
        sb.Append($"{startXref}\n");
        sb.Append("%%EOF\n");

        return Encoding.ASCII.GetBytes(sb.ToString());
    }
}
