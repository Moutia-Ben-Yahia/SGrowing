using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Messaging;

public static class EventFanoutEndpoints
{
    private static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private static Guid ResolveTenantId(Guid? tenantId, Guid? headerTenantId)
    {
        if (tenantId.HasValue && tenantId.Value != Guid.Empty) return tenantId.Value;
        if (headerTenantId.HasValue && headerTenantId.Value != Guid.Empty) return headerTenantId.Value;
        return DefaultTenantId;
    }

    public static IEndpointRouteBuilder MapEventFanoutEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/messaging/events")
            .WithTags("Multi-Broker Real-Time Event Fanout");

        group.MapPost("/fanout", async (
            [FromBody] DispatchEventFanoutRequest req,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IMultiBrokerEventFanoutService fanoutService,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var result = await fanoutService.FanoutEventAsync(
                effectiveTenantId,
                req.EventType,
                req.PayloadJson,
                req.Options,
                null,
                ct
            );

            return Results.Ok(result);
        })
        .WithName("DispatchMultiBrokerEventFanout")
        .WithSummary("Fan out an event concurrently across HTTP Webhooks, SignalR, Redis Streams, and AMQP queues with fault isolation");

        return app;
    }
}
