using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using SGrowing.Api.Modules.Documents;
using Xunit;

namespace SGrowing.Tests.Unit;

public class MinioDocumentMirrorTests
{
    private readonly MinioDocumentMirrorService _mirror = new();
    private readonly Guid _tenantId = Guid.NewGuid();

    [Fact]
    public async Task MirrorDocumentSealAsync_StoresWithCorrectChecksumAndBucket()
    {
        var sealId = Guid.NewGuid();
        var fakePdfContent = Encoding.UTF8.GetBytes("%PDF-1.7\n1 0 obj\n<< /Type /Catalog >>\nendobj\n%%EOF");
        var expectedChecksum = Convert.ToHexStringLower(SHA256.HashData(fakePdfContent));

        var result = await _mirror.MirrorDocumentSealAsync(_tenantId, sealId, fakePdfContent, "contract_v1.pdf");

        Assert.Equal($"sgrowing-tenant-{_tenantId:N}", result.BucketName);
        Assert.Contains(sealId.ToString("N"), result.ObjectKey);
        Assert.Contains("contract_v1.pdf", result.ObjectKey);
        Assert.Equal(expectedChecksum, result.Sha256Checksum);
        Assert.Equal(fakePdfContent.Length, result.ByteLength);
        Assert.True(result.IntegrityVerified);
        Assert.Equal("GLACIER_INSTANT_RETRIEVAL", result.StorageClass);
    }

    [Fact]
    public async Task VerifyObjectIntegrityAsync_ReturnsTrueForValidAndFalseForTampered()
    {
        var sealId = Guid.NewGuid();
        var rawBytes = Encoding.UTF8.GetBytes("Original Immutable Document Content");
        var validHash = Convert.ToHexStringLower(SHA256.HashData(rawBytes));

        var mirrorResult = await _mirror.MirrorDocumentSealAsync(_tenantId, sealId, rawBytes, "audit_receipt.pdf");

        // Verify valid hash
        var isValid = await _mirror.VerifyObjectIntegrityAsync(_tenantId, mirrorResult.ObjectKey, validHash);
        Assert.True(isValid);

        // Verify tampered hash
        var tamperedHash = "0000000000000000000000000000000000000000000000000000000000000000";
        var isTamperedValid = await _mirror.VerifyObjectIntegrityAsync(_tenantId, mirrorResult.ObjectKey, tamperedHash);
        Assert.False(isTamperedValid);
    }

    [Fact]
    public async Task ListMirroredDocumentsAsync_ListsTenantSpecificDocuments()
    {
        var seal1 = Guid.NewGuid();
        var seal2 = Guid.NewGuid();

        await _mirror.MirrorDocumentSealAsync(_tenantId, seal1, Encoding.UTF8.GetBytes("Doc 1"), "doc1.pdf");
        await _mirror.MirrorDocumentSealAsync(_tenantId, seal2, Encoding.UTF8.GetBytes("Doc 2"), "doc2.pdf");

        var list = await _mirror.ListMirroredDocumentsAsync(_tenantId);

        Assert.Equal(2, list.Count);
        Assert.Contains(list, d => d.SealId == seal1);
        Assert.Contains(list, d => d.SealId == seal2);

        // Another tenant should have 0
        var otherTenantList = await _mirror.ListMirroredDocumentsAsync(Guid.NewGuid());
        Assert.Empty(otherTenantList);
    }
}
