using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Tenancy.SnapshotsAndRebilling;

public record SnapshotDeploymentResultDto(
    Guid SnapshotId,
    Guid TargetTenantId,
    string SnapshotName,
    int CustomFieldsCreated,
    int WorkflowsCreated,
    int FunnelsCreated,
    DateTime DeployedAtUtc,
    string Message
);

public interface ITenantSnapshotService
{
    Task<TenantSnapshotRecord> CreateSnapshotAsync(
        Guid tenantId,
        string snapshotName,
        string category,
        string description = "");

    Task<SnapshotDeploymentResultDto> DeploySnapshotAsync(
        Guid targetTenantId,
        Guid snapshotId,
        string targetTenantName);

    Task<List<TenantSnapshotRecord>> ListSnapshotsAsync(Guid tenantId);

    Task<TenantSnapshotRecord?> GetSnapshotAsync(Guid snapshotId);
}
