using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SGrowing.Api.Common;

namespace SGrowing.Api.Modules.Commerce;

/// <summary>
/// Minimal API endpoints for offline-first courier PWA sync and Cash-on-Delivery reconciliation.
/// </summary>
public class CourierDeliverySyncEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/courier/deliveries")
            .WithTags("Commerce - Courier PWA Offline Reconciliation & COD");

        group.MapPost("/sync", async (
            CourierDeliverySyncBatchRequest request,
            ICourierDeliverySyncService syncService) =>
        {
            var result = await syncService.SyncBatchAsync(request);
            return Results.Ok(result);
        })
        .WithName("SyncOfflineCourierDeliveriesBatch")
        .WithSummary("Reconciles batched offline courier deliveries collected by the mobile PWA.");

        group.MapGet("/driver/{driverId}", async (
            string driverId,
            Guid tenantId,
            ICourierDeliverySyncService syncService) =>
        {
            var records = await syncService.GetDriverDeliveriesAsync(tenantId, driverId);
            return Results.Ok(records);
        })
        .WithName("GetCourierDriverDeliveries")
        .WithSummary("Retrieves all delivery logs recorded by a specific driver.");

        group.MapGet("/driver/{driverId}/reconciliation", async (
            string driverId,
            Guid tenantId,
            ICourierDeliverySyncService syncService) =>
        {
            var summary = await syncService.GetDriverReconciliationSummaryAsync(tenantId, driverId);
            return Results.Ok(summary);
        })
        .WithName("GetCourierDriverReconciliationSummary")
        .WithSummary("Computes Cash-on-Delivery (COD) cash balances and delivery outcome counts for a driver.");
    }
}
