using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Storage.Media;

public interface IMediaVaultService
{
    Task<PreSignedUploadUrlResult> GeneratePreSignedUploadUrlAsync(Guid tenantId, CreateUploadUrlRequest req, CancellationToken ct = default);
    Task<bool> ConfirmUploadAsync(Guid tenantId, ConfirmUploadRequest req, CancellationToken ct = default);
    Task<PreSignedDownloadUrlResult?> GeneratePreSignedDownloadUrlAsync(Guid tenantId, Guid mediaId, int expiryMinutes = 60, CancellationToken ct = default);
    Task<IEnumerable<MediaVaultRecord>> GetMediaItemsAsync(Guid tenantId, CancellationToken ct = default);

    // Specialized Direct-to-S3 / MinIO pipeline upload helpers
    Task<PreSignedUploadUrlResult> GeneratePodSignatureUploadUrlAsync(Guid tenantId, Guid deliveryId, CancellationToken ct = default);
    Task<PreSignedUploadUrlResult> GenerateCallRecordingUploadUrlAsync(Guid tenantId, Guid callId, CancellationToken ct = default);
    Task<PreSignedUploadUrlResult> GenerateWhatsAppMediaUploadUrlAsync(Guid tenantId, string instanceName, string fileName, string contentType, CancellationToken ct = default);
}
