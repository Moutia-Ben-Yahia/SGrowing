using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Analytics.DataCleanRoom;

public static class DataCleanRoomEndpoints
{
    public static IServiceCollection AddDataCleanRoomServices(this IServiceCollection services)
    {
        services.AddSingleton<DataCleanRoomEngine>();
        return services;
    }

    public static RouteGroupBuilder MapDataCleanRoomEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/analytics/clean-room")
            .WithTags("AnalyticsDataCleanRoom");

        group.MapGet("/participants", async Task<Ok<IReadOnlyList<CleanRoomParticipant>>> (
            [FromServices] DataCleanRoomEngine engine,
            CancellationToken ct) =>
        {
            var participants = await engine.GetParticipantsAsync(ct);
            return TypedResults.Ok(participants);
        })
        .WithName("GetCleanRoomParticipants")
        .WithSummary("Retrieve verified clean room publisher and advertiser participants");

        group.MapGet("/datasets", async Task<Ok<IReadOnlyList<AudienceCohortDataset>>> (
            [FromServices] DataCleanRoomEngine engine,
            CancellationToken ct) =>
        {
            var datasets = await engine.GetDatasetsAsync(ct);
            return TypedResults.Ok(datasets);
        })
        .WithName("GetCleanRoomDatasets")
        .WithSummary("List salted cryptographic audience cohorts available for private intersection");

        group.MapPost("/queries/overlap", async Task<Results<Ok<CleanRoomOverlapResult>, BadRequest<string>, NotFound<string>>> (
            [FromBody] RunOverlapQueryRequest request,
            [FromServices] DataCleanRoomEngine engine,
            CancellationToken ct) =>
        {
            try
            {
                var result = await engine.ExecuteOverlapMeasurementAsync(request, ct);
                return TypedResults.Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return TypedResults.NotFound(ex.Message);
            }
            catch (ArgumentException ex)
            {
                return TypedResults.BadRequest(ex.Message);
            }
        })
        .WithName("ExecuteCleanRoomOverlapQuery")
        .WithSummary("Execute zero-knowledge audience overlap with differential privacy noise");

        group.MapGet("/summary", async Task<Ok<CleanRoomSummary>> (
            [FromServices] DataCleanRoomEngine engine,
            CancellationToken ct) =>
        {
            var summary = await engine.GetSummaryAsync(ct);
            return TypedResults.Ok(summary);
        })
        .WithName("GetCleanRoomSummary")
        .WithSummary("Get clean room privacy budget consumed, participants, and hashed volume");

        return group;
    }
}
