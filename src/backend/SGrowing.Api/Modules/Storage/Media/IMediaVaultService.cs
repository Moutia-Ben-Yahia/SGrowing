using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Storage.Media;

public interface IMediaVaultService
{
    Task<PreSignedUploadUrlResult> GeneratePreSignedUploadUrlAsync(Guid tenantId, CreateUploadUrlRequest req, CancellationToken ct = default);
    Task<bool> ConfirmUploadAsync(Guid tenantId, ConfirmUploadRequest req, CancellationToken ct = default);
    Task<PreSignedDownloadUrlResult?> GeneratePreSignedDownloadUrlAsync(Guid tenantId, Guid mediaId, int expiryMinutes = 60, CancellationToken ct = default);
    Task<IEnumerable<MediaVaultRecord>> GetMediaItemsAsync(Guid tenantId, CancellationToken ct = default);
}
