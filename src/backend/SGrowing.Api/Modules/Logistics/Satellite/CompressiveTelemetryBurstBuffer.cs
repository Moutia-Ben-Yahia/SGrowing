using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SGrowing.Api.Modules.Logistics.Satellite;

/// <summary>
/// High-density telemetry compressor and priority ring buffer engineered for satellite SBD
/// constraints, Delta-RLE binary compaction, Deflate compression, and Head-of-Line preemption.
/// </summary>
public sealed class CompressiveTelemetryBurstBuffer : ICompressiveTelemetryBurstBuffer
{
    private const byte Magic0 = 0x53; // 'S'
    private const byte Magic1 = 0x47; // 'G'
    private const byte FormatVersion = 1;

    private readonly Lock _lock = new();
    private readonly List<CompressedTelemetryFrame> _frames = [];
    private long _evictedFramesCount;

    public int Capacity { get; }

    public CompressiveTelemetryBurstBuffer(int capacity = 500)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be greater than zero.");
        }
        Capacity = capacity;
    }

    /// <summary>
    /// Compresses a single sensor reading into a high-density frame.
    /// </summary>
    public CompressedTelemetryFrame CompressReading(SensorTelemetryReading reading, TimeSpan? customTtl = null)
    {
        ArgumentNullException.ThrowIfNull(reading);
        return CompressBatch([reading], reading.Priority, customTtl);
    }

    /// <summary>
    /// Compresses a batch of sensor readings into an ultra-dense satellite frame using Delta-RLE and Deflate.
    /// </summary>
    public CompressedTelemetryFrame CompressBatch(
        IReadOnlyList<SensorTelemetryReading> readings,
        TelemetryPriority? overridePriority = null,
        TimeSpan? customTtl = null)
    {
        if (readings == null || readings.Count == 0)
        {
            throw new ArgumentException("Readings batch cannot be null or empty.", nameof(readings));
        }

        var primary = readings[0];
        var priority = overridePriority ?? readings.Max(r => r.Priority);
        var assetId = primary.AssetId;
        var timestampUtc = primary.TimestampUtc;

        // Baseline JSON representation length to evaluate compression ratio
        var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(readings);
        var originalBytesLength = jsonBytes.Length;

        // Perform Delta-RLE binary compaction
        byte[] uncompressedCompactedBytes;
        using (var mem = new MemoryStream())
        {
            using (var writer = new BinaryWriter(mem, Encoding.UTF8, leaveOpen: true))
            {
                writer.Write(Magic0);
                writer.Write(Magic1);
                writer.Write(FormatVersion);
                writer.Write((byte)priority);
                writer.Write(assetId);
                writer.Write((ushort)readings.Count);

                // Write Reading IDs and priorities
                for (int i = 0; i < readings.Count; i++)
                {
                    writer.Write(readings[i].ReadingId);
                    writer.Write((byte)readings[i].Priority);
                }

                // Columnar Delta-RLE: Time offsets (seconds from base)
                var baseEpochSec = new DateTimeOffset(readings[0].TimestampUtc).ToUnixTimeSeconds();
                writer.Write(baseEpochSec);
                var timeOffsets = readings.Select(r => (int)(new DateTimeOffset(r.TimestampUtc).ToUnixTimeSeconds() - baseEpochSec)).ToList();
                WriteDeltaRleInt32(writer, timeOffsets);

                // Columnar Delta-RLE: Latitudes (microdegrees)
                var lats = readings.Select(r => (int)Math.Round(r.Latitude * 1_000_000)).ToList();
                WriteDeltaRleInt32(writer, lats);

                // Columnar Delta-RLE: Longitudes (microdegrees)
                var lons = readings.Select(r => (int)Math.Round(r.Longitude * 1_000_000)).ToList();
                WriteDeltaRleInt32(writer, lons);

                // Columnar Delta-RLE: Altitude (decimeters)
                var alts = readings.Select(r => (int)Math.Round(r.AltitudeMeters * 10)).ToList();
                WriteDeltaRleInt32(writer, alts);

                // Columnar Delta-RLE: Temperature (centidegrees)
                var temps = readings.Select(r => (int)Math.Round(r.TemperatureCelsius * 100)).ToList();
                WriteDeltaRleInt32(writer, temps);

                // Columnar Delta-RLE: Humidity (centipercent)
                var hums = readings.Select(r => (int)Math.Round(r.HumidityPct * 100)).ToList();
                WriteDeltaRleInt32(writer, hums);

                // Columnar Delta-RLE: Battery (centipercent)
                var bats = readings.Select(r => (int)Math.Round(r.BatteryPct * 100)).ToList();
                WriteDeltaRleInt32(writer, bats);

                // Columnar Delta-RLE: Shock (centi-G)
                var shocks = readings.Select(r => (int)Math.Round(r.ShockGForce * 100)).ToList();
                WriteDeltaRleInt32(writer, shocks);
            }
            uncompressedCompactedBytes = mem.ToArray();
        }

        // Apply Deflate compression for maximal entropy reduction
        byte[] compressedBytes;
        using (var outputStream = new MemoryStream())
        {
            using (var deflate = new DeflateStream(outputStream, CompressionLevel.SmallestSize, leaveOpen: true))
            {
                deflate.Write(uncompressedCompactedBytes, 0, uncompressedCompactedBytes.Length);
            }
            compressedBytes = outputStream.ToArray();
        }

        var compressedBytesLength = compressedBytes.Length;
        var ratio = Math.Max(0.0, (1.0 - ((double)compressedBytesLength / originalBytesLength)) * 100.0);
        var checksum = Convert.ToHexString(SHA256.HashData(compressedBytes));
        var base64 = Convert.ToBase64String(compressedBytes);

        var ttl = customTtl ?? (priority switch
        {
            TelemetryPriority.EmergencySos => TimeSpan.FromDays(7),
            TelemetryPriority.CriticalAlarmThermalBreach => TimeSpan.FromDays(3),
            TelemetryPriority.HighValueAsset => TimeSpan.FromHours(48),
            _ => TimeSpan.FromHours(24)
        });

        var frameId = $"FRM-{Guid.NewGuid():N}"[..16].ToUpperInvariant();

        return new CompressedTelemetryFrame(
            FrameId: frameId,
            AssetId: assetId,
            TimestampUtc: timestampUtc,
            OriginalBytesLength: originalBytesLength,
            CompressedBytesLength: compressedBytesLength,
            CompressionRatioPercent: Math.Round(ratio, 2),
            CompressedData: compressedBytes,
            PayloadBase64: base64,
            Priority: priority,
            EncodingType: "DeltaRLE+Deflate-v1",
            ChecksumSha256: checksum,
            ExpiresAtUtc: DateTime.UtcNow.Add(ttl)
        );
    }

    /// <summary>
    /// Decompresses a frame back into original sensor readings (lossless verification).
    /// </summary>
    public IReadOnlyList<SensorTelemetryReading> DecompressFrame(CompressedTelemetryFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        // Verify SHA256 integrity
        var actualChecksum = Convert.ToHexString(SHA256.HashData(frame.CompressedData));
        if (!string.Equals(actualChecksum, frame.ChecksumSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Compressed frame checksum verification failed - payload corrupted.");
        }

        byte[] uncompressedBytes;
        using (var inputStream = new MemoryStream(frame.CompressedData))
        using (var deflate = new DeflateStream(inputStream, CompressionMode.Decompress))
        using (var outStream = new MemoryStream())
        {
            deflate.CopyTo(outStream);
            uncompressedBytes = outStream.ToArray();
        }

        using var mem = new MemoryStream(uncompressedBytes);
        using var reader = new BinaryReader(mem, Encoding.UTF8);

        var m0 = reader.ReadByte();
        var m1 = reader.ReadByte();
        if (m0 != Magic0 || m1 != Magic1)
        {
            throw new FormatException("Invalid frame magic header.");
        }

        var version = reader.ReadByte();
        if (version != FormatVersion)
        {
            throw new NotSupportedException($"Unsupported frame version {version}.");
        }

        _ = (TelemetryPriority)reader.ReadByte();
        var assetId = reader.ReadString();
        var count = reader.ReadUInt16();

        var readingIds = new List<string>(count);
        var priorities = new List<TelemetryPriority>(count);
        for (int i = 0; i < count; i++)
        {
            readingIds.Add(reader.ReadString());
            priorities.Add((TelemetryPriority)reader.ReadByte());
        }

        // Base time & time offsets
        var baseEpochSec = reader.ReadInt64();
        var timeOffsets = ReadDeltaRleInt32(reader, count);
        var lats = ReadDeltaRleInt32(reader, count);
        var lons = ReadDeltaRleInt32(reader, count);
        var alts = ReadDeltaRleInt32(reader, count);
        var temps = ReadDeltaRleInt32(reader, count);
        var hums = ReadDeltaRleInt32(reader, count);
        var bats = ReadDeltaRleInt32(reader, count);
        var shocks = ReadDeltaRleInt32(reader, count);

        var readings = new List<SensorTelemetryReading>(count);
        for (int i = 0; i < count; i++)
        {
            var timeSec = baseEpochSec + timeOffsets[i];
            var timestamp = DateTimeOffset.FromUnixTimeSeconds(timeSec).UtcDateTime;

            readings.Add(new SensorTelemetryReading(
                ReadingId: readingIds[i],
                AssetId: assetId,
                TimestampUtc: timestamp,
                Latitude: Math.Round(lats[i] / 1_000_000.0, 6),
                Longitude: Math.Round(lons[i] / 1_000_000.0, 6),
                AltitudeMeters: Math.Round(alts[i] / 10.0, 1),
                TemperatureCelsius: Math.Round(temps[i] / 100.0, 2),
                HumidityPct: Math.Round(hums[i] / 100.0, 2),
                BatteryPct: Math.Round(bats[i] / 100.0, 2),
                ShockGForce: Math.Round(shocks[i] / 100.0, 2),
                Priority: priorities[i]
            ));
        }

        return readings;
    }

    /// <summary>
    /// Enqueues a telemetry reading into the priority ring buffer with head-of-line preemption.
    /// </summary>
    public CompressedTelemetryFrame Enqueue(SensorTelemetryReading reading, TimeSpan? customTtl = null)
    {
        var frame = CompressReading(reading, customTtl);
        EnqueueFrame(frame);
        return frame;
    }

    /// <summary>
    /// Enqueues an already compressed frame into the buffer.
    /// </summary>
    public bool EnqueueFrame(CompressedTelemetryFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        lock (_lock)
        {
            if (_frames.Count >= Capacity)
            {
                PurgeExpiredInternal(DateTime.UtcNow);
            }

            if (_frames.Count >= Capacity)
            {
                // Try to evict the lowest priority item if the new frame is of higher or equal priority to evictable
                var lowestPriority = _frames.Min(f => f.Priority);
                if (frame.Priority > lowestPriority)
                {
                    // Evict oldest item of lowest priority
                    var evictCandidate = _frames
                        .Where(f => f.Priority == lowestPriority)
                        .OrderBy(f => f.TimestampUtc)
                        .First();

                    _frames.Remove(evictCandidate);
                    _evictedFramesCount++;
                }
                else if (frame.Priority == lowestPriority && lowestPriority == TelemetryPriority.RoutinePeriodic)
                {
                    // For routine periodic, roll over oldest routine frame
                    var oldestRoutine = _frames
                        .Where(f => f.Priority == TelemetryPriority.RoutinePeriodic)
                        .OrderBy(f => f.TimestampUtc)
                        .First();

                    _frames.Remove(oldestRoutine);
                    _evictedFramesCount++;
                }
                else
                {
                    // Buffer is full of higher priority frames; drop this lower priority frame
                    _evictedFramesCount++;
                    return false;
                }
            }

            // Head-of-line insertion: sorted by Priority DESC, then TimestampUtc ASC
            int insertIndex = _frames.FindIndex(f =>
                f.Priority < frame.Priority ||
                (f.Priority == frame.Priority && f.TimestampUtc > frame.TimestampUtc));

            if (insertIndex < 0)
            {
                _frames.Add(frame);
            }
            else
            {
                _frames.Insert(insertIndex, frame);
            }

            return true;
        }
    }

    /// <summary>
    /// Dequeues the highest priority head-of-line frame.
    /// </summary>
    public bool TryDequeue(out CompressedTelemetryFrame? frame)
    {
        lock (_lock)
        {
            PurgeExpiredInternal(DateTime.UtcNow);

            if (_frames.Count == 0)
            {
                frame = null;
                return false;
            }

            frame = _frames[0];
            _frames.RemoveAt(0);
            return true;
        }
    }

    /// <summary>
    /// Peeks at the highest priority head-of-line frame without removing it.
    /// </summary>
    public bool TryPeek(out CompressedTelemetryFrame? frame)
    {
        lock (_lock)
        {
            PurgeExpiredInternal(DateTime.UtcNow);

            if (_frames.Count == 0)
            {
                frame = null;
                return false;
            }

            frame = _frames[0];
            return true;
        }
    }

    /// <summary>
    /// Returns current queue metrics and priority breakdown.
    /// </summary>
    public SatelliteQueueStatus GetStatus()
    {
        lock (_lock)
        {
            PurgeExpiredInternal(DateTime.UtcNow);

            var emergency = _frames.Count(f => f.Priority == TelemetryPriority.EmergencySos);
            var critical = _frames.Count(f => f.Priority == TelemetryPriority.CriticalAlarmThermalBreach);
            var highValue = _frames.Count(f => f.Priority == TelemetryPriority.HighValueAsset);
            var routine = _frames.Count(f => f.Priority == TelemetryPriority.RoutinePeriodic);
            var oldest = _frames.Count > 0 ? _frames.Min(f => f.TimestampUtc) : (DateTime?)null;
            var avgRatio = _frames.Count > 0 ? _frames.Average(f => f.CompressionRatioPercent) : 0.0;

            return new SatelliteQueueStatus(
                TotalQueued: _frames.Count,
                EmergencyCount: emergency,
                CriticalThermalBreachCount: critical,
                HighValueCount: highValue,
                RoutineCount: routine,
                RingBufferCapacity: Capacity,
                EvictedFramesCount: _evictedFramesCount,
                OldestQueuedUtc: oldest,
                NextScheduledPassUtc: null,
                ActiveConstellations: [
                    ConstellationType.IridiumShortBurstData,
                    ConstellationType.StarlinkDirectToCell,
                    ConstellationType.AstSpaceMobile3GppNtn
                ],
                AverageCompressionRatioPct: Math.Round(avgRatio, 2)
            );
        }
    }

    /// <summary>
    /// Purges all expired frames from the buffer based on TTL.
    /// </summary>
    public int PurgeExpired(DateTime? currentUtc = null)
    {
        lock (_lock)
        {
            return PurgeExpiredInternal(currentUtc ?? DateTime.UtcNow);
        }
    }

    /// <summary>
    /// Clears all frames and resets queue.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _frames.Clear();
            _evictedFramesCount = 0;
        }
    }

    private int PurgeExpiredInternal(DateTime nowUtc)
    {
        var expired = _frames.Where(f => f.ExpiresAtUtc <= nowUtc).ToList();
        foreach (var frame in expired)
        {
            _frames.Remove(frame);
            _evictedFramesCount++;
        }
        return expired.Count;
    }

    #region Columnar Delta-RLE Serialization Helpers

    private static void WriteDeltaRleInt32(BinaryWriter writer, List<int> values)
    {
        if (values.Count == 0) return;

        // Base value
        writer.Write(values[0]);

        if (values.Count == 1)
        {
            writer.Write((ushort)0); // 0 runs
            return;
        }

        // Calculate consecutive deltas
        var deltas = new List<int>(values.Count - 1);
        for (int i = 1; i < values.Count; i++)
        {
            deltas.Add(values[i] - values[i - 1]);
        }

        // Run-length encode deltas
        var runs = new List<(int Delta, ushort Count)>();
        int currentDelta = deltas[0];
        ushort currentCount = 1;

        for (int i = 1; i < deltas.Count; i++)
        {
            if (deltas[i] == currentDelta && currentCount < ushort.MaxValue)
            {
                currentCount++;
            }
            else
            {
                runs.Add((currentDelta, currentCount));
                currentDelta = deltas[i];
                currentCount = 1;
            }
        }
        runs.Add((currentDelta, currentCount));

        // Write runs
        writer.Write((ushort)runs.Count);
        foreach (var (delta, count) in runs)
        {
            writer.Write(delta);
            writer.Write(count);
        }
    }

    private static List<int> ReadDeltaRleInt32(BinaryReader reader, int totalCount)
    {
        var result = new List<int>(totalCount);
        if (totalCount == 0) return result;

        var baseValue = reader.ReadInt32();
        result.Add(baseValue);

        if (totalCount == 1)
        {
            var runsCount = reader.ReadUInt16(); // consume 0
            return result;
        }

        var runSegments = reader.ReadUInt16();
        int currentValue = baseValue;

        for (int i = 0; i < runSegments; i++)
        {
            var delta = reader.ReadInt32();
            var count = reader.ReadUInt16();

            for (int k = 0; k < count; k++)
            {
                currentValue += delta;
                result.Add(currentValue);
            }
        }

        return result;
    }

    #endregion
}
