using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace SGrowing.Api.Modules.Voice;

public static class VoiceDiarizationEndpoints
{
    public static IEndpointRouteBuilder MapVoiceDiarizationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/voice/diarization")
            .WithTags("VoiceDiarization");

        // GET /calls
        group.MapGet("/calls", async (
            [FromQuery] Guid? tenantId,
            [FromQuery] string? search,
            IVoiceTranscriptionDiarizationService service) =>
        {
            var calls = await service.ListCallsAsync(tenantId, search);
            return Results.Ok(calls);
        })
        .WithName("ListVoiceDiarizationCalls")
        .WithSummary("List all transcribed and diarized voice calls with real-time sentiment velocity and churn alerts.");

        // GET /calls/{callId}
        group.MapGet("/calls/{callId}", async (
            string callId,
            IVoiceTranscriptionDiarizationService service) =>
        {
            var call = await service.GetCallByIdAsync(callId);
            return call is not null ? Results.Ok(call) : Results.NotFound(new { error = $"Call with ID '{callId}' not found." });
        })
        .WithName("GetVoiceDiarizationCallById")
        .WithSummary("Get full acoustic diarization transcript, sentiment momentum, and post-call structured summary.");

        // POST /stream-utterance
        group.MapPost("/stream-utterance", async (
            [FromBody] StreamUtteranceRequest request,
            IVoiceTranscriptionDiarizationService service) =>
        {
            if (string.IsNullOrWhiteSpace(request.CallId))
            {
                return Results.BadRequest(new { error = "CallId must be specified." });
            }

            if (string.IsNullOrWhiteSpace(request.Text))
            {
                return Results.BadRequest(new { error = "Text must be specified." });
            }

            try
            {
                var utterance = await service.StreamUtteranceAsync(request);
                var call = await service.GetCallByIdAsync(request.CallId);

                return Results.Ok(new
                {
                    utterance,
                    callSentimentScore = call?.OverallSentimentScore ?? utterance.SentimentScore,
                    sentimentTrajectory = call?.SentimentTrajectory.ToString() ?? "Stable",
                    frustrationAlert = call?.FrustrationAlert
                });
            }
            catch (Exception ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: 500);
            }
        })
        .WithName("StreamVoiceUtterance")
        .WithSummary("Ingest real-time speech utterance chunk, compute lexical sentiment velocity, and detect churn alert triggers.");

        // POST /generate-summary
        group.MapPost("/generate-summary", async (
            [FromBody] GenerateSummaryRequest request,
            IVoiceTranscriptionDiarizationService service) =>
        {
            if (string.IsNullOrWhiteSpace(request.CallId))
            {
                return Results.BadRequest(new { error = "CallId must be specified." });
            }

            try
            {
                var summary = await service.GeneratePostCallSummaryAsync(request.CallId);
                return Results.Ok(summary);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new { error = $"Call '{request.CallId}' was not found." });
            }
            catch (Exception ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: 500);
            }
        })
        .WithName("GenerateVoicePostCallSummary")
        .WithSummary("Generate or re-synthesize post-call structured executive summary with CRM deal association.");

        return app;
    }
}
