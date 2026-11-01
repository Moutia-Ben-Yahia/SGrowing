using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace SGrowing.Api.Modules.Voice.Streaming;

public enum AudioEncodingFormat
{
    Pcm16_16kHz = 1,
    G711u = 2,
    Pcm16_8kHz = 3
}

public static class AudioFrameConstants
{
    public const int DurationMsPerFrame = 20;

    // 16kHz 16-bit mono PCM: 16000 * 0.02s = 320 samples * 2 bytes = 640 bytes
    public const int Pcm16_16kHz_SampleRate = 16000;
    public const int Pcm16_16kHz_SamplesPerFrame = 320;
    public const int Pcm16_16kHz_FrameSizeBytes = 640;

    // 8kHz 8-bit G.711u: 8000 * 0.02s = 160 samples * 1 byte = 160 bytes
    public const int G711u_SampleRate = 8000;
    public const int G711u_SamplesPerFrame = 160;
    public const int G711u_FrameSizeBytes = 160;

    // 8kHz 16-bit mono PCM: 8000 * 0.02s = 160 samples * 2 bytes = 320 bytes
    public const int Pcm16_8kHz_SampleRate = 8000;
    public const int Pcm16_8kHz_SamplesPerFrame = 160;
    public const int Pcm16_8kHz_FrameSizeBytes = 320;

    public static int GetExpectedFrameSize(AudioEncodingFormat format) => format switch
    {
        AudioEncodingFormat.Pcm16_16kHz => Pcm16_16kHz_FrameSizeBytes,
        AudioEncodingFormat.G711u => G711u_FrameSizeBytes,
        AudioEncodingFormat.Pcm16_8kHz => Pcm16_8kHz_FrameSizeBytes,
        _ => Pcm16_16kHz_FrameSizeBytes
    };

    public static int GetSampleRate(AudioEncodingFormat format) => format switch
    {
        AudioEncodingFormat.Pcm16_16kHz => Pcm16_16kHz_SampleRate,
        AudioEncodingFormat.G711u => G711u_SampleRate,
        AudioEncodingFormat.Pcm16_8kHz => Pcm16_8kHz_SampleRate,
        _ => Pcm16_16kHz_SampleRate
    };
}

public record TurnaroundLatencyBreakdown(
    Guid TurnId,
    DateTime UserSpeechEndedUtc,
    DateTime VadSilenceDetectedUtc,
    DateTime LlmFirstTokenUtc,
    DateTime TtsFirstByteUtc,
    double VadLatencyMs,
    double LlmLatencyMs,
    double TtsLatencyMs,
    double TotalTurnaroundMs,
    bool IsSub500MsCompliant,
    string LatencyGrade
);

public class AudioRingBuffer
{
    private readonly byte[] _buffer;
    private readonly int _capacity;
    private readonly Lock _sync = new();
    private int _head;
    private int _tail;
    private int _count;

    public AudioRingBuffer(int capacityBytes = 64000)
    {
        _capacity = Math.Max(capacityBytes, 1024);
        _buffer = new byte[_capacity];
    }

    public int Capacity => _capacity;
    public int AvailableBytes
    {
        get
        {
            lock (_sync)
            {
                return _count;
            }
        }
    }

    public int FreeBytes
    {
        get
        {
            lock (_sync)
            {
                return _capacity - _count;
            }
        }
    }

    public int Write(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty) return 0;

        lock (_sync)
        {
            int toWrite = Math.Min(data.Length, _capacity - _count);
            if (toWrite <= 0) return 0;

            int firstChunk = Math.Min(toWrite, _capacity - _tail);
            data[..firstChunk].CopyTo(_buffer.AsSpan(_tail, firstChunk));

            int secondChunk = toWrite - firstChunk;
            if (secondChunk > 0)
            {
                data.Slice(firstChunk, secondChunk).CopyTo(_buffer.AsSpan(0, secondChunk));
            }

            _tail = (_tail + toWrite) % _capacity;
            _count += toWrite;
            return toWrite;
        }
    }

    public int Read(Span<byte> destination)
    {
        if (destination.IsEmpty) return 0;

        lock (_sync)
        {
            int toRead = Math.Min(destination.Length, _count);
            if (toRead <= 0) return 0;

            int firstChunk = Math.Min(toRead, _capacity - _head);
            _buffer.AsSpan(_head, firstChunk).CopyTo(destination[..firstChunk]);

            int secondChunk = toRead - firstChunk;
            if (secondChunk > 0)
            {
                _buffer.AsSpan(0, secondChunk).CopyTo(destination.Slice(firstChunk, secondChunk));
            }

            _head = (_head + toRead) % _capacity;
            _count -= toRead;
            return toRead;
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            _head = 0;
            _tail = 0;
            _count = 0;
        }
    }
}

public class LowLatencyAudioSession
{
    private readonly ConcurrentQueue<ReadOnlyMemory<byte>> _inboundQueue = new();
    private readonly ConcurrentQueue<ReadOnlyMemory<byte>> _outboundQueue = new();
    private readonly ConcurrentBag<TurnaroundLatencyBreakdown> _latencyMetrics = [];
    private long _totalFramesIngested;
    private long _totalFramesDispatched;
    private int _flushCount;

    public Guid SessionId { get; init; } = Guid.NewGuid();
    public AudioEncodingFormat Encoding { get; init; } = AudioEncodingFormat.Pcm16_16kHz;
    public AudioRingBuffer RingBuffer { get; }
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;

    public long TotalFramesIngested => Interlocked.Read(ref _totalFramesIngested);
    public long TotalFramesDispatched => Interlocked.Read(ref _totalFramesDispatched);
    public int FlushCount => Volatile.Read(ref _flushCount);

    public int InboundQueueLength => _inboundQueue.Count;
    public int OutboundQueueLength => _outboundQueue.Count;

    public LowLatencyAudioSession(Guid sessionId, AudioEncodingFormat encoding, int ringBufferCapacityBytes = 64000)
    {
        SessionId = sessionId;
        Encoding = encoding;
        RingBuffer = new AudioRingBuffer(ringBufferCapacityBytes);
    }

    public void EnqueueInbound(ReadOnlyMemory<byte> frame)
    {
        _inboundQueue.Enqueue(frame);
        RingBuffer.Write(frame.Span);
        Interlocked.Increment(ref _totalFramesIngested);
    }

    public bool TryDequeueInbound(out ReadOnlyMemory<byte> frame)
    {
        return _inboundQueue.TryDequeue(out frame);
    }

    public void EnqueueOutbound(ReadOnlyMemory<byte> audioChunk)
    {
        _outboundQueue.Enqueue(audioChunk);
        Interlocked.Increment(ref _totalFramesDispatched);
    }

    public bool TryDequeueOutbound(out ReadOnlyMemory<byte> audioChunk)
    {
        return _outboundQueue.TryDequeue(out audioChunk);
    }

    public (int FlushedChunks, int SavedAudioMs) FlushOutboundQueue()
    {
        int flushed = 0;
        int totalBytes = 0;

        while (_outboundQueue.TryDequeue(out var chunk))
        {
            flushed++;
            totalBytes += chunk.Length;
        }

        Interlocked.Increment(ref _flushCount);

        int frameBytes = AudioFrameConstants.GetExpectedFrameSize(Encoding);
        int savedFrames = frameBytes > 0 ? totalBytes / frameBytes : 0;
        int savedAudioMs = savedFrames * AudioFrameConstants.DurationMsPerFrame;

        return (flushed, savedAudioMs);
    }

    public void RecordLatency(TurnaroundLatencyBreakdown metric)
    {
        _latencyMetrics.Add(metric);
    }

    public IReadOnlyList<TurnaroundLatencyBreakdown> GetLatencyHistory() => _latencyMetrics.ToArray();
}

public interface ILowLatencyAudioStreamingEngine
{
    LowLatencyAudioSession GetOrCreateSession(Guid sessionId, AudioEncodingFormat encoding = AudioEncodingFormat.Pcm16_16kHz);
    bool TryGetSession(Guid sessionId, out LowLatencyAudioSession? session);
    bool RemoveSession(Guid sessionId);

    void EnqueueInboundFrame(Guid sessionId, ReadOnlyMemory<byte> frame);
    bool TryDequeueInboundFrame(Guid sessionId, out ReadOnlyMemory<byte> frame);

    void EnqueueOutboundChunk(Guid sessionId, ReadOnlyMemory<byte> audioChunk);
    bool TryDequeueOutboundChunk(Guid sessionId, out ReadOnlyMemory<byte> audioChunk);

    (int FlushedChunks, int SavedAudioMs) FlushOutboundQueue(Guid sessionId);

    TurnaroundLatencyBreakdown RecordTurnaroundTurn(
        Guid sessionId,
        Guid turnId,
        DateTime userSpeechEndedUtc,
        DateTime vadSilenceDetectedUtc,
        DateTime llmFirstTokenUtc,
        DateTime ttsFirstByteUtc);

    double CalculateRmsEnergy(ReadOnlySpan<byte> frame, AudioEncodingFormat encoding);
    byte[] Transcode(ReadOnlySpan<byte> source, AudioEncodingFormat from, AudioEncodingFormat to);
}

public class LowLatencyAudioStreamingEngine : ILowLatencyAudioStreamingEngine
{
    private readonly ConcurrentDictionary<Guid, LowLatencyAudioSession> _sessions = new();
    private readonly ILogger<LowLatencyAudioStreamingEngine>? _logger;

    public LowLatencyAudioStreamingEngine(ILogger<LowLatencyAudioStreamingEngine>? logger = null)
    {
        _logger = logger;
    }

    public LowLatencyAudioSession GetOrCreateSession(Guid sessionId, AudioEncodingFormat encoding = AudioEncodingFormat.Pcm16_16kHz)
    {
        return _sessions.GetOrAdd(sessionId, id => new LowLatencyAudioSession(id, encoding));
    }

    public bool TryGetSession(Guid sessionId, out LowLatencyAudioSession? session)
    {
        return _sessions.TryGetValue(sessionId, out session);
    }

    public bool RemoveSession(Guid sessionId)
    {
        return _sessions.TryRemove(sessionId, out _);
    }

    public void EnqueueInboundFrame(Guid sessionId, ReadOnlyMemory<byte> frame)
    {
        var session = GetOrCreateSession(sessionId);
        session.EnqueueInbound(frame);
    }

    public bool TryDequeueInboundFrame(Guid sessionId, out ReadOnlyMemory<byte> frame)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            return session.TryDequeueInbound(out frame);
        }
        frame = ReadOnlyMemory<byte>.Empty;
        return false;
    }

    public void EnqueueOutboundChunk(Guid sessionId, ReadOnlyMemory<byte> audioChunk)
    {
        var session = GetOrCreateSession(sessionId);
        session.EnqueueOutbound(audioChunk);
    }

    public bool TryDequeueOutboundChunk(Guid sessionId, out ReadOnlyMemory<byte> audioChunk)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            return session.TryDequeueOutbound(out audioChunk);
        }
        audioChunk = ReadOnlyMemory<byte>.Empty;
        return false;
    }

    public (int FlushedChunks, int SavedAudioMs) FlushOutboundQueue(Guid sessionId)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            var result = session.FlushOutboundQueue();
            _logger?.LogInformation("Outbound queue flushed for session {SessionId}: {Chunks} chunks, {SavedMs}ms saved audio.",
                sessionId, result.FlushedChunks, result.SavedAudioMs);
            return result;
        }
        return (0, 0);
    }

    public TurnaroundLatencyBreakdown RecordTurnaroundTurn(
        Guid sessionId,
        Guid turnId,
        DateTime userSpeechEndedUtc,
        DateTime vadSilenceDetectedUtc,
        DateTime llmFirstTokenUtc,
        DateTime ttsFirstByteUtc)
    {
        double vadMs = Math.Max(0, (vadSilenceDetectedUtc - userSpeechEndedUtc).TotalMilliseconds);
        double llmMs = Math.Max(0, (llmFirstTokenUtc - vadSilenceDetectedUtc).TotalMilliseconds);
        double ttsMs = Math.Max(0, (ttsFirstByteUtc - llmFirstTokenUtc).TotalMilliseconds);
        double totalMs = Math.Max(0, (ttsFirstByteUtc - userSpeechEndedUtc).TotalMilliseconds);

        bool isSub500 = totalMs < 500.0;
        string grade = totalMs switch
        {
            < 300.0 => "UltraFast",
            < 500.0 => "Sub500msCompliant",
            < 750.0 => "Acceptable",
            _ => "HighLatency"
        };

        var breakdown = new TurnaroundLatencyBreakdown(
            TurnId: turnId,
            UserSpeechEndedUtc: userSpeechEndedUtc,
            VadSilenceDetectedUtc: vadSilenceDetectedUtc,
            LlmFirstTokenUtc: llmFirstTokenUtc,
            TtsFirstByteUtc: ttsFirstByteUtc,
            VadLatencyMs: Math.Round(vadMs, 2),
            LlmLatencyMs: Math.Round(llmMs, 2),
            TtsLatencyMs: Math.Round(ttsMs, 2),
            TotalTurnaroundMs: Math.Round(totalMs, 2),
            IsSub500MsCompliant: isSub500,
            LatencyGrade: grade
        );

        if (_sessions.TryGetValue(sessionId, out var session))
        {
            session.RecordLatency(breakdown);
        }

        return breakdown;
    }

    public double CalculateRmsEnergy(ReadOnlySpan<byte> frame, AudioEncodingFormat encoding)
    {
        if (frame.IsEmpty) return 0.0;

        return encoding switch
        {
            AudioEncodingFormat.G711u => CalculateMuLawRms(frame),
            AudioEncodingFormat.Pcm16_16kHz or AudioEncodingFormat.Pcm16_8kHz => CalculatePcm16Rms(frame),
            _ => CalculatePcm16Rms(frame)
        };
    }

    public static double CalculateMuLawRms(ReadOnlySpan<byte> muLawBytes)
    {
        if (muLawBytes.IsEmpty) return 0.0;

        double sumSquares = 0.0;
        for (int i = 0; i < muLawBytes.Length; i++)
        {
            short pcm = MuLawToPcm16Sample(muLawBytes[i]);
            sumSquares += pcm * pcm;
        }

        return Math.Sqrt(sumSquares / muLawBytes.Length);
    }

    public static double CalculatePcm16Rms(ReadOnlySpan<byte> pcm16Bytes)
    {
        if (pcm16Bytes.Length < 2) return 0.0;

        int sampleCount = pcm16Bytes.Length / 2;
        double sumSquares = 0.0;

        for (int i = 0; i < sampleCount; i++)
        {
            short sample = BitConverter.ToInt16(pcm16Bytes.Slice(i * 2, 2));
            sumSquares += sample * sample;
        }

        return Math.Sqrt(sumSquares / sampleCount);
    }

    public static short MuLawToPcm16Sample(byte muLawByte)
    {
        byte mu = (byte)~muLawByte;
        int sign = mu & 0x80;
        int exponent = (mu >> 4) & 0x07;
        int mantissa = mu & 0x0F;

        int sample = ((mantissa << 3) + 0x84) << exponent;
        sample -= 0x84;

        return (short)(sign != 0 ? -sample : sample);
    }

    public static byte Pcm16ToMuLawSample(short pcm)
    {
        const int bias = 0x84;
        const int clip = 32635;

        int sign = (pcm >> 8) & 0x80;
        if (sign != 0) pcm = (short)-pcm;
        if (pcm > clip) pcm = clip;

        pcm = (short)(pcm + bias);
        int exponent = 7;
        for (int expMask = 0x4000; (pcm & expMask) == 0 && exponent > 0; expMask >>= 1)
        {
            exponent--;
        }

        int mantissa = (pcm >> (exponent + 3)) & 0x0F;
        byte muLawByte = (byte)~(sign | (exponent << 4) | mantissa);
        return muLawByte;
    }

    public byte[] Transcode(ReadOnlySpan<byte> source, AudioEncodingFormat from, AudioEncodingFormat to)
    {
        if (from == to) return source.ToArray();

        // G711u (8kHz) -> PCM-16 (8kHz or 16kHz)
        if (from == AudioEncodingFormat.G711u && to is AudioEncodingFormat.Pcm16_8kHz or AudioEncodingFormat.Pcm16_16kHz)
        {
            int sampleCount = source.Length;
            short[] pcm8k = new short[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                pcm8k[i] = MuLawToPcm16Sample(source[i]);
            }

            if (to == AudioEncodingFormat.Pcm16_8kHz)
            {
                byte[] output = new byte[sampleCount * 2];
                for (int i = 0; i < sampleCount; i++)
                {
                    BitConverter.TryWriteBytes(output.AsSpan(i * 2, 2), pcm8k[i]);
                }
                return output;
            }

            // Resample 8kHz -> 16kHz via linear interpolation (double the samples)
            byte[] output16k = new byte[sampleCount * 4];
            for (int i = 0; i < sampleCount; i++)
            {
                short s1 = pcm8k[i];
                short s2 = (i + 1 < sampleCount) ? pcm8k[i + 1] : s1;
                short interpolated = (short)((s1 + s2) / 2);

                BitConverter.TryWriteBytes(output16k.AsSpan(i * 4, 2), s1);
                BitConverter.TryWriteBytes(output16k.AsSpan(i * 4 + 2, 2), interpolated);
            }
            return output16k;
        }

        // PCM-16 (16kHz or 8kHz) -> G711u (8kHz)
        if (from is AudioEncodingFormat.Pcm16_16kHz or AudioEncodingFormat.Pcm16_8kHz && to == AudioEncodingFormat.G711u)
        {
            int sampleCount = source.Length / 2;
            short[] pcmSamples = new short[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                pcmSamples[i] = BitConverter.ToInt16(source.Slice(i * 2, 2));
            }

            if (from == AudioEncodingFormat.Pcm16_8kHz)
            {
                byte[] g711u = new byte[sampleCount];
                for (int i = 0; i < sampleCount; i++)
                {
                    g711u[i] = Pcm16ToMuLawSample(pcmSamples[i]);
                }
                return g711u;
            }

            // Decimate 16kHz -> 8kHz (take every 2nd sample)
            int decCount = sampleCount / 2;
            byte[] g711uDown = new byte[decCount];
            for (int i = 0; i < decCount; i++)
            {
                g711uDown[i] = Pcm16ToMuLawSample(pcmSamples[i * 2]);
            }
            return g711uDown;
        }

        return source.ToArray();
    }
}
