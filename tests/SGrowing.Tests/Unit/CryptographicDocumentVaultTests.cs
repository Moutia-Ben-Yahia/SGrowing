using System;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Documents;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Unit;

public class CryptographicDocumentVaultTests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public async Task SealDocumentWithPkcs7Async_PersistsSealRecordInDatabase()
    {
        using var db = CreateDbContext();
        var vault = new PersistentDocumentVaultService(db);
        var tenantId = Guid.NewGuid();
        var documentId = Guid.NewGuid();

        var pdfBytes = Encoding.UTF8.GetBytes("%PDF-1.7 Original Document Content /ByteRange [0 20 40 20] EndOfPayload");

        var sealResult = await vault.SealDocumentWithPkcs7Async(
            tenantId,
            documentId,
            pdfBytes,
            "signer.lead@enterprise.com",
            "10.0.0.42"
        );

        sealResult.Should().NotBeNull();
        sealResult.DocumentId.Should().Be(documentId);
        sealResult.Sha256Hash.Should().NotBeNullOrWhiteSpace();
        sealResult.SignatureFormat.Should().Be("ISO 32000-1 PKCS#7 / PAdES-B-LTV");

        var persisted = await vault.GetSealRecordAsync(tenantId, documentId);
        persisted.Should().NotBeNull();
        persisted!.Sha256Hash.Should().Be(sealResult.Sha256Hash);
        persisted.SignerEmail.Should().Be("signer.lead@enterprise.com");
        persisted.SignerIpAddress.Should().Be("10.0.0.42");
        persisted.AuditChecksum.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task VerifySealAsync_WithIntactDocumentAndValidToken_ReturnsValid()
    {
        using var db = CreateDbContext();
        var vault = new PersistentDocumentVaultService(db);
        var tenantId = Guid.NewGuid();
        var documentId = Guid.NewGuid();

        var originalPdf = Encoding.UTF8.GetBytes("%PDF-1.7 Legally Binding Contract Terms");

        var seal = await vault.SealDocumentWithPkcs7Async(
            tenantId,
            documentId,
            originalPdf,
            "director@partner.com"
        );

        var verify = await vault.VerifySealAsync(
            tenantId,
            documentId,
            verificationToken: seal.TamperProofVerificationToken,
            pdfRawBytes: originalPdf
        );

        verify.IsSealValid.Should().BeTrue();
        verify.IntegrityIntact.Should().BeTrue();
        verify.VerificationTokenValid.Should().BeTrue();
        verify.Status.Should().Be("Valid");
    }

    [Fact]
    public async Task VerifySealAsync_WithTamperedPayload_DetectsTampering()
    {
        using var db = CreateDbContext();
        var vault = new PersistentDocumentVaultService(db);
        var tenantId = Guid.NewGuid();
        var documentId = Guid.NewGuid();

        var originalPdf = Encoding.UTF8.GetBytes("%PDF-1.7 Agreement Price: $10,000 USD");
        var tamperedPdf = Encoding.UTF8.GetBytes("%PDF-1.7 Agreement Price: $1,000 USD (Hacked)");

        var seal = await vault.SealDocumentWithPkcs7Async(
            tenantId,
            documentId,
            originalPdf,
            "director@partner.com"
        );

        var verify = await vault.VerifySealAsync(
            tenantId,
            documentId,
            verificationToken: seal.TamperProofVerificationToken,
            pdfRawBytes: tamperedPdf
        );

        verify.IsSealValid.Should().BeFalse();
        verify.IntegrityIntact.Should().BeFalse();
        verify.Status.Should().Be("Tampered");
        verify.Message.Should().Contain("modified");
    }

    [Fact]
    public async Task VerifySealAsync_WithForgedVerificationToken_ReportsInvalidToken()
    {
        using var db = CreateDbContext();
        var vault = new PersistentDocumentVaultService(db);
        var tenantId = Guid.NewGuid();
        var documentId = Guid.NewGuid();

        var pdf = Encoding.UTF8.GetBytes("%PDF-1.7 Standard Terms");

        await vault.SealDocumentWithPkcs7Async(tenantId, documentId, pdf, "officer@corp.com");

        var verify = await vault.VerifySealAsync(
            tenantId,
            documentId,
            verificationToken: "attacker_forged_token_0000",
            pdfRawBytes: pdf
        );

        verify.IsSealValid.Should().BeFalse();
        verify.VerificationTokenValid.Should().BeFalse();
        verify.Status.Should().Be("InvalidVerificationToken");
    }

    [Fact]
    public async Task GenerateCertificateOfCompletionAsync_PopulatesRfc3161AuthorityAndSigner()
    {
        using var db = CreateDbContext();
        var vault = new PersistentDocumentVaultService(db);
        var tenantId = Guid.NewGuid();
        var documentId = Guid.NewGuid();

        var contract = DigitalContract.Create(tenantId, "Enterprise MSA 2026", "MSA", "Jane Doe", "jane@corp.com", 50000m);
        contract.Id = documentId;
        db.DigitalContracts.Add(contract);
        await db.SaveChangesAsync();

        var pdf = Encoding.UTF8.GetBytes("%PDF-1.7 MSA Content");
        await vault.SealDocumentWithPkcs7Async(tenantId, documentId, pdf, "jane@corp.com", "192.168.1.100");

        var cert = await vault.GenerateCertificateOfCompletionAsync(tenantId, documentId);

        cert.DocumentId.Should().Be(documentId);
        cert.DocumentTitle.Should().Be("Enterprise MSA 2026");
        cert.SignerName.Should().Be("Jane Doe");
        cert.SignerEmail.Should().Be("jane@corp.com");
        cert.TsaAuthorityName.Should().Contain("RFC 3161");
        cert.VerificationUrl.Should().Contain("verify.sgrowing.io");
    }
}
