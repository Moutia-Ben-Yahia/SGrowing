namespace SGrowing.Api.Modules.Voice.IVR;

public static class VoiceIvrEndpoints
{
    public static IEndpointRouteBuilder MapVoiceIvrEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/voice/ivr")
            .WithTags("Voice IVR Flow Engine");

        group.MapPost("/flows", async (
            IvrFlow flow,
            IVoiceIvrEngineService service,
            CancellationToken ct) =>
        {
            var saved = await service.SaveFlowAsync(flow, ct);
            return Results.Ok(saved);
        })
        .WithName("SaveIvrFlow")
        .WithSummary("Saves and validates an interactive IVR node flow graph.");

        group.MapGet("/flows", async (
            Guid tenantId,
            IVoiceIvrEngineService service,
            CancellationToken ct) =>
        {
            var list = await service.ListFlowsAsync(tenantId, ct);
            return Results.Ok(list);
        })
        .WithName("ListIvrFlows")
        .WithSummary("Lists all IVR flows for a tenant.");

        group.MapPost("/sessions/initiate", async (
            Guid tenantId,
            Guid flowId,
            string callerPhoneNumber,
            IVoiceIvrEngineService service,
            CancellationToken ct) =>
        {
            var session = await service.InitiateSessionAsync(tenantId, flowId, callerPhoneNumber, ct: ct);
            return Results.Ok(session);
        })
        .WithName("InitiateIvrSession")
        .WithSummary("Spawns an active call leg session, evaluating business hours schedules.");

        group.MapPost("/sessions/input", async (
            SubmitSessionInputRequest request,
            IVoiceIvrEngineService service,
            CancellationToken ct) =>
        {
            var updated = await service.ProcessInputAsync(request, ct);
            return Results.Ok(updated);
        })
        .WithName("ProcessIvrInput")
        .WithSummary("Processes DTMF keypress or transcribed speech and evaluates state transitions.");

        group.MapGet("/sessions/active", async (
            Guid tenantId,
            IVoiceIvrEngineService service,
            CancellationToken ct) =>
        {
            var sessions = await service.GetActiveSessionsAsync(tenantId, ct);
            return Results.Ok(sessions);
        })
        .WithName("GetActiveIvrSessions")
        .WithSummary("Lists real-time active IVR call sessions.");

        return app;
    }
}
