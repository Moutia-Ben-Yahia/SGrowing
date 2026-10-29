using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Storage.Media;
using Xunit;

namespace SGrowing.Tests.Storage;

public class MediaVaultSpecializedUrlsTests
{
    [Fact]
    public async Task GeneratePodSignatureUploadUrlAsync_ReturnsValidPreSignedUrl()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: $"MediaVaultDb_{Guid.NewGuid()}")
            .Options;

        var tenantId = Guid.NewGuid();
        var deliveryId = Guid.NewGuid();

        using var db = new SGrowingDbContext(options);
        var service = new MediaVaultService(db);

        // Act
        var result = await service.GeneratePodSignatureUploadUrlAsync(tenantId, deliveryId);

        // Assert
        result.Should().NotBeNull();
        result.StorageKey.Should().Contain("pod-signature");
        result.PreSignedUploadUrl.Should().StartWith("https://vault.storage.sgrowing.io/");
        result.PreSignedUploadUrl.Should().Contain("X-SGrowing-Expires");
        result.PreSignedUploadUrl.Should().Contain("X-SGrowing-Signature");
        result.Method.Should().Be("PUT");
    }

    [Fact]
    public async Task GenerateCallRecordingUploadUrlAsync_ReturnsValidPreSignedUrl()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: $"MediaVaultDb_{Guid.NewGuid()}")
            .Options;

        var tenantId = Guid.NewGuid();
        var callId = Guid.NewGuid();

        using var db = new SGrowingDbContext(options);
        var service = new MediaVaultService(db);

        // Act
        var result = await service.GenerateCallRecordingUploadUrlAsync(tenantId, callId);

        // Assert
        result.Should().NotBeNull();
        result.StorageKey.Should().Contain("call-recording");
        result.PreSignedUploadUrl.Should().Contain("X-SGrowing-Signature");
        result.ExpiresInSeconds.Should().Be(120 * 60);
    }

    [Fact]
    public async Task GenerateWhatsAppMediaUploadUrlAsync_ReturnsValidPreSignedUrl()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: $"MediaVaultDb_{Guid.NewGuid()}")
            .Options;

        var tenantId = Guid.NewGuid();
        using var db = new SGrowingDbContext(options);
        var service = new MediaVaultService(db);

        // Act
        var result = await service.GenerateWhatsAppMediaUploadUrlAsync(tenantId, "sales-wa-instance", "voice-note.ogg", "audio/ogg");

        // Assert
        result.Should().NotBeNull();
        result.StorageKey.Should().Contain("wa-sales-wa-instance-voice-note.ogg");
        result.PreSignedUploadUrl.Should().Contain("X-SGrowing-Signature");
    }
}
