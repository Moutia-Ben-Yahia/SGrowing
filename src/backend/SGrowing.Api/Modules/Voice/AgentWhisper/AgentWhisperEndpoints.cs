using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Voice.AgentWhisper;

public static class AgentWhisperEndpoints
{
    public static IServiceCollection AddAgentWhisperServices(this IServiceCollection services)
    {
        services.AddSingleton<IAgentWhisperEngine, AgentWhisperEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapAgentWhisperEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/voice/agent-whisper")
            .WithTags("Voice - Real-Time Agent Whisper & Negotiation Co-Pilot");

        group.MapGet("/overview", (IAgentWhisperEngine engine) =>
        {
            var overview = engine.GetOverview();
            return Results.Ok(overview);
        })
        .WithName("GetAgentWhisperOverview")
        .WithSummary("Live call whisper sessions, concession adherence rate, and margin protection index");

        group.MapPost("/analyze-speech-turn", (AnalyzeSpeechTurnCommand command, IAgentWhisperEngine engine) =>
        {
            var result = engine.AnalyzeSpeechTurn(command);
            return Results.Ok(result);
        })
        .WithName("AnalyzeCallSpeechTurn")
        .WithSummary("Evaluates spoken customer utterance, checks discount rules, and dispatches real-time HUD whisper");

        group.MapPost("/calculate-concession", (CalculateConcessionCommand command, IAgentWhisperEngine engine) =>
        {
            var result = engine.CalculateConcession(command);
            return Results.Ok(result);
        })
        .WithName("CalculateDealConcession")
        .WithSummary("Computes optimal concession terms under deal size constraints and margin guardrails");

        return app;
    }
}
