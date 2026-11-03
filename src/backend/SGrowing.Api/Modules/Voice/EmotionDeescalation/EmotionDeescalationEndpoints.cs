using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Voice.EmotionDeescalation;

public static class EmotionDeescalationEndpoints
{
    public static IServiceCollection AddEmotionDeescalationServices(this IServiceCollection services)
    {
        services.AddSingleton<EmotionDeescalationEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapEmotionDeescalationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/voice/emotion-deescalation")
            .WithTags("Voice - Emotion De-escalation & Empathy Routing");

        group.MapGet("/overview", (EmotionDeescalationEngine engine) =>
        {
            var overview = engine.GetOverview();
            return Results.Ok(overview);
        })
        .WithName("GetEmotionDeescalationOverview")
        .WithSummary("Retrieve live call acoustic emotion telemetry, de-escalation rate, and supervisor queue");

        group.MapPost("/analyze-acoustics", (AnalyzeAcousticsRequest request, EmotionDeescalationEngine engine) =>
        {
            var result = engine.AnalyzeAcoustics(request);
            return Results.Ok(result);
        })
        .WithName("AnalyzeCallAcoustics")
        .WithSummary("Evaluate voice audio chunk for pitch variance, speech velocity, and frustration markers");

        group.MapPost("/trigger-handoff", (SupervisorHandoffRequest request, EmotionDeescalationEngine engine) =>
        {
            var receipt = engine.TriggerSupervisorHandoff(request);
            return Results.Ok(receipt);
        })
        .WithName("TriggerSupervisorHandoff")
        .WithSummary("Initiate zero-latency warm-handoff to human supervisor with context briefcase");

        return endpoints;
    }
}
