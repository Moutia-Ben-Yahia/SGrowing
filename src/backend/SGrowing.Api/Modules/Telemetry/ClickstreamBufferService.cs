using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;
using SGrowing.Core.Messaging;
using StackExchange.Redis;

namespace SGrowing.Api.Modules.Telemetry;

public interface IClickstreamBufferService
{
    Task<bool> BufferEventAsync(TelemetryEvent telemetryEvent, CancellationToken cancellationToken = default);
    Task<int> FlushBufferBatchAsync(int batchSize = 100, CancellationToken cancellationToken = default);
    int GetPendingBufferCount();
}

public class ClickstreamBufferService : IClickstreamBufferService
{
    private readonly SGrowingDbContext _db;
    private readonly IConnectionMultiplexer? _redis;
    private readonly ILogger<ClickstreamBufferService>? _logger;
    private readonly ConcurrentQueue<TelemetryEvent> _fallbackQueue = new();
    private const string RedisStreamKey = "sgrowing:stream:clickstream";
    private const string RedisListKey = "sgrowing:buffer:clickstream";

    public ClickstreamBufferService(
        SGrowingDbContext db,
        IConnectionMultiplexer? redis = null,
        ILogger<ClickstreamBufferService>? logger = null)
    {
        _db = db;
        _redis = redis;
        _logger = logger;
    }

    public async Task<bool> BufferEventAsync(TelemetryEvent telemetryEvent, CancellationToken cancellationToken = default)
    {
        if (telemetryEvent == null) return false;

        if (_redis != null && _redis.IsConnected)
        {
            try
            {
                var redisDb = _redis.GetDatabase();
                var json = JsonSerializer.Serialize(telemetryEvent);

                // Buffer to Redis list
                await redisDb.ListRightPushAsync(RedisListKey, json);
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Redis buffer push failed. Falling back to local memory buffer.");
            }
        }

        // In-memory concurrent queue fallback
        _fallbackQueue.Enqueue(telemetryEvent);
        return true;
    }

    public async Task<int> FlushBufferBatchAsync(int batchSize = 100, CancellationToken cancellationToken = default)
    {
        batchSize = Math.Max(1, batchSize);
        var batch = new List<TelemetryEvent>();

        // 1. Drain from Redis if connected
        if (_redis != null && _redis.IsConnected)
        {
            try
            {
                var redisDb = _redis.GetDatabase();
                for (int i = 0; i < batchSize; i++)
                {
                    var val = await redisDb.ListLeftPopAsync(RedisListKey);
                    if (val.IsNullOrEmpty) break;

                    var ev = JsonSerializer.Deserialize<TelemetryEvent>(val.ToString());
                    if (ev != null)
                    {
                        batch.Add(ev);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error reading clickstream batch from Redis buffer.");
            }
        }

        // 2. Drain from memory fallback queue
        while (batch.Count < batchSize && _fallbackQueue.TryDequeue(out var ev))
        {
            batch.Add(ev);
        }

        if (batch.Count == 0) return 0;

        // 3. Batch bulk insert into database
        await _db.TelemetryEvents.AddRangeAsync(batch, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        _logger?.LogInformation("Flushed clickstream batch of {Count} telemetry events to database.", batch.Count);
        return batch.Count;
    }

    public int GetPendingBufferCount()
    {
        int redisCount = 0;
        if (_redis != null && _redis.IsConnected)
        {
            try
            {
                redisCount = (int)_redis.GetDatabase().ListLength(RedisListKey);
            }
            catch { }
        }

        return redisCount + _fallbackQueue.Count;
    }
}
