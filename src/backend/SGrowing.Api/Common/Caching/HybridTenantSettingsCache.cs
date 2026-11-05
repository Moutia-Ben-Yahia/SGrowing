using System;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace SGrowing.Api.Common.Caching;

public class HybridTenantSettingsCache : ITenantSettingsCache
{
    private static readonly TimeSpan DefaultExpiration = TimeSpan.FromMinutes(15);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IMemoryCache _memoryCache;
    private readonly IDistributedCache? _distributedCache;
    private readonly IConnectionMultiplexer? _redis;
    private readonly ILogger<HybridTenantSettingsCache>? _logger;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _keyLocks = new();

    public HybridTenantSettingsCache(
        IMemoryCache? memoryCache = null,
        IDistributedCache? distributedCache = null,
        IConnectionMultiplexer? redis = null,
        ILogger<HybridTenantSettingsCache>? logger = null)
    {
        _memoryCache = memoryCache ?? new MemoryCache(new MemoryCacheOptions());
        _distributedCache = distributedCache;
        _redis = redis;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Cache key cannot be null or whitespace.", nameof(key));

        // 1. Check L1 Memory Cache
        if (_memoryCache.TryGetValue(key, out var rawL1))
        {
            if (rawL1 is T typedL1)
            {
                return typedL1;
            }

            if (rawL1 is JsonElement jsonElement)
            {
                return jsonElement.Deserialize<T>(JsonOptions);
            }
        }

        // 2. Check L2 Distributed Cache (IDistributedCache or Redis)
        try
        {
            if (_distributedCache != null)
            {
                var bytes = await _distributedCache.GetAsync(key, cancellationToken);
                if (bytes != null && bytes.Length > 0)
                {
                    var val = JsonSerializer.Deserialize<T>(bytes, JsonOptions);
                    if (val != null)
                    {
                        _memoryCache.Set(key, val, DefaultExpiration);
                    }
                    return val;
                }
            }
            else if (_redis != null && _redis.IsConnected)
            {
                var db = _redis.GetDatabase();
                var redisVal = await db.StringGetAsync(key);
                if (redisVal.HasValue)
                {
                    var val = JsonSerializer.Deserialize<T>((string)redisVal!, JsonOptions);
                    if (val != null)
                    {
                        _memoryCache.Set(key, val, DefaultExpiration);
                    }
                    return val;
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to read from L2 distributed cache for key '{Key}'. Falling back to miss.", key);
        }

        return default;
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Cache key cannot be null or whitespace.", nameof(key));

        var effectiveExpiry = expiration ?? DefaultExpiration;

        // 1. Set L1 Memory Cache
        _memoryCache.Set(key, value, effectiveExpiry);

        // 2. Set L2 Distributed Cache
        try
        {
            if (_distributedCache != null)
            {
                var bytes = JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions);
                await _distributedCache.SetAsync(
                    key,
                    bytes,
                    new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = effectiveExpiry },
                    cancellationToken);
            }
            else if (_redis != null && _redis.IsConnected)
            {
                var db = _redis.GetDatabase();
                var json = JsonSerializer.Serialize(value, JsonOptions);
                await db.StringSetAsync(key, json, effectiveExpiry);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to write to L2 distributed cache for key '{Key}'.", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Cache key cannot be null or whitespace.", nameof(key));

        // 1. Evict from L1 Memory Cache
        _memoryCache.Remove(key);

        // 2. Evict from L2 Distributed Cache
        try
        {
            if (_distributedCache != null)
            {
                await _distributedCache.RemoveAsync(key, cancellationToken);
            }
            else if (_redis != null && _redis.IsConnected)
            {
                var db = _redis.GetDatabase();
                await db.KeyDeleteAsync(key);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to remove from L2 distributed cache for key '{Key}'.", key);
        }
    }

    public async Task<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Cache key cannot be null or whitespace.", nameof(key));
        if (factory == null)
            throw new ArgumentNullException(nameof(factory));

        // Quick check without lock
        var cached = await GetAsync<T>(key, cancellationToken);
        if (cached != null)
        {
            return cached;
        }

        // Cache stampede protection using per-key lock
        var keyLock = _keyLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await keyLock.WaitAsync(cancellationToken);
        try
        {
            // Double check inside lock
            cached = await GetAsync<T>(key, cancellationToken);
            if (cached != null)
            {
                return cached;
            }

            var created = await factory(cancellationToken);
            if (created != null)
            {
                await SetAsync(key, created, expiration, cancellationToken);
            }
            return created;
        }
        finally
        {
            keyLock.Release();
        }
    }
}
