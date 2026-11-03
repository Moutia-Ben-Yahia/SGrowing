using FluentAssertions;
using SGrowing.Api.Modules.Voice.SpeechStreamingMesh;
using Xunit;

namespace SGrowing.Tests.Voice;

public class SpeechStreamingMeshTests
{
    private readonly SpeechStreamingMeshEngine _engine = new();

    [Fact]
    public void GetOverview_ReturnsActiveSessionsAndBargeIns()
    {
        var overview = _engine.GetOverview();

        overview.Should().NotBeNull();
        overview.ActiveFullDuplexStreamsCount.Should().Be(2);
        overview.P99AudioLatencyMs.Should().BeLessThan(80.0);
        overview.BargeInCancellationLatencyMs.Should().BeLessThan(50.0);
        overview.ActiveSessions.Should().HaveCount(2);
        overview.RecentBargeIns.Should().HaveCount(1);
    }

    [Fact]
    public void ProcessAudioChunk_ExecutesSub80msS2sNeuralSynthesis()
    {
        var request = new ProcessAudioChunkRequest(
            "S2S-STREAM-901",
            "T1BVUzQ4a0JBU0U2NENIVU5LRk9STUFUPT0=",
            104,
            false
        );

        var result = _engine.ProcessAudioChunk(request);

        result.Should().NotBeNull();
        result.SessionId.Should().Be("S2S-STREAM-901");
        result.IngestedSequenceNumber.Should().Be(104);
        result.SynthesizedAudioChunkBase64.Should().NotBeNullOrEmpty();
        result.ChunkLatencyMs.Should().BeLessThan(80);
    }

    [Fact]
    public void TriggerBargeIn_CancelsTtsAudioInstantly()
    {
        var request = new TriggerBargeInRequest(
            "S2S-STREAM-901",
            -18.5,
            "00:01:24.450"
        );

        var receipt = _engine.TriggerBargeIn(request);

        receipt.Should().NotBeNull();
        receipt.ReceiptId.Should().StartWith("RCP-BARGE-");
        receipt.Status.Should().Be("AUDIO_PLAYBACK_DAMPENED_INSTANT");
        receipt.CutoffLatencyMs.Should().BeLessThan(50.0);
    }
}
