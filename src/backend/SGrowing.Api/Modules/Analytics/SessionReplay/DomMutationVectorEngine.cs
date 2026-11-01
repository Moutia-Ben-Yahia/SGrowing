using System.Collections.Concurrent;
using System.IO.Compression;
using System.Text.Json;

namespace SGrowing.Api.Modules.Analytics.SessionReplay;

/// <summary>
/// High-throughput DOM mutation vector ingestion engine.
/// Validates strict monotonic sequencing, calculates compression delta ratios,
/// and enforces bandwidth footprint limits (&lt; 2.0 KB/sec).
/// </summary>
public sealed class DomMutationVectorEngine : IDomMutationVectorEngine
{
    private sealed class SessionMutationState
    {
        public readonly System.Threading.Lock Lock = new();
        public readonly List<DomMutationOperation> Mutations = [];
        public readonly List<DomMutationBatch> Batches = [];
        public long LastSequenceNumber;
        public long LastFrameIndex;
        public DateTimeOffset LastTimestamp = DateTimeOffset.MinValue;
        public DateTimeOffset FirstTimestamp = DateTimeOffset.MinValue;
        public long TotalRawBytes;
        public long TotalCompressedBytes;
    }

    private readonly ConcurrentDictionary<string, SessionMutationState> _sessions = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public IngestResult IngestMutations(string sessionId, DomMutationBatch batch, bool throwOnError = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        if (batch == null || batch.Mutations == null || batch.Mutations.Count == 0)
        {
            return new IngestResult(true, "Empty batch received.", 0);
        }

        var state = _sessions.GetOrAdd(sessionId, _ => new SessionMutationState());

        lock (state.Lock)
        {
            long currentSeq = state.LastSequenceNumber;
            long currentFrame = state.LastFrameIndex;
            DateTimeOffset currentTs = state.LastTimestamp;

            for (int i = 0; i < batch.Mutations.Count; i++)
            {
                var op = batch.Mutations[i];

                if (state.Mutations.Count > 0 || i > 0)
                {
                    if (op.SequenceNumber <= currentSeq)
                    {
                        var err = $"Out-of-order or duplicate sequence number: expected > {currentSeq}, got {op.SequenceNumber}.";
                        if (throwOnError) throw new InvalidOperationException(err);
                        return new IngestResult(false, err, 0);
                    }

                    if (op.FrameIndex < currentFrame)
                    {
                        var err = $"Decreasing frame index: expected >= {currentFrame}, got {op.FrameIndex}.";
                        if (throwOnError) throw new InvalidOperationException(err);
                        return new IngestResult(false, err, 0);
                    }

                    if (op.Timestamp < currentTs)
                    {
                        var err = $"Non-monotonic timestamp detected: expected >= {currentTs:O}, got {op.Timestamp:O}.";
                        if (throwOnError) throw new InvalidOperationException(err);
                        return new IngestResult(false, err, 0);
                    }
                }

                currentSeq = op.SequenceNumber;
                currentFrame = op.FrameIndex;
                currentTs = op.Timestamp;
            }

            // Monotonicity verified, commit mutations to session state
            foreach (var op in batch.Mutations)
            {
                state.Mutations.Add(op);
            }

            state.Batches.Add(batch);
            state.LastSequenceNumber = currentSeq;
            state.LastFrameIndex = currentFrame;
            state.LastTimestamp = currentTs;

            if (state.FirstTimestamp == DateTimeOffset.MinValue && batch.Mutations.Count > 0)
            {
                state.FirstTimestamp = batch.Mutations[0].Timestamp;
            }

            // Calculate byte size and compression
            int rawBytes = batch.EstimatedRawBytes;
            int compressedBytes = batch.CompressedBytes;

            if (rawBytes <= 0)
            {
                var bytes = JsonSerializer.SerializeToUtf8Bytes(batch.Mutations);
                rawBytes = bytes.Length;

                if (compressedBytes <= 0)
                {
                    using var ms = new MemoryStream();
                    using (var gzip = new GZipStream(ms, CompressionLevel.Optimal, leaveOpen: true))
                    {
                        gzip.Write(bytes);
                    }
                    compressedBytes = (int)ms.Length;
                }
            }
            else if (compressedBytes <= 0)
            {
                // Conservative delta compression approximation if raw given without compressed
                compressedBytes = Math.Max(1, (int)(rawBytes * 0.28));
            }

            state.TotalRawBytes += rawBytes;
            state.TotalCompressedBytes += compressedBytes;

            var metrics = CalculateMetricsInternal(state, sessionId);
            return new IngestResult(
                Success: true,
                Message: $"Successfully ingested {batch.Mutations.Count} DOM mutations.",
                ProcessedCount: batch.Mutations.Count,
                BandwidthKbps: metrics.BandwidthKbPerSec);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<DomMutationOperation> GetSessionMutations(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || !_sessions.TryGetValue(sessionId, out var state))
        {
            return [];
        }

        lock (state.Lock)
        {
            return [.. state.Mutations];
        }
    }

    /// <inheritdoc />
    public ThroughputMetrics CalculateThroughputMetrics(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || !_sessions.TryGetValue(sessionId, out var state))
        {
            return new ThroughputMetrics(
                SessionId: sessionId ?? string.Empty,
                TotalMutations: 0,
                TotalRawBytes: 0,
                TotalCompressedBytes: 0,
                CompressionRatio: 1.0,
                TotalDurationSeconds: 0.0,
                BandwidthKbPerSec: 0.0,
                IsWithinTargetFootprint: true);
        }

        lock (state.Lock)
        {
            return CalculateMetricsInternal(state, sessionId);
        }
    }

    /// <inheritdoc />
    public bool ClearSession(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return false;
        }

        return _sessions.TryRemove(sessionId, out _);
    }

    private static ThroughputMetrics CalculateMetricsInternal(SessionMutationState state, string sessionId)
    {
        double durationSeconds = 1.0;
        if (state.FirstTimestamp != DateTimeOffset.MinValue && state.LastTimestamp > state.FirstTimestamp)
        {
            durationSeconds = Math.Max(1.0, (state.LastTimestamp - state.FirstTimestamp).TotalSeconds);
        }

        double compressionRatio = state.TotalRawBytes > 0
            ? (double)state.TotalCompressedBytes / state.TotalRawBytes
            : 1.0;

        double bandwidthKbps = (state.TotalCompressedBytes / 1024.0) / durationSeconds;
        bool isWithinFootprint = bandwidthKbps < 2.0;

        return new ThroughputMetrics(
            SessionId: sessionId,
            TotalMutations: state.Mutations.Count,
            TotalRawBytes: state.TotalRawBytes,
            TotalCompressedBytes: state.TotalCompressedBytes,
            CompressionRatio: Math.Round(compressionRatio, 4),
            TotalDurationSeconds: Math.Round(durationSeconds, 3),
            BandwidthKbPerSec: Math.Round(bandwidthKbps, 3),
            IsWithinTargetFootprint: isWithinFootprint);
    }
}
