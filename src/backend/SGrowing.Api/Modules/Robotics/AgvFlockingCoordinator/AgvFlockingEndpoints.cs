using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Robotics.AgvFlockingCoordinator;

public static class AgvFlockingEndpoints
{
    public static IServiceCollection AddAgvFlockingServices(this IServiceCollection services)
    {
        services.AddSingleton<AgvFlockingCollisionAvoidanceEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapAgvFlockingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/robotics/agv-flocking")
            .WithTags("Autonomous Space-Constrained AGV Multi-Agent Flocking & Collision-Free Path Coordination");

        group.MapPost("/coordinate-fleet", (
            CoordinateAgvFleetRequest request,
            AgvFlockingCollisionAvoidanceEngine engine) =>
        {
            var result = engine.CoordinateFleetVelocity(request);
            return Results.Ok(result);
        });

        return app;
    }
}
