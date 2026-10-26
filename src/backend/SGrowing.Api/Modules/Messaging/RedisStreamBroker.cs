using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using SGrowing.Core.Messaging;
using StackExchange.Redis;

namespace SGrowing.Api.Modules.Messaging;

public class RedisStreamBroker : IRedisStreamBroker
{
    private readonly IConnectionMultiplexer? _redis;
    private readonly ILogger<RedisStreamBroker> _logger;

    public RedisStreamBroker(ILogger<RedisStreamBroker> logger, IConnectionMultiplexer? redis = null)
    {
        _logger = logger;
        _redis = redis;
    }

    public bool IsConnected => _redis != null && _redis.IsConnected;

    public async Task<string> PublishEventAsync(string streamKey, string eventType, string payloadJson, Guid tenantId)
    {
        if (_redis == null || !_redis.IsConnected)
        {
            _logger.LogWarning("Redis is not connected. Emulating stream publish for {EventType} on {StreamKey}", eventType, streamKey);
            return $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}-0";
        }

        var db = _redis.GetDatabase();
        var entries = new NameValueEntry[]
        {
            new("eventType", eventType),
            new("tenantId", tenantId.ToString()),
            new("payload", payloadJson),
            new("publishedAtUtc", DateTime.UtcNow.ToString("O"))
        };

        var messageId = await db.StreamAddAsync(streamKey, entries);
        _logger.LogInformation("Published event {EventType} to Redis Stream {StreamKey} with ID {MessageId}", eventType, streamKey, messageId);
        return messageId.ToString();
    }

    public async Task EnsureConsumerGroupAsync(string streamKey, string groupName)
    {
        if (_redis == null || !_redis.IsConnected) return;

        try
        {
            var db = _redis.GetDatabase();
            await db.StreamCreateConsumerGroupAsync(streamKey, groupName, StreamPosition.Beginning);
            _logger.LogInformation("Created consumer group {GroupName} on stream {StreamKey}", groupName, streamKey);
        }
        catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP"))
        {
            // Consumer group already exists - perfectly normal
            _logger.LogDebug("Consumer group {GroupName} already exists on {StreamKey}", groupName, streamKey);
        }
    }

    public async Task<List<StreamMessageEnvelope>> ReadGroupMessagesAsync(
        string streamKey,
        string groupName,
        string consumerName,
        int count = 10)
    {
        if (_redis == null || !_redis.IsConnected) return new List<StreamMessageEnvelope>();

        var db = _redis.GetDatabase();
        var streamEntries = await db.StreamReadGroupAsync(
            streamKey,
            groupName,
            consumerName,
            StreamPosition.NewMessages,
            count);

        var list = new List<StreamMessageEnvelope>();
        foreach (var entry in streamEntries)
        {
            var dict = entry.Values.ToDictionary(x => x.Name.ToString(), x => x.Value.ToString());
            dict.TryGetValue("eventType", out var eventType);
            dict.TryGetValue("tenantId", out var tenantStr);
            dict.TryGetValue("payload", out var payload);
            dict.TryGetValue("publishedAtUtc", out var publishedStr);

            Guid.TryParse(tenantStr, out var tenantId);
            DateTime.TryParse(publishedStr, out var publishedAt);

            list.Add(new StreamMessageEnvelope(
                entry.Id.ToString(),
                streamKey,
                eventType ?? "Unknown",
                tenantId,
                payload ?? "{}",
                publishedAt == default ? DateTime.UtcNow : publishedAt
            ));
        }

        return list;
    }

    public async Task AcknowledgeAsync(string streamKey, string groupName, string messageId)
    {
        if (_redis == null || !_redis.IsConnected) return;

        var db = _redis.GetDatabase();
        await db.StreamAcknowledgeAsync(streamKey, groupName, messageId);
        _logger.LogDebug("Acknowledged message {MessageId} in group {GroupName} on stream {StreamKey}", messageId, groupName, streamKey);
    }
}

public class InMemoryStreamBroker : IRedisStreamBroker
{
    private readonly ConcurrentDictionary<string, List<StreamMessageEnvelope>> _streams = new();
    private readonly ConcurrentDictionary<string, HashSet<string>> _acknowledged = new();

    public bool IsConnected => true;

    public Task<string> PublishEventAsync(string streamKey, string eventType, string payloadJson, Guid tenantId)
    {
        var messageId = $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}-{Guid.NewGuid().ToString()[..4]}";
        var envelope = new StreamMessageEnvelope(
            messageId,
            streamKey,
            eventType,
            tenantId,
            payloadJson,
            DateTime.UtcNow
        );

        _streams.AddOrUpdate(streamKey,
            _ => new List<StreamMessageEnvelope> { envelope },
            (_, list) =>
            {
                lock (list) { list.Add(envelope); }
                return list;
            });

        return Task.FromResult(messageId);
    }

    public Task EnsureConsumerGroupAsync(string streamKey, string groupName)
    {
        _streams.GetOrAdd(streamKey, _ => new List<StreamMessageEnvelope>());
        return Task.CompletedTask;
    }

    public Task<List<StreamMessageEnvelope>> ReadGroupMessagesAsync(
        string streamKey,
        string groupName,
        string consumerName,
        int count = 10)
    {
        if (!_streams.TryGetValue(streamKey, out var list))
            return Task.FromResult(new List<StreamMessageEnvelope>());

        var ackSet = _acknowledged.GetOrAdd($"{streamKey}:{groupName}", _ => new HashSet<string>());

        lock (list)
        {
            var unacked = list
                .Where(m => !ackSet.Contains(m.MessageId))
                .Take(count)
                .ToList();
            return Task.FromResult(unacked);
        }
    }

    public Task AcknowledgeAsync(string streamKey, string groupName, string messageId)
    {
        var ackSet = _acknowledged.GetOrAdd($"{streamKey}:{groupName}", _ => new HashSet<string>());
        lock (ackSet)
        {
            ackSet.Add(messageId);
        }
        return Task.CompletedTask;
    }
}
