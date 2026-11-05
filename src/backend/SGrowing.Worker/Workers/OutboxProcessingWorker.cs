using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SGrowing.Api.Common.Outbox;
using SGrowing.Core.Entities;

namespace SGrowing.Worker.Workers;

public interface IOutboxMessageDispatcher
{
    Task DispatchAsync(OutboxMessageEntity message, CancellationToken cancellationToken = default);
}

public class OutboxProcessingWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxProcessingWorker> _logger;
    private readonly TimeSpan _pollingInterval;
    private readonly Func<OutboxMessageEntity, CancellationToken, Task>? _dispatchHandler;

    public OutboxProcessingWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxProcessingWorker> logger)
        : this(scopeFactory, logger, TimeSpan.FromSeconds(5), null)
    {
    }

    public OutboxProcessingWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxProcessingWorker> logger,
        TimeSpan pollingInterval,
        Func<OutboxMessageEntity, CancellationToken, Task>? dispatchHandler = null)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _pollingInterval = pollingInterval;
        _dispatchHandler = dispatchHandler;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutboxProcessingWorker started. Polling every {Interval}s.", _pollingInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in OutboxProcessingWorker loop.");
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

        _logger.LogInformation("OutboxProcessingWorker stopped gracefully.");
    }

    public async Task<int> ProcessBatchAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var outboxService = scope.ServiceProvider.GetService<IOutboxService>();
        if (outboxService == null)
        {
            _logger.LogWarning("IOutboxService is not registered in the service provider.");
            return 0;
        }

        var pendingMessages = await outboxService.GetPendingMessagesAsync(batchSize: 50, cancellationToken);
        if (pendingMessages == null || pendingMessages.Count == 0)
        {
            return 0;
        }

        _logger.LogInformation("OutboxProcessingWorker retrieved batch of {Count} messages.", pendingMessages.Count);

        var dispatcher = _dispatchHandler != null 
            ? null 
            : scope.ServiceProvider.GetService<IOutboxMessageDispatcher>();
        var broker = (_dispatchHandler == null && dispatcher == null)
            ? scope.ServiceProvider.GetService<SGrowing.Core.Messaging.IRedisStreamBroker>()
            : null;

        int processed = 0;
        foreach (var message in pendingMessages)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                if (_dispatchHandler != null)
                {
                    await _dispatchHandler(message, cancellationToken);
                }
                else if (dispatcher != null)
                {
                    await dispatcher.DispatchAsync(message, cancellationToken);
                }
                else if (broker != null)
                {
                    var destination = message.DestinationTopic ?? "sgrowing:stream:outbox";
                    await broker.PublishEventAsync(
                        destination,
                        message.EventType,
                        message.PayloadJson,
                        message.TenantId);
                }
                else
                {
                    _logger.LogInformation(
                        "Dispatched outbox event {EventType} ({MessageId}) for Tenant {TenantId}.",
                        message.EventType,
                        message.Id,
                        message.TenantId);
                }

                await outboxService.MarkAsPublishedAsync(message.Id, cancellationToken);
                processed++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to dispatch outbox message {MessageId} ({EventType}).", message.Id, message.EventType);
                await outboxService.MarkAsFailedAsync(message.Id, ex.Message, cancellationToken);
            }
        }

        return processed;
    }
}
