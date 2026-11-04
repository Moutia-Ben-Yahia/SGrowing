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

    [Fact]
    public void ProcessAudioChunk_IncrementsSessionFramesProcessed()
    {
        var sessionBefore = _engine.GetOverview().ActiveSessions.First(s => s.SessionId == "S2S-STREAM-902");
        var initialFrames = sessionBefore.FramesProcessed;

        var request = new ProcessAudioChunkRequest(
            "S2S-STREAM-902",
            "T1BVUzQ4a0JBU0U2NENIVU5LRk9STUFUPT0=",
            201,
            false
        );

        var result = _engine.ProcessAudioChunk(request);

        result.Should().NotBeNull();
        result.SessionId.Should().Be("S2S-STREAM-902");

        var sessionAfter = _engine.GetOverview().ActiveSessions.First(s => s.SessionId == "S2S-STREAM-902");
        sessionAfter.FramesProcessed.Should().Be(initialFrames + 1);
    }

    [Fact]
    public void ProcessAudioChunk_UnknownSession_FallsBackToFirstSession()
    {
        var request = new ProcessAudioChunkRequest(
            "SESSION-NONEXISTENT-777",
            "T1BVUzQ4a0JBU0U2NENIVU5LRk9STUFUPT0=",
            1,
            true
        );

        var result = _engine.ProcessAudioChunk(request);

        result.Should().NotBeNull();
        result.SessionId.Should().NotBeNullOrEmpty();
        result.SynthesizedAudioChunkBase64.Should().NotBeNullOrEmpty();
        result.EmotionTone.Should().Be("CONFIDENT_COLLABORATIVE");
    }

    [Fact]
    public void TriggerBargeIn_MultipleInvocations_RecordsReceiptsWithCorrectStatusAndCutoff()
    {
        var req1 = new TriggerBargeInRequest("S2S-STREAM-901", -22.0, "00:00:15.100");
        var req2 = new TriggerBargeInRequest("S2S-STREAM-902", -19.5, "00:00:28.350");

        var receipt1 = _engine.TriggerBargeIn(req1);
        var receipt2 = _engine.TriggerBargeIn(req2);

        receipt1.SessionId.Should().Be("S2S-STREAM-901");
        receipt2.SessionId.Should().Be("S2S-STREAM-902");
        receipt1.Status.Should().Be("AUDIO_PLAYBACK_DAMPENED_INSTANT");
        receipt2.Status.Should().Be("AUDIO_PLAYBACK_DAMPENED_INSTANT");
        receipt1.CutoffLatencyMs.Should().Be(38.5);
        receipt2.CutoffLatencyMs.Should().Be(38.5);
    }

    [Fact]
    public void ProcessAudioChunk_ConsecutiveChunks_IncrementsFramesSequentially()
    {
        var session = _engine.GetOverview().ActiveSessions.First(s => s.SessionId == "S2S-STREAM-901");
        var initialFrames = session.FramesProcessed;

        _engine.ProcessAudioChunk(new ProcessAudioChunkRequest("S2S-STREAM-901", "CHUNK1", 1, false));
        _engine.ProcessAudioChunk(new ProcessAudioChunkRequest("S2S-STREAM-901", "CHUNK2", 2, false));
        _engine.ProcessAudioChunk(new ProcessAudioChunkRequest("S2S-STREAM-901", "CHUNK3", 3, false));

        var updated = _engine.GetOverview().ActiveSessions.First(s => s.SessionId == "S2S-STREAM-901");
        updated.FramesProcessed.Should().Be(initialFrames + 3);
    }

    [Fact]
    public void TriggerBargeIn_WithDecibelThresholdAndAudioTimestamp_ReturnsValidReceiptWithSession()
    {
        var request = new TriggerBargeInRequest("S2S-STREAM-902", -15.0, "00:02:10.800");
        var receipt = _engine.TriggerBargeIn(request);

        receipt.Should().NotBeNull();
        receipt.SessionId.Should().Be("S2S-STREAM-902");
        receipt.ReceiptId.Should().StartWith("RCP-BARGE-");
        receipt.CutoffLatencyMs.Should().Be(38.5);
    }
}
