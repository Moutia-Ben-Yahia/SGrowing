using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Voice.AgentAssist;

public static class AgentAssistEndpoints
{
    public static IServiceCollection AddAgentAssistServices(this IServiceCollection services)
    {
        services.AddSingleton<IAgentAssistEngine, AgentAssistEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapAgentAssistEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/voice/agent-assist")
            .WithTags("Voice - Real-Time Agent Assist & Whisper Coaching");

        group.MapPost("/stream-chunk", (StreamChunkRequest request, IAgentAssistEngine engine) =>
        {
            var session = engine.StreamChunk(request);
            return Results.Ok(session);
        })
        .WithName("StreamLiveCallChunk")
        .WithSummary("Streams real-time transcript chunk and evaluates talk-time pacing & competitor mentions");

        group.MapGet("/session/{callSessionId}", (string callSessionId, IAgentAssistEngine engine) =>
        {
            var session = engine.GetSessionById(callSessionId);
            return session is not null ? Results.Ok(session) : Results.NotFound();
        })
        .WithName("GetLiveAgentAssistSession")
        .WithSummary("Retrieves active call session with transcripts, talk ratio, and battlecards");

        group.MapPost("/session/{callSessionId}/battlecard/{competitorName}", (string callSessionId, string competitorName, IAgentAssistEngine engine) =>
        {
            var session = engine.TriggerBattlecard(callSessionId, competitorName);
            return Results.Ok(session);
        })
        .WithName("TriggerCompetitorBattlecard")
        .WithSummary("Manually or semantically displays differentiator battlecard for rep");

        return endpoints;
    }
}
