using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Commerce.CartReservations;

public record CommitCartReservationRequest(Guid OrderId);

public static class CartReservationEndpoints
{
    private static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private static Guid ResolveTenantId(Guid? tenantId, Guid? headerTenantId)
    {
        if (tenantId.HasValue && tenantId.Value != Guid.Empty) return tenantId.Value;
        if (headerTenantId.HasValue && headerTenantId.Value != Guid.Empty) return headerTenantId.Value;
        return DefaultTenantId;
    }

    public static IEndpointRouteBuilder MapCartReservationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/commerce/cart-reservations")
            .WithTags("Cart Reservation Locks & High-Velocity WMS");

        group.MapPost("/acquire", async (
            [FromBody] AcquireCartReservationRequest req,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            ICartReservationService reservationService,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var result = await reservationService.AcquireReservationAsync(effectiveTenantId, req, ct);

            if (!result.Success)
            {
                return Results.Problem(
                    title: "Insufficient Warehouse Inventory for Reservation",
                    detail: result.Message,
                    statusCode: StatusCodes.Status409Conflict,
                    instance: "/api/v1/commerce/cart-reservations/acquire",
                    type: "https://sgrowing.io/errors/insufficient-stock"
                );
            }

            return Results.Ok(result);
        })
        .WithName("AcquireCartReservation")
        .WithSummary("Acquire a time-bound stock reservation lock on a warehouse SKU for an in-flight cart");

        group.MapPost("/{cartId:guid}/release", async (
            Guid cartId,
            [FromBody] ReleaseCartReservationRequest? req,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            ICartReservationService reservationService,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var success = await reservationService.ReleaseReservationAsync(
                effectiveTenantId,
                req ?? new ReleaseCartReservationRequest(cartId),
                ct
            );

            return Results.Ok(new { cartId, released = success });
        })
        .WithName("ReleaseCartReservation")
        .WithSummary("Release active stock reservation locks for a specific cart back to physical available");

        group.MapPost("/{cartId:guid}/commit", async (
            Guid cartId,
            [FromBody] CommitCartReservationRequest req,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            ICartReservationService reservationService,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var success = await reservationService.CommitReservationAsync(effectiveTenantId, cartId, req.OrderId, ct);
            return Results.Ok(new { cartId, orderId = req.OrderId, committed = success });
        })
        .WithName("CommitCartReservation")
        .WithSummary("Commit active cart reservation to a confirmed order");

        group.MapPost("/sweep-expired", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            ICartReservationService reservationService,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var summary = await reservationService.SweepExpiredReservationsAsync(effectiveTenantId, ct);
            return Results.Ok(summary);
        })
        .WithName("SweepExpiredCartReservations")
        .WithSummary("Sweep expired cart reservation locks and restore stock to physical availability");

        group.MapGet("/active", async (
            [FromQuery] Guid? warehouseId,
            [FromQuery] string? sku,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            ICartReservationService reservationService,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var list = await reservationService.GetActiveReservationsAsync(effectiveTenantId, warehouseId, sku, ct);
            return Results.Ok(list);
        })
        .WithName("GetActiveCartReservations")
        .WithSummary("Query all active cart reservation locks with warehouse and SKU filters");

        return app;
    }
}
