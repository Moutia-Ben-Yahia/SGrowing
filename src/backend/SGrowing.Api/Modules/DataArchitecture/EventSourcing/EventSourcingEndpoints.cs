using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.DataArchitecture.EventSourcing;

public static class EventSourcingEndpoints
{
    /// <summary>
    /// Registers the Event Store, CQRS Projection Engine, and CDC Outbox Streamer services into DI.
    /// </summary>
    public static IServiceCollection AddEventSourcingCqrsServices(this IServiceCollection services)
    {
        services.AddSingleton<ICqrsProjectionEngine, CqrsProjectionEngine>();
        services.AddSingleton<IOutboxCdcStreamingService, OutboxCdcStreamingService>();
        services.AddSingleton<IEventStoreEngine, EventStoreEngine>();

        return services;
    }

    /// <summary>
    /// Maps the Event Sourcing, CQRS Read Projections, and CDC Outbox endpoints.
    /// </summary>
    public static IEndpointRouteBuilder MapEventSourcingCqrsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/architecture/event-sourcing")
            .WithTags("High-Throughput Event Sourcing, CQRS & CDC Outbox");

        // 1. GET /aggregates/{id}/stream
        group.MapGet("/aggregates/{id}/stream", async (
            string id,
            [FromQuery] long fromVersion,
            [FromServices] IEventStoreEngine eventStore,
            HttpContext httpContext) =>
        {
            var stream = await eventStore.GetEventStreamAsync(id, fromVersion, httpContext.RequestAborted);
            if (stream.Count == 0 && fromVersion == 0)
            {
                return Results.NotFound(new { error = "AGGREGATE_NOT_FOUND", message = $"Aggregate '{id}' was not found in event journal." });
            }

            return Results.Ok(new
            {
                aggregate_id = id,
                from_version = fromVersion,
                event_count = stream.Count,
                events = stream
            });
        });

        // 2. GET /aggregates/{id}/timetravel
        group.MapGet("/aggregates/{id}/timetravel", async (
            string id,
            [FromQuery] DateTimeOffset at,
            [FromQuery] string? type,
            [FromServices] IEventStoreEngine eventStore,
            HttpContext httpContext) =>
        {
            var aggregateType = type?.ToLowerInvariant() switch
            {
                "wallet" => "wallet",
                "order" => "order",
                _ => id.StartsWith("wal", StringComparison.OrdinalIgnoreCase) ? "wallet" : "order"
            };

            if (aggregateType == "wallet")
            {
                var wallet = await eventStore.ReconstructAggregateAtTimeAsync<WalletAggregate>(id, at, httpContext.RequestAborted);
                if (wallet == null)
                {
                    return Results.NotFound(new { error = "HISTORICAL_STATE_NOT_FOUND", message = $"No events found for wallet '{id}' on or before {at:O}." });
                }

                return Results.Ok(new
                {
                    aggregate_id = wallet.AggregateId,
                    aggregate_type = "Wallet",
                    point_in_time = at,
                    version_at_time = wallet.Version,
                    state = new
                    {
                        owner_id = wallet.OwnerId,
                        currency = wallet.Currency,
                        available_balance = wallet.AvailableBalance,
                        escrow_locked_balance = wallet.EscrowLockedBalance
                    }
                });
            }
            else
            {
                var order = await eventStore.ReconstructAggregateAtTimeAsync<OrderAggregate>(id, at, httpContext.RequestAborted);
                if (order == null)
                {
                    return Results.NotFound(new { error = "HISTORICAL_STATE_NOT_FOUND", message = $"No events found for order '{id}' on or before {at:O}." });
                }

                return Results.Ok(new
                {
                    aggregate_id = order.AggregateId,
                    aggregate_type = "Order",
                    point_in_time = at,
                    version_at_time = order.Version,
                    state = new
                    {
                        customer_id = order.CustomerId,
                        status = order.Status.ToString(),
                        total_amount = order.TotalAmount,
                        items = order.Items
                    }
                });
            }
        });

        // 3. GET /projections/order/{orderId}
        group.MapGet("/projections/order/{orderId}", async (
            string orderId,
            [FromServices] ICqrsProjectionEngine projectionEngine,
            HttpContext httpContext) =>
        {
            var model = await projectionEngine.GetOrderSummaryAsync(orderId, httpContext.RequestAborted);
            if (model == null)
            {
                return Results.NotFound(new { error = "ORDER_PROJECTION_NOT_FOUND", message = $"Order '{orderId}' was not found in read projections." });
            }

            return Results.Ok(model);
        });

        // 4. GET /projections/wallet/{walletId}
        group.MapGet("/projections/wallet/{walletId}", async (
            string walletId,
            [FromServices] ICqrsProjectionEngine projectionEngine,
            HttpContext httpContext) =>
        {
            var model = await projectionEngine.GetWalletBalanceAsync(walletId, httpContext.RequestAborted);
            if (model == null)
            {
                return Results.NotFound(new { error = "WALLET_PROJECTION_NOT_FOUND", message = $"Wallet '{walletId}' was not found in read projections." });
            }

            return Results.Ok(model);
        });

        // 5. GET /cdc/metrics
        group.MapGet("/cdc/metrics", async (
            [FromServices] IOutboxCdcStreamingService cdcService,
            HttpContext httpContext) =>
        {
            var metrics = await cdcService.GetMetricsAsync(httpContext.RequestAborted);
            return Results.Ok(metrics);
        });

        // 6. GET /health
        group.MapGet("/health", async (
            [FromServices] IEventStoreEngine eventStore,
            [FromServices] ICqrsProjectionEngine projectionEngine,
            [FromServices] IOutboxCdcStreamingService cdcService,
            HttpContext httpContext) =>
        {
            var cdcMetrics = await cdcService.GetMetricsAsync(httpContext.RequestAborted);

            return Results.Ok(new
            {
                status = "Healthy",
                engine = "Antigravity High-Throughput Event Sourcing & CQRS",
                active_streams = eventStore.ActiveStreamsCount,
                total_events = eventStore.TotalEventsCount,
                total_snapshots = eventStore.TotalSnapshotsCount,
                read_model_orders = projectionEngine.TrackedOrdersCount,
                read_model_wallets = projectionEngine.TrackedWalletsCount,
                cdc_produced = cdcMetrics.TotalMessagesProduced,
                cdc_committed = cdcMetrics.TotalMessagesCommitted,
                cdc_dlq_count = cdcMetrics.TotalMessagesInDlq,
                checked_at = DateTimeOffset.UtcNow
            });
        });

        return app;
    }
}
