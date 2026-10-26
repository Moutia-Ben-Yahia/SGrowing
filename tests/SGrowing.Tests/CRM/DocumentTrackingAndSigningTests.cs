using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Moq;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.CRM;
using SGrowing.Core.Entities;
using SGrowing.Core.Outbox;
using Xunit;

namespace SGrowing.Tests.CRM;

public class DocumentTrackingAndSigningTests
{
    private readonly SGrowingDbContext _db;
    private readonly Mock<IOutbox> _outboxMock;
    private readonly DocumentTrackingService _service;

    public DocumentTrackingAndSigningTests()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new SGrowingDbContext(options);
        _outboxMock = new Mock<IOutbox>();
        _service = new DocumentTrackingService(_db, _outboxMock.Object);
    }

    [Fact]
    public async Task CreateEnvelope_ComputesSha256ChecksumAndInitializesSigners()
    {
        var tenantId = Guid.NewGuid();
        var rawContent = "Master Services Agreement Terms & Conditions 2026";
        var expectedHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawContent)));

        var envelope = await _service.CreateEnvelopeAsync(
            tenantId,
            "Master Services Agreement 2026",
            "https://cdn.sgrowing.io/docs/msa-2026.pdf",
            rawContent,
            isSequential: true
        );

        var signer1 = await _service.AddSignerAsync(tenantId, envelope.Id, "Alice Smith", "alice@enterprise.com", SignerRole.Signer, 1);
        var signer2 = await _service.AddSignerAsync(tenantId, envelope.Id, "Bob Jones", "bob@partner.com", SignerRole.Approver, 2);

        Assert.NotNull(envelope);
        Assert.Equal("Master Services Agreement 2026", envelope.Title);
        Assert.Equal(expectedHash, envelope.ChecksumSha256);
        Assert.Equal(EnvelopeStatus.Sent, envelope.Status);
        Assert.NotNull(signer1);
        Assert.NotNull(signer2);
        Assert.Equal("alice@enterprise.com", signer1.Email);
        Assert.Equal(1, signer1.OrderIndex);
        Assert.Equal(2, signer2.OrderIndex);
    }

    [Fact]
    public async Task SignDocument_SequentialOrderViolation_ThrowsInvalidOperationException()
    {
        var tenantId = Guid.NewGuid();
        var envelope = await _service.CreateEnvelopeAsync(
            tenantId,
            "Sequential Agreement",
            "https://cdn.sgrowing.io/docs/seq.pdf",
            "Content",
            isSequential: true
        );

        await _service.AddSignerAsync(tenantId, envelope.Id, "First Signer", "first@corp.com", SignerRole.Signer, 1);
        await _service.AddSignerAsync(tenantId, envelope.Id, "Second Signer", "second@corp.com", SignerRole.Signer, 2);

        // Second signer attempts to sign before First Signer
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.SubmitSignatureAsync(tenantId, envelope.Id, "second@corp.com", "sig_data_2", "192.168.1.100"));

        Assert.Contains("must sign first", ex.Message);
    }

    [Fact]
    public async Task SignDocument_FullSequentialFlow_TransitionsToCompletedWithAuditCertificate()
    {
        var tenantId = Guid.NewGuid();
        var envelope = await _service.CreateEnvelopeAsync(
            tenantId,
            "Enterprise Order PO-900",
            "https://cdn.sgrowing.io/docs/po-900.pdf",
            "Full Order Purchase Spec",
            isSequential: true
        );

        await _service.AddSignerAsync(tenantId, envelope.Id, "Buyer Rep", "buyer@client.com", SignerRole.Signer, 1);
        await _service.AddSignerAsync(tenantId, envelope.Id, "VP Sales", "vp@vendor.com", SignerRole.Approver, 2);

        // 1. Signer 1 signs
        var afterSign1 = await _service.SubmitSignatureAsync(tenantId, envelope.Id, "buyer@client.com", "sig_buyer_bytes", "10.0.0.1");
        Assert.Equal(EnvelopeStatus.Sent, afterSign1.Status);

        // 2. Signer 2 signs -> complete
        var afterSign2 = await _service.SubmitSignatureAsync(tenantId, envelope.Id, "vp@vendor.com", "sig_vp_bytes", "10.0.0.2");
        Assert.Equal(EnvelopeStatus.Completed, afterSign2.Status);
        Assert.NotNull(afterSign2.CompletedAtUtc);
        Assert.False(string.IsNullOrWhiteSpace(afterSign2.AuditCertificateJson));
        Assert.Contains("buyer@client.com", afterSign2.AuditCertificateJson);
        Assert.Contains("vp@vendor.com", afterSign2.AuditCertificateJson);

        // Outbox event should be dispatched
        _outboxMock.Verify(o => o.EnqueueAsync(
            tenantId,
            "Document.EnvelopeCompleted",
            It.IsAny<DocumentEnvelopeCertificateDto>(),
            It.IsAny<CancellationToken>()), Times.Once);

        // Certificate deserialization works
        var cert = await _service.GetEnvelopeCertificateAsync(tenantId, envelope.Id);
        Assert.NotNull(cert);
        Assert.Equal(2, cert.Signers.Count);
    }

    [Fact]
    public async Task RecordDwellTelemetry_AggregatesPageDwellAndCalculatesAverage()
    {
        var tenantId = Guid.NewGuid();
        var envelope = await _service.CreateEnvelopeAsync(tenantId, "Series B Pitch", "https://cdn.sgrowing.io/pitch.pdf", "Deck");

        // Record telemetry: page 1 dwell
        await _service.RecordPageDwellAsync(tenantId, envelope.Id, pageNumber: 1, viewerEmail: "vc1@fund.com", viewerIp: "127.0.0.1", dwellSeconds: 15, maxScrollPercent: 80);
        await _service.RecordPageDwellAsync(tenantId, envelope.Id, pageNumber: 1, viewerEmail: "vc2@fund.com", viewerIp: "127.0.0.2", dwellSeconds: 10, maxScrollPercent: 100);
        // Page 2 dwell
        await _service.RecordPageDwellAsync(tenantId, envelope.Id, pageNumber: 2, viewerEmail: "vc1@fund.com", viewerIp: "127.0.0.1", dwellSeconds: 30, maxScrollPercent: 90);

        var report = await _service.GetDocumentDwellReportAsync(tenantId, envelope.Id);

        Assert.NotNull(report);
        Assert.Equal(55, report.TotalDwellSeconds);
        Assert.Equal(2, report.TotalUniqueViewers);
        Assert.Equal(2, report.PagesBreakdown.Count);

        var page1 = report.PagesBreakdown.First(p => p.PageNumber == 1);
        Assert.Equal(25, page1.TotalDwellSeconds);
        Assert.Equal(2, page1.ViewersCount);
        Assert.Equal(12.5, page1.AverageDwellSeconds);
    }

    [Fact]
    public async Task AccessGate_PasscodeValidation()
    {
        var tenantId = Guid.NewGuid();
        var envelope = await _service.CreateEnvelopeAsync(tenantId, "Financials Q3", "https://cdn.sgrowing.io/fin.pdf", "Financial Data");

        await _service.ConfigureAccessGateAsync(
            tenantId,
            envelope.Id,
            requireEmail: true,
            passcode: "ALPHA99"
        );

        // Wrong passcode fails
        var wrongPass = await _service.VerifyPasscodeAsync(tenantId, envelope.Id, "WRONG");
        Assert.False(wrongPass);

        // Right passcode succeeds
        var validAccess = await _service.VerifyPasscodeAsync(tenantId, envelope.Id, "ALPHA99");
        Assert.True(validAccess);
    }
}
