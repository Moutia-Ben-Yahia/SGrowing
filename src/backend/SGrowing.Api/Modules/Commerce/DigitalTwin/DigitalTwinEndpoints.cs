using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Commerce.DigitalTwin;

/// <summary>
/// Minimal API endpoints and Dependency Injection registration for Horizon 56:
/// Autonomous Warehouse Digital Twin, Real-Time Physics Simulator &amp; Robotics Kinematics Emulation.
/// </summary>
public static class DigitalTwinEndpoints
{
    /// <summary>
    /// Registers digital twin physics simulation, kinematics emulation, and multi-agent path finding services.
    /// </summary>
    public static IServiceCollection AddDigitalTwinServices(this IServiceCollection services)
    {
        services.AddSingleton<IWarehouseDigitalTwinPhysicsEngine, WarehouseDigitalTwinPhysicsEngine>();
        services.AddSingleton<IRoboticsKinematicsSimulatorService, RoboticsKinematicsSimulatorService>();
        services.AddSingleton<ICollisionAvoidanceTrajectoryPlanner, CollisionAvoidanceTrajectoryPlanner>();

        return services;
    }

    /// <summary>
    /// Maps warehouse digital twin Minimal API endpoints under /api/v1/commerce/digital-twin.
    /// </summary>
    public static IEndpointRouteBuilder MapDigitalTwinEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/commerce/digital-twin")
            .WithTags("Autonomous Warehouse Digital Twin & Robotics Physics Engine");

        // 1. POST /api/v1/commerce/digital-twin/physics/braking - Simulate emergency braking
        group.MapPost("/physics/braking", (
            [FromBody] EmergencyBrakingSimulationRequest req,
            [FromServices] IWarehouseDigitalTwinPhysicsEngine physicsEngine) =>
        {
            var result = physicsEngine.SimulateEmergencyBraking(req);
            return Results.Ok(new
            {
                Success = true,
                Simulation = result
            });
        })
        .WithName("SimulateEmergencyBraking")
        .WithSummary("Simulates AGV emergency stopping distance and tipping stability under floor friction conditions.");

        // 2. POST /api/v1/commerce/digital-twin/kinematics/palletizer - Forward kinematics for robotic arms
        group.MapPost("/kinematics/palletizer", (
            [FromBody] PalletizerKinematicsRequest req,
            [FromServices] IRoboticsKinematicsSimulatorService kinematicsService) =>
        {
            var result = kinematicsService.ComputeForwardKinematics(req);
            return Results.Ok(new
            {
                Success = true,
                Kinematics = result
            });
        })
        .WithName("ComputePalletizerKinematics")
        .WithSummary("Computes forward kinematics 4x4 D-H transformation and work envelope validation for palletizer arm.");

        // 3. POST /api/v1/commerce/digital-twin/trajectories/plan - Multi-agent conflict-free trajectory planning
        group.MapPost("/trajectories/plan", (
            [FromBody] MultiAgentPathRequest req,
            [FromServices] ICollisionAvoidanceTrajectoryPlanner planner) =>
        {
            var result = planner.PlanMultiAgentTrajectories(req);
            return Results.Ok(new
            {
                Success = result.AllRoutesFound,
                MultiAgentResult = result
            });
        })
        .WithName("PlanMultiAgentTrajectories")
        .WithSummary("Plans 4D space-time collision-free trajectories for warehouse mobile robot fleets.");

        // 4. GET /api/v1/commerce/digital-twin/robots - Get all active robots in digital twin
        group.MapGet("/robots", (
            [FromServices] IWarehouseDigitalTwinPhysicsEngine physicsEngine) =>
        {
            var robots = physicsEngine.GetAllRobots();
            return Results.Ok(new
            {
                Success = true,
                TotalActive = robots.Count,
                Robots = robots
            });
        })
        .WithName("GetDigitalTwinRobots")
        .WithSummary("Retrieves telemetry and poses of all simulated robots in the digital twin.");

        return group;
    }
}
