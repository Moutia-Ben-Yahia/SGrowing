using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace SGrowing.Api.Modules.Voice;

public class RealtimeSessionConfig
{
    public string Voice { get; set; } = "alloy"; // alloy, echo, shimmer, sage, coral
    public string Model { get; set; } = "gpt-4o-realtime-preview";
    public string AudioFormat { get; set; } = "g711_ulaw"; // g711_ulaw, pcm16
    public string Instructions { get; set; } = "You are SGrowing AI enterprise sales and support voice specialist. Keep responses concise, natural, and helpful.";
    public double Temperature { get; set; } = 0.7;
}

public class RealtimeBridgeMessage
{
    public string EventType { get; set; } = string.Empty;
    public string? EventId { get; set; }
    public string? AudioPayloadBase64 { get; set; }
    public string? TranscriptDelta { get; set; }
    public bool IsBargeInInterruption { get; set; }
}

public class OpenAiRealtimeBridgeService
{
    private readonly ILogger<OpenAiRealtimeBridgeService>? _logger;

    public OpenAiRealtimeBridgeService(ILogger<OpenAiRealtimeBridgeService>? logger = null)
    {
        _logger = logger;
    }

    public string BuildSessionUpdateCommand(RealtimeSessionConfig config)
    {
        var payload = new
        {
            event_id = "evt_" + Guid.NewGuid().ToString("N").Substring(0, 12),
            type = "session.update",
            session = new
            {
                modalities = new[] { "text", "audio" },
                instructions = config.Instructions,
                voice = config.Voice,
                input_audio_format = config.AudioFormat,
                output_audio_format = config.AudioFormat,
                turn_detection = new
                {
                    type = "server_vad",
                    threshold = 0.5,
                    prefix_padding_ms = 300,
                    silence_duration_ms = 500
                },
                temperature = config.Temperature
            }
        };

        return JsonSerializer.Serialize(payload);
    }

    public string BuildAudioAppendCommand(string audioBase64)
    {
        var payload = new
        {
            event_id = "evt_" + Guid.NewGuid().ToString("N").Substring(0, 12),
            type = "input_audio_buffer.append",
            audio = audioBase64
        };

        return JsonSerializer.Serialize(payload);
    }

    public RealtimeBridgeMessage ParseUpstreamRealtimeEvent(string upstreamJson)
    {
        using var doc = JsonDocument.Parse(upstreamJson);
        var root = doc.RootElement;

        var eventType = root.TryGetProperty("type", out var typeEl) ? typeEl.GetString() ?? "" : "";
        var eventId = root.TryGetProperty("event_id", out var idEl) ? idEl.GetString() : null;

        var result = new RealtimeBridgeMessage
        {
            EventType = eventType,
            EventId = eventId
        };

        if (eventType == "response.audio.delta" && root.TryGetProperty("delta", out var deltaEl))
        {
            result.AudioPayloadBase64 = deltaEl.GetString();
        }
        else if (eventType == "response.audio_transcript.delta" && root.TryGetProperty("delta", out var textEl))
        {
            result.TranscriptDelta = textEl.GetString();
        }
        else if (eventType == "input_audio_buffer.speech_started")
        {
            result.IsBargeInInterruption = true;
            _logger?.LogInformation("Upstream Realtime VAD detected caller speech start. Triggering barge-in playback cancellation.");
        }

        return result;
    }
}
