using System;
using System.Text.Json;
using System.Threading.Tasks;
using SGrowing.Api.Modules.Voice.Streaming;
using Xunit;

namespace SGrowing.Tests.Unit;

public class TwilioMediaStreamBridgeTests
{
    private readonly WebRtcAudioBridgeService _audioBridge = new();
    private readonly TwilioMediaStreamBridgeService _bridge;

    public TwilioMediaStreamBridgeTests()
    {
        _bridge = new TwilioMediaStreamBridgeService(_audioBridge);
    }

    [Fact]
    public void G711MuLawCodec_RoundTrip_PreservesSignalIntegrity()
    {
        // 16-bit PCM test buffer
        short[] originalSamples = new short[] { 0, 1000, -1000, 16000, -16000, 32000, -32000 };
        byte[] pcmBytes = new byte[originalSamples.Length * 2];
        for (int i = 0; i < originalSamples.Length; i++)
        {
            var b = BitConverter.GetBytes(originalSamples[i]);
            pcmBytes[i * 2] = b[0];
            pcmBytes[i * 2 + 1] = b[1];
        }

        // Encode to mu-law
        byte[] muLawBytes = G711MuLawCodec.EncodePcm16ToMuLaw(pcmBytes);
        Assert.Equal(originalSamples.Length, muLawBytes.Length);

        // Decode back to 16-bit PCM
        byte[] decodedPcm = G711MuLawCodec.DecodeMuLawToPcm16(muLawBytes);
        Assert.Equal(pcmBytes.Length, decodedPcm.Length);

        // Verify logarithmic tolerance within typical mu-law quantization error (~5%)
        for (int i = 0; i < originalSamples.Length; i++)
        {
            short decoded = BitConverter.ToInt16(decodedPcm, i * 2);
            short orig = originalSamples[i];
            if (orig == 0)
            {
                Assert.InRange(decoded, -100, 100);
            }
            else
            {
                double diff = Math.Abs(decoded - orig);
                double relError = diff / Math.Abs((double)orig);
                Assert.True(relError < 0.15, $"Sample {i}: orig={orig}, decoded={decoded}, relError={relError}");
            }
        }
    }

    [Fact]
    public void G711MuLawCodec_Resample_ExpandsAndCompressesAccurately()
    {
        byte[] pcm8k = new byte[320]; // 160 samples (20ms at 8kHz)
        for (int i = 0; i < 160; i++)
        {
            short sample = (short)(Math.Sin(2 * Math.PI * i / 20.0) * 10000);
            var b = BitConverter.GetBytes(sample);
            pcm8k[i * 2] = b[0];
            pcm8k[i * 2 + 1] = b[1];
        }

        // Resample up to 16kHz
        byte[] pcm16k = G711MuLawCodec.Resample8kTo16k(pcm8k);
        Assert.Equal(640, pcm16k.Length); // 320 samples (20ms at 16kHz)

        // Resample back down to 8kHz
        byte[] resampled8k = G711MuLawCodec.Resample16kTo8k(pcm16k);
        Assert.Equal(320, resampled8k.Length);
    }

    [Fact]
    public async Task HandleInboundMessageAsync_ProcessesMediaAndInvokesCallback()
    {
        var sessionId = Guid.NewGuid();
        byte[] receivedAudio = Array.Empty<byte>();

        // Generate synthetic mu-law base64 chunk
        byte[] fakeMuLaw = new byte[160];
        Array.Fill(fakeMuLaw, (byte)0xFF); // silence in mu-law
        var base64 = Convert.ToBase64String(fakeMuLaw);

        var json = $"{{\"event\":\"media\",\"streamSid\":\"MZ123\",\"media\":{{\"payload\":\"{base64}\"}}}}";

        await _bridge.HandleInboundMessageAsync(sessionId, json, pcm =>
        {
            receivedAudio = pcm;
            return Task.CompletedTask;
        });

        Assert.NotEmpty(receivedAudio);
        Assert.Equal(640, receivedAudio.Length); // 160 mu-law samples -> 160 pcm8k samples -> 320 pcm16k samples (640 bytes)
    }

    [Fact]
    public async Task OutboundMediaAndBargeIn_CreatesValidTwilioFrames()
    {
        var sessionId = Guid.NewGuid();
        var streamSid = "MZ999";

        // Outbound media JSON
        byte[] pcm16k = new byte[640];
        var mediaJson = await _bridge.CreateOutboundMediaJsonAsync(sessionId, streamSid, pcm16k);

        using var doc = JsonDocument.Parse(mediaJson);
        Assert.Equal("media", doc.RootElement.GetProperty("event").GetString());
        Assert.Equal(streamSid, doc.RootElement.GetProperty("streamSid").GetString());
        Assert.True(doc.RootElement.GetProperty("media").TryGetProperty("payload", out var payload));
        Assert.False(string.IsNullOrEmpty(payload.GetString()));

        // Interruption handling
        Assert.False(_bridge.IsInterrupted(sessionId));
        _bridge.NotifyInterruption(sessionId);
        Assert.True(_bridge.IsInterrupted(sessionId));

        var clearJson = await _bridge.CreateClearJsonAsync(streamSid);
        using var clearDoc = JsonDocument.Parse(clearJson);
        Assert.Equal("clear", clearDoc.RootElement.GetProperty("event").GetString());
        Assert.Equal(streamSid, clearDoc.RootElement.GetProperty("streamSid").GetString());

        _bridge.ResetInterruption(sessionId);
        Assert.False(_bridge.IsInterrupted(sessionId));
    }
}
