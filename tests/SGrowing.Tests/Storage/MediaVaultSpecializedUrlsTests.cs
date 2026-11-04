using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Storage.Media;
using SGrowing.Core.Entities;
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

    [Fact]
    public async Task GeneratePreSignedUploadUrlAsync_ClampsExpiryMinutes_Between5And1440()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: $"MediaVaultDb_{Guid.NewGuid()}")
            .Options;

        var tenantId = Guid.NewGuid();
        using var db = new SGrowingDbContext(options);
        var service = new MediaVaultService(db);

        // Act: Lower than minimum (2 mins -> should clamp to 5 mins = 300s)
        var lowResult = await service.GeneratePreSignedUploadUrlAsync(tenantId, new CreateUploadUrlRequest(
            FileName: "test-low.pdf",
            ContentType: "application/pdf",
            FileSizeBytes: 1024,
            ExpiryMinutes: 2
        ));

        // Act: Higher than maximum (2000 mins -> should clamp to 1440 mins = 86400s)
        var highResult = await service.GeneratePreSignedUploadUrlAsync(tenantId, new CreateUploadUrlRequest(
            FileName: "test-high.pdf",
            ContentType: "application/pdf",
            FileSizeBytes: 2048,
            ExpiryMinutes: 2000
        ));

        // Assert
        lowResult.ExpiresInSeconds.Should().Be(5 * 60);
        highResult.ExpiresInSeconds.Should().Be(1440 * 60);
    }

    [Fact]
    public async Task GeneratePreSignedUploadUrlAsync_PersistsPendingUploadRecordInDb()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: $"MediaVaultDb_{Guid.NewGuid()}")
            .Options;

        var tenantId = Guid.NewGuid();
        using var db = new SGrowingDbContext(options);
        var service = new MediaVaultService(db);

        // Act
        var result = await service.GeneratePreSignedUploadUrlAsync(tenantId, new CreateUploadUrlRequest(
            FileName: "presentation.pptx",
            ContentType: "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            FileSizeBytes: 15_400_000,
            ExpiryMinutes: 45
        ));

        // Assert
        var savedRecord = await db.MediaVaultRecords.FirstOrDefaultAsync(m => m.Id == result.MediaId);
        savedRecord.Should().NotBeNull();
        savedRecord!.TenantId.Should().Be(tenantId);
        savedRecord.FileName.Should().Be("presentation.pptx");
        savedRecord.ContentType.Should().Be("application/vnd.openxmlformats-officedocument.presentationml.presentation");
        savedRecord.FileSizeBytes.Should().Be(15_400_000);
        savedRecord.Status.Should().Be("PendingUpload");
        savedRecord.StorageKey.Should().Be(result.StorageKey);
    }

    [Fact]
    public async Task ConfirmUploadAsync_ExistingMedia_SetsStatusReadyAndReturnsTrue()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: $"MediaVaultDb_{Guid.NewGuid()}")
            .Options;

        var tenantId = Guid.NewGuid();
        using var db = new SGrowingDbContext(options);
        var service = new MediaVaultService(db);

        var upload = await service.GeneratePreSignedUploadUrlAsync(tenantId, new CreateUploadUrlRequest(
            FileName: "avatar.png",
            ContentType: "image/png",
            FileSizeBytes: 42000
        ));

        // Act
        var confirmed = await service.ConfirmUploadAsync(tenantId, new ConfirmUploadRequest(upload.MediaId));

        // Assert
        confirmed.Should().BeTrue();
        var record = await db.MediaVaultRecords.FindAsync(upload.MediaId);
        record.Should().NotBeNull();
        record!.Status.Should().Be("Ready");
    }

    [Fact]
    public async Task ConfirmUploadAsync_NonExistentMedia_ReturnsFalse()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: $"MediaVaultDb_{Guid.NewGuid()}")
            .Options;

        var tenantId = Guid.NewGuid();
        using var db = new SGrowingDbContext(options);
        var service = new MediaVaultService(db);

        // Act
        var confirmed = await service.ConfirmUploadAsync(tenantId, new ConfirmUploadRequest(Guid.NewGuid()));

        // Assert
        confirmed.Should().BeFalse();
    }

    [Fact]
    public async Task GeneratePreSignedDownloadUrlAsync_ExistingMedia_ReturnsValidDownloadUrl()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: $"MediaVaultDb_{Guid.NewGuid()}")
            .Options;

        var tenantId = Guid.NewGuid();
        using var db = new SGrowingDbContext(options);
        var service = new MediaVaultService(db);

        var upload = await service.GeneratePreSignedUploadUrlAsync(tenantId, new CreateUploadUrlRequest(
            FileName: "invoice-oct-2026.pdf",
            ContentType: "application/pdf",
            FileSizeBytes: 89000
        ));

        // Act
        var download = await service.GeneratePreSignedDownloadUrlAsync(tenantId, upload.MediaId, expiryMinutes: 30);

        // Assert
        download.Should().NotBeNull();
        download!.MediaId.Should().Be(upload.MediaId);
        download.FileName.Should().Be("invoice-oct-2026.pdf");
        download.ExpiresInSeconds.Should().Be(30 * 60);
        download.PreSignedDownloadUrl.Should().StartWith("https://vault.storage.sgrowing.io/");
        download.PreSignedDownloadUrl.Should().Contain("X-SGrowing-Expires");
        download.PreSignedDownloadUrl.Should().Contain("X-SGrowing-Signature");
    }

    [Fact]
    public async Task GeneratePreSignedDownloadUrlAsync_NonExistentMedia_ReturnsNull()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: $"MediaVaultDb_{Guid.NewGuid()}")
            .Options;

        var tenantId = Guid.NewGuid();
        using var db = new SGrowingDbContext(options);
        var service = new MediaVaultService(db);

        // Act
        var download = await service.GeneratePreSignedDownloadUrlAsync(tenantId, Guid.NewGuid());

        // Assert
        download.Should().BeNull();
    }

    [Fact]
    public async Task GetMediaItemsAsync_SeedsDefaultMedia_WhenNoneExistsForTenant()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: $"MediaVaultDb_{Guid.NewGuid()}")
            .Options;

        var tenantId = Guid.NewGuid();
        using var db = new SGrowingDbContext(options);
        var service = new MediaVaultService(db);

        // Act
        var items = (await service.GetMediaItemsAsync(tenantId)).ToList();

        // Assert
        items.Should().NotBeEmpty();
        items.Should().ContainSingle(m => m.FileName == "whatsapp-order-pod-signature.png");
        items.First().Status.Should().Be("Ready");
        items.First().TenantId.Should().Be(tenantId);
    }

    [Fact]
    public async Task GetMediaItemsAsync_ReturnsExistingItems_OrderedByCreatedAtUtcDescending()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: $"MediaVaultDb_{Guid.NewGuid()}")
            .Options;

        var tenantId = Guid.NewGuid();
        using var db = new SGrowingDbContext(options);
        var service = new MediaVaultService(db);

        // Seed 2 custom media items with different CreatedAtUtc
        var older = new MediaVaultRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FileName = "older.png",
            Status = "Ready",
            CreatedAtUtc = DateTime.UtcNow.AddHours(-5)
        };
        var newer = new MediaVaultRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FileName = "newer.png",
            Status = "Ready",
            CreatedAtUtc = DateTime.UtcNow.AddHours(-1)
        };
        db.MediaVaultRecords.AddRange(older, newer);
        await db.SaveChangesAsync();

        // Act
        var items = (await service.GetMediaItemsAsync(tenantId)).ToList();

        // Assert
        items.Should().HaveCount(2);
        items[0].FileName.Should().Be("newer.png");
        items[1].FileName.Should().Be("older.png");
    }
}
