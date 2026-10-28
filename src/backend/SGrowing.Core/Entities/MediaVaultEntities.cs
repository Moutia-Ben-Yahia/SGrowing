namespace SGrowing.Core.Entities;

public class MediaVaultRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public long FileSizeBytes { get; set; }
    public string StorageKey { get; set; } = string.Empty;
    public string Status { get; set; } = "PendingUpload"; // PendingUpload, Ready, Expired
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public record CreateUploadUrlRequest(
    string FileName,
    string ContentType,
    long FileSizeBytes,
    int ExpiryMinutes = 60);

public record PreSignedUploadUrlResult(
    Guid MediaId,
    string StorageKey,
    string PreSignedUploadUrl,
    string Method,
    int ExpiresInSeconds);

public record PreSignedDownloadUrlResult(
    Guid MediaId,
    string FileName,
    string PreSignedDownloadUrl,
    int ExpiresInSeconds);

public record ConfirmUploadRequest(
    Guid MediaId);
