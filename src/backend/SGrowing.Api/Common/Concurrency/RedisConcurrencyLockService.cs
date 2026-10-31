using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SGrowing.Core.Common;
using StackExchange.Redis;

namespace SGrowing.Api.Common.Concurrency;

/// <summary>
/// Production-grade Redis distributed concurrency lock service with lease expiration,
/// owner verification via unique tokens, atomic Lua script release, and in-memory fallback.
/// </summary>
public class RedisConcurrencyLockService : IConcurrencyLockService
{
    private const string ReleaseLockScript = @"
        if redis.call('get', KEYS[1]) == ARGV[1] then
            return redis.call('del', KEYS[1])
        else
            return 0
        end";

    private class RedisLockHandle : IConcurrencyLockHandle
    {
        private readonly RedisConcurrencyLockService _service;
        private int _disposed;

        public string ResourceKey { get; }
        public string LockToken { get; }
        public bool IsAcquired => _disposed == 0;
        public DateTime ExpiresAtUtc { get; }

        public RedisLockHandle(RedisConcurrencyLockService service, string resourceKey, string lockToken, DateTime expiresAtUtc)
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

    private readonly IConnectionMultiplexer? _redis;
    private readonly ILogger<RedisConcurrencyLockService>? _logger;
    private readonly DistributedMemoryLockService _memoryFallback = new();

    public RedisConcurrencyLockService(
        IConnectionMultiplexer? redis = null,
        ILogger<RedisConcurrencyLockService>? logger = null)
    {
        _redis = redis;
        _logger = logger;
    }

    public bool IsConnected => _redis != null && _redis.IsConnected;

    public async Task<IConcurrencyLockHandle?> TryAcquireLockAsync(
        string resourceKey,
        TimeSpan? expiry = null,
        TimeSpan? waitTimeout = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(resourceKey))
            throw new ArgumentNullException(nameof(resourceKey));

        string trimmedKey = resourceKey.Trim();

        if (_redis == null || !_redis.IsConnected)
        {
            _logger?.LogDebug("Redis not connected. Delegating lock acquisition for '{Key}' to in-memory lock engine.", trimmedKey);
            return await _memoryFallback.TryAcquireLockAsync(trimmedKey, expiry, waitTimeout, ct);
        }

        var leaseDuration = expiry ?? TimeSpan.FromSeconds(15);
        var timeout = waitTimeout ?? TimeSpan.FromSeconds(2);
        var deadline = DateTime.UtcNow.Add(timeout);

        string redisKey = $"sgrowing:lock:{trimmedKey}";
        string token = Guid.NewGuid().ToString("N");
        var db = _redis.GetDatabase();

        while (!ct.IsCancellationRequested)
        {
            bool acquired = await db.StringSetAsync(redisKey, token, leaseDuration, When.NotExists);
            if (acquired)
            {
                var expiresAt = DateTime.UtcNow.Add(leaseDuration);
                _logger?.LogDebug("Acquired distributed lock '{Key}' with token '{Token}'. Expires at {Expiry:O}.", redisKey, token, expiresAt);
                return new RedisLockHandle(this, trimmedKey, token, expiresAt);
            }

            if (DateTime.UtcNow >= deadline)
            {
                _logger?.LogDebug("Failed to acquire distributed lock '{Key}' within {Timeout}s.", redisKey, timeout.TotalSeconds);
                return null;
            }

            try
            {
                await Task.Delay(50, ct);
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

        string trimmedKey = resourceKey.Trim();

        if (_redis == null || !_redis.IsConnected)
        {
            return await _memoryFallback.ReleaseLockAsync(trimmedKey, lockToken, ct);
        }

        try
        {
            var db = _redis.GetDatabase();
            string redisKey = $"sgrowing:lock:{trimmedKey}";
            var result = await db.ScriptEvaluateAsync(
                ReleaseLockScript,
                [redisKey],
                [lockToken]);

            bool released = (int)result == 1;
            if (released)
            {
                _logger?.LogDebug("Released distributed lock '{Key}' with token '{Token}'.", redisKey, lockToken);
            }
            return released;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to release distributed lock '{Key}' in Redis. Falling back to memory release.", trimmedKey);
            return await _memoryFallback.ReleaseLockAsync(trimmedKey, lockToken, ct);
        }
    }

    public async Task<bool> IsLockedAsync(string resourceKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(resourceKey))
            return false;

        string trimmedKey = resourceKey.Trim();

        if (_redis == null || !_redis.IsConnected)
        {
            return await _memoryFallback.IsLockedAsync(trimmedKey, ct);
        }

        try
        {
            var db = _redis.GetDatabase();
            string redisKey = $"sgrowing:lock:{trimmedKey}";
            return await db.KeyExistsAsync(redisKey);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to check lock '{Key}' in Redis. Falling back to memory check.", trimmedKey);
            return await _memoryFallback.IsLockedAsync(trimmedKey, ct);
        }
    }
}
