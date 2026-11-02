using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Advertising.AudienceSync;

public static class AudienceSyncEndpoints
{
    public static IServiceCollection AddAudienceSyncServices(this IServiceCollection services)
    {
        services.AddSingleton<IAudienceSyncEngine, AudienceSyncEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapAudienceSyncEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/advertising/audience-sync")
            .WithTags("Advertising - Real-Time Audience Sync Mesh");

        group.MapPost("/push", (BatchAudienceSyncRequest request, IAudienceSyncEngine engine) =>
        {
            var result = engine.SyncCohortBatch(request);
            return Results.Ok(result);
        })
        .WithName("SyncCohortBatch")
        .WithSummary("Pushes real-time hashed audience cohort updates to DSP platforms (Google, Meta, TikTok, LinkedIn)");

        group.MapGet("/connectors", (IAudienceSyncEngine engine) =>
        {
            var connectors = engine.GetConnectorStatuses();
            return Results.Ok(connectors);
        })
        .WithName("GetConnectorStatuses")
        .WithSummary("Retrieves operational health, record counts, and streaming latency across connected DSPs");

        group.MapPost("/retry-failed", (RetryFailedSyncRequest request, IAudienceSyncEngine engine) =>
        {
            var result = engine.RetryFailedSync(request.SyncJobId);
            return Results.Ok(result);
        })
        .WithName("RetryFailedSync")
        .WithSummary("Retries a previously buffered or rate-limited audience sync job");

        return endpoints;
    }
}
