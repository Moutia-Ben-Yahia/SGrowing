using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Voice.CallStateResumption;

public static class CallStateResumptionEndpoints
{
    public static IServiceCollection AddCallStateResumptionServices(this IServiceCollection services)
    {
        services.AddSingleton<CallStateResumptionEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapCallStateResumptionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/voice/call-state-resumption")
            .WithTags("Voice - Call Disconnect Auto-Callback & State Resumption");

        group.MapGet("/overview", (CallStateResumptionEngine engine) =>
        {
            var overview = engine.GetOverview();
            return Results.Ok(overview);
        })
        .WithName("GetCallStateResumptionOverview")
        .WithSummary("Retrieve monitored active call dialog snapshots, disconnect events, and auto-callback latency");

        group.MapPost("/auto-callback", (TriggerAutoCallbackRequest request, CallStateResumptionEngine engine) =>
        {
            var evt = engine.TriggerAutoCallback(request);
            return Results.Ok(evt);
        })
        .WithName("TriggerCallAutoCallback")
        .WithSummary("Instantly triggers automated outbound PSTN/SIP reconnect with conversational context restoration");

        group.MapPost("/save-snapshot", (SaveCallSnapshotRequest request, CallStateResumptionEngine engine) =>
        {
            var snapshot = engine.SaveCallSnapshot(request);
            return Results.Ok(snapshot);
        })
        .WithName("SaveCallSessionSnapshot")
        .WithSummary("Saves incremental dialog AST and acoustic turn state for zero-loss recovery");

        return endpoints;
    }
}
