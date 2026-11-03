using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Sales.CollaborativeDealRoom;

public static class CollaborativeDealRoomEndpoints
{
    public static IServiceCollection AddCollaborativeDealRoomServices(this IServiceCollection services)
    {
        services.AddSingleton<CollaborativeDealRoomEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapCollaborativeDealRoomEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/sales/collaborative-deal-room")
            .WithTags("Sales - Collaborative Deal Room & Mutual Action Workspace");

        group.MapGet("/overview", (CollaborativeDealRoomEngine engine) =>
        {
            var overview = engine.GetOverview();
            return Results.Ok(overview);
        })
        .WithName("GetCollaborativeDealRoomOverview")
        .WithSummary("Retrieve active collaborative deal rooms, milestone velocity, and closing metrics");

        group.MapPost("/create-room", (CreateDealRoomRequest request, CollaborativeDealRoomEngine engine) =>
        {
            var record = engine.CreateDealRoom(request);
            return Results.Ok(record);
        })
        .WithName("CreateCollaborativeDealRoom")
        .WithSummary("Create structured mutual closing roadmap with shared executive stakeholders");

        group.MapPost("/complete-milestone", (CompleteDealRoomMilestoneRequest request, CollaborativeDealRoomEngine engine) =>
        {
            var receipt = engine.CompleteMilestone(request);
            return Results.Ok(receipt);
        })
        .WithName("CompleteDealRoomMilestone")
        .WithSummary("Cryptographically attest milestone completion with dual buyer/seller signoff");

        return endpoints;
    }
}
