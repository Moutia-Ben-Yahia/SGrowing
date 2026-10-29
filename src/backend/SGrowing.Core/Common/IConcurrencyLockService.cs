namespace SGrowing.Core.Common;

/// <summary>
/// Represents an acquired distributed concurrency lock lease.
/// Releasing or disposing the handle immediately frees the lock for other callers.
/// </summary>
public interface IConcurrencyLockHandle : IAsyncDisposable
{
    string ResourceKey { get; }
    string LockToken { get; }
    bool IsAcquired { get; }
    DateTime ExpiresAtUtc { get; }
}

/// <summary>
/// Distributed concurrency lock service ensuring mutual exclusion for critical sections
/// (e.g. multi-warehouse SKU stock reservation, order state transitions, financial ledger commits).
/// </summary>
public interface IConcurrencyLockService
{
    /// <summary>
    /// Attempts to acquire an exclusive lock for the specified resourceKey.
    /// Returns null if the lock could not be acquired within the waitTimeout.
    /// </summary>
    Task<IConcurrencyLockHandle?> TryAcquireLockAsync(
        string resourceKey,
        TimeSpan? expiry = null,
        TimeSpan? waitTimeout = null,
        CancellationToken ct = default);

    /// <summary>
    /// Explicitly releases an acquired lock if the lockToken matches the current owner.
    /// </summary>
    Task<bool> ReleaseLockAsync(
        string resourceKey,
        string lockToken,
        CancellationToken ct = default);

    /// <summary>
    /// Checks whether a resourceKey is currently locked.
    /// </summary>
    Task<bool> IsLockedAsync(
        string resourceKey,
        CancellationToken ct = default);
}

/// <summary>
/// Exception thrown when a distributed concurrency lock cannot be acquired within the designated timeout.
/// </summary>
public class ConcurrencyLockConflictException : Exception
{
    public string ResourceKey { get; }

    public ConcurrencyLockConflictException(string resourceKey)
        : base($"Failed to acquire concurrency lock for resource '{resourceKey}'. Resource is locked by another process.")
    {
        ResourceKey = resourceKey;
    }

    public ConcurrencyLockConflictException(string resourceKey, string message)
        : base(message)
    {
        ResourceKey = resourceKey;
    }
}
