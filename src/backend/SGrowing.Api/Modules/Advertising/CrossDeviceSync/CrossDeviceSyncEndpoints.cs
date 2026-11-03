using Microsoft.AspNetCore.Mvc;

namespace SGrowing.Api.Modules.Advertising.CrossDeviceSync;

public static class CrossDeviceSyncEndpoints
{
    public static IServiceCollection AddCrossDeviceSyncServices(this IServiceCollection services)
    {
        services.AddSingleton<ICrossDeviceSyncEngine, CrossDeviceSyncEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapCrossDeviceSyncEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/advertising/cross-device")
            .WithTags("Cross-Device Identity & CAPI Offline Sync");

        group.MapPost("/resolve", (
            [FromBody] ResolveIdentityRequest request,
            [FromServices] ICrossDeviceSyncEngine engine) =>
        {
            var cluster = engine.ResolveIdentityCluster(request);
            return Results.Created($"/api/v1/advertising/cross-device/clusters/{cluster.ClusterId}", cluster);
        })
        .WithName("ResolveCrossDeviceIdentity")
        .WithSummary("Resolves and unifies cross-device identifiers into household clusters");

        group.MapGet("/clusters", (
            [FromServices] ICrossDeviceSyncEngine engine) =>
        {
            var clusters = engine.GetClusters();
            return Results.Ok(clusters);
        })
        .WithName("GetCrossDeviceClusters")
        .WithSummary("Retrieves all resolved cross-device identity clusters");

        group.MapPost("/match-offline", (
            [FromBody] OfflineConversionReceipt receipt,
            [FromServices] ICrossDeviceSyncEngine engine) =>
        {
            var match = engine.MatchOfflineConversion(receipt);
            return Results.Ok(match);
        })
        .WithName("MatchOfflineConversion")
        .WithSummary("Matches offline POS/ERP purchase receipt with pre-conversion impression touchpoints");

        group.MapGet("/match-events", (
            [FromServices] ICrossDeviceSyncEngine engine) =>
        {
            var events = engine.GetMatchedEvents();
            return Results.Ok(events);
        })
        .WithName("GetMatchedConversionEvents")
        .WithSummary("Lists all matched CAPI conversion events forwarded to Meta and Google");

        group.MapGet("/metrics", (
            [FromServices] ICrossDeviceSyncEngine engine) =>
        {
            var metrics = engine.GetMetrics();
            return Results.Ok(metrics);
        })
        .WithName("GetCrossDeviceSyncMetrics")
        .WithSummary("Retrieves overall match rates, active clusters, and attributed revenue");

        return app;
    }
}
