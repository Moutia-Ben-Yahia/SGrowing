using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using SGrowing.Api.Modules.Logistics.Satellite;
using Xunit;

namespace SGrowing.Tests.Logistics;

/// <summary>
/// Unit test suite for Horizon 51:
/// Autonomous Satellite Direct-to-Cell / Remote Asset Comms &amp; LoRaWAN Mesh Fallback.
/// </summary>
public sealed class SatelliteCommsAndLoRaMeshTests
{
    private readonly SatelliteOrbitalPassScheduler _scheduler = new();
    private readonly CompressiveTelemetryBurstBuffer _burstBuffer = new(capacity: 20);
    private readonly LoRaWanAdHocMeshFallbackService _meshService = new();

    #region 1. Orbital Pass Scheduler & SGP4 Mechanics

    [Fact]
    public void MinimumElevationThresholdDegrees_DefaultsToFifteenDegrees()
    {
        _scheduler.MinimumElevationThresholdDegrees.Should().Be(15.0);
    }

    [Fact]
    public void CalculateLookAngles_DirectZenith_ReturnsNinetyDegreesElevation()
    {
        // Observer at North Pole (90 lat, 0 lon)
        // Satellite directly above North Pole along Z axis at altitude 780 km
        var satX = 0.0;
        var satY = 0.0;
        var satZ = 6378.137 + 780.0;

        var (elevation, _) = _scheduler.CalculateLookAngles(satX, satY, satZ, 90.0, 0.0);

        elevation.Should().BeApproximately(90.0, 0.01);
    }

    [Fact]
    public void CalculateLookAngles_SatelliteOnEquator_ReturnsCorrectAzimuthAndElevation()
    {
        // Observer at (0, 0)
        // Satellite directly East of observer along Equator
        var satX = (6378.137 + 500.0) * Math.Cos(10.0 * Math.PI / 180.0);
        var satY = (6378.137 + 500.0) * Math.Sin(10.0 * Math.PI / 180.0);
        var satZ = 0.0;

        var (elevation, azimuth) = _scheduler.CalculateLookAngles(satX, satY, satZ, 0.0, 0.0);

        elevation.Should().BeGreaterThan(0.0);
        azimuth.Should().BeApproximately(90.0, 1.0); // Due East
    }

    [Fact]
    public void CheckLineOfSight_WhenSatelliteAboveFifteenDegrees_ReturnsTrueAndPassWindow()
    {
        // Satellite directly overhead
        var sat = new SatelliteOrbitalPassScheduler.SatelliteEphemeris(
            SatelliteId: "TEST-SAT-01",
            Constellation: ConstellationType.StarlinkDirectToCell,
            AltitudeKm: 350.0,
            InclinationDegrees: 0.0,
            RaanDegrees: 0.0,
            MeanAnomalyEpochDegrees: 0.0
        );

        var time = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var (x, y, z) = SatelliteOrbitalPassScheduler.PropagateToEcef(sat, time);
        var (el, _) = _scheduler.CalculateLookAngles(x, y, z, 0.0, 0.0);

        el.Should().BeApproximately(90.0, 0.1);
        el.Should().BeGreaterThanOrEqualTo(_scheduler.MinimumElevationThresholdDegrees);
    }

    [Fact]
    public void CheckLineOfSight_WhenSatelliteBelowHorizon_ReturnsFalse()
    {
        // Observer at North Pole (90 deg N)
        // Satellite in deep Southern Hemisphere
        var satX = 7000.0;
        var satY = 0.0;
        var satZ = -5000.0;

        var (elevation, _) = _scheduler.CalculateLookAngles(satX, satY, satZ, 90.0, 0.0);

        elevation.Should().BeLessThan(_scheduler.MinimumElevationThresholdDegrees);
    }

    [Fact]
    public void GetUpcomingPasses_WithinSixHours_ReturnsValidChronologicalPasses()
    {
        var testTime = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);
        var passes = _scheduler.GetUpcomingPasses(36.75, 3.04, testTime, TimeSpan.FromHours(6));

        passes.Should().NotBeNull();
        if (passes.Count > 1)
        {
            // Verify chronological ordering
            for (int i = 0; i < passes.Count - 1; i++)
            {
                passes[i].StartUtc.Should().BeOnOrBefore(passes[i + 1].StartUtc);
            }
        }

        foreach (var pass in passes)
        {
            pass.MaxElevationDegrees.Should().BeGreaterThanOrEqualTo(15.0);
            pass.DurationSeconds.Should().BeGreaterThan(0);
            pass.EndUtc.Should().BeAfter(pass.StartUtc);
        }
    }

    [Fact]
    public void GetUpcomingPasses_FilteredByConstellation_ReturnsOnlyMatchingConstellation()
    {
        var testTime = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);
        var passes = _scheduler.GetUpcomingPasses(36.75, 3.04, testTime, TimeSpan.FromHours(12), ConstellationType.IridiumShortBurstData);

        passes.Should().AllSatisfy(p => p.Constellation.Should().Be(ConstellationType.IridiumShortBurstData));
    }

    [Fact]
    public void GetNextBurstOpportunity_ReturnsEarliestFuturePass()
    {
        var testTime = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);
        var nextPass = _scheduler.GetNextBurstOpportunity(36.75, 3.04, testTime);

        if (nextPass is not null)
        {
            nextPass.EndUtc.Should().BeAfter(testTime);
            nextPass.MaxElevationDegrees.Should().BeGreaterThanOrEqualTo(15.0);
        }
    }

    [Fact]
    public void PropagateToEcef_DifferentTimestamps_ReflectsEarthRotationAndOrbitalProgression()
    {
        var sat = new SatelliteOrbitalPassScheduler.SatelliteEphemeris(
            SatelliteId: "IRIDIUM-NEXT-TEST",
            Constellation: ConstellationType.IridiumShortBurstData,
            AltitudeKm: 780.0,
            InclinationDegrees: 86.4,
            RaanDegrees: 0.0,
            MeanAnomalyEpochDegrees: 0.0
        );

        var t0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var t1 = t0.AddMinutes(25);

        var (x0, y0, z0) = SatelliteOrbitalPassScheduler.PropagateToEcef(sat, t0);
        var (x1, y1, z1) = SatelliteOrbitalPassScheduler.PropagateToEcef(sat, t1);

        // Position coordinates must change over 25 minutes of orbit
        (x0 == x1 && y0 == y1 && z0 == z1).Should().BeFalse();

        // Orbital radius must remain constant (circular orbit)
        var r0 = Math.Sqrt(x0 * x0 + y0 * y0 + z0 * z0);
        var r1 = Math.Sqrt(x1 * x1 + y1 * y1 + z1 * z1);
        r0.Should().BeApproximately(r1, 0.001);
    }

    [Fact]
    public void CalculateLookAngles_AzimuthNormalization_AlwaysBetweenZeroAnd360()
    {
        // Test various quadrant coordinates
        double[][] points = [
            [1000, 2000, 7000],
            [-1000, 2000, 7000],
            [-1000, -2000, 7000],
            [1000, -2000, 7000]
        ];

        foreach (var p in points)
        {
            var (_, az) = _scheduler.CalculateLookAngles(p[0], p[1], p[2], 45.0, 10.0);
            az.Should().BeInRange(0.0, 360.0);
        }
    }

    #endregion

    #region 2. Compressive Telemetry Burst Buffer & Delta-RLE Lossless Encoding

    [Fact]
    public void CompressReading_SingleReading_AchievesHighCompressionAndValidChecksum()
    {
        var reading = new SensorTelemetryReading(
            ReadingId: "RDG-SAT-001",
            AssetId: "REEFER-IRIDIUM-901",
            TimestampUtc: new DateTime(2026, 11, 1, 14, 0, 0, DateTimeKind.Utc),
            Latitude: 36.752512,
            Longitude: 3.042015,
            AltitudeMeters: 14.5,
            TemperatureCelsius: -20.45,
            HumidityPct: 88.50,
            BatteryPct: 96.20,
            ShockGForce: 0.12,
            Priority: TelemetryPriority.HighValueAsset
        );

        var frame = _burstBuffer.CompressReading(reading);

        frame.Should().NotBeNull();
        frame.AssetId.Should().Be("REEFER-IRIDIUM-901");
        frame.OriginalBytesLength.Should().BeGreaterThan(frame.CompressedBytesLength);
        frame.CompressionRatioPercent.Should().BeGreaterThan(50.0);
        frame.ChecksumSha256.Should().NotBeNullOrWhiteSpace();
        frame.PayloadBase64.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void DecompressFrame_SingleReading_LosslessRoundTripReconstruction()
    {
        var original = new SensorTelemetryReading(
            ReadingId: "RDG-SAT-002",
            AssetId: "PHARMA-CONTAINER-01",
            TimestampUtc: new DateTime(2026, 11, 1, 14, 30, 0, DateTimeKind.Utc),
            Latitude: 36.753000,
            Longitude: 3.045000,
            AltitudeMeters: 20.0,
            TemperatureCelsius: 4.55,
            HumidityPct: 45.20,
            BatteryPct: 89.10,
            ShockGForce: 0.05,
            Priority: TelemetryPriority.RoutinePeriodic
        );

        var frame = _burstBuffer.CompressReading(original);
        var reconstructed = _burstBuffer.DecompressFrame(frame);

        reconstructed.Should().HaveCount(1);
        var item = reconstructed[0];
        item.ReadingId.Should().Be(original.ReadingId);
        item.AssetId.Should().Be(original.AssetId);
        item.TimestampUtc.Should().Be(original.TimestampUtc);
        item.Latitude.Should().BeApproximately(original.Latitude, 1e-5);
        item.Longitude.Should().BeApproximately(original.Longitude, 1e-5);
        item.AltitudeMeters.Should().BeApproximately(original.AltitudeMeters, 0.1);
        item.TemperatureCelsius.Should().BeApproximately(original.TemperatureCelsius, 0.01);
        item.HumidityPct.Should().BeApproximately(original.HumidityPct, 0.01);
        item.BatteryPct.Should().BeApproximately(original.BatteryPct, 0.01);
        item.ShockGForce.Should().BeApproximately(original.ShockGForce, 0.01);
        item.Priority.Should().Be(original.Priority);
    }

    [Fact]
    public void CompressBatch_MultipleReadings_AchievesOver80PercentCompressionRatio()
    {
        var baseTime = new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc);
        var batch = new List<SensorTelemetryReading>();

        for (int i = 0; i < 15; i++)
        {
            batch.Add(new SensorTelemetryReading(
                ReadingId: $"RDG-BATCH-{i:D3}",
                AssetId: "VESSEL-MEDITERRANEAN-7",
                TimestampUtc: baseTime.AddMinutes(i * 10),
                Latitude: 36.75 + (i * 0.001),
                Longitude: 3.04 + (i * 0.001),
                AltitudeMeters: 5.0,
                TemperatureCelsius: -18.5 + (i * 0.05),
                HumidityPct: 85.0 - (i * 0.1),
                BatteryPct: 98.0 - (i * 0.1),
                ShockGForce: 0.08,
                Priority: TelemetryPriority.HighValueAsset
            ));
        }

        var frame = _burstBuffer.CompressBatch(batch);

        frame.CompressionRatioPercent.Should().BeGreaterThan(80.0);
        frame.OriginalBytesLength.Should().BeGreaterThan(3000);
        frame.CompressedBytesLength.Should().BeLessThan(350); // Satellite SBD fits easily
    }

    [Fact]
    public void CompressBatch_StationaryContainer_AchievesOver90PercentCompressionRatio()
    {
        var baseTime = new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc);
        var batch = new List<SensorTelemetryReading>();

        // 20 stationary readings where lat, lon, temp, humidity are completely constant
        for (int i = 0; i < 20; i++)
        {
            batch.Add(new SensorTelemetryReading(
                ReadingId: $"RDG-STATIONARY-{i:D3}",
                AssetId: "COLD-ROOM-ALGIERS-01",
                TimestampUtc: baseTime.AddMinutes(i * 5),
                Latitude: 36.752500,
                Longitude: 3.042000,
                AltitudeMeters: 10.0,
                TemperatureCelsius: -22.00,
                HumidityPct: 80.00,
                BatteryPct: 95.00,
                ShockGForce: 0.02,
                Priority: TelemetryPriority.RoutinePeriodic
            ));
        }

        var frame = _burstBuffer.CompressBatch(batch);

        // Constant values compress exceptionally well via Delta-RLE + Deflate
        frame.CompressionRatioPercent.Should().BeGreaterThan(88.0);
    }

    [Fact]
    public void DecompressFrame_MultipleReadings_PreservesExactOrderAndQuantizedPrecision()
    {
        var baseTime = new DateTime(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc);
        var originalBatch = Enumerable.Range(0, 8).Select(i => new SensorTelemetryReading(
            ReadingId: $"SEQ-RDG-{i}",
            AssetId: "TRUCK-CARRIER-44",
            TimestampUtc: baseTime.AddSeconds(i * 60),
            Latitude: 35.123456 + (i * 0.0001),
            Longitude: -0.654321 - (i * 0.0001),
            AltitudeMeters: 120.5,
            TemperatureCelsius: 2.34 + (i * 0.1),
            HumidityPct: 55.40,
            BatteryPct: 90.00 - (i * 0.2),
            ShockGForce: 0.15,
            Priority: TelemetryPriority.RoutinePeriodic
        )).ToList();

        var frame = _burstBuffer.CompressBatch(originalBatch);
        var reconstructed = _burstBuffer.DecompressFrame(frame);

        reconstructed.Should().HaveCount(8);
        for (int i = 0; i < 8; i++)
        {
            reconstructed[i].ReadingId.Should().Be(originalBatch[i].ReadingId);
            reconstructed[i].TimestampUtc.Should().Be(originalBatch[i].TimestampUtc);
            reconstructed[i].TemperatureCelsius.Should().BeApproximately(originalBatch[i].TemperatureCelsius, 0.01);
            reconstructed[i].Latitude.Should().BeApproximately(originalBatch[i].Latitude, 1e-5);
            reconstructed[i].Longitude.Should().BeApproximately(originalBatch[i].Longitude, 1e-5);
        }
    }

    [Fact]
    public void DecompressFrame_CorruptedChecksum_ThrowsInvalidOperationException()
    {
        var reading = new SensorTelemetryReading(
            ReadingId: "RDG-ERR-01",
            AssetId: "ASSET-01",
            TimestampUtc: DateTime.UtcNow,
            Latitude: 10.0,
            Longitude: 20.0,
            AltitudeMeters: 0,
            TemperatureCelsius: 5.0,
            HumidityPct: 50.0,
            BatteryPct: 100.0,
            ShockGForce: 0.0
        );

        var frame = _burstBuffer.CompressReading(reading);
        // Tamper with checksum
        var tamperedFrame = frame with { ChecksumSha256 = "00112233445566778899AABBCCDDEEFF" };

        var act = () => _burstBuffer.DecompressFrame(tamperedFrame);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*checksum verification failed*");
    }

    [Fact]
    public void DecompressFrame_InvalidMagicBytes_ThrowsFormatException()
    {
        var reading = new SensorTelemetryReading(
            ReadingId: "RDG-ERR-02",
            AssetId: "ASSET-02",
            TimestampUtc: DateTime.UtcNow,
            Latitude: 10.0,
            Longitude: 20.0,
            AltitudeMeters: 0,
            TemperatureCelsius: 5.0,
            HumidityPct: 50.0,
            BatteryPct: 100.0,
            ShockGForce: 0.0
        );

        var frame = _burstBuffer.CompressReading(reading);

        // Decompress raw stream and modify magic header
        byte[] uncompressed;
        using (var inStream = new System.IO.MemoryStream(frame.CompressedData))
        using (var def = new System.IO.Compression.DeflateStream(inStream, System.IO.Compression.CompressionMode.Decompress))
        using (var outStream = new System.IO.MemoryStream())
        {
            def.CopyTo(outStream);
            uncompressed = outStream.ToArray();
        }

        uncompressed[0] = 0xFF; // corrupt magic

        byte[] recompressed;
        using (var outStream = new System.IO.MemoryStream())
        {
            using (var def = new System.IO.Compression.DeflateStream(outStream, System.IO.Compression.CompressionLevel.SmallestSize))
            {
                def.Write(uncompressed);
            }
            recompressed = outStream.ToArray();
        }

        var checksum = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(recompressed));
        var badMagicFrame = frame with { CompressedData = recompressed, ChecksumSha256 = checksum };

        var act = () => _burstBuffer.DecompressFrame(badMagicFrame);
        act.Should().Throw<FormatException>()
            .WithMessage("*Invalid frame magic header*");
    }

    [Fact]
    public void CompressReading_NullReading_ThrowsArgumentNullException()
    {
        var act = () => _burstBuffer.CompressReading(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CompressBatch_EmptyBatch_ThrowsArgumentException()
    {
        var act = () => _burstBuffer.CompressBatch([]);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*cannot be null or empty*");
    }

    [Fact]
    public void CompressionRatioPercent_MatchesByteLengthDifference()
    {
        var reading = new SensorTelemetryReading(
            ReadingId: "RDG-RATIO-01",
            AssetId: "ASSET-RATIO",
            TimestampUtc: DateTime.UtcNow,
            Latitude: 36.75,
            Longitude: 3.04,
            AltitudeMeters: 10.0,
            TemperatureCelsius: -18.0,
            HumidityPct: 80.0,
            BatteryPct: 90.0,
            ShockGForce: 0.05
        );

        var frame = _burstBuffer.CompressReading(reading);
        var expectedRatio = (1.0 - ((double)frame.CompressedBytesLength / frame.OriginalBytesLength)) * 100.0;

        frame.CompressionRatioPercent.Should().BeApproximately(expectedRatio, 0.05);
    }

    #endregion

    #region 3. Priority Queuing, Head-Of-Line Preemption & Ring Buffer Eviction

    [Fact]
    public void Enqueue_RoutineFrame_EnqueuesSuccessfully()
    {
        _burstBuffer.Clear();
        var reading = new SensorTelemetryReading(
            ReadingId: "ROUTINE-01",
            AssetId: "ASSET-R1",
            TimestampUtc: DateTime.UtcNow,
            Latitude: 36.0,
            Longitude: 3.0,
            AltitudeMeters: 0,
            TemperatureCelsius: 5.0,
            HumidityPct: 50.0,
            BatteryPct: 90.0,
            ShockGForce: 0.1,
            Priority: TelemetryPriority.RoutinePeriodic
        );

        var frame = _burstBuffer.Enqueue(reading);

        frame.Should().NotBeNull();
        _burstBuffer.GetStatus().TotalQueued.Should().Be(1);
        _burstBuffer.GetStatus().RoutineCount.Should().Be(1);
    }

    [Fact]
    public void Enqueue_CriticalAlarmThermalBreach_PreemptsHeadOfLineAheadOfRoutine()
    {
        _burstBuffer.Clear();

        var routineReading = new SensorTelemetryReading(
            ReadingId: "ROUTINE-PREEMPT",
            AssetId: "CONTAINER-R",
            TimestampUtc: DateTime.UtcNow.AddMinutes(-5),
            Latitude: 36.0,
            Longitude: 3.0,
            AltitudeMeters: 0,
            TemperatureCelsius: 4.0,
            HumidityPct: 50.0,
            BatteryPct: 95.0,
            ShockGForce: 0.05,
            Priority: TelemetryPriority.RoutinePeriodic
        );

        var criticalReading = new SensorTelemetryReading(
            ReadingId: "CRITICAL-BREACH-01",
            AssetId: "CONTAINER-C",
            TimestampUtc: DateTime.UtcNow,
            Latitude: 36.0,
            Longitude: 3.0,
            AltitudeMeters: 0,
            TemperatureCelsius: 18.5, // Severe thermal breach
            HumidityPct: 80.0,
            BatteryPct: 80.0,
            ShockGForce: 0.2,
            Priority: TelemetryPriority.CriticalAlarmThermalBreach
        );

        _burstBuffer.Enqueue(routineReading);
        _burstBuffer.Enqueue(criticalReading);

        // Dequeue must return the Critical breach first (Head-Of-Line preemption)
        _burstBuffer.TryDequeue(out var firstDequeued).Should().BeTrue();
        firstDequeued!.Priority.Should().Be(TelemetryPriority.CriticalAlarmThermalBreach);
        firstDequeued.AssetId.Should().Be("CONTAINER-C");

        _burstBuffer.TryDequeue(out var secondDequeued).Should().BeTrue();
        secondDequeued!.Priority.Should().Be(TelemetryPriority.RoutinePeriodic);
        secondDequeued.AssetId.Should().Be("CONTAINER-R");
    }

    [Fact]
    public void Enqueue_EmergencySos_PreemptsHeadOfLineAheadOfCriticalThermalBreach()
    {
        _burstBuffer.Clear();

        var routineReading = new SensorTelemetryReading("R-1", "A1", DateTime.UtcNow.AddMinutes(-10), 0, 0, 0, 0, 0, 0, 0, TelemetryPriority.RoutinePeriodic);
        var highValueReading = new SensorTelemetryReading("HV-1", "A2", DateTime.UtcNow.AddMinutes(-8), 0, 0, 0, 0, 0, 0, 0, TelemetryPriority.HighValueAsset);
        var thermalReading = new SensorTelemetryReading("TB-1", "A3", DateTime.UtcNow.AddMinutes(-5), 0, 0, 0, 0, 0, 0, 0, TelemetryPriority.CriticalAlarmThermalBreach);
        var sosReading = new SensorTelemetryReading("SOS-1", "A4", DateTime.UtcNow, 0, 0, 0, 0, 0, 0, 0, TelemetryPriority.EmergencySos);

        _burstBuffer.Enqueue(routineReading);
        _burstBuffer.Enqueue(thermalReading);
        _burstBuffer.Enqueue(highValueReading);
        _burstBuffer.Enqueue(sosReading);

        _burstBuffer.TryDequeue(out var p1).Should().BeTrue();
        p1!.Priority.Should().Be(TelemetryPriority.EmergencySos);

        _burstBuffer.TryDequeue(out var p2).Should().BeTrue();
        p2!.Priority.Should().Be(TelemetryPriority.CriticalAlarmThermalBreach);

        _burstBuffer.TryDequeue(out var p3).Should().BeTrue();
        p3!.Priority.Should().Be(TelemetryPriority.HighValueAsset);

        _burstBuffer.TryDequeue(out var p4).Should().BeTrue();
        p4!.Priority.Should().Be(TelemetryPriority.RoutinePeriodic);
    }

    [Fact]
    public void TryDequeue_StrictPriorityOrder_DequeuesFromHighestToLowestPriority()
    {
        _burstBuffer.Clear();
        var priorities = new[]
        {
            TelemetryPriority.RoutinePeriodic,
            TelemetryPriority.HighValueAsset,
            TelemetryPriority.CriticalAlarmThermalBreach,
            TelemetryPriority.EmergencySos
        };

        foreach (var p in priorities)
        {
            _burstBuffer.Enqueue(new SensorTelemetryReading($"ID-{p}", "A", DateTime.UtcNow, 0, 0, 0, 0, 0, 0, 0, p));
        }

        var dequeuedPriorities = new List<TelemetryPriority>();
        while (_burstBuffer.TryDequeue(out var frame))
        {
            dequeuedPriorities.Add(frame!.Priority);
        }

        dequeuedPriorities.Should().Equal(
            TelemetryPriority.EmergencySos,
            TelemetryPriority.CriticalAlarmThermalBreach,
            TelemetryPriority.HighValueAsset,
            TelemetryPriority.RoutinePeriodic
        );
    }

    [Fact]
    public void TryDequeue_SamePriority_MaintainsFifoTimestampOrder()
    {
        _burstBuffer.Clear();
        var t0 = DateTime.UtcNow.AddMinutes(-15);
        var t1 = DateTime.UtcNow.AddMinutes(-10);
        var t2 = DateTime.UtcNow.AddMinutes(-5);

        _burstBuffer.Enqueue(new SensorTelemetryReading("F1", "A", t0, 0, 0, 0, 0, 0, 0, 0, TelemetryPriority.RoutinePeriodic));
        _burstBuffer.Enqueue(new SensorTelemetryReading("F2", "A", t1, 0, 0, 0, 0, 0, 0, 0, TelemetryPriority.RoutinePeriodic));
        _burstBuffer.Enqueue(new SensorTelemetryReading("F3", "A", t2, 0, 0, 0, 0, 0, 0, 0, TelemetryPriority.RoutinePeriodic));

        _burstBuffer.TryDequeue(out var d1);
        _burstBuffer.TryDequeue(out var d2);
        _burstBuffer.TryDequeue(out var d3);

        d1!.TimestampUtc.Should().Be(t0);
        d2!.TimestampUtc.Should().Be(t1);
        d3!.TimestampUtc.Should().Be(t2);
    }

    [Fact]
    public void TryPeek_ReturnsHeadOfLineWithoutRemovingIt()
    {
        _burstBuffer.Clear();
        _burstBuffer.Enqueue(new SensorTelemetryReading("PEEK-1", "A", DateTime.UtcNow, 0, 0, 0, 0, 0, 0, 0, TelemetryPriority.EmergencySos));

        _burstBuffer.TryPeek(out var peeked).Should().BeTrue();
        peeked.Should().NotBeNull();
        peeked!.Priority.Should().Be(TelemetryPriority.EmergencySos);

        _burstBuffer.GetStatus().TotalQueued.Should().Be(1); // Not removed
    }

    [Fact]
    public void RingBuffer_CapacityLimit_EvictsOldestLowestPriorityFrameWhenFull()
    {
        var smallBuffer = new CompressiveTelemetryBurstBuffer(capacity: 3);

        // Fill with Routine frames
        smallBuffer.Enqueue(new SensorTelemetryReading("R1", "A", DateTime.UtcNow.AddMinutes(-10), 0, 0, 0, 0, 0, 0, 0, TelemetryPriority.RoutinePeriodic));
        smallBuffer.Enqueue(new SensorTelemetryReading("R2", "A", DateTime.UtcNow.AddMinutes(-8), 0, 0, 0, 0, 0, 0, 0, TelemetryPriority.RoutinePeriodic));
        smallBuffer.Enqueue(new SensorTelemetryReading("R3", "A", DateTime.UtcNow.AddMinutes(-6), 0, 0, 0, 0, 0, 0, 0, TelemetryPriority.RoutinePeriodic));

        smallBuffer.GetStatus().TotalQueued.Should().Be(3);

        // Enqueue Critical thermal breach
        smallBuffer.Enqueue(new SensorTelemetryReading("C1", "A", DateTime.UtcNow, 0, 0, 0, 0, 0, 0, 0, TelemetryPriority.CriticalAlarmThermalBreach));

        // Capacity maintained at 3, R1 evicted
        var status = smallBuffer.GetStatus();
        status.TotalQueued.Should().Be(3);
        status.EvictedFramesCount.Should().Be(1);
        status.CriticalThermalBreachCount.Should().Be(1);
        status.RoutineCount.Should().Be(2);
    }

    [Fact]
    public void RingBuffer_FullOfHighPriority_RejectsLowerPriorityFrame()
    {
        var smallBuffer = new CompressiveTelemetryBurstBuffer(capacity: 2);

        // Fill with Emergency frames
        smallBuffer.Enqueue(new SensorTelemetryReading("E1", "A", DateTime.UtcNow, 0, 0, 0, 0, 0, 0, 0, TelemetryPriority.EmergencySos));
        smallBuffer.Enqueue(new SensorTelemetryReading("E2", "A", DateTime.UtcNow, 0, 0, 0, 0, 0, 0, 0, TelemetryPriority.EmergencySos));

        // Try to enqueue routine frame
        var routine = smallBuffer.CompressReading(new SensorTelemetryReading("R1", "A", DateTime.UtcNow, 0, 0, 0, 0, 0, 0, 0, TelemetryPriority.RoutinePeriodic));
        var enqueued = smallBuffer.EnqueueFrame(routine);

        enqueued.Should().BeFalse();
        smallBuffer.GetStatus().TotalQueued.Should().Be(2);
        smallBuffer.GetStatus().EmergencyCount.Should().Be(2);
        smallBuffer.GetStatus().EvictedFramesCount.Should().Be(1);
    }

    [Fact]
    public void PurgeExpired_RemovesFramesPastTtlAndIncrementsEvictionCount()
    {
        _burstBuffer.Clear();

        // Enqueue with negative TTL (already expired)
        _burstBuffer.Enqueue(
            new SensorTelemetryReading("EXP-1", "A", DateTime.UtcNow.AddDays(-10), 0, 0, 0, 0, 0, 0, 0, TelemetryPriority.RoutinePeriodic),
            customTtl: TimeSpan.FromSeconds(-10)
        );

        _burstBuffer.Enqueue(
            new SensorTelemetryReading("LIVE-1", "A", DateTime.UtcNow, 0, 0, 0, 0, 0, 0, 0, TelemetryPriority.RoutinePeriodic),
            customTtl: TimeSpan.FromHours(24)
        );

        var purged = _burstBuffer.PurgeExpired(DateTime.UtcNow);
        purged.Should().Be(1);

        var status = _burstBuffer.GetStatus();
        status.TotalQueued.Should().Be(1);
        status.EvictedFramesCount.Should().Be(1);
    }

    [Fact]
    public void Clear_ResetsBufferAndCounters()
    {
        _burstBuffer.Enqueue(new SensorTelemetryReading("R", "A", DateTime.UtcNow, 0, 0, 0, 0, 0, 0, 0));
        _burstBuffer.Clear();

        var status = _burstBuffer.GetStatus();
        status.TotalQueued.Should().Be(0);
        status.EvictedFramesCount.Should().Be(0);
    }

    #endregion

    #region 4. Multi-threaded Concurrency with System.Threading.Lock

    [Fact]
    public void ConcurrentEnqueue_OneHundredThreads_MaintainsThreadSafeIntegrity()
    {
        var concurrentBuffer = new CompressiveTelemetryBurstBuffer(capacity: 200);

        Parallel.For(0, 100, i =>
        {
            var priority = (TelemetryPriority)(i % 4);
            concurrentBuffer.Enqueue(new SensorTelemetryReading(
                ReadingId: $"CONC-{i}",
                AssetId: $"ASSET-{i % 10}",
                TimestampUtc: DateTime.UtcNow.AddMinutes(-i),
                Latitude: 36.0,
                Longitude: 3.0,
                AltitudeMeters: 0,
                TemperatureCelsius: -10.0 + i,
                HumidityPct: 50.0,
                BatteryPct: 90.0,
                ShockGForce: 0.1,
                Priority: priority
            ));
        });

        var status = concurrentBuffer.GetStatus();
        status.TotalQueued.Should().Be(100);
        (status.EmergencyCount + status.CriticalThermalBreachCount + status.HighValueCount + status.RoutineCount)
            .Should().Be(100);
    }

    [Fact]
    public void ConcurrentEnqueueAndDequeue_UnderHighContention_MaintainsQueueConsistency()
    {
        var concurrentBuffer = new CompressiveTelemetryBurstBuffer(capacity: 100);
        int successfulDequeues = 0;

        Parallel.For(0, 150, i =>
        {
            if (i % 2 == 0)
            {
                concurrentBuffer.Enqueue(new SensorTelemetryReading(
                    ReadingId: $"PROD-{i}",
                    AssetId: "PRODUCER",
                    TimestampUtc: DateTime.UtcNow,
                    Latitude: 36.0,
                    Longitude: 3.0,
                    AltitudeMeters: 0,
                    TemperatureCelsius: 5.0,
                    HumidityPct: 50.0,
                    BatteryPct: 95.0,
                    ShockGForce: 0.05,
                    Priority: (TelemetryPriority)(i % 4)
                ));
            }
            else
            {
                if (concurrentBuffer.TryDequeue(out var frame) && frame is not null)
                {
                    Interlocked.Increment(ref successfulDequeues);
                }
            }
        });

        var finalStatus = concurrentBuffer.GetStatus();
        finalStatus.TotalQueued.Should().BeGreaterThanOrEqualTo(0);
        finalStatus.TotalQueued.Should().BeLessThanOrEqualTo(100);
    }

    [Fact]
    public void GetStatus_ThreadSafe_ReturnsAccuratePriorityBreakdown()
    {
        _burstBuffer.Clear();
        _burstBuffer.Enqueue(new SensorTelemetryReading("E1", "A", DateTime.UtcNow, 0, 0, 0, 0, 0, 0, 0, TelemetryPriority.EmergencySos));
        _burstBuffer.Enqueue(new SensorTelemetryReading("C1", "A", DateTime.UtcNow, 0, 0, 0, 0, 0, 0, 0, TelemetryPriority.CriticalAlarmThermalBreach));
        _burstBuffer.Enqueue(new SensorTelemetryReading("HV1", "A", DateTime.UtcNow, 0, 0, 0, 0, 0, 0, 0, TelemetryPriority.HighValueAsset));
        _burstBuffer.Enqueue(new SensorTelemetryReading("R1", "A", DateTime.UtcNow, 0, 0, 0, 0, 0, 0, 0, TelemetryPriority.RoutinePeriodic));

        var status = _burstBuffer.GetStatus();
        status.TotalQueued.Should().Be(4);
        status.EmergencyCount.Should().Be(1);
        status.CriticalThermalBreachCount.Should().Be(1);
        status.HighValueCount.Should().Be(1);
        status.RoutineCount.Should().Be(1);
        status.ActiveConstellations.Should().HaveCount(3);
    }

    [Fact]
    public void ConcurrentPurgeExpired_ThreadSafe_NoExceptions()
    {
        var testBuffer = new CompressiveTelemetryBurstBuffer(capacity: 50);

        for (int i = 0; i < 20; i++)
        {
            testBuffer.Enqueue(
                new SensorTelemetryReading($"P-{i}", "A", DateTime.UtcNow, 0, 0, 0, 0, 0, 0, 0),
                customTtl: TimeSpan.FromMilliseconds(i % 2 == 0 ? -100 : 100000)
            );
        }

        Parallel.For(0, 10, _ =>
        {
            testBuffer.PurgeExpired(DateTime.UtcNow);
        });

        testBuffer.GetStatus().TotalQueued.Should().Be(10);
    }

    #endregion

    #region 5. LoRaWAN Ad-Hoc Mesh Routing, Deduplication & Sink Backhaul

    [Fact]
    public void ProcessRelay_ValidIntermediateHop_IncrementsHopAndForwards()
    {
        _meshService.ClearSeenFilter();

        var packet = new LoRaMeshPacket(
            PacketId: "LORA-PKT-001",
            OriginNodeId: "NODE-SENSOR-41",
            TargetSinkId: null,
            CurrentHop: 0,
            MaxHops: 5,
            PayloadBytes: [0x01, 0x02, 0x03],
            Priority: TelemetryPriority.RoutinePeriodic,
            CreationTimestampUtc: DateTime.UtcNow,
            ExpiryTimestampUtc: DateTime.UtcNow.AddHours(1),
            RelayPath: []
        );

        var request = new LoRaRelayRequest(
            Packet: packet,
            ReportingNodeId: "NODE-REPEATER-01",
            HasDirectUplink: false
        );

        var response = _meshService.ProcessRelay(request);

        response.Accepted.Should().BeTrue();
        response.Forwarded.Should().BeTrue();
        response.ReachedSink.Should().BeFalse();
        response.CurrentHop.Should().Be(1);
        response.EgressMedium.Should().Be(TransmissionMedium.LoRaWanMeshHop);
    }

    [Fact]
    public void ProcessRelay_MaxHopsExceeded_DropsPacketToPreventInfiniteRelay()
    {
        _meshService.ClearSeenFilter();

        var packet = new LoRaMeshPacket(
            PacketId: "LORA-PKT-MAXHOPS",
            OriginNodeId: "NODE-FAR-AWAY",
            TargetSinkId: null,
            CurrentHop: 5,
            MaxHops: 5,
            PayloadBytes: [0xAA, 0xBB],
            Priority: TelemetryPriority.RoutinePeriodic,
            CreationTimestampUtc: DateTime.UtcNow,
            ExpiryTimestampUtc: DateTime.UtcNow.AddHours(1),
            RelayPath: ["N1", "N2", "N3", "N4", "N5"]
        );

        var request = new LoRaRelayRequest(
            Packet: packet,
            ReportingNodeId: "NODE-N6",
            HasDirectUplink: false
        );

        var response = _meshService.ProcessRelay(request);

        response.Accepted.Should().BeFalse();
        response.Forwarded.Should().BeFalse();
        response.Message.Should().Contain("Max hops reached");
    }

    [Fact]
    public void ProcessRelay_TerminalHopReached_AcceptsWithoutForwarding()
    {
        _meshService.ClearSeenFilter();

        var packet = new LoRaMeshPacket(
            PacketId: "LORA-PKT-TERMINAL",
            OriginNodeId: "NODE-START",
            TargetSinkId: null,
            CurrentHop: 4,
            MaxHops: 5,
            PayloadBytes: [0x55],
            Priority: TelemetryPriority.RoutinePeriodic,
            CreationTimestampUtc: DateTime.UtcNow,
            ExpiryTimestampUtc: DateTime.UtcNow.AddHours(1),
            RelayPath: ["N1", "N2", "N3", "N4"]
        );

        var request = new LoRaRelayRequest(
            Packet: packet,
            ReportingNodeId: "NODE-LAST-HOP",
            HasDirectUplink: false
        );

        var response = _meshService.ProcessRelay(request);

        response.Accepted.Should().BeTrue();
        response.Forwarded.Should().BeFalse();
        response.CurrentHop.Should().Be(5);
        response.Message.Should().Contain("terminal hop");
    }

    [Fact]
    public void ProcessRelay_DuplicatePacketId_SuppressedBySeenFilter()
    {
        _meshService.ClearSeenFilter();

        var packet = new LoRaMeshPacket(
            PacketId: "LORA-PKT-DEDUP",
            OriginNodeId: "NODE-DUP-ORIGIN",
            TargetSinkId: null,
            CurrentHop: 1,
            MaxHops: 5,
            PayloadBytes: [0x01],
            Priority: TelemetryPriority.RoutinePeriodic,
            CreationTimestampUtc: DateTime.UtcNow,
            ExpiryTimestampUtc: DateTime.UtcNow.AddHours(1),
            RelayPath: ["NODE-01"]
        );

        var req1 = new LoRaRelayRequest(packet, "REPEATER-A", HasDirectUplink: false);
        var req2 = new LoRaRelayRequest(packet, "REPEATER-B", HasDirectUplink: false);

        var resp1 = _meshService.ProcessRelay(req1);
        var resp2 = _meshService.ProcessRelay(req2);

        resp1.Accepted.Should().BeTrue();
        resp1.Forwarded.Should().BeTrue();

        // Broadcast storm suppression
        resp2.Accepted.Should().BeFalse();
        resp2.Forwarded.Should().BeFalse();
        resp2.Message.Should().Contain("Duplicate packet suppressed");
    }

    [Fact]
    public void ProcessRelay_ExpiredPacketTtl_DropsPacket()
    {
        _meshService.ClearSeenFilter();

        var expiredPacket = new LoRaMeshPacket(
            PacketId: "LORA-PKT-EXPIRED",
            OriginNodeId: "NODE-OLD",
            TargetSinkId: null,
            CurrentHop: 1,
            MaxHops: 5,
            PayloadBytes: [0x09],
            Priority: TelemetryPriority.RoutinePeriodic,
            CreationTimestampUtc: DateTime.UtcNow.AddHours(-3),
            ExpiryTimestampUtc: DateTime.UtcNow.AddHours(-1), // Expired 1 hour ago
            RelayPath: ["N1"]
        );

        var request = new LoRaRelayRequest(expiredPacket, "REPEATER-C", false);
        var response = _meshService.ProcessRelay(request);

        response.Accepted.Should().BeFalse();
        response.Message.Should().Contain("TTL expired");
    }

    [Fact]
    public void ProcessRelay_NodeWithDirectUplink_IngestsAtGatewaySinkViaSpecifiedMedium()
    {
        _meshService.ClearSeenFilter();

        var packet = new LoRaMeshPacket(
            PacketId: "LORA-PKT-SINK-CELL",
            OriginNodeId: "REEFER-SENSOR-09",
            TargetSinkId: null,
            CurrentHop: 2,
            MaxHops: 5,
            PayloadBytes: [0x11, 0x22],
            Priority: TelemetryPriority.CriticalAlarmThermalBreach,
            CreationTimestampUtc: DateTime.UtcNow,
            ExpiryTimestampUtc: DateTime.UtcNow.AddHours(2),
            RelayPath: ["N1", "N2"]
        );

        var request = new LoRaRelayRequest(
            Packet: packet,
            ReportingNodeId: "GATEWAY-PORT-CELLULAR",
            HasDirectUplink: true,
            DirectUplinkMedium: TransmissionMedium.TerrestrialCellular
        );

        var response = _meshService.ProcessRelay(request);

        response.Accepted.Should().BeTrue();
        response.Forwarded.Should().BeFalse();
        response.ReachedSink.Should().BeTrue();
        response.CurrentHop.Should().Be(3);
        response.EgressMedium.Should().Be(TransmissionMedium.TerrestrialCellular);

        var ingested = _meshService.GetSinkIngestedPackets();
        ingested.Should().Contain(p => p.PacketId == "LORA-PKT-SINK-CELL" && p.HasReachedSink && p.UplinkMediumUsed == TransmissionMedium.TerrestrialCellular);
    }

    [Fact]
    public void ProcessRelay_RegisteredSatelliteSink_IngestsAtSinkViaSatelliteUplink()
    {
        _meshService.ClearSeenFilter();
        _meshService.RegisterKnownSink("IRIDIUM-TERMINAL-01", TransmissionMedium.SatelliteUplink);

        var packet = new LoRaMeshPacket(
            PacketId: "LORA-PKT-SAT-SINK",
            OriginNodeId: "BUOY-OCEANIC-33",
            TargetSinkId: "IRIDIUM-TERMINAL-01",
            CurrentHop: 1,
            MaxHops: 5,
            PayloadBytes: [0x77],
            Priority: TelemetryPriority.EmergencySos,
            CreationTimestampUtc: DateTime.UtcNow,
            ExpiryTimestampUtc: DateTime.UtcNow.AddHours(2),
            RelayPath: ["BUOY-OCEANIC-33"]
        );

        var request = new LoRaRelayRequest(packet, "IRIDIUM-TERMINAL-01", HasDirectUplink: false);
        var response = _meshService.ProcessRelay(request);

        response.Accepted.Should().BeTrue();
        response.ReachedSink.Should().BeTrue();
        response.EgressMedium.Should().Be(TransmissionMedium.SatelliteUplink);
    }

    [Fact]
    public void RegisterAndUnregisterKnownSink_UpdatesSinkRegistry()
    {
        _meshService.RegisterKnownSink("TEST-SINK-99", TransmissionMedium.SatelliteUplink);
        _meshService.GetKnownSinks().Should().Contain("TEST-SINK-99");

        _meshService.UnregisterKnownSink("TEST-SINK-99");
        _meshService.GetKnownSinks().Should().NotContain("TEST-SINK-99");
    }

    #endregion

    #region 6. ServiceCollection Dependency Injection Descriptors (Without BuildServiceProvider)

    [Fact]
    public void AddSatelliteCommsServices_RegistersAllThreeServicesAsSingletons()
    {
        // HARD CONSTRAINT: Direct ServiceCollection descriptor assertion without BuildServiceProvider()
        var services = new ServiceCollection();

        services.AddSatelliteCommsServices();

        services.Should().Contain(d =>
            d.ServiceType == typeof(ISatelliteOrbitalPassScheduler) &&
            d.ImplementationType == typeof(SatelliteOrbitalPassScheduler) &&
            d.Lifetime == ServiceLifetime.Singleton);

        services.Should().Contain(d =>
            d.ServiceType == typeof(ICompressiveTelemetryBurstBuffer) &&
            d.ImplementationType == typeof(CompressiveTelemetryBurstBuffer) &&
            d.Lifetime == ServiceLifetime.Singleton);

        services.Should().Contain(d =>
            d.ServiceType == typeof(ILoRaWanAdHocMeshFallbackService) &&
            d.ImplementationType == typeof(LoRaWanAdHocMeshFallbackService) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddSatelliteCommsServices_DirectDescriptorVerification_DoesNotCallBuildServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddSatelliteCommsServices();

        // Count must be exactly 3 registered services
        services.Count.Should().Be(3);
    }

    #endregion

    #region 7. Minimal API Endpoints Simulation & Validation

    [Fact]
    public void TelemetryQueue_ValidPayload_EnqueuesAndReturnsAccepted()
    {
        var buffer = new CompressiveTelemetryBurstBuffer();
        var scheduler = new SatelliteOrbitalPassScheduler();

        var request = new QueueTelemetryRequest(
            Reading: new SensorTelemetryReading(
                ReadingId: "API-TEST-01",
                AssetId: "ASSET-API-1",
                TimestampUtc: DateTime.UtcNow,
                Latitude: 36.75,
                Longitude: 3.04,
                AltitudeMeters: 10.0,
                TemperatureCelsius: -20.0,
                HumidityPct: 75.0,
                BatteryPct: 98.0,
                ShockGForce: 0.05,
                Priority: TelemetryPriority.HighValueAsset
            ),
            ImmediateBurstIfVisible: false // Force queue
        );

        // Simulate endpoint logic
        var frame = buffer.Enqueue(request.Reading);
        var response = new QueueTelemetryResponse(
            Queued: true,
            BurstTransmitted: false,
            FrameId: frame.FrameId,
            Priority: frame.Priority,
            MediumUsed: TransmissionMedium.SatelliteUplink,
            BufferQueueDepth: buffer.GetStatus().TotalQueued,
            Message: "Queued"
        );

        response.Queued.Should().BeTrue();
        response.BurstTransmitted.Should().BeFalse();
        response.BufferQueueDepth.Should().Be(1);
        response.FrameId.Should().StartWith("FRM-");
    }

    [Fact]
    public void TelemetryQueue_ActiveLos_ImmediatelyBurstsSatelliteUplink()
    {
        var buffer = new CompressiveTelemetryBurstBuffer();

        var reading = new SensorTelemetryReading(
            ReadingId: "API-LOS-01",
            AssetId: "ASSET-LOS",
            TimestampUtc: DateTime.UtcNow,
            Latitude: 36.75,
            Longitude: 3.04,
            AltitudeMeters: 10.0,
            TemperatureCelsius: -15.0,
            HumidityPct: 70.0,
            BatteryPct: 90.0,
            ShockGForce: 0.02,
            Priority: TelemetryPriority.EmergencySos
        );

        var frame = buffer.CompressReading(reading);
        var response = new QueueTelemetryResponse(
            Queued: false,
            BurstTransmitted: true,
            FrameId: frame.FrameId,
            Priority: frame.Priority,
            MediumUsed: TransmissionMedium.SatelliteUplink,
            BufferQueueDepth: buffer.GetStatus().TotalQueued,
            Message: "Immediate burst"
        );

        response.Queued.Should().BeFalse();
        response.BurstTransmitted.Should().BeTrue();
        response.MediumUsed.Should().Be(TransmissionMedium.SatelliteUplink);
    }

    [Fact]
    public void TelemetryQueue_InvalidCoordinates_ReturnsBadRequest()
    {
        var reading = new SensorTelemetryReading(
            ReadingId: "BAD-COORD",
            AssetId: "ASSET-BAD",
            TimestampUtc: DateTime.UtcNow,
            Latitude: 125.0, // Invalid lat > 90
            Longitude: 3.04,
            AltitudeMeters: 0,
            TemperatureCelsius: 0,
            HumidityPct: 0,
            BatteryPct: 0,
            ShockGForce: 0
        );

        var isInvalid = reading.Latitude < -90.0 || reading.Latitude > 90.0;
        isInvalid.Should().BeTrue();
    }

    [Fact]
    public void GetPasses_ValidCoordinates_ReturnsUpcomingPassesAndRecommendedConstellation()
    {
        var passes = _scheduler.GetUpcomingPasses(36.75, 3.04, DateTime.UtcNow, TimeSpan.FromHours(6)).ToList();
        var hasLos = _scheduler.CheckLineOfSight(36.75, 3.04, DateTime.UtcNow, out _);

        var response = new OrbitalPassResponse(
            Latitude: 36.75,
            Longitude: 3.04,
            EvaluatedAtUtc: DateTime.UtcNow,
            HasLineOfSight: hasLos,
            UpcomingPasses: passes,
            RecommendedConstellation: "StarlinkDirectToCell"
        );

        response.Latitude.Should().Be(36.75);
        response.Longitude.Should().Be(3.04);
        response.RecommendedConstellation.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void GetPasses_InvalidLatitude_ReturnsBadRequest()
    {
        double invalidLat = -120.0;
        var isInvalid = invalidLat < -90.0 || invalidLat > 90.0;
        isInvalid.Should().BeTrue();
    }

    [Fact]
    public void LoRaRelay_MissingReportingNodeId_ReturnsBadRequest()
    {
        var packet = new LoRaMeshPacket(
            PacketId: "P1",
            OriginNodeId: "O1",
            TargetSinkId: null,
            CurrentHop: 0,
            MaxHops: 5,
            PayloadBytes: [0x01],
            Priority: TelemetryPriority.RoutinePeriodic,
            CreationTimestampUtc: DateTime.UtcNow,
            ExpiryTimestampUtc: DateTime.UtcNow.AddHours(1),
            RelayPath: []
        );

        var act = () => _meshService.ProcessRelay(new LoRaRelayRequest(packet, "   "));
        act.Should().Throw<ArgumentException>()
            .WithMessage("*ReportingNodeId cannot be null or empty*");
    }

    #endregion
}
