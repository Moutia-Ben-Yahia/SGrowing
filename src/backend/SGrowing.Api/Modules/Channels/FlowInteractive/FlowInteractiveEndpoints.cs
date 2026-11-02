using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Channels.FlowInteractive;

public static class FlowInteractiveEndpoints
{
    public static IServiceCollection AddFlowInteractiveServices(this IServiceCollection services)
    {
        services.AddSingleton<FlowInteractiveEngine>();
        return services;
    }

    public static RouteGroupBuilder MapFlowInteractiveEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/channels/whatsapp/interactive-flows")
            .WithTags("ChannelsWhatsAppInteractiveFlows");

        group.MapGet("/", async Task<Ok<IReadOnlyList<WhatsAppFlowDefinition>>> (
            [FromServices] FlowInteractiveEngine engine,
            CancellationToken ct) =>
        {
            var flows = await engine.GetFlowsAsync(ct);
            return TypedResults.Ok(flows);
        })
        .WithName("GetInteractiveFlows")
        .WithSummary("Retrieve published native WhatsApp Flow screen DAG definitions");

        group.MapGet("/{id}", async Task<Results<Ok<WhatsAppFlowDefinition>, NotFound<string>>> (
            [FromRoute] string id,
            [FromServices] FlowInteractiveEngine engine,
            CancellationToken ct) =>
        {
            try
            {
                var flow = await engine.GetFlowByIdAsync(id, ct);
                return TypedResults.Ok(flow);
            }
            catch (KeyNotFoundException)
            {
                return TypedResults.NotFound($"WhatsApp flow '{id}' not found.");
            }
        })
        .WithName("GetInteractiveFlowById")
        .WithSummary("Fetch WhatsApp flow layout components and terminal actions");

        group.MapPost("/data-exchange", async Task<Results<Ok<FlowDataExchangeResponse>, NotFound<string>, BadRequest<string>>> (
            [FromBody] FlowDataExchangeRequest request,
            [FromServices] FlowInteractiveEngine engine,
            CancellationToken ct) =>
        {
            try
            {
                var response = await engine.ProcessDataExchangeAsync(request, ct);
                return TypedResults.Ok(response);
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
        .WithName("ProcessFlowDataExchange")
        .WithSummary("Handle inbound WhatsApp Flow encrypted data exchange and state transitions");

        group.MapGet("/analytics", async Task<Ok<FlowAnalyticsSummary>> (
            [FromServices] FlowInteractiveEngine engine,
            CancellationToken ct) =>
        {
            var analytics = await engine.GetAnalyticsAsync(ct);
            return TypedResults.Ok(analytics);
        })
        .WithName("GetFlowAnalytics")
        .WithSummary("Retrieve funnel conversion rates and screen drop-off metrics for flows");

        return group;
    }
}
