using System.Collections.Concurrent;

namespace SGrowing.Core.Common;

/// <summary>
/// High-throughput, thread-safe memory-backed distributed concurrency lock engine.
/// Provides lease-based expiration, owner token verification, and asynchronous wait queues.
/// </summary>
public class DistributedMemoryLockService : IConcurrencyLockService
{
    private class LockEntry
    {
        public string LockToken { get; set; } = string.Empty;
        public DateTime ExpiresAtUtc { get; set; }
        public SemaphoreSlim Semaphore { get; } = new(1, 1);
    }

    private class ConcurrencyLockHandle : IConcurrencyLockHandle
    {
        private readonly DistributedMemoryLockService _service;
        private int _disposed;

        public string ResourceKey { get; }
        public string LockToken { get; }
        public bool IsAcquired => _disposed == 0;
        public DateTime ExpiresAtUtc { get; }

        public ConcurrencyLockHandle(DistributedMemoryLockService service, string resourceKey, string lockToken, DateTime expiresAtUtc)
        {
            _service = service;
            ResourceKey = resourceKey;
            LockToken = lockToken;
            ExpiresAtUtc = expiresAtUtc;
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                await _service.ReleaseLockAsync(ResourceKey, LockToken);
            }
        }
    }

    private readonly ConcurrentDictionary<string, LockEntry> _locks = new();

    public async Task<IConcurrencyLockHandle?> TryAcquireLockAsync(
        string resourceKey,
        TimeSpan? expiry = null,
        TimeSpan? waitTimeout = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(resourceKey))
            throw new ArgumentNullException(nameof(resourceKey));

        string key = resourceKey.Trim();
        var leaseDuration = expiry ?? TimeSpan.FromSeconds(15);
        var wait = waitTimeout ?? TimeSpan.FromSeconds(2);

        var entry = _locks.GetOrAdd(key, _ => new LockEntry());

        // Attempt to enter the semaphore within the wait timeout
        bool acquired = await entry.Semaphore.WaitAsync(wait, ct);
        if (!acquired)
        {
            return null;
        }

        try
        {
            var now = DateTime.UtcNow;

            // Check if currently held and not expired
            if (!string.IsNullOrEmpty(entry.LockToken) && entry.ExpiresAtUtc > now)
            {
                // Lock is active and held by an unexpired lease
                entry.Semaphore.Release();
                return null;
            }

            string token = Guid.NewGuid().ToString("N");
            var expiresAt = now.Add(leaseDuration);

            entry.LockToken = token;
            entry.ExpiresAtUtc = expiresAt;

            return new ConcurrencyLockHandle(this, key, token, expiresAt);
        }
        catch
        {
            entry.Semaphore.Release();
            throw;
        }
    }

    public Task<bool> ReleaseLockAsync(
        string resourceKey,
        string lockToken,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(resourceKey) || string.IsNullOrWhiteSpace(lockToken))
            return Task.FromResult(false);

        string key = resourceKey.Trim();

        if (_locks.TryGetValue(key, out var entry))
        {
            if (entry.LockToken == lockToken)
            {
                entry.LockToken = string.Empty;
                entry.ExpiresAtUtc = DateTime.MinValue;
                try
                {
                    entry.Semaphore.Release();
                }
                catch (SemaphoreFullException)
                {
                    // Already released
                }
                return Task.FromResult(true);
            }
        }

        return Task.FromResult(false);
    }

    public Task<bool> IsLockedAsync(string resourceKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(resourceKey))
            return Task.FromResult(false);

        string key = resourceKey.Trim();

        if (_locks.TryGetValue(key, out var entry))
        {
            bool locked = !string.IsNullOrEmpty(entry.LockToken) && entry.ExpiresAtUtc > DateTime.UtcNow;
            return Task.FromResult(locked);
        }

        return Task.FromResult(false);
    }
}
