using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SGrowing.Core.Messaging;

namespace SGrowing.Worker.Services;

public class RedisStreamConsumerDaemon : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RedisStreamConsumerDaemon> _logger;
    private readonly string _streamKey;
    private readonly string _groupName;
    private readonly string _consumerName;
    private readonly TimeSpan _pollInterval;

    public RedisStreamConsumerDaemon(
        IServiceProvider serviceProvider,
        ILogger<RedisStreamConsumerDaemon> logger,
        string streamKey = "sgrowing:stream:outbox",
        string groupName = "sgrowing-worker-group",
        string? consumerName = null,
        TimeSpan? pollInterval = null)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _streamKey = streamKey;
        _groupName = groupName;
        _consumerName = consumerName ?? $"worker-{Guid.NewGuid().ToString()[..6]}";
        _pollInterval = pollInterval ?? TimeSpan.FromSeconds(2);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RedisStreamConsumerDaemon starting. Consumer '{Consumer}' on stream '{Stream}', group '{Group}'.",
            _consumerName, _streamKey, _groupName);

        using var scope = _serviceProvider.CreateScope();
        var broker = scope.ServiceProvider.GetService<IRedisStreamBroker>();
        if (broker == null)
        {
            _logger.LogWarning("No IRedisStreamBroker registered. Daemon will exit.");
            return;
        }

        await broker.EnsureConsumerGroupAsync(_streamKey, _groupName);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessStreamBatchAsync(broker, stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Error occurred while reading messages from Redis stream {StreamKey}", _streamKey);
            }

            try
            {
                await Task.Delay(_pollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("RedisStreamConsumerDaemon stopping.");
    }

    public async Task<int> ProcessStreamBatchAsync(IRedisStreamBroker broker, CancellationToken cancellationToken = default)
    {
        var messages = await broker.ReadGroupMessagesAsync(_streamKey, _groupName, _consumerName, count: 20);
        if (messages.Count == 0) return 0;

        _logger.LogInformation("Consumer '{Consumer}' read {Count} messages from Redis stream '{Stream}'.",
            _consumerName, messages.Count, _streamKey);

        foreach (var msg in messages)
        {
            try
            {
                // Process event based on EventType
                _logger.LogInformation("Executing event handler for {EventType} (StreamId: {MsgId}, Tenant: {Tenant})",
                    msg.EventType, msg.MessageId, msg.TenantId);

                // Acknowledge processed message in Redis
                await broker.AcknowledgeAsync(_streamKey, _groupName, msg.MessageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process stream message {MessageId} of type {EventType}",
                    msg.MessageId, msg.EventType);
            }
        }

        return messages.Count;
    }
}
