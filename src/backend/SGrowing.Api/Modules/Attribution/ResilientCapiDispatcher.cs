using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SGrowing.Api.Common.Outbox;

namespace SGrowing.Api.Modules.Attribution;

public record CapiDispatchResult(
    bool DispatchedImmediately,
    bool EnqueuedToOutbox,
    string EventId,
    string? OutboxMessageId = null,
    string? Note = null
);

public interface IResilientCapiDispatcher
{
    Task<CapiDispatchResult> EnqueueOrDispatchAsync(CapiDispatchEventRequest request, CancellationToken ct = default);
}

public class ResilientCapiDispatcher : IResilientCapiDispatcher
{
    private readonly IServerSideCapiFanoutService _fanoutService;
    private readonly IOutboxService _outboxService;
    private readonly ILogger<ResilientCapiDispatcher>? _logger;

    public ResilientCapiDispatcher(
        IServerSideCapiFanoutService fanoutService,
        IOutboxService outboxService,
        ILogger<ResilientCapiDispatcher>? logger = null)
    {
        _fanoutService = fanoutService;
        _outboxService = outboxService;
        _logger = logger;
    }

    public async Task<CapiDispatchResult> EnqueueOrDispatchAsync(CapiDispatchEventRequest request, CancellationToken ct = default)
    {
        var eventId = request.EventId ?? Guid.NewGuid().ToString("N");
        var normalizedRequest = request with { EventId = eventId };

        // Check if any target platform circuit breaker is open
        var platforms = request.TargetPlatforms ?? new List<CapiPlatform> { CapiPlatform.Meta, CapiPlatform.Google };
        bool anyCircuitOpen = false;
        foreach (var platform in platforms)
        {
            if (_fanoutService.GetCircuitBreakerState(platform) == CircuitState.Open)
            {
                anyCircuitOpen = true;
                break;
            }
        }

        if (anyCircuitOpen)
        {
            _logger?.LogWarning("Target CAPI circuit is OPEN for event {EventId}. Enqueuing to Transactional Outbox directly.", eventId);
            var outboxMsg = await _outboxService.EnqueueAsync(
                request.TenantId,
                "Advertising.CapiConversion.Queued",
                normalizedRequest,
                correlationId: eventId,
                cancellationToken: ct);

            return new CapiDispatchResult(
                DispatchedImmediately: false,
                EnqueuedToOutbox: true,
                EventId: eventId,
                OutboxMessageId: outboxMsg.Id.ToString(),
                Note: "Circuit open - enqueued to transactional outbox for background delivery.");
        }

        try
        {
            var fanoutResult = await _fanoutService.DispatchAsync(normalizedRequest, ct);

            if (fanoutResult.AllSucceeded)
            {
                return new CapiDispatchResult(
                    DispatchedImmediately: true,
                    EnqueuedToOutbox: false,
                    EventId: eventId,
                    Note: "All platforms dispatched successfully.");
            }

            // Some or all failed - enqueue to outbox for dead-letter/retry processing
            _logger?.LogWarning("Partial or total CAPI dispatch failure for event {EventId}. Persisting to Outbox DLQ.", eventId);
            var outboxMsg = await _outboxService.EnqueueAsync(
                request.TenantId,
                "Advertising.CapiConversion.FailedRetry",
                normalizedRequest,
                correlationId: eventId,
                cancellationToken: ct);

            return new CapiDispatchResult(
                DispatchedImmediately: false,
                EnqueuedToOutbox: true,
                EventId: eventId,
                OutboxMessageId: outboxMsg.Id.ToString(),
                Note: "Dispatch partial failure - enqueued to outbox DLQ for scheduled backoff retry.");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected exception during CAPI dispatch for event {EventId}. Rescuing into Outbox.", eventId);
            var outboxMsg = await _outboxService.EnqueueAsync(
                request.TenantId,
                "Advertising.CapiConversion.ExceptionRescued",
                normalizedRequest,
                correlationId: eventId,
                cancellationToken: ct);

            return new CapiDispatchResult(
                DispatchedImmediately: false,
                EnqueuedToOutbox: true,
                EventId: eventId,
                OutboxMessageId: outboxMsg.Id.ToString(),
                Note: $"Rescued from exception: {ex.Message}");
        }
    }
}
