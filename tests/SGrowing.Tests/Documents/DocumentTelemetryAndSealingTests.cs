using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Documents;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Documents;

public class DocumentTelemetryAndSealingTests
{
    private readonly SGrowingDbContext _db;
    private readonly DocumentTelemetryService _telemetryService;
    private readonly CryptographicPdfSealingService _sealingService;

    public DocumentTelemetryAndSealingTests()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new SGrowingDbContext(options);
        _telemetryService = new DocumentTelemetryService(_db);
        _sealingService = new CryptographicPdfSealingService(_db, _telemetryService);
    }

    [Fact]
    public async Task DwellTimeRecording_AggregatesBreakdownAndBuildsDropOffHeatmap()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var documentId = Guid.NewGuid();

        // Simulate readers browsing pages 1, 2, 3
        // Reader 1 views pages 1, 2, 3
        await _sealingService.RecordPageDwellTimeAsync(tenantId, documentId, 1, 25.0, "192.168.1.10", "alice@prospect.com");
        await _sealingService.RecordPageDwellTimeAsync(tenantId, documentId, 2, 30.0, "192.168.1.10", "alice@prospect.com");
        await _sealingService.RecordPageDwellTimeAsync(tenantId, documentId, 3, 20.0, "192.168.1.10", "alice@prospect.com");

        // Reader 2 views only page 1 and 2 (drops off before page 3)
        await _sealingService.RecordPageDwellTimeAsync(tenantId, documentId, 1, 15.0, "10.0.0.5", "bob@prospect.com");
        await _sealingService.RecordPageDwellTimeAsync(tenantId, documentId, 2, 10.0, "10.0.0.5", "bob@prospect.com");

        // Act
        var summary = await _sealingService.GetDocumentTelemetrySummaryAsync(tenantId, documentId);

        // Assert
        Assert.NotNull(summary);
        Assert.Equal(documentId, summary.DocumentId);
        Assert.Equal(100.0, summary.TotalDocumentDwellSeconds); // 25 + 30 + 20 + 15 + 10 = 100s
        Assert.Equal(5, summary.TotalViews);
        Assert.Equal(2, summary.UniqueViewers);

        // Verify Page Dwell Breakdown
        Assert.Equal(3, summary.PageDwellBreakdown.Count);

        var page1 = summary.PageDwellBreakdown.First(p => p.PageNumber == 1);
        Assert.Equal(40.0, page1.TotalDwellSeconds); // 25 + 15
        Assert.Equal(2, page1.ViewCount);
        Assert.Equal(20.0, page1.AverageDwellSeconds);
        Assert.Equal(40.0, page1.AttentionPercentage); // 40/100 = 40%

        var page2 = summary.PageDwellBreakdown.First(p => p.PageNumber == 2);
        Assert.Equal(40.0, page2.TotalDwellSeconds); // 30 + 10
        Assert.Equal(40.0, page2.AttentionPercentage);

        var page3 = summary.PageDwellBreakdown.First(p => p.PageNumber == 3);
        Assert.Equal(20.0, page3.TotalDwellSeconds);
        Assert.True(page3.IsPricingPage);
        Assert.False(page3.HasHighInterestAlert); // 20s < 45s threshold

        // Verify Drop-Off Heatmap
        Assert.Equal(3, summary.DropOffHeatmap.Count);
        var hop1 = summary.DropOffHeatmap.First(h => h.PageNumber == 1);
        Assert.Equal(2, hop1.ViewersReached);
        Assert.Equal(100.0, hop1.RetentionRate);
        Assert.Equal(0.0, hop1.DropOffRate);

        var hop2 = summary.DropOffHeatmap.First(h => h.PageNumber == 2);
        Assert.Equal(2, hop2.ViewersReached);
        Assert.Equal(100.0, hop2.RetentionRate);

        var hop3 = summary.DropOffHeatmap.First(h => h.PageNumber == 3);
        Assert.Equal(1, hop3.ViewersReached);
        Assert.Equal(50.0, hop3.RetentionRate);
        Assert.Equal(50.0, hop3.DropOffRate);
    }

    [Fact]
    public async Task PricingPage_DwellGreaterThan45s_TriggersHighInterestAlert()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var documentId = Guid.NewGuid();

        // Initial normal dwell on pages 1 and 2
        await _sealingService.RecordPageDwellTimeAsync(tenantId, documentId, 1, 10.0, "192.168.1.100", "cto@megacorp.com");
        await _sealingService.RecordPageDwellTimeAsync(tenantId, documentId, 2, 12.0, "192.168.1.100", "cto@megacorp.com");

        // Dwell on Pricing Page (Page 3) exceeding 45 seconds (52 seconds)
        await _sealingService.RecordPageDwellTimeAsync(tenantId, documentId, 3, 52.0, "192.168.1.100", "cto@megacorp.com");

        // Act
        var summary = await _sealingService.GetDocumentTelemetrySummaryAsync(tenantId, documentId);

        // Assert
        Assert.NotNull(summary);
        Assert.True(summary.HasPricingHighInterestAlert, "Summary should indicate a high-interest pricing page alert");
        Assert.NotEmpty(summary.HighInterestAlerts);

        var alert = summary.HighInterestAlerts.First(a => a.PageNumber == 3);
        Assert.Equal("PricingPageHighInterest", alert.AlertType);
        Assert.Contains("52.0", alert.Message);
        Assert.Contains("45", alert.Message);

        var page3Metric = summary.PageDwellBreakdown.First(p => p.PageNumber == 3);
        Assert.True(page3Metric.IsPricingPage);
        Assert.True(page3Metric.HasHighInterestAlert);
    }

    [Fact]
    public async Task PricingPage_DwellUnder45s_DoesNotTriggerAlert()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var documentId = Guid.NewGuid();

        await _sealingService.RecordPageDwellTimeAsync(tenantId, documentId, 1, 15.0, "192.168.1.100", "procurement@client.com");
        await _sealingService.RecordPageDwellTimeAsync(tenantId, documentId, 2, 10.0, "192.168.1.100", "procurement@client.com");
        await _sealingService.RecordPageDwellTimeAsync(tenantId, documentId, 3, 30.0, "192.168.1.100", "procurement@client.com"); // 30s <= 45s

        // Act
        var summary = await _sealingService.GetDocumentTelemetrySummaryAsync(tenantId, documentId);

        // Assert
        Assert.NotNull(summary);
        Assert.False(summary.HasPricingHighInterestAlert);
        Assert.Empty(summary.HighInterestAlerts);

        var page3Metric = summary.PageDwellBreakdown.First(p => p.PageNumber == 3);
        Assert.True(page3Metric.IsPricingPage);
        Assert.False(page3Metric.HasHighInterestAlert);
    }

    [Fact]
    public async Task Pkcs7Seal_ValidDocument_VerifiesIntegrityAndCompletionCertificate()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var rawPdfBytes = Encoding.UTF8.GetBytes("%PDF-1.7\n1 0 obj\n<< /Type /Catalog >>\nendobj\n%%EOF");
        var signerEmail = "cfo@enterprise.io";

        // Pre-populate contract entity in database
        var contract = DigitalContract.Create(tenantId, "Master SaaS Subscription Agreement 2026", "MSA", "Sarah Connor", signerEmail, 150000m);
        contract.Id = documentId;
        _db.DigitalContracts.Add(contract);
        await _db.SaveChangesAsync();

        // Seal Document
        var sealResult = await _sealingService.SealDocumentWithPkcs7Async(tenantId, documentId, rawPdfBytes, signerEmail);
        Assert.NotNull(sealResult);
        Assert.False(string.IsNullOrEmpty(sealResult.Sha256Hash));
        Assert.False(string.IsNullOrEmpty(sealResult.TamperProofVerificationToken));

        // Act - Verify Seal with intact bytes and matching token
        var verifyResult = await _sealingService.VerifySealAsync(tenantId, documentId, sealResult.TamperProofVerificationToken, rawPdfBytes);

        // Assert
        Assert.NotNull(verifyResult);
        Assert.True(verifyResult.IsSealValid);
        Assert.True(verifyResult.IntegrityIntact);
        Assert.True(verifyResult.VerificationTokenValid);
        Assert.Equal("Valid", verifyResult.Status);
        Assert.Contains("verified", verifyResult.Message, StringComparison.OrdinalIgnoreCase);

        // Verify Certificate of Completion is populated
        Assert.NotNull(verifyResult.Certificate);
        Assert.Equal(documentId, verifyResult.Certificate.DocumentId);
        Assert.Equal(signerEmail, verifyResult.Certificate.SignerEmail);
        Assert.Equal(sealResult.Sha256Hash, verifyResult.Certificate.DocumentChecksumSha256);
        Assert.True(verifyResult.HasRfc3161Timestamp);
    }

    [Fact]
    public async Task Pkcs7Seal_TamperedPdfBytes_DetectsTamperingAndFailsIntegrity()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var originalPdfBytes = Encoding.UTF8.GetBytes("%PDF-1.7\nPayment Terms: Net 30 Days\n%%EOF");
        var tamperedPdfBytes = Encoding.UTF8.GetBytes("%PDF-1.7\nPayment Terms: Net 90 Days [TAMPERED]\n%%EOF");

        // Seal original
        var sealResult = await _sealingService.SealDocumentWithPkcs7Async(tenantId, documentId, originalPdfBytes, "finance@atlas.com");

        // Act - Verify with tampered bytes
        var verifyResult = await _sealingService.VerifySealAsync(tenantId, documentId, sealResult.TamperProofVerificationToken, tamperedPdfBytes);

        // Assert
        Assert.NotNull(verifyResult);
        Assert.False(verifyResult.IsSealValid, "Tampered document must fail seal validation");
        Assert.False(verifyResult.IntegrityIntact, "Integrity must be flagged as violated");
        Assert.True(verifyResult.VerificationTokenValid, "Token was valid, but contents were altered");
        Assert.Equal("Tampered", verifyResult.Status);
        Assert.Contains("Cryptographic tamper detected", verifyResult.Message);
        Assert.NotEqual(verifyResult.DocumentSha256, verifyResult.ComputedByteRangeHash);
    }

    [Fact]
    public async Task Pkcs7Seal_InvalidVerificationToken_RejectsSealVerification()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var pdfBytes = Encoding.UTF8.GetBytes("%PDF-1.7\nConfidential Agreement\n%%EOF");

        var sealResult = await _sealingService.SealDocumentWithPkcs7Async(tenantId, documentId, pdfBytes, "legal@enterprise.com");

        // Act - Attempt verification with bogus token
        var verifyResult = await _sealingService.VerifySealAsync(tenantId, documentId, "FAKE-TOKEN-999-TAMPER", pdfBytes);

        // Assert
        Assert.NotNull(verifyResult);
        Assert.False(verifyResult.IsSealValid);
        Assert.False(verifyResult.VerificationTokenValid);
        Assert.Equal("InvalidVerificationToken", verifyResult.Status);
        Assert.Contains("Tamper check failed", verifyResult.Message);
    }

    [Fact]
    public async Task Iso32000ByteRange_ExcludesSignatureContentsCorrectly()
    {
        // Arrange
        // Range 1 (offset 0..50): 50 bytes
        var header = "/ByteRange [ 0 50 90 40 ]".PadRight(50, ' ');
        // Signature container gap (offset 50..90): 40 bytes
        var sigContainerA = "<AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA>";
        var sigContainerB = "<BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB>";
        // Range 2 (offset 90..130): exactly 40 bytes
        var trailer = "TRAILER".PadRight(40, '!');

        var bytesA = Encoding.ASCII.GetBytes(header + sigContainerA + trailer);
        var bytesB = Encoding.ASCII.GetBytes(header + sigContainerB + trailer);
        var bytesC = Encoding.ASCII.GetBytes(header.Replace("/ByteRange", "/Mutated--") + sigContainerA + trailer);

        // Act
        var hashA = CryptographicPdfSealingService.ComputeIso32000ByteRangeHash(bytesA);
        var hashB = CryptographicPdfSealingService.ComputeIso32000ByteRangeHash(bytesB);
        var hashC = CryptographicPdfSealingService.ComputeIso32000ByteRangeHash(bytesC);

        // Assert
        // Changing bytes inside the signature container does not alter ISO 32000-1 byte range hash!
        Assert.Equal(hashA, hashB);

        // Changing bytes inside covered range DOES alter the hash
        Assert.NotEqual(hashA, hashC);
    }
}
