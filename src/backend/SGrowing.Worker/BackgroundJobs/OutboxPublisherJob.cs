using SGrowing.Api.Data;
using SGrowing.Core.Entities;
using SGrowing.Core.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SGrowing.Worker.BackgroundJobs;

public class OutboxPublisherJob : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxPublisherJob> _logger;
    private readonly TimeSpan _pollingInterval;
    public const int MaxRetries = 5;

    public OutboxPublisherJob(
        IServiceProvider serviceProvider,
        ILogger<OutboxPublisherJob> logger,
        TimeSpan? pollingInterval = null)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _pollingInterval = pollingInterval ?? TimeSpan.FromSeconds(5);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutboxPublisherJob initialized. Polling every {Interval}s with exponential backoff and DLQ support.",
            _pollingInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingOutboxMessagesAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Error processing outbox publisher batch.");
            }

            try
            {
                await Task.Delay(_pollingInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("OutboxPublisherJob stopped.");
    }

    public async Task<int> ProcessPendingOutboxMessagesAsync(CancellationToken ct = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SGrowingDbContext>();
        var broker = scope.ServiceProvider.GetService<IRedisStreamBroker>();

        var pending = await db.OutboxMessages
            .Where(m => m.ProcessedOnUtc == null)
            .OrderBy(m => m.OccurredOnUtc)
            .Take(100)
            .ToListAsync(ct);

        if (pending.Count == 0) return 0;

        var processedCount = 0;
        var now = DateTime.UtcNow;

        foreach (var message in pending)
        {
            // If retry count exceeded max, move to Dead Letter Queue (DLQ) state
            if (message.RetryCount >= MaxRetries)
            {
                _logger.LogWarning("Outbox message {MessageId} (EventType: {EventType}) exceeded max retries ({MaxRetries}). Dead-lettered.",
                    message.Id, message.EventType, MaxRetries);
                continue;
            }

            // Exponential backoff check: 2^retryCount seconds delay
            if (message.RetryCount > 0)
            {
                var delaySeconds = Math.Min(300, Math.Pow(2, message.RetryCount));
                if (now < message.OccurredOnUtc.AddSeconds(delaySeconds))
                {
                    // Backoff duration not yet elapsed, skip in this cycle
                    continue;
                }
            }

            try
            {
                if (broker != null)
                {
                    await broker.PublishEventAsync(
                        streamKey: "sgrowing:stream:outbox",
                        eventType: message.EventType,
                        payloadJson: message.Payload,
                        tenantId: message.TenantId);
                }

                message.MarkProcessed();
                processedCount++;

                _logger.LogInformation("OutboxPublisherJob: successfully published event {EventType} ({MessageId}).",
                    message.EventType, message.Id);
            }
            catch (Exception ex)
            {
                message.MarkFailed(ex.Message);
                _logger.LogError(ex, "OutboxPublisherJob: failed publishing message {MessageId}. RetryCount={RetryCount}.",
                    message.Id, message.RetryCount);

                if (message.RetryCount >= MaxRetries)
                {
                    _logger.LogCritical("OutboxPublisherJob DLQ Alert: Message {MessageId} reached {MaxRetries} retries and is now dead-lettered.",
                        message.Id, MaxRetries);
                }
            }
        }

        await db.SaveChangesAsync(ct);
        return processedCount;
    }
}
