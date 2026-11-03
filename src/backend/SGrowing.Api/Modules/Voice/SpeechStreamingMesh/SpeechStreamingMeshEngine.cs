using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SGrowing.Api.Modules.Voice.SpeechStreamingMesh;

public sealed class SpeechStreamingMeshEngine
{
    private readonly ConcurrentDictionary<string, ActiveS2sSession> _sessions = new();
    private readonly ConcurrentDictionary<string, BargeInEventRecord> _bargeIns = new();
    private readonly ConcurrentBag<BargeInReceipt> _bargeInHistory = new();

    public SpeechStreamingMeshEngine()
    {
        InitializeDefaultData();
    }

    private void InitializeDefaultData()
    {
        var s1 = new ActiveS2sSession(
            "S2S-STREAM-901",
            "Opus 48kHz (Fullband stereo)",
            "Executive Sovereign British Female (Neural S2S)",
            68.5,
            12450,
            "STREAMING_ACTIVE"
        );

        var s2 = new ActiveS2sSession(
            "S2S-STREAM-902",
            "PCM16 24kHz",
            "Conversational Silicon Valley Male (Low-Jitter)",
            74.2,
            8320,
            "STREAMING_ACTIVE"
        );

        _sessions[s1.SessionId] = s1;
        _sessions[s2.SessionId] = s2;

        var b1 = new BargeInEventRecord(
            "EVT-BARGE-441",
            s1.SessionId,
            42.0,
            "Certainly, our SLA provides 99.999% up...",
            DateTime.UtcNow.AddMinutes(-3)
        );

        _bargeIns[b1.EventId] = b1;
    }

    public SpeechStreamingOverview GetOverview()
    {
        var sessionList = _sessions.Values.ToList();
        var bargeList = _bargeIns.Values.ToList();

        return new SpeechStreamingOverview(
            sessionList.Count,
            72.4,
            44.0,
            97.6,
            sessionList,
            bargeList
        );
    }

    public ProcessAudioChunkResult ProcessAudioChunk(ProcessAudioChunkRequest request)
    {
        var session = _sessions.TryGetValue(request.SessionId, out var s) ? s : _sessions.Values.First();

        var updated = session with { FramesProcessed = session.FramesProcessed + 1 };
        _sessions[session.SessionId] = updated;

        return new ProcessAudioChunkResult(
            session.SessionId,
            request.ChunkSequenceNumber,
            "T1BVUzQ4a0F1ZGlvU3ludGhDaHVuazk5ODg3Nw==", // Base64 chunk
            135.5,
            "CONFIDENT_COLLABORATIVE",
            64
        );
    }

    public BargeInReceipt TriggerBargeIn(TriggerBargeInRequest request)
    {
        var receipt = new BargeInReceipt(
            $"RCP-BARGE-{Guid.NewGuid():N}"[..10].ToUpperInvariant(),
            request.SessionId,
            "AUDIO_PLAYBACK_DAMPENED_INSTANT",
            38.5,
            DateTime.UtcNow
        );

        _bargeInHistory.Add(receipt);
        return receipt;
    }
}
