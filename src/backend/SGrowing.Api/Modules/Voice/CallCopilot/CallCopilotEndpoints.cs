namespace SGrowing.Api.Modules.Voice.CallCopilot;

public static class CallCopilotEndpoints
{
    public static IServiceCollection AddCallCopilotServices(this IServiceCollection services)
    {
        services.AddSingleton<ICallCopilotEngine, CallCopilotEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapCallCopilotEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/voice/copilot")
            .WithTags("Real-Time Voice Call Diarization, Sentiment & In-Call Copilot");

        group.MapGet("/sessions", (ICallCopilotEngine engine) =>
        {
            var sessions = engine.ListSessions();
            return Results.Ok(sessions);
        })
        .WithName("ListCallCopilotSessions")
        .WithSummary("List all live and past diarized call sessions with sentiment tracking");

        group.MapPost("/sessions", (ICallCopilotEngine engine, StartCallSessionRequest request) =>
        {
            var session = engine.StartSession(request.AgentId, request.ProspectPhone, request.ProspectCompany);
            return Results.Created($"/api/v1/voice/copilot/sessions/{session.SessionId}", session);
        })
        .WithName("StartCallCopilotSession")
        .WithSummary("Initialize a live speech diarization and sentiment telemetry session");

        group.MapGet("/sessions/{id}", (ICallCopilotEngine engine, string id) =>
        {
            var session = engine.GetSession(id);
            return session is not null ? Results.Ok(session) : Results.NotFound();
        })
        .WithName("GetCallCopilotSession")
        .WithSummary("Get live utterances and surfaced battlecards for a call session");

        group.MapPost("/utterances", (ICallCopilotEngine engine, IngestUtteranceRequest request) =>
        {
            try
            {
                var utterance = engine.IngestUtterance(request);
                return Results.Ok(utterance);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .WithName("IngestCallUtterance")
        .WithSummary("Stream a live diarized audio utterance and trigger real-time battlecard lookups");

        group.MapPost("/sessions/{id}/end", (ICallCopilotEngine engine, string id) =>
        {
            try
            {
                var summary = engine.EndSessionAndSummarize(id);
                return Results.Ok(summary);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .WithName("EndCallCopilotSession")
        .WithSummary("Conclude live call session and synthesize executive summary and CRM action items");

        group.MapGet("/battlecards", (ICallCopilotEngine engine) =>
        {
            var battlecards = engine.GetAvailableBattlecards();
            return Results.Ok(battlecards);
        })
        .WithName("GetAvailableBattlecards")
        .WithSummary("List all configured objection handling battlecards and ROI proof points");

        return app;
    }
}

public record StartCallSessionRequest(string AgentId, string ProspectPhone, string ProspectCompany);
