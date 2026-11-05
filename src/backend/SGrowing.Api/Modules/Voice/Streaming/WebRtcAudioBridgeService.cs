using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SGrowing.Api.Modules.Voice.Streaming;

public record RtpAudioPacket(
    uint SequenceNumber,
    uint Timestamp,
    byte[] Payload,
    DateTime ReceivedAtUtc
);

public record JitterBufferStats(
    int PacketsReceived,
    int PacketsDelivered,
    int PacketsDroppedLate,
    double CurrentJitterMs,
    double PacketLossRatio,
    bool FallbackToSipTriggered
);

public interface IWebRtcAudioBridgeService
{
    void IngestPacket(Guid sessionId, RtpAudioPacket packet);
    IReadOnlyList<RtpAudioPacket> DrainPlayablePackets(Guid sessionId, int maxPackets = 10);
    JitterBufferStats GetStats(Guid sessionId);
    bool ShouldFallbackToSipTrunk(Guid sessionId, double lossThreshold = 0.15);
    void ClearSession(Guid sessionId);
}

public class WebRtcAudioBridgeService : IWebRtcAudioBridgeService
{
    private class SessionBuffer
    {
        public readonly SortedDictionary<uint, RtpAudioPacket> Buffer = new();
        public readonly System.Threading.Lock Lock = new();
        public uint NextExpectedSequence = 0;
        public bool Initialized = false;
        public int TotalReceived = 0;
        public int TotalDelivered = 0;
        public int TotalDropped = 0;
        public double LastTransitTime = 0;
        public double EstimatedJitterMs = 0;
        public bool SipFallbackTriggered = false;
    }

    private readonly ConcurrentDictionary<Guid, SessionBuffer> _sessions = new();
    private readonly ILogger<WebRtcAudioBridgeService>? _logger;

    public WebRtcAudioBridgeService(ILogger<WebRtcAudioBridgeService>? logger = null)
    {
        _logger = logger;
    }

    public void IngestPacket(Guid sessionId, RtpAudioPacket packet)
    {
        var session = _sessions.GetOrAdd(sessionId, _ => new SessionBuffer());

        lock (session.Lock)
        {
            session.TotalReceived++;

            if (!session.Initialized)
            {
                session.NextExpectedSequence = packet.SequenceNumber;
                session.Initialized = true;
            }
            else if (session.TotalDelivered == 0 && packet.SequenceNumber < session.NextExpectedSequence)
            {
                // In initial buffering phase before any packet has been drained,
                // adjust expected start sequence down if earlier packets arrive out of order
                session.NextExpectedSequence = packet.SequenceNumber;
            }

            // Reject packets that are older than what has already been drained
            if (packet.SequenceNumber < session.NextExpectedSequence)
            {
                session.TotalDropped++;
                return;
            }

            // Calculate RFC 3550 jitter approximation
            var arrivalMs = packet.ReceivedAtUtc.Ticks / TimeSpan.TicksPerMillisecond;
            var transit = arrivalMs - (packet.Timestamp / 8); // Assuming 8kHz/16kHz sample clock
            var d = Math.Abs(transit - session.LastTransitTime);
            session.LastTransitTime = transit;
            session.EstimatedJitterMs += (d - session.EstimatedJitterMs) / 16.0;

            session.Buffer[packet.SequenceNumber] = packet;
        }
    }

    public IReadOnlyList<RtpAudioPacket> DrainPlayablePackets(Guid sessionId, int maxPackets = 10)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
            return Array.Empty<RtpAudioPacket>();

        var playable = new List<RtpAudioPacket>();

        lock (session.Lock)
        {
            while (playable.Count < maxPackets && session.Buffer.Count > 0)
            {
                var firstKey = session.Buffer.Keys.First();

                // If next packet is in order or gap is too large (drift rescue)
                if (firstKey == session.NextExpectedSequence || (firstKey > session.NextExpectedSequence && session.Buffer.Count > 5))
                {
                    playable.Add(session.Buffer[firstKey]);
                    session.Buffer.Remove(firstKey);
                    session.NextExpectedSequence = firstKey + 1;
                    session.TotalDelivered++;
                }
                else
                {
                    // Still waiting for lost/delayed packet
                    break;
                }
            }
        }

        return playable;
    }

    public JitterBufferStats GetStats(Guid sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            return new JitterBufferStats(0, 0, 0, 0, 0, false);
        }

        lock (session.Lock)
        {
            var loss = session.TotalReceived > 0
                ? (double)session.TotalDropped / session.TotalReceived
                : 0.0;

            return new JitterBufferStats(
                PacketsReceived: session.TotalReceived,
                PacketsDelivered: session.TotalDelivered,
                PacketsDroppedLate: session.TotalDropped,
                CurrentJitterMs: Math.Round(session.EstimatedJitterMs, 2),
                PacketLossRatio: Math.Round(loss, 4),
                FallbackToSipTriggered: session.SipFallbackTriggered);
        }
    }

    public bool ShouldFallbackToSipTrunk(Guid sessionId, double lossThreshold = 0.15)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
            return false;

        lock (session.Lock)
        {
            if (session.TotalReceived < 20) return false;

            var loss = (double)session.TotalDropped / session.TotalReceived;
            if (loss >= lossThreshold && !session.SipFallbackTriggered)
            {
                session.SipFallbackTriggered = true;
                _logger?.LogWarning("WebRTC packet loss ({Loss:P1}) exceeded threshold ({Threshold:P1}) for session {SessionId}. Triggering SIP trunk fallback.", loss, lossThreshold, sessionId);
                return true;
            }

            return session.SipFallbackTriggered;
        }
    }

    public void ClearSession(Guid sessionId)
    {
        _sessions.TryRemove(sessionId, out _);
    }
}
