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
        public SemaphoreSlim Gate { get; } = new(1, 1);
        public string LockToken { get; set; } = string.Empty;
        public DateTime ExpiresAtUtc { get; set; }
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
        var timeout = waitTimeout ?? TimeSpan.FromSeconds(2);
        var deadline = DateTime.UtcNow.Add(timeout);

        var entry = _locks.GetOrAdd(key, _ => new LockEntry());

        while (!ct.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;
            var remaining = deadline - now;
            var stepWait = remaining < TimeSpan.FromMilliseconds(50)
                ? (remaining > TimeSpan.Zero ? remaining : TimeSpan.FromMilliseconds(1))
                : TimeSpan.FromMilliseconds(50);

            bool entered = await entry.Gate.WaitAsync(stepWait, ct);
            if (entered)
            {
                try
                {
                    now = DateTime.UtcNow;
                    bool isFree = string.IsNullOrEmpty(entry.LockToken) || entry.ExpiresAtUtc <= now;
                    if (isFree)
                    {
                        string token = Guid.NewGuid().ToString("N");
                        var expiresAt = now.Add(leaseDuration);

                        entry.LockToken = token;
                        entry.ExpiresAtUtc = expiresAt;

                        return new ConcurrencyLockHandle(this, key, token, expiresAt);
                    }
                }
                finally
                {
                    entry.Gate.Release();
                }
            }

            if (DateTime.UtcNow >= deadline)
            {
                return null;
            }

            try
            {
                var delay = Math.Min(25, (int)(deadline - DateTime.UtcNow).TotalMilliseconds);
                if (delay > 0)
                {
                    await Task.Delay(delay, ct);
                }
                else
                {
                    return null;
                }
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        return null;
    }

    public async Task<bool> ReleaseLockAsync(
        string resourceKey,
        string lockToken,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(resourceKey) || string.IsNullOrWhiteSpace(lockToken))
            return false;

        string key = resourceKey.Trim();

        if (_locks.TryGetValue(key, out var entry))
        {
            await entry.Gate.WaitAsync(ct);
            try
            {
                if (entry.LockToken == lockToken)
                {
                    entry.LockToken = string.Empty;
                    entry.ExpiresAtUtc = DateTime.MinValue;
                    return true;
                }
            }
            finally
            {
                entry.Gate.Release();
            }
        }

        return false;
    }

    public async Task<bool> IsLockedAsync(string resourceKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(resourceKey))
            return false;

        string key = resourceKey.Trim();

        if (_locks.TryGetValue(key, out var entry))
        {
            await entry.Gate.WaitAsync(ct);
            try
            {
                return !string.IsNullOrEmpty(entry.LockToken) && entry.ExpiresAtUtc > DateTime.UtcNow;
            }
            finally
            {
                entry.Gate.Release();
            }
        }

        return false;
    }
}
