using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Storage.Media;

public class MediaVaultService : IMediaVaultService
{
    private readonly SGrowingDbContext _db;
    private static readonly byte[] HmacKey = Encoding.UTF8.GetBytes("SGrowing-Object-Storage-Signing-Secret-Key-2026-Oct-28");

    public MediaVaultService(SGrowingDbContext db)
    {
        _db = db;
    }

    public async Task<PreSignedUploadUrlResult> GeneratePreSignedUploadUrlAsync(Guid tenantId, CreateUploadUrlRequest req, CancellationToken ct = default)
    {
        var mediaId = Guid.NewGuid();
        int expiryMins = Math.Clamp(req.ExpiryMinutes, 5, 1440);
        var expiryUtc = DateTime.UtcNow.AddMinutes(expiryMins);
        long unixExpiry = new DateTimeOffset(expiryUtc).ToUnixTimeSeconds();

        string safeFileName = Path.GetFileName(req.FileName?.Trim() ?? $"upload-{mediaId:N}.bin");
        string storageKey = $"tenants/{tenantId:N}/media/{mediaId:N}/{safeFileName}";

        string signature = GenerateSignature($"{tenantId:N}:{storageKey}:{unixExpiry}:PUT");
        string preSignedUrl = $"https://vault.storage.sgrowing.io/{storageKey}?X-SGrowing-Expires={unixExpiry}&X-SGrowing-Signature={signature}";

        var record = new MediaVaultRecord
        {
            Id = mediaId,
            TenantId = tenantId,
            FileName = safeFileName,
            ContentType = string.IsNullOrWhiteSpace(req.ContentType) ? "application/octet-stream" : req.ContentType.Trim(),
            FileSizeBytes = req.FileSizeBytes,
            StorageKey = storageKey,
            Status = "PendingUpload",
            ExpiresAtUtc = expiryUtc,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.MediaVaultRecords.Add(record);
        await _db.SaveChangesAsync(ct);

        return new PreSignedUploadUrlResult(
            MediaId: mediaId,
            StorageKey: storageKey,
            PreSignedUploadUrl: preSignedUrl,
            Method: "PUT",
            ExpiresInSeconds: expiryMins * 60
        );
    }

    public async Task<bool> ConfirmUploadAsync(Guid tenantId, ConfirmUploadRequest req, CancellationToken ct = default)
    {
        var record = await _db.MediaVaultRecords
            .FirstOrDefaultAsync(m => m.Id == req.MediaId && m.TenantId == tenantId, ct);

        if (record == null) return false;

        record.Status = "Ready";
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<PreSignedDownloadUrlResult?> GeneratePreSignedDownloadUrlAsync(Guid tenantId, Guid mediaId, int expiryMinutes = 60, CancellationToken ct = default)
    {
        var record = await _db.MediaVaultRecords
            .FirstOrDefaultAsync(m => m.Id == mediaId && m.TenantId == tenantId, ct);

        if (record == null) return null;

        int expiryMins = Math.Clamp(expiryMinutes, 5, 1440);
        var expiryUtc = DateTime.UtcNow.AddMinutes(expiryMins);
        long unixExpiry = new DateTimeOffset(expiryUtc).ToUnixTimeSeconds();

        string signature = GenerateSignature($"{tenantId:N}:{record.StorageKey}:{unixExpiry}:GET");
        string preSignedUrl = $"https://vault.storage.sgrowing.io/{record.StorageKey}?X-SGrowing-Expires={unixExpiry}&X-SGrowing-Signature={signature}";

        return new PreSignedDownloadUrlResult(
            MediaId: record.Id,
            FileName: record.FileName,
            PreSignedDownloadUrl: preSignedUrl,
            ExpiresInSeconds: expiryMins * 60
        );
    }

    public async Task<IEnumerable<MediaVaultRecord>> GetMediaItemsAsync(Guid tenantId, CancellationToken ct = default)
    {
        await EnsureDefaultMediaAsync(tenantId, ct);
        return await _db.MediaVaultRecords
            .Where(m => m.TenantId == tenantId)
            .OrderByDescending(m => m.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public Task<PreSignedUploadUrlResult> GeneratePodSignatureUploadUrlAsync(Guid tenantId, Guid deliveryId, CancellationToken ct = default)
    {
        return GeneratePreSignedUploadUrlAsync(tenantId, new CreateUploadUrlRequest(
            FileName: $"pod-signature-{deliveryId:N}.png",
            ContentType: "image/png",
            FileSizeBytes: 150_000,
            ExpiryMinutes: 60
        ), ct);
    }

    public Task<PreSignedUploadUrlResult> GenerateCallRecordingUploadUrlAsync(Guid tenantId, Guid callId, CancellationToken ct = default)
    {
        return GeneratePreSignedUploadUrlAsync(tenantId, new CreateUploadUrlRequest(
            FileName: $"call-recording-{callId:N}.mp3",
            ContentType: "audio/mpeg",
            FileSizeBytes: 5_000_000,
            ExpiryMinutes: 120
        ), ct);
    }

    public Task<PreSignedUploadUrlResult> GenerateWhatsAppMediaUploadUrlAsync(Guid tenantId, string instanceName, string fileName, string contentType, CancellationToken ct = default)
    {
        string safeName = Path.GetFileName(fileName);
        return GeneratePreSignedUploadUrlAsync(tenantId, new CreateUploadUrlRequest(
            FileName: $"wa-{instanceName}-{safeName}",
            ContentType: string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
            FileSizeBytes: 20_000_000,
            ExpiryMinutes: 60
        ), ct);
    }

    private static string GenerateSignature(string payload)
    {
        using var hmac = new HMACSHA256(HmacKey);
        byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexStringLower(hash);
    }

    private async Task EnsureDefaultMediaAsync(Guid tenantId, CancellationToken ct)
    {
        bool hasMedia = await _db.MediaVaultRecords.AnyAsync(m => m.TenantId == tenantId, ct);
        if (hasMedia) return;

        var defaultMedia = new MediaVaultRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FileName = "whatsapp-order-pod-signature.png",
            ContentType = "image/png",
            FileSizeBytes = 84520,
            StorageKey = $"tenants/{tenantId:N}/media/pod/whatsapp-order-pod-signature.png",
            Status = "Ready",
            ExpiresAtUtc = DateTime.UtcNow.AddYears(1),
            CreatedAtUtc = DateTime.UtcNow.AddDays(-3)
        };

        _db.MediaVaultRecords.Add(defaultMedia);
        await _db.SaveChangesAsync(ct);
    }
}
