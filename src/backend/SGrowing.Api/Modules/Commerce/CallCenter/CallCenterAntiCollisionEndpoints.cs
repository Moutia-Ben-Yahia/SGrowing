using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace SGrowing.Api.Modules.Commerce.CallCenter;

public record AcquireLockRequest(
    Guid OrderId,
    string OrderNumber,
    Guid UserId,
    string UserName,
    int? DurationSeconds = null
);

public record ReleaseLockRequest(
    Guid OrderId,
    Guid UserId
);

public static class CallCenterAntiCollisionEndpoints
{
    public static IEndpointRouteBuilder MapCallCenterAntiCollisionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/call-center/locks")
            .WithTags("CallCenterLocks");

        group.MapPost("/acquire", async (
            Guid tenantId,
            AcquireLockRequest request,
            ICallCenterAntiCollisionService service) =>
        {
            var ttl = request.DurationSeconds.HasValue
                ? TimeSpan.FromSeconds(request.DurationSeconds.Value)
                : TimeSpan.FromMinutes(3);

            var result = await service.AcquireLockAsync(
                tenantId,
                request.OrderId,
                request.OrderNumber,
                request.UserId,
                request.UserName,
                ttl);

            return result.IsAcquired
                ? Results.Ok(result)
                : Results.Conflict(result);
        });

        group.MapPost("/release", async (
            Guid tenantId,
            ReleaseLockRequest request,
            ICallCenterAntiCollisionService service) =>
        {
            var released = await service.ReleaseLockAsync(tenantId, request.OrderId, request.UserId);
            return Results.Ok(new { Released = released, OrderId = request.OrderId });
        });

        group.MapGet("/{orderId:guid}", async (
            Guid tenantId,
            Guid orderId,
            ICallCenterAntiCollisionService service) =>
        {
            var lockInfo = await service.GetActiveLockAsync(tenantId, orderId);
            return Results.Ok(lockInfo);
        });

        group.MapPost("/sweep", async (
            Guid tenantId,
            ICallCenterAntiCollisionService service) =>
        {
            var swept = await service.SweepExpiredLocksAsync(tenantId);
            return Results.Ok(new { SweptCount = swept });
        });

        return app;
    }
}
