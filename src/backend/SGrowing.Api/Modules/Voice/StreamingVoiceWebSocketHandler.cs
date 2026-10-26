using System.Diagnostics;
using System.Text;
using System.Text.Json;
using SGrowing.Core.Entities;
using Microsoft.Extensions.Logging;

namespace SGrowing.Api.Modules.Voice;

public class InboundChunkProcessResult
{
    public VadStatus Status { get; set; }
    public double RmsEnergy { get; set; }
    public bool IsSpeech { get; set; }
    public double LatencyMs { get; set; }
    public string? ClearBufferCommand { get; set; }
}

public class StreamingVoiceWebSocketHandler
{
    private readonly BargeInInterruptionService _bargeInService;
    private readonly ILogger<StreamingVoiceWebSocketHandler>? _logger;
    private const double SpeechRmsThreshold = 400.0; // Audio energy threshold for 8kHz mu-law decoded PCM

    public StreamingVoiceWebSocketHandler(
        BargeInInterruptionService bargeInService,
        ILogger<StreamingVoiceWebSocketHandler>? logger = null)
    {
        _bargeInService = bargeInService;
        _logger = logger;
    }

    public InboundChunkProcessResult ProcessInboundAudioChunk(
        StreamingVoiceCallSession session,
        string payloadBase64,
        long sequenceNumber)
    {
        var sw = Stopwatch.StartNew();

        byte[] mulawBytes;
        try
        {
            mulawBytes = Convert.FromBase64String(payloadBase64);
        }
        catch
        {
            mulawBytes = Array.Empty<byte>();
        }

        var rms = CalculateMuLawRmsEnergy(mulawBytes);
        var isSpeech = rms >= SpeechRmsThreshold;

        sw.Stop();
        var latencyMs = sw.Elapsed.TotalMilliseconds;

        var priorSpeakingState = session.IsAgentSpeaking;
        session.RecordInboundFrame(mulawBytes.Length, rms, isSpeech, latencyMs);

        string? clearCmd = null;
        if (session.CurrentVadStatus == VadStatus.BargeInInterrupted)
        {
            _logger?.LogWarning("Barge-in interruption detected on session {SessionId}! Caller began speaking while agent was talking.", session.Id);
            clearCmd = _bargeInService.CreateClearBufferCommand(session.StreamSid);
        }

        return new InboundChunkProcessResult
        {
            Status = session.CurrentVadStatus,
            RmsEnergy = rms,
            IsSpeech = isSpeech,
            LatencyMs = latencyMs,
            ClearBufferCommand = clearCmd
        };
    }

    public static double CalculateMuLawRmsEnergy(byte[] mulawData)
    {
        if (mulawData == null || mulawData.Length == 0)
        {
            return 0.0;
        }

        double sumSquares = 0.0;
        for (int i = 0; i < mulawData.Length; i++)
        {
            short pcmSample = MuLawToPcm16(mulawData[i]);
            sumSquares += pcmSample * pcmSample;
        }

        return Math.Sqrt(sumSquares / mulawData.Length);
    }

    public static short MuLawToPcm16(byte muLawByte)
    {
        byte mu = (byte)~muLawByte;
        int sign = (mu & 0x80);
        int exponent = (mu >> 4) & 0x07;
        int mantissa = mu & 0x0F;

        int sample = ((mantissa << 3) + 0x84) << exponent;
        sample -= 0x84;

        return (short)(sign != 0 ? -sample : sample);
    }

    public static List<string> SegmentSpeculativeSentences(ref string buffer, string newTokens)
    {
        buffer += newTokens;
        var sentences = new List<string>();

        var delimiters = new[] { '.', '!', '?', '\n' };
        int searchStartIndex = 0;

        while (true)
        {
            int delimiterIndex = buffer.IndexOfAny(delimiters, searchStartIndex);
            if (delimiterIndex == -1)
            {
                break;
            }

            // Ensure the delimiter isn't part of an abbreviation (e.g., "e.g.", "Mr.")
            var sentenceCandidate = buffer.Substring(0, delimiterIndex + 1).Trim();
            if (sentenceCandidate.Length > 2)
            {
                sentences.Add(sentenceCandidate);
                buffer = buffer.Substring(delimiterIndex + 1).TrimStart();
                searchStartIndex = 0;
            }
            else
            {
                searchStartIndex = delimiterIndex + 1;
            }
        }

        return sentences;
    }
}
