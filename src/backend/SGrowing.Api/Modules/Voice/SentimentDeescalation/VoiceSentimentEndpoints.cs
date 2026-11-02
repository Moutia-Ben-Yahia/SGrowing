using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Voice.SentimentDeescalation;

public static class VoiceSentimentEndpoints
{
    public static IServiceCollection AddVoiceSentimentDeescalationServices(this IServiceCollection services)
    {
        services.AddSingleton<AcousticSentimentClassifierEngine>();
        services.AddSingleton<AutonomousVoiceDeescalationCoordinator>();
        return services;
    }

    public static IEndpointRouteBuilder MapVoiceSentimentDeescalationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/voice/sentiment-deescalation")
            .WithTags("Autonomous AI Voice Sentiment & Emotional De-escalation Telephony Mesh");

        group.MapPost("/evaluate", (
            VoiceCallAcousticFrame frame,
            AcousticSentimentClassifierEngine engine) =>
        {
            var evaluation = engine.ClassifyFrame(frame);
            return Results.Ok(evaluation);
        });

        group.MapPost("/interventions/dispatch", (
            EmotionalStateEvaluation evaluation,
            AutonomousVoiceDeescalationCoordinator coordinator) =>
        {
            var dispatch = coordinator.ExecuteIntervention(evaluation);
            return Results.Ok(dispatch);
        });

        group.MapGet("/calls/{callId}/status", (
            string callId,
            AutonomousVoiceDeescalationCoordinator coordinator) =>
        {
            var dispatch = coordinator.GetLastDispatch(callId);
            return dispatch != null ? Results.Ok(dispatch) : Results.NotFound();
        });

        return app;
    }
}
