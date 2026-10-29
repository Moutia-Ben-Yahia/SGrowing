using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Commerce.CallCenter;

public record CallCenterLockResult(
    bool IsAcquired,
    Guid? LockId,
    Guid OrderId,
    string OrderNumber,
    Guid? LockedByUserId,
    string? LockedByUserName,
    DateTime? ExpiresAtUtc,
    int ExpiresInSeconds,
    string Reason
);

public interface ICallCenterAntiCollisionService
{
    Task<CallCenterLockResult> AcquireLockAsync(
        Guid tenantId,
        Guid orderId,
        string orderNumber,
        Guid userId,
        string userName,
        TimeSpan? ttl = null);

    Task<bool> RenewLockAsync(
        Guid tenantId,
        Guid orderId,
        Guid userId,
        TimeSpan? extension = null);

    Task<bool> ReleaseLockAsync(
        Guid tenantId,
        Guid orderId,
        Guid userId);

    Task<CallCenterLockResult> GetActiveLockAsync(
        Guid tenantId,
        Guid orderId);

    Task<int> SweepExpiredLocksAsync(Guid tenantId);
}
