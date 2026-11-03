using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Voice.PitchTuning;

public static class PitchTuningEndpoints
{
    public static IServiceCollection AddPitchTuningServices(this IServiceCollection services)
    {
        services.AddSingleton<IPitchTuningEngine, PitchTuningEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapPitchTuningEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/voice/pitch-tuning")
            .WithTags("Voice - AI Agent Telephony Sentiment & Real-Time Pitch Tuning");

        group.MapGet("/overview", (IPitchTuningEngine engine) =>
        {
            var overview = engine.GetOverview();
            return Results.Ok(overview);
        })
        .WithName("GetPitchTuningOverview")
        .WithSummary("Retrieves active call acoustic sentiment, speech rates, and real-time pitch parameters");

        group.MapPost("/tune", (TunePitchCommand command, IPitchTuningEngine engine) =>
        {
            try
            {
                var result = engine.TunePitch(command);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new { error = $"Call {command.CallId} not found." });
            }
        })
        .WithName("TuneVoicePitch")
        .WithSummary("Adjusts speech fundamental frequency, WPM rate, and persona tone in real time");

        group.MapPost("/{callId}/simulate-shift", (string callId, SimulateSentimentShiftRequest request, IPitchTuningEngine engine) =>
        {
            try
            {
                var result = engine.SimulateSentimentShift(callId, request.NewSentiment);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new { error = $"Call {callId} not found." });
            }
        })
        .WithName("SimulateVoiceSentimentShift")
        .WithSummary("Simulates an in-flight caller sentiment shift and triggers automated pitch/angle adaptation");

        return endpoints;
    }
}
