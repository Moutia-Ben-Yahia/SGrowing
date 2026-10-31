using SGrowing.Api.Data;
using SGrowing.Core.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SGrowing.Worker.Services;

public class OutboxProcessorDaemon : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxProcessorDaemon> _logger;
    private readonly IConcurrencyLockService? _lockService;
    private readonly TimeSpan _pollingInterval;

    public OutboxProcessorDaemon(
        IServiceProvider serviceProvider,
        ILogger<OutboxProcessorDaemon> logger,
        TimeSpan? pollingInterval = null,
        IConcurrencyLockService? lockService = null)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _lockService = lockService;
        _pollingInterval = pollingInterval ?? TimeSpan.FromSeconds(5);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutboxProcessorDaemon started. Polling every {Interval}s.", _pollingInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var lockService = _lockService ?? _serviceProvider.GetService<IConcurrencyLockService>();
                if (lockService != null)
                {
                    await using var lockHandle = await lockService.TryAcquireLockAsync(
                        "sweeper:OutboxProcessor",
                        TimeSpan.FromSeconds(10),
                        TimeSpan.FromMilliseconds(200),
                        stoppingToken);

                    if (lockHandle == null || !lockHandle.IsAcquired)
                    {
                        _logger.LogDebug("OutboxProcessorDaemon: Another worker holds the outbox processing lease. Skipping.");
                    }
                    else
                    {
                        await ProcessBatchAsync(stoppingToken);
                    }
                }
                else
                {
                    await ProcessBatchAsync(stoppingToken);
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Error occurred while processing outbox messages.");
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

        _logger.LogInformation("OutboxProcessorDaemon stopping.");
    }

    public async Task<int> ProcessBatchAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SGrowingDbContext>();

        var pendingMessages = await db.OutboxMessages
            .Where(m => m.ProcessedOnUtc == null)
            .OrderBy(m => m.OccurredOnUtc)
            .Take(50)
            .ToListAsync(cancellationToken);

        if (pendingMessages.Count == 0)
        {
            return 0;
        }

        _logger.LogInformation("Processing batch of {Count} outbox messages.", pendingMessages.Count);

        var broker = scope.ServiceProvider.GetService<SGrowing.Core.Messaging.IRedisStreamBroker>();

        foreach (var message in pendingMessages)
        {
            try
            {
                if (broker != null)
                {
                    await broker.PublishEventAsync(
                        "sgrowing:stream:outbox",
                        message.EventType,
                        message.Payload,
                        message.TenantId);
                }

                _logger.LogInformation("Dispatched event {EventType} ({MessageId}) via Durable Redis Stream.",
                    message.EventType, message.Id);

                message.MarkProcessed();
            }
            catch (Exception ex)
            {
                message.MarkFailed(ex.Message);
                _logger.LogError(ex, "Failed to process outbox message {MessageId}.", message.Id);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return pendingMessages.Count;
    }
}
