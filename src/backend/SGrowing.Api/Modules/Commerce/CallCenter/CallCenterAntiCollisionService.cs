using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Hubs;
using SGrowing.Core.Diagnostics;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Commerce.CallCenter;

public class CallCenterAntiCollisionService : ICallCenterAntiCollisionService
{
    private readonly SGrowingDbContext _db;
    private readonly IHubContext<OmniHub, IOmniClient>? _hubContext;
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public CallCenterAntiCollisionService(SGrowingDbContext db, IHubContext<OmniHub, IOmniClient>? hubContext = null)
    {
        _db = db;
        _hubContext = hubContext;
    }

    public async Task<CallCenterLockResult> AcquireLockAsync(
        Guid tenantId,
        Guid orderId,
        string orderNumber,
        Guid userId,
        string userName,
        TimeSpan? ttl = null)
    {
        var key = $"{tenantId}_{orderId}";
        var sem = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await sem.WaitAsync();

        try
        {
            var now = DateTime.UtcNow;
            var activeLock = await _db.CallCenterOrderLocks
                .Where(l => l.TenantId == tenantId && l.OrderId == orderId && l.IsActive && l.ExpiresAtUtc > now)
                .OrderByDescending(l => l.LockedAtUtc)
                .FirstOrDefaultAsync();

            var duration = ttl ?? TimeSpan.FromMinutes(3);

            if (activeLock != null)
            {
                if (activeLock.LockedByUserId == userId)
                {
                    // Same agent: renew lock
                    activeLock.Renew(duration);
                    await _db.SaveChangesAsync();

                    if (_hubContext != null)
                    {
                        await _hubContext.Clients.Group(OmniHub.GetTenantGroup(tenantId))
                            .OnOrderLocked(orderId, userId.ToString(), userName, activeLock.ExpiresAtUtc);
                    }

                    return new CallCenterLockResult(
                        true,
                        activeLock.Id,
                        orderId,
                        orderNumber,
                        userId,
                        userName,
                        activeLock.ExpiresAtUtc,
                        (int)(activeLock.ExpiresAtUtc - now).TotalSeconds,
                        "Lock renewed by current agent");
                }

                // Different agent: collision detected!
                return new CallCenterLockResult(
                    false,
                    activeLock.Id,
                    orderId,
                    orderNumber,
                    activeLock.LockedByUserId,
                    activeLock.LockedByUserName,
                    activeLock.ExpiresAtUtc,
                    Math.Max(0, (int)(activeLock.ExpiresAtUtc - now).TotalSeconds),
                    $"Collision: Order currently dialed by {activeLock.LockedByUserName}");
            }

            // No active lock or expired: acquire new lock
            var newLock = CallCenterOrderLockRecord.Create(tenantId, orderId, orderNumber, userId, userName, duration);
            _db.CallCenterOrderLocks.Add(newLock);
            await _db.SaveChangesAsync();

            if (_hubContext != null)
            {
                await _hubContext.Clients.Group(OmniHub.GetTenantGroup(tenantId))
                    .OnOrderLocked(orderId, userId.ToString(), userName, newLock.ExpiresAtUtc);
            }

            SGrowingTelemetry.CallCenterCollisionLocksAcquiredCounter.Add(1,
                new KeyValuePair<string, object?>("tenant_id", tenantId.ToString()));

            return new CallCenterLockResult(
                true,
                newLock.Id,
                orderId,
                orderNumber,
                userId,
                userName,
                newLock.ExpiresAtUtc,
                (int)duration.TotalSeconds,
                "Lock successfully acquired");
        }
        finally
        {
            sem.Release();
        }
    }

    public async Task<bool> RenewLockAsync(Guid tenantId, Guid orderId, Guid userId, TimeSpan? extension = null)
    {
        var now = DateTime.UtcNow;
        var activeLock = await _db.CallCenterOrderLocks
            .Where(l => l.TenantId == tenantId && l.OrderId == orderId && l.LockedByUserId == userId && l.IsActive && l.ExpiresAtUtc > now)
            .FirstOrDefaultAsync();

        if (activeLock == null) return false;

        activeLock.Renew(extension ?? TimeSpan.FromMinutes(3));
        await _db.SaveChangesAsync();

        if (_hubContext != null)
        {
            await _hubContext.Clients.Group(OmniHub.GetTenantGroup(tenantId))
                .OnOrderLocked(orderId, userId.ToString(), activeLock.LockedByUserName, activeLock.ExpiresAtUtc);
        }

        return true;
    }

    public async Task<bool> ReleaseLockAsync(Guid tenantId, Guid orderId, Guid userId)
    {
        var activeLock = await _db.CallCenterOrderLocks
            .Where(l => l.TenantId == tenantId && l.OrderId == orderId && l.LockedByUserId == userId && l.IsActive)
            .FirstOrDefaultAsync();

        if (activeLock == null) return false;

        activeLock.Release();
        await _db.SaveChangesAsync();

        if (_hubContext != null)
        {
            await _hubContext.Clients.Group(OmniHub.GetTenantGroup(tenantId))
                .OnOrderUnlocked(orderId);
        }

        return true;
    }

    public async Task<CallCenterLockResult> GetActiveLockAsync(Guid tenantId, Guid orderId)
    {
        var now = DateTime.UtcNow;
        var activeLock = await _db.CallCenterOrderLocks
            .Where(l => l.TenantId == tenantId && l.OrderId == orderId && l.IsActive && l.ExpiresAtUtc > now)
            .OrderByDescending(l => l.LockedAtUtc)
            .FirstOrDefaultAsync();

        if (activeLock == null)
        {
            return new CallCenterLockResult(
                false,
                null,
                orderId,
                string.Empty,
                null,
                null,
                null,
                0,
                "No active lock");
        }

        return new CallCenterLockResult(
            true,
            activeLock.Id,
            orderId,
            activeLock.OrderNumber,
            activeLock.LockedByUserId,
            activeLock.LockedByUserName,
            activeLock.ExpiresAtUtc,
            (int)(activeLock.ExpiresAtUtc - now).TotalSeconds,
            "Active lock found");
    }

    public async Task<int> SweepExpiredLocksAsync(Guid tenantId)
    {
        var now = DateTime.UtcNow;
        var expiredLocks = await _db.CallCenterOrderLocks
            .Where(l => l.TenantId == tenantId && l.IsActive && l.ExpiresAtUtc <= now)
            .ToListAsync();

        foreach (var lck in expiredLocks)
        {
            lck.IsActive = false;
        }

        if (expiredLocks.Count > 0)
        {
            await _db.SaveChangesAsync();
        }

        return expiredLocks.Count;
    }
}
