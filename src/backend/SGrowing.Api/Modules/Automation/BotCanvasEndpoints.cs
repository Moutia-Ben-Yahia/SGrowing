using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Automation;

public record StartBotSimulationRequest(
    string FlowName,
    string NodesJson,
    Dictionary<string, object>? InitialVariables = null
);

public record StepBotSimulationRequest(
    string NodesJson,
    BotSimulationSession Session,
    string? UserMessage = null
);

public static class BotCanvasEndpoints
{
    public static IEndpointRouteBuilder MapBotCanvasEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/automation/bots")
            .WithTags("Conversational Bot Canvas");

        group.MapPost("/simulate/start", (StartBotSimulationRequest req, IBotSimulatorService simulator) =>
        {
            var workflow = WorkflowGraph.Create(Guid.NewGuid(), req.FlowName, "BotSimulation", req.NodesJson);
            var session = simulator.StartSimulation(workflow, req.InitialVariables);
            return Results.Ok(session);
        })
        .WithName("StartBotSimulation")
        .WithSummary("Start interactive in-memory execution simulation of a bot canvas workflow");

        group.MapPost("/simulate/step", (StepBotSimulationRequest req, IBotSimulatorService simulator) =>
        {
            var workflow = WorkflowGraph.Create(Guid.NewGuid(), "SimulatedFlow", "BotSimulation", req.NodesJson);
            var result = simulator.SimulateUserMessage(workflow, req.Session, req.UserMessage);
            return Results.Ok(result);
        })
        .WithName("StepBotSimulation")
        .WithSummary("Simulate user message input and advance bot flow execution");

        return app;
    }
}
