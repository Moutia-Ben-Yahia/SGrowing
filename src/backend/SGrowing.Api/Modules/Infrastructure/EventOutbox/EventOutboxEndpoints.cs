using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SGrowing.Api.Common;

namespace SGrowing.Api.Modules.Infrastructure.EventOutbox;

/// <summary>
/// Modular Minimal API endpoint module exposing enterprise transactional outbox and event streaming mesh operations.
/// </summary>
public class EventOutboxEndpoints : IEndpointModule
{
    /// <summary>
    /// Initializes a new instance of <see cref="EventOutboxEndpoints"/>.
    /// </summary>
    public EventOutboxEndpoints()
    {
    }

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapEventOutboxEndpoints();
    }
}

/// <summary>
/// Extension methods for dependency injection registration and route mapping of Event Outbox services.
/// </summary>
public static class EventOutboxEndpointExtensions
{
    /// <summary>
    /// Registers the <see cref="IEventOutboxService"/> singleton within the DI container.
    /// </summary>
    public static IServiceCollection AddEventOutboxServices(this IServiceCollection services)
    {
        services.AddSingleton<IEventOutboxService, EventOutboxService>();
        return services;
    }

    /// <summary>
    /// Maps REST API endpoints for transactional outbox, idempotency ledger, batch dispatch, and dead-letter queue operations.
    /// </summary>
    public static IEndpointRouteBuilder MapEventOutboxEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/event-outbox")
            .WithTags("InfrastructureEventOutbox");

        // 1. Enqueue general outbox event
        group.MapPost("/enqueue", async (
            [FromBody] EnqueueOutboxEventRequest request,
            [FromServices] IEventOutboxService outboxService,
            CancellationToken ct) =>
        {
            var result = await outboxService.EnqueueAsync(request, ct);
            return Results.Ok(result);
        })
        .WithName("EnqueueOutboxEvent")
        .WithSummary("Stages an event into the transactional outbox with SHA-256 idempotency deduplication check")
        .Produces<EnqueueOutboxEventResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        // 2. Enqueue strongly-typed OrderPaid event
        group.MapPost("/enqueue/order-paid", async (
            [FromQuery] Guid tenantId,
            [FromBody] OrderPaidPayload payload,
            [FromQuery] string? correlationId,
            [FromServices] IEventOutboxService outboxService,
            CancellationToken ct) =>
        {
            var result = await outboxService.EnqueueOrderPaidAsync(tenantId, payload, correlationId, ct);
            return Results.Ok(result);
        })
        .WithName("EnqueueOrderPaidEvent")
        .WithSummary("Stages a typed OrderPaid domain event into the transactional outbox")
        .Produces<EnqueueOutboxEventResult>(StatusCodes.Status200OK);

        // 3. Enqueue strongly-typed DealWon event
        group.MapPost("/enqueue/deal-won", async (
            [FromQuery] Guid tenantId,
            [FromBody] DealWonPayload payload,
            [FromQuery] string? correlationId,
            [FromServices] IEventOutboxService outboxService,
            CancellationToken ct) =>
        {
            var result = await outboxService.EnqueueDealWonAsync(tenantId, payload, correlationId, ct);
            return Results.Ok(result);
        })
        .WithName("EnqueueDealWonEvent")
        .WithSummary("Stages a typed DealWon domain event into the transactional outbox")
        .Produces<EnqueueOutboxEventResult>(StatusCodes.Status200OK);

        // 4. Enqueue strongly-typed LeadQualified event
        group.MapPost("/enqueue/lead-qualified", async (
            [FromQuery] Guid tenantId,
            [FromBody] LeadQualifiedPayload payload,
            [FromQuery] string? correlationId,
            [FromServices] IEventOutboxService outboxService,
            CancellationToken ct) =>
        {
            var result = await outboxService.EnqueueLeadQualifiedAsync(tenantId, payload, correlationId, ct);
            return Results.Ok(result);
        })
        .WithName("EnqueueLeadQualifiedEvent")
        .WithSummary("Stages a typed LeadQualified domain event into the transactional outbox")
        .Produces<EnqueueOutboxEventResult>(StatusCodes.Status200OK);

        // 5. Enqueue strongly-typed TicketEscalated event
        group.MapPost("/enqueue/ticket-escalated", async (
            [FromQuery] Guid tenantId,
            [FromBody] TicketEscalatedPayload payload,
            [FromQuery] string? correlationId,
            [FromServices] IEventOutboxService outboxService,
            CancellationToken ct) =>
        {
            var result = await outboxService.EnqueueTicketEscalatedAsync(tenantId, payload, correlationId, ct);
            return Results.Ok(result);
        })
        .WithName("EnqueueTicketEscalatedEvent")
        .WithSummary("Stages a typed TicketEscalated domain event into the transactional outbox")
        .Produces<EnqueueOutboxEventResult>(StatusCodes.Status200OK);

        // 6. Batch dispatch endpoint
        group.MapPost("/dispatch-batch", async (
            [FromBody] DispatchBatchRequest? request,
            [FromServices] IEventOutboxService outboxService,
            CancellationToken ct) =>
        {
            var req = request ?? new DispatchBatchRequest();
            var result = await outboxService.ProcessPendingOutboxBatchAsync(req.BatchSize, req.TenantId, null, ct);
            return Results.Ok(result);
        })
        .WithName("DispatchOutboxBatch")
        .WithSummary("Polls pending and retry-ready outbox events and executes resilient dispatch with exponential backoff")
        .Produces<DispatchBatchResult>(StatusCodes.Status200OK);

        // 7. DLQ Inspection - Query messages
        group.MapGet("/dead-letter", async (
            [FromQuery] Guid? tenantId,
            [FromQuery] string? eventType,
            [FromQuery] string? searchTerm,
            [FromQuery] int page,
            [FromQuery] int pageSize,
            [FromServices] IEventOutboxService outboxService,
            CancellationToken ct) =>
        {
            var query = new DeadLetterQueryParameters(
                TenantId: tenantId,
                EventType: eventType,
                SearchTerm: searchTerm,
                Page: page <= 0 ? 1 : page,
                PageSize: pageSize <= 0 ? 50 : pageSize
            );
            var items = await outboxService.GetDeadLetterMessagesAsync(query, ct);
            return Results.Ok(items);
        })
        .WithName("GetDeadLetterMessages")
        .WithSummary("Queries quarantined messages residing in the Dead Letter Queue (DLQ)")
        .Produces<IReadOnlyList<DeadLetterEventDetail>>(StatusCodes.Status200OK);

        // 8. DLQ Inspection - Get single message
        group.MapGet("/dead-letter/{id:guid}", async (
            [FromRoute] Guid id,
            [FromServices] IEventOutboxService outboxService,
            CancellationToken ct) =>
        {
            var detail = await outboxService.GetDeadLetterMessageByIdAsync(id, ct);
            return detail is not null ? Results.Ok(detail) : Results.NotFound();
        })
        .WithName("GetDeadLetterMessageById")
        .WithSummary("Retrieves a quarantined Dead Letter Queue message by its ID for payload and diagnostic inspection")
        .Produces<DeadLetterEventDetail>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        // 9. DLQ Replay endpoint
        group.MapPost("/dead-letter/replay", async (
            [FromBody] ReplayDeadLetterRequest request,
            [FromServices] IEventOutboxService outboxService,
            CancellationToken ct) =>
        {
            var result = await outboxService.ReplayDeadLetterMessagesAsync(request, ct);
            return Results.Ok(result);
        })
        .WithName("ReplayDeadLetterMessages")
        .WithSummary("Replays selected quarantined messages by returning them to Pending state and resetting backoff timers")
        .Produces<ReplayDeadLetterResult>(StatusCodes.Status200OK);

        // 10. DLQ Purge endpoint
        group.MapDelete("/dead-letter/{id:guid}", async (
            [FromRoute] Guid id,
            [FromServices] IEventOutboxService outboxService,
            CancellationToken ct) =>
        {
            var purged = await outboxService.PurgeDeadLetterMessageAsync(id, ct);
            return purged ? Results.NoContent() : Results.NotFound();
        })
        .WithName("PurgeDeadLetterMessage")
        .WithSummary("Permanently purges a poison event from the Dead Letter Queue")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound);

        // 11. Mesh statistics
        group.MapGet("/statistics", async (
            [FromQuery] Guid? tenantId,
            [FromServices] IEventOutboxService outboxService,
            CancellationToken ct) =>
        {
            var stats = await outboxService.GetMeshStatisticsAsync(tenantId, ct);
            return Results.Ok(stats);
        })
        .WithName("GetOutboxMeshStatistics")
        .WithSummary("Retrieves live health, queue depth, deduplication metrics, and delivery success rate of the outbox mesh")
        .Produces<OutboxMeshStatistics>(StatusCodes.Status200OK);

        // 12. Idempotency deduplication check
        group.MapGet("/check-duplicate", async (
            [FromQuery] string hash,
            [FromServices] IEventOutboxService outboxService,
            CancellationToken ct) =>
        {
            var isDuplicate = await outboxService.IsDuplicateAsync(hash, ct);
            return Results.Ok(new { hash, isDuplicate });
        })
        .WithName("CheckIdempotencyHash")
        .WithSummary("Checks if a SHA-256 deduplication hash is already present in the active deduplication ledger")
        .Produces(StatusCodes.Status200OK);

        return app;
    }
}
