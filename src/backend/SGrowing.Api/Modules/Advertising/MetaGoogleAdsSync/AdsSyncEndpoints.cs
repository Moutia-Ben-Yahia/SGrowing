using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Advertising.MetaGoogleAdsSync;

public static class AdsSyncEndpoints
{
    public static IServiceCollection AddAdsSyncServices(this IServiceCollection services)
    {
        services.AddScoped<IAdsSyncEngine, AdsSyncEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapAdsSyncEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/advertising")
            .WithTags("Advertising Campaign & CAPI Sync")
            .RequireAuthorization();

        group.MapPost("/conversions/capi-upload", async (CapiUploadRequest request, IAdsSyncEngine engine) =>
        {
            var result = await engine.UploadConversionsAsync(request);
            return Results.Ok(result);
        }).WithName("UploadCapiConversions");

        group.MapPost("/audiences/sync-hashes", (AudienceHashSyncRequest request, IAdsSyncEngine engine) =>
        {
            var result = engine.SyncAudienceHashes(request);
            return Results.Ok(result);
        }).WithName("SyncAudienceHashes");

        group.MapPost("/budgets/optimize-roas", (RoasOptimizationRequest request, IAdsSyncEngine engine) =>
        {
            var result = engine.OptimizeRoas(request);
            return Results.Ok(result);
        }).WithName("OptimizeRoasBudgets");

        return endpoints;
    }
}
