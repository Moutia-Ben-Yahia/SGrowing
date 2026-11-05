using System;
using System.Buffers.Text;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SGrowing.Api.Modules.Voice.Streaming;

/// <summary>
/// High-performance ITU-T G.711 mu-law audio codec and linear PCM resampler.
/// Converts between standard telephony 8kHz mu-law audio and 16kHz linear PCM used by modern speech AI models.
/// </summary>
public static class G711MuLawCodec
{
    private const int Bias = 0x84;
    private const int Clip = 32635;

    private static readonly short[] MuLawToLinearTable = new short[256];

    static G711MuLawCodec()
    {
        for (int i = 0; i < 256; i++)
        {
            byte input = (byte)~i;
            int sign = (input & 0x80);
            int exponent = (input >> 4) & 0x07;
            int mantissa = input & 0x0F;
            int sample = ((mantissa << 3) + Bias) << exponent;
            sample -= Bias;
            if (sign == 0)
            {
                sample = -sample;
            }
            MuLawToLinearTable[i] = (short)Math.Clamp(sample, short.MinValue, short.MaxValue);
        }
    }

    public static byte LinearToMuLawSample(short sample)
    {
        int sign = 0;
        if (sample < 0)
        {
            sample = (short)-sample;
        }
        else
        {
            sign = 0x80;
        }

        if (sample > Clip)
        {
            sample = Clip;
        }
        sample = (short)(sample + Bias);
        int exponent = 7;
        for (int expMask = 0x4000; (sample & expMask) == 0 && exponent > 0; expMask >>= 1)
        {
            exponent--;
        }
        int mantissa = (sample >> (exponent + 3)) & 0x0F;
        byte muLawByte = (byte)(sign | (exponent << 4) | mantissa);
        return (byte)~muLawByte;
    }

    public static short MuLawToLinearSample(byte muLaw)
    {
        return MuLawToLinearTable[muLaw];
    }

    public static byte[] EncodePcm16ToMuLaw(byte[] pcm16Bytes)
    {
        int sampleCount = pcm16Bytes.Length / 2;
        byte[] muLaw = new byte[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            short sample = BitConverter.ToInt16(pcm16Bytes, i * 2);
            muLaw[i] = LinearToMuLawSample(sample);
        }
        return muLaw;
    }

    public static byte[] DecodeMuLawToPcm16(byte[] muLawBytes)
    {
        byte[] pcm16 = new byte[muLawBytes.Length * 2];
        for (int i = 0; i < muLawBytes.Length; i++)
        {
            short sample = MuLawToLinearSample(muLawBytes[i]);
            byte[] bytes = BitConverter.GetBytes(sample);
            pcm16[i * 2] = bytes[0];
            pcm16[i * 2 + 1] = bytes[1];
        }
        return pcm16;
    }

    /// <summary>
    /// Resamples 8kHz 16-bit linear PCM up to 16kHz 16-bit linear PCM using linear interpolation.
    /// </summary>
    public static byte[] Resample8kTo16k(byte[] pcm8kBytes)
    {
        int count8k = pcm8kBytes.Length / 2;
        if (count8k == 0) return Array.Empty<byte>();

        short[] samples8k = new short[count8k];
        for (int i = 0; i < count8k; i++)
        {
            samples8k[i] = BitConverter.ToInt16(pcm8kBytes, i * 2);
        }

        int count16k = count8k * 2;
        byte[] pcm16k = new byte[count16k * 2];

        for (int i = 0; i < count8k; i++)
        {
            short current = samples8k[i];
            short next = (i + 1 < count8k) ? samples8k[i + 1] : current;
            short midpoint = (short)((current + next) / 2);

            // Sample 0 (exact)
            byte[] b0 = BitConverter.GetBytes(current);
            pcm16k[i * 4] = b0[0];
            pcm16k[i * 4 + 1] = b0[1];

            // Sample 1 (interpolated)
            byte[] b1 = BitConverter.GetBytes(midpoint);
            pcm16k[i * 4 + 2] = b1[0];
            pcm16k[i * 4 + 3] = b1[1];
        }

        return pcm16k;
    }

    /// <summary>
    /// Downsamples 16kHz 16-bit linear PCM to 8kHz 16-bit linear PCM by decimation.
    /// </summary>
    public static byte[] Resample16kTo8k(byte[] pcm16kBytes)
    {
        int count16k = pcm16kBytes.Length / 2;
        int count8k = count16k / 2;
        byte[] pcm8k = new byte[count8k * 2];

        for (int i = 0; i < count8k; i++)
        {
            pcm8k[i * 2] = pcm16kBytes[i * 4];
            pcm8k[i * 2 + 1] = pcm16kBytes[i * 4 + 1];
        }

        return pcm8k;
    }
}

public interface ISipTrunkBridgeService
{
    Task HandleInboundMessageAsync(
        Guid sessionId,
        string rawJsonMessage,
        Func<byte[], Task> onAudioDecodedAsync,
        CancellationToken ct = default);

    Task<string> CreateOutboundMediaJsonAsync(
        Guid sessionId,
        string streamSid,
        byte[] pcm16kAudio,
        string? markName = null);

    Task<string> CreateClearJsonAsync(string streamSid);

    bool IsInterrupted(Guid sessionId);
    void NotifyInterruption(Guid sessionId);
    void ResetInterruption(Guid sessionId);
    void ClearSession(Guid sessionId);
}

/// <summary>
/// Production SIP trunk & Twilio Media Streams bridge service (PRD Domain G).
/// Manages full-duplex WebSocket JSON envelopes, G.711 mu-law transcoding,
/// marker tracking, and caller barge-in / clear interruption handling.
/// </summary>
public class TwilioMediaStreamBridgeService : ISipTrunkBridgeService
{
    private readonly IWebRtcAudioBridgeService _audioBridge;
    private readonly ILogger<TwilioMediaStreamBridgeService>? _logger;
    private readonly ConcurrentDictionary<Guid, bool> _interruptedFlags = new();
    private readonly ConcurrentDictionary<Guid, uint> _sequenceCounters = new();

    public TwilioMediaStreamBridgeService(
        IWebRtcAudioBridgeService audioBridge,
        ILogger<TwilioMediaStreamBridgeService>? logger = null)
    {
        _audioBridge = audioBridge;
        _logger = logger;
    }

    public async Task HandleInboundMessageAsync(
        Guid sessionId,
        string rawJsonMessage,
        Func<byte[], Task> onAudioDecodedAsync,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rawJsonMessage)) return;

        using var doc = JsonDocument.Parse(rawJsonMessage);
        var root = doc.RootElement;

        if (!root.TryGetProperty("event", out var eventProp))
        {
            return;
        }

        var eventType = eventProp.GetString();

        switch (eventType)
        {
            case "connected":
                _logger?.LogInformation("Twilio Media Stream connected for session {SessionId}", sessionId);
                ResetInterruption(sessionId);
                break;

            case "start":
                if (root.TryGetProperty("start", out var startEl))
                {
                    var streamSid = startEl.TryGetProperty("streamSid", out var s) ? s.GetString() : "unknown";
                    var callSid = startEl.TryGetProperty("callSid", out var c) ? c.GetString() : "unknown";
                    _logger?.LogInformation("Call started. CallSid: {CallSid}, StreamSid: {StreamSid}", callSid, streamSid);
                }
                break;

            case "media":
                if (root.TryGetProperty("media", out var mediaEl) &&
                    mediaEl.TryGetProperty("payload", out var payloadEl))
                {
                    var base64MuLaw = payloadEl.GetString();
                    if (!string.IsNullOrEmpty(base64MuLaw))
                    {
                        var muLawBytes = Convert.FromBase64String(base64MuLaw);
                        var pcm8k = G711MuLawCodec.DecodeMuLawToPcm16(muLawBytes);
                        var pcm16k = G711MuLawCodec.Resample8kTo16k(pcm8k);

                        // Ingest into Jitter Buffer
                        var seq = _sequenceCounters.AddOrUpdate(sessionId, 1, (_, curr) => curr + 1);
                        _audioBridge.IngestPacket(sessionId, new RtpAudioPacket(
                            SequenceNumber: seq,
                            Timestamp: (uint)(seq * 160),
                            Payload: pcm16k,
                            ReceivedAtUtc: DateTime.UtcNow
                        ));

                        // Invoke consumer callback
                        await onAudioDecodedAsync(pcm16k);
                    }
                }
                break;

            case "mark":
                if (root.TryGetProperty("mark", out var markEl) &&
                    markEl.TryGetProperty("name", out var markName))
                {
                    _logger?.LogDebug("Mark received: {Mark} for session {SessionId}", markName.GetString(), sessionId);
                }
                break;

            case "stop":
                _logger?.LogInformation("Call stopped for session {SessionId}", sessionId);
                ClearSession(sessionId);
                break;
        }
    }

    public Task<string> CreateOutboundMediaJsonAsync(
        Guid sessionId,
        string streamSid,
        byte[] pcm16kAudio,
        string? markName = null)
    {
        // Resample 16k -> 8k and encode to mu-law
        var pcm8k = G711MuLawCodec.Resample16kTo8k(pcm16kAudio);
        var muLawBytes = G711MuLawCodec.EncodePcm16ToMuLaw(pcm8k);
        var base64Payload = Convert.ToBase64String(muLawBytes);

        var json = JsonSerializer.Serialize(new
        {
            @event = "media",
            streamSid = streamSid,
            media = new
            {
                payload = base64Payload
            }
        });

        return Task.FromResult(json);
    }

    public Task<string> CreateClearJsonAsync(string streamSid)
    {
        var json = JsonSerializer.Serialize(new
        {
            @event = "clear",
            streamSid = streamSid
        });
        return Task.FromResult(json);
    }

    public bool IsInterrupted(Guid sessionId)
    {
        return _interruptedFlags.TryGetValue(sessionId, out var interrupted) && interrupted;
    }

    public void NotifyInterruption(Guid sessionId)
    {
        _interruptedFlags[sessionId] = true;
        _logger?.LogInformation("Voice session {SessionId} marked interrupted (Barge-In triggered)", sessionId);
    }

    public void ResetInterruption(Guid sessionId)
    {
        _interruptedFlags[sessionId] = false;
    }

    public void ClearSession(Guid sessionId)
    {
        _interruptedFlags.TryRemove(sessionId, out _);
        _sequenceCounters.TryRemove(sessionId, out _);
        _audioBridge.ClearSession(sessionId);
    }
}
