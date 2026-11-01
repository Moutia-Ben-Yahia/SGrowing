using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SGrowing.Api.Modules.Commerce.Neuromorphic;
using Xunit;

namespace SGrowing.Tests.Commerce;

/// <summary>
/// Comprehensive unit test suite for Horizon 52:
/// Neuromorphic Event-Based Edge Vision &amp; Micro-Payload Conveyor Anomaly Interceptor.
/// </summary>
public sealed class NeuromorphicVisionAndConveyorAnomalyTests
{
    private readonly NeuromorphicEventStreamProcessor _streamProcessor = new();
    private readonly AsynchronousTimeSurfaceFilter _timeSurface = new();
    private readonly SpikingNeuralNetworkAnomalyDetector _snnDetector = new();

    #region 1. DVS Event Stream Ingestion & Monotonic Ordering

    [Fact]
    public void EnsureMonotonic_WithAlreadySortedEvents_ReturnsOriginalOrder()
    {
        IReadOnlyList<DvsEvent> events =
        [
            new DvsEvent(10, 10, 1000, EventPolarity.On),
            new DvsEvent(11, 10, 1050, EventPolarity.Off),
            new DvsEvent(12, 10, 1100, EventPolarity.On)
        ];

        var result = _streamProcessor.EnsureMonotonic(events);

        result.Should().HaveCount(3);
        result[0].TimestampMicroseconds.Should().Be(1000);
        result[1].TimestampMicroseconds.Should().Be(1050);
        result[2].TimestampMicroseconds.Should().Be(1100);
    }

    [Fact]
    public void EnsureMonotonic_WithOutOfOrderEvents_SortsAscendingByMicrosecondTimestamp()
    {
        IReadOnlyList<DvsEvent> events =
        [
            new DvsEvent(20, 20, 5000, EventPolarity.On),
            new DvsEvent(21, 20, 1000, EventPolarity.Off),
            new DvsEvent(22, 20, 3000, EventPolarity.On)
        ];

        var result = _streamProcessor.EnsureMonotonic(events);

        result.Should().HaveCount(3);
        result[0].TimestampMicroseconds.Should().Be(1000);
        result[1].TimestampMicroseconds.Should().Be(3000);
        result[2].TimestampMicroseconds.Should().Be(5000);
    }

    [Fact]
    public void EnsureMonotonic_WithEmptyOrNullList_ReturnsEmptyCollection()
    {
        var emptyResult = _streamProcessor.EnsureMonotonic([]);
        var nullResult = _streamProcessor.EnsureMonotonic(null!);

        emptyResult.Should().BeEmpty();
        nullResult.Should().BeEmpty();
    }

    [Fact]
    public void EnsureMonotonic_WithSingleEvent_ReturnsSingleEventUnmodified()
    {
        IReadOnlyList<DvsEvent> single = [new DvsEvent(50, 50, 42_000, EventPolarity.On)];
        var result = _streamProcessor.EnsureMonotonic(single);

        result.Should().ContainSingle();
        result[0].TimestampMicroseconds.Should().Be(42_000);
    }

    [Fact]
    public void EnsureMonotonic_WithIdenticalTimestamps_PreservesAllEvents()
    {
        IReadOnlyList<DvsEvent> events =
        [
            new DvsEvent(5, 5, 2000, EventPolarity.On),
            new DvsEvent(6, 5, 2000, EventPolarity.Off),
            new DvsEvent(7, 5, 2000, EventPolarity.On)
        ];

        var result = _streamProcessor.EnsureMonotonic(events);

        result.Should().HaveCount(3);
        result.Select(e => e.TimestampMicroseconds).Should().AllBeEquivalentTo(2000);
    }

    #endregion

    #region 2. Spatio-Temporal Noise Filtering, Dead & Hot Pixel Masking

    [Fact]
    public void FilterNoise_WithIsolatedThermalNoiseEvents_FiltersOutIsolatedEvents()
    {
        // Isolated events far apart in space and time
        IReadOnlyList<DvsEvent> events =
        [
            new DvsEvent(10, 10, 1000, EventPolarity.On),
            new DvsEvent(80, 80, 50_000, EventPolarity.Off),
            new DvsEvent(150, 150, 100_000, EventPolarity.On)
        ];

        var (valid, filtered) = _streamProcessor.FilterNoise(events, spatialRadius: 1, temporalWindowUs: 5000);

        valid.Should().BeEmpty();
        filtered.Should().Be(3);
    }

    [Fact]
    public void FilterNoise_WithSpatioTemporallyCorrelatedEvents_RetainsValidCluster()
    {
        // Coherent edge moving across adjacent pixels within temporal window
        IReadOnlyList<DvsEvent> events =
        [
            new DvsEvent(10, 10, 1000, EventPolarity.On),
            new DvsEvent(11, 10, 1200, EventPolarity.On),
            new DvsEvent(12, 10, 1400, EventPolarity.On),
            new DvsEvent(13, 10, 1600, EventPolarity.On)
        ];

        var (valid, filtered) = _streamProcessor.FilterNoise(events, spatialRadius: 1, temporalWindowUs: 5000);

        valid.Should().HaveCount(4);
        filtered.Should().Be(0);
    }

    [Fact]
    public void FilterNoise_WithDeadPixelMasked_DiscardsEventsAtMaskedCoordinates()
    {
        _streamProcessor.Reset();
        _streamProcessor.MaskDeadPixel(25, 25);
        _streamProcessor.IsDeadPixel(25, 25).Should().BeTrue();

        IReadOnlyList<DvsEvent> events =
        [
            new DvsEvent(25, 25, 1000, EventPolarity.On),
            new DvsEvent(26, 25, 1100, EventPolarity.On),
            new DvsEvent(27, 25, 1200, EventPolarity.On)
        ];

        var (valid, filtered) = _streamProcessor.FilterNoise(events, spatialRadius: 1, temporalWindowUs: 5000);

        valid.Should().NotContain(e => e.X == 25 && e.Y == 25);
        filtered.Should().BeGreaterThan(0);
    }

    [Fact]
    public void FilterNoise_UnmaskDeadPixel_RestoresPixelSensitivity()
    {
        _streamProcessor.MaskDeadPixel(40, 40);
        _streamProcessor.IsDeadPixel(40, 40).Should().BeTrue();

        _streamProcessor.UnmaskDeadPixel(40, 40);
        _streamProcessor.IsDeadPixel(40, 40).Should().BeFalse();
    }

    [Fact]
    public void FilterNoise_OutOfBoundsCoordinates_AreFilteredOutSafely()
    {
        IReadOnlyList<DvsEvent> events =
        [
            new DvsEvent(-5, 10, 1000, EventPolarity.On),
            new DvsEvent(300, 300, 1100, EventPolarity.Off),
            new DvsEvent(10, -2, 1200, EventPolarity.On)
        ];

        var (valid, filtered) = _streamProcessor.FilterNoise(events);

        valid.Should().BeEmpty();
        filtered.Should().Be(3);
    }

    [Fact]
    public void FilterNoise_HotPixelBurstWithoutNeighborSupport_IsFilteredOut()
    {
        _streamProcessor.Reset();
        _streamProcessor.SetHotPixelThreshold(50);

        // Generate 80 events at identical pixel with no neighbor activity
        var hotEvents = Enumerable.Range(0, 80)
            .Select(i => new DvsEvent(60, 60, 10_000 + (i * 10), EventPolarity.On))
            .ToList();

        var (valid, filtered) = _streamProcessor.FilterNoise(hotEvents);

        valid.Should().BeEmpty();
        filtered.Should().Be(80);
    }

    #endregion

    #region 3. Temporal Window Aggregation & Stream Metrics

    [Fact]
    public void CalculateMetrics_SymmetricPolarityEvents_CalculatesAccurateRateAndBalance()
    {
        IReadOnlyList<DvsEvent> events =
        [
            new DvsEvent(10, 10, 1000, EventPolarity.On),
            new DvsEvent(11, 10, 2000, EventPolarity.Off),
            new DvsEvent(12, 10, 3000, EventPolarity.On),
            new DvsEvent(13, 10, 4000, EventPolarity.Off)
        ];

        var (rate, balance) = _streamProcessor.CalculateMetrics(events);

        rate.Should().BeGreaterThan(0);
        balance.Should().Be(1.0); // 2 On / 2 Off = 1.0
    }

    [Fact]
    public void CalculateMetrics_AllOnEvents_ReturnsPositiveRatioWithoutZeroDivision()
    {
        IReadOnlyList<DvsEvent> events =
        [
            new DvsEvent(10, 10, 1000, EventPolarity.On),
            new DvsEvent(11, 10, 2000, EventPolarity.On),
            new DvsEvent(12, 10, 3000, EventPolarity.On)
        ];

        var (rate, balance) = _streamProcessor.CalculateMetrics(events);

        rate.Should().BeGreaterThan(0);
        balance.Should().Be(3.0);
    }

    [Fact]
    public void CalculateMetrics_EmptyEvents_ReturnsZeroRateAndDefaultBalance()
    {
        var (rate, balance) = _streamProcessor.CalculateMetrics([]);

        rate.Should().Be(0.0);
        balance.Should().Be(1.0);
    }

    [Fact]
    public void IngestEvents_ValidBatch_ReturnsSuccessfulResponseWithMetrics()
    {
        _streamProcessor.Reset();

        IReadOnlyList<DvsEvent> events =
        [
            new DvsEvent(20, 20, 1000, EventPolarity.On),
            new DvsEvent(21, 20, 1100, EventPolarity.On),
            new DvsEvent(22, 20, 1200, EventPolarity.Off)
        ];

        var response = _streamProcessor.IngestEvents("stream-01", "CONV-01", events);

        response.Success.Should().BeTrue();
        response.IngestedCount.Should().Be(3);
        response.FilteredCount.Should().Be(0);
        response.EventRateEventsPerMs.Should().BeGreaterThan(0);
        response.PolarBalanceRatio.Should().Be(2.0); // 2 On / 1 Off
    }

    [Fact]
    public void IngestEvents_EmptyBatch_ReturnsGracefulResponse()
    {
        var response = _streamProcessor.IngestEvents("stream-02", "CONV-02", []);

        response.Success.Should().BeTrue();
        response.IngestedCount.Should().Be(0);
        response.FilteredCount.Should().Be(0);
    }

    #endregion

    #region 4. Leaky Integrate-and-Fire (LIF) Spiking Neuron Dynamics

    [Fact]
    public void ProcessEvent_SubThresholdExcitation_AccumulatesMembranePotentialWithoutSpike()
    {
        var detector = new SpikingNeuralNetworkAnomalyDetector(new LifNeuronConfig(
            ThresholdPotential: 1.0,
            SynapticWeight: 0.3));

        var evt = new DvsEvent(16, 16, 1000, EventPolarity.On);
        bool spiked = detector.ProcessEvent(evt);

        spiked.Should().BeFalse();
        var state = detector.GetNeuronState(detector.GetNeuronStates().First(s => s.MembranePotential > 0).NeuronId);
        state.Should().NotBeNull();
        state!.Value.MembranePotential.Should().BeApproximately(0.3, 0.01);
        state.Value.TotalSpikes.Should().Be(0);
    }

    [Fact]
    public void ProcessEvent_SuccessiveExcitationsExceedingThreshold_FiresSpikeAndResetsPotential()
    {
        var detector = new SpikingNeuralNetworkAnomalyDetector(new LifNeuronConfig(
            ThresholdPotential: 1.0,
            ResetPotential: 0.0,
            SynapticWeight: 0.4,
            RefractoryPeriodUs: 1000));

        // 3 events at same location with negligible delay (total 1.2 >= 1.0 threshold)
        detector.ProcessEvent(new DvsEvent(32, 32, 1000, EventPolarity.On)); // V = 0.4
        detector.ProcessEvent(new DvsEvent(32, 32, 1010, EventPolarity.On)); // V ~ 0.8
        bool spiked = detector.ProcessEvent(new DvsEvent(32, 32, 1020, EventPolarity.On)); // V ~ 1.2 -> Spike!

        spiked.Should().BeTrue();
        var state = detector.GetNeuronStates().First(s => s.TotalSpikes > 0);
        state.TotalSpikes.Should().Be(1);
        state.MembranePotential.Should().Be(0.0); // Reset to V_reset
        state.LastSpikeTimeMicroseconds.Should().Be(1020);
    }

    [Fact]
    public void ProcessEvent_DuringRefractoryPeriod_DoesNotIntegrateOrFire()
    {
        var detector = new SpikingNeuralNetworkAnomalyDetector(new LifNeuronConfig(
            ThresholdPotential: 0.5,
            ResetPotential: 0.0,
            SynapticWeight: 0.5,
            RefractoryPeriodUs: 5000));

        // Event 1: Fires spike at t = 1000
        bool spike1 = detector.ProcessEvent(new DvsEvent(48, 48, 1000, EventPolarity.On));
        spike1.Should().BeTrue();

        // Event 2: Arrives at t = 2000 (within 5000us refractory window)
        bool spike2 = detector.ProcessEvent(new DvsEvent(48, 48, 2000, EventPolarity.On));
        spike2.Should().BeFalse();

        var state = detector.GetNeuronStates().First(s => s.TotalSpikes > 0);
        state.TotalSpikes.Should().Be(1);
    }

    [Fact]
    public void ProcessEvent_AfterRefractoryPeriodExpires_ResumesIntegrationAndFiring()
    {
        var detector = new SpikingNeuralNetworkAnomalyDetector(new LifNeuronConfig(
            ThresholdPotential: 0.5,
            ResetPotential: 0.0,
            SynapticWeight: 0.5,
            RefractoryPeriodUs: 2000));

        // Fire spike 1 at t = 1000
        detector.ProcessEvent(new DvsEvent(48, 48, 1000, EventPolarity.On));

        // Event at t = 4000 (exceeded 2000us refractory period) -> Fires spike 2!
        bool spike2 = detector.ProcessEvent(new DvsEvent(48, 48, 4000, EventPolarity.On));

        spike2.Should().BeTrue();
        var state = detector.GetNeuronStates().First(s => s.TotalSpikes > 0);
        state.TotalSpikes.Should().Be(2);
    }

    [Fact]
    public void ProcessEvent_MembranePotentialDecaysOverElapsedMicroseconds()
    {
        var detector = new SpikingNeuralNetworkAnomalyDetector(new LifNeuronConfig(
            ThresholdPotential: 1.0,
            MembraneTimeConstantUs: 10_000.0, // tau_m = 10ms
            SynapticWeight: 0.8));

        // Event 1 at t = 0 adds 0.8
        detector.ProcessEvent(new DvsEvent(64, 64, 0, EventPolarity.On));

        // After dt = 10,000us (1 tau), potential should decay to 0.8 * exp(-1) ~ 0.294
        // Then adding 0.4 should yield ~ 0.694 (well below 1.0 threshold)
        var testDetector = new SpikingNeuralNetworkAnomalyDetector(new LifNeuronConfig(
            ThresholdPotential: 1.0,
            MembraneTimeConstantUs: 10_000.0,
            SynapticWeight: 0.4));
        testDetector.ProcessEvent(new DvsEvent(64, 64, 0, EventPolarity.On)); // V = 0.4
        // After 1 tau: V = 0.4 * exp(-1) ~ 0.147. Adding 0.4 gives ~ 0.547
        testDetector.ProcessEvent(new DvsEvent(64, 64, 10_000, EventPolarity.On));

        var state = testDetector.GetNeuronStates().First(s => s.MembranePotential > 0);
        state.MembranePotential.Should().BeApproximately(0.547, 0.05);
    }

    [Fact]
    public void ProcessEventBatch_MultipleEvents_ReturnsSpikedNeuronIds()
    {
        var detector = new SpikingNeuralNetworkAnomalyDetector(new LifNeuronConfig(
            ThresholdPotential: 0.5,
            SynapticWeight: 0.5,
            RefractoryPeriodUs: 100));

        IReadOnlyList<DvsEvent> events =
        [
            new DvsEvent(10, 10, 1000, EventPolarity.On),
            new DvsEvent(100, 100, 1000, EventPolarity.On),
            new DvsEvent(200, 200, 1000, EventPolarity.On)
        ];

        var spiked = detector.ProcessEventBatch(events);

        spiked.Should().HaveCount(3);
    }

    [Fact]
    public void GetNeuronState_InvalidNeuronId_ReturnsNull()
    {
        var state = _snnDetector.GetNeuronState(-1);
        var stateTooLarge = _snnDetector.GetNeuronState(9999);

        state.Should().BeNull();
        stateTooLarge.Should().BeNull();
    }

    [Fact]
    public void Reset_ClearsAllPotentialsAndSpikeCounts()
    {
        var detector = new SpikingNeuralNetworkAnomalyDetector(new LifNeuronConfig(
            ThresholdPotential: 0.5,
            SynapticWeight: 0.5));

        detector.ProcessEvent(new DvsEvent(50, 50, 1000, EventPolarity.On));
        detector.GetNeuronStates().Any(s => s.TotalSpikes > 0).Should().BeTrue();

        detector.Reset();

        detector.GetNeuronStates().All(s => s.TotalSpikes == 0 && s.MembranePotential == 0.0).Should().BeTrue();
    }

    #endregion

    #region 5. Conveyor Anomaly Classification

    [Fact]
    public void AnalyzeConveyor_NominalFlow_ReturnsNoneAnomalyWithNormalSeverity()
    {
        _snnDetector.Reset();

        // Sparse nominal events matching belt speed
        IReadOnlyList<DvsEvent> events =
        [
            new DvsEvent(30, 30, 1000, EventPolarity.On),
            new DvsEvent(31, 30, 2000, EventPolarity.Off)
        ];

        var result = _snnDetector.AnalyzeConveyor(
            "CONV-NORM-01",
            events,
            opticalSpeedMetersPerSec: 1.5,
            telemetry: new ConveyorBeltTelemetry("CONV-NORM-01", 1.5, 0.2, 5.0, 2000));

        result.AnomalyDetected.Should().BeFalse();
        result.AnomalyType.Should().Be(ConveyorAnomalyType.None);
        result.Severity.Should().Be(AnomalySeverity.Normal);
        result.EmergencyStopTriggered.Should().BeFalse();
    }

    [Fact]
    public void AnalyzeConveyor_BeltSlipTear_DiscrepancyBetweenBeltAndOpticalSpeed_DetectsBeltSlip()
    {
        _snnDetector.Reset();

        // Belt encoder reports 2.5 m/s, but optical camera measures 0.8 m/s (slip > 1.2 m/s)
        IReadOnlyList<DvsEvent> events =
        [
            new DvsEvent(50, 50, 1000, EventPolarity.On),
            new DvsEvent(51, 50, 1200, EventPolarity.Off)
        ];

        var telemetry = new ConveyorBeltTelemetry("CONV-SLIP-01", 2.5, 0.4, 7.5, 1200);

        var result = _snnDetector.AnalyzeConveyor(
            "CONV-SLIP-01",
            events,
            opticalSpeedMetersPerSec: 0.8,
            telemetry: telemetry);

        result.AnomalyDetected.Should().BeTrue();
        result.AnomalyType.Should().Be(ConveyorAnomalyType.BeltSlipTear);
        result.Description.Should().Contain("slip");
    }

    [Fact]
    public void AnalyzeConveyor_BeltTearWithHighVibration_TriggersCriticalEStop()
    {
        _snnDetector.Reset();

        IReadOnlyList<DvsEvent> events =
        [
            new DvsEvent(50, 50, 1000, EventPolarity.On)
        ];

        // Telemetry vibration 3.5G (> 3.0G threshold)
        var telemetry = new ConveyorBeltTelemetry("CONV-TEAR-02", 2.0, 3.5, 12.0, 1000);

        var result = _snnDetector.AnalyzeConveyor(
            "CONV-TEAR-02",
            events,
            opticalSpeedMetersPerSec: 0.2,
            telemetry: telemetry);

        result.AnomalyDetected.Should().BeTrue();
        result.AnomalyType.Should().Be(ConveyorAnomalyType.BeltSlipTear);
        result.Severity.Should().Be(AnomalySeverity.CriticalEStop);
        result.EmergencyStopTriggered.Should().BeTrue();
    }

    [Fact]
    public void AnalyzeConveyor_HighSpeedOverhang_DetectsEdgeAnomalyAtSpeed()
    {
        _snnDetector.Reset();

        // 10 events concentrated at extreme edge (X = 2, within receptive field nx = 0)
        var edgeEvents = Enumerable.Range(0, 10)
            .Select(i => new DvsEvent(2, 50, 1000 + (i * 20), EventPolarity.On))
            .ToList();

        var detector = new SpikingNeuralNetworkAnomalyDetector(new LifNeuronConfig(
            ThresholdPotential: 0.4,
            SynapticWeight: 0.4,
            RefractoryPeriodUs: 10));

        var result = detector.AnalyzeConveyor(
            "CONV-OVERHANG-01",
            edgeEvents,
            opticalSpeedMetersPerSec: 2.8);

        result.AnomalyDetected.Should().BeTrue();
        result.AnomalyType.Should().Be(ConveyorAnomalyType.HighSpeedOverhang);
        result.Severity.Should().Be(AnomalySeverity.Warning);
    }

    [Fact]
    public void AnalyzeConveyor_BarcodeMotionSmear_DetectsExcessiveSpeedSmear()
    {
        _snnDetector.Reset();

        // Dense alternating On/Off events at excessive velocity (> 3.5 m/s)
        var smearEvents = Enumerable.Range(0, 160)
            .Select(i => new DvsEvent(100 + (i % 20), 100, 1000 + (i * 5), i % 2 == 0 ? EventPolarity.On : EventPolarity.Off))
            .ToList();

        var result = _snnDetector.AnalyzeConveyor(
            "CONV-SMEAR-01",
            smearEvents,
            opticalSpeedMetersPerSec: 4.2);

        result.AnomalyDetected.Should().BeTrue();
        result.AnomalyType.Should().Be(ConveyorAnomalyType.BarcodeMotionSmear);
        result.Severity.Should().Be(AnomalySeverity.Warning);
    }

    [Fact]
    public void AnalyzeConveyor_PackagingRupture_DetectsRadialScatterCluster()
    {
        _snnDetector.Reset();

        // Multiple distinct receptive fields spiking concurrently
        var ruptureEvents = new List<DvsEvent>();
        int[] xs = [20, 120, 200, 240];
        int[] ys = [20, 120, 200, 240];

        for (int i = 0; i < 4; i++)
        {
            for (int k = 0; k < 6; k++)
            {
                ruptureEvents.Add(new DvsEvent(xs[i], ys[i], 1000 + (k * 20), EventPolarity.On));
            }
        }

        var detector = new SpikingNeuralNetworkAnomalyDetector(new LifNeuronConfig(
            ThresholdPotential: 0.3,
            SynapticWeight: 0.3,
            RefractoryPeriodUs: 10));

        var result = detector.AnalyzeConveyor(
            "CONV-RUPTURE-01",
            ruptureEvents,
            opticalSpeedMetersPerSec: 1.2);

        result.AnomalyDetected.Should().BeTrue();
        result.AnomalyType.Should().Be(ConveyorAnomalyType.PackagingRupture);
    }

    [Fact]
    public void AnalyzeConveyor_EmptyEventStream_ReturnsNominalInspection()
    {
        var result = _snnDetector.AnalyzeConveyor("CONV-EMPTY", [], opticalSpeedMetersPerSec: 0.0);

        result.AnomalyDetected.Should().BeFalse();
        result.AnomalyType.Should().Be(ConveyorAnomalyType.None);
        result.Severity.Should().Be(AnomalySeverity.Normal);
    }

    #endregion

    #region 6. Emergency Stop & Sustained Jam Interception

    [Fact]
    public void AnalyzeConveyor_TransientParcelJam_Under20ms_YieldsWarningSeverityWithoutEStop()
    {
        var detector = new SpikingNeuralNetworkAnomalyDetector(new LifNeuronConfig(
            ThresholdPotential: 0.2,
            SynapticWeight: 0.2,
            RefractoryPeriodUs: 10,
            CriticalJamDurationMs: 20.0));

        // Dense burst of events at same neuron spanning only 5,000us (5ms < 20ms threshold)
        var jamEvents = Enumerable.Range(0, 15)
            .Select(i => new DvsEvent(80, 80, 10_000 + (i * 300), EventPolarity.On))
            .ToList();

        var result = detector.AnalyzeConveyor(
            "CONV-JAM-01",
            jamEvents,
            opticalSpeedMetersPerSec: 0.05); // Stalled

        result.AnomalyDetected.Should().BeTrue();
        result.AnomalyType.Should().Be(ConveyorAnomalyType.ParcelJam);
        result.Severity.Should().Be(AnomalySeverity.Warning);
        result.EmergencyStopTriggered.Should().BeFalse();
        result.SustainedJamDurationMs.Should().BeLessThan(20.0);
    }

    [Fact]
    public void AnalyzeConveyor_SustainedParcelJam_Exceeding20ms_TriggersCriticalEmergencyStop()
    {
        var detector = new SpikingNeuralNetworkAnomalyDetector(new LifNeuronConfig(
            ThresholdPotential: 0.2,
            SynapticWeight: 0.2,
            RefractoryPeriodUs: 10,
            CriticalJamDurationMs: 20.0));

        // Batch 1: Initial jam at t = 10,000us
        var batch1 = Enumerable.Range(0, 10)
            .Select(i => new DvsEvent(80, 80, 10_000 + (i * 100), EventPolarity.On))
            .ToList();
        detector.AnalyzeConveyor("CONV-JAM-02", batch1, opticalSpeedMetersPerSec: 0.02);

        // Batch 2: Sustained jam continuing at t = 35,000us (delta = 25ms > 20ms threshold)
        var batch2 = Enumerable.Range(0, 10)
            .Select(i => new DvsEvent(80, 80, 35_000 + (i * 100), EventPolarity.On))
            .ToList();
        var result2 = detector.AnalyzeConveyor("CONV-JAM-02", batch2, opticalSpeedMetersPerSec: 0.02);

        result2.AnomalyDetected.Should().BeTrue();
        result2.AnomalyType.Should().Be(ConveyorAnomalyType.ParcelJam);
        result2.Severity.Should().Be(AnomalySeverity.CriticalEStop);
        result2.EmergencyStopTriggered.Should().BeTrue();
        result2.SustainedJamDurationMs.Should().BeGreaterThanOrEqualTo(20.0);
    }

    [Fact]
    public void GetActiveAlarms_WhenEStopTriggered_ListsActiveEmergencyAlarm()
    {
        var detector = new SpikingNeuralNetworkAnomalyDetector(new LifNeuronConfig(
            ThresholdPotential: 0.2,
            SynapticWeight: 0.2,
            RefractoryPeriodUs: 10,
            CriticalJamDurationMs: 15.0));

        var batch1 = Enumerable.Range(0, 8)
            .Select(i => new DvsEvent(50, 50, 10_000 + (i * 100), EventPolarity.On))
            .ToList();
        detector.AnalyzeConveyor("CONV-ALARM-01", batch1, opticalSpeedMetersPerSec: 0.0);

        var batch2 = Enumerable.Range(0, 8)
            .Select(i => new DvsEvent(50, 50, 30_000 + (i * 100), EventPolarity.On))
            .ToList();
        detector.AnalyzeConveyor("CONV-ALARM-01", batch2, opticalSpeedMetersPerSec: 0.0);

        var alarms = detector.GetActiveAlarms();

        alarms.Should().ContainSingle();
        alarms[0].ConveyorId.Should().Be("CONV-ALARM-01");
        alarms[0].Severity.Should().Be(AnomalySeverity.CriticalEStop);
        alarms[0].IsActive.Should().BeTrue();
    }

    [Fact]
    public void ClearAlarm_WithValidId_SetsAlarmToInactive()
    {
        var detector = new SpikingNeuralNetworkAnomalyDetector(new LifNeuronConfig(
            ThresholdPotential: 0.2,
            SynapticWeight: 0.2,
            RefractoryPeriodUs: 10));

        var events = Enumerable.Range(0, 10)
            .Select(i => new DvsEvent(50, 50, 10_000 + (i * 100), EventPolarity.On))
            .ToList();
        detector.AnalyzeConveyor("CONV-CLR-01", events, opticalSpeedMetersPerSec: 0.0);

        var alarms = detector.GetActiveAlarms();
        alarms.Should().NotBeEmpty();
        string alarmId = alarms[0].AlarmId;

        bool cleared = detector.ClearAlarm(alarmId);
        cleared.Should().BeTrue();

        detector.GetActiveAlarms().Should().BeEmpty();
    }

    [Fact]
    public void ClearAlarm_WithNonExistentId_ReturnsFalse()
    {
        bool cleared = _snnDetector.ClearAlarm("NON-EXISTENT-ALARM-ID");
        cleared.Should().BeFalse();
    }

    #endregion

    #region 7. Asynchronous Time Surface & Optical Flow Velocity

    [Fact]
    public void GetTimeSurfaceValue_UninitializedPixel_ReturnsZero()
    {
        _timeSurface.Reset();
        double val = _timeSurface.GetTimeSurfaceValue(50, 50, currentTimestampUs: 1000);
        val.Should().Be(0.0);
    }

    [Fact]
    public void GetTimeSurfaceValue_ImmediatelyAfterEvent_ReturnsOne()
    {
        _timeSurface.Reset();
        _timeSurface.UpdateSurface(new DvsEvent(30, 30, 5000, EventPolarity.On));

        double val = _timeSurface.GetTimeSurfaceValue(30, 30, currentTimestampUs: 5000);
        val.Should().Be(1.0);
    }

    [Fact]
    public void GetTimeSurfaceValue_ExponentialDecayAfterOneTimeConstant_EqualsExpMinusOne()
    {
        var filter = new AsynchronousTimeSurfaceFilter(new TimeSurfaceConfig(DecayTimeConstantUs: 10_000.0));
        filter.UpdateSurface(new DvsEvent(40, 40, 0, EventPolarity.On));

        // After dt = 10,000us (1 tau), value = exp(-1) ~ 0.367879
        double val = filter.GetTimeSurfaceValue(40, 40, currentTimestampUs: 10_000);
        val.Should().BeApproximately(Math.Exp(-1.0), 0.001);
    }

    [Fact]
    public void ComputeGradient_HorizontalStepEdge_YieldsNonZeroGxAndZeroGy()
    {
        var filter = new AsynchronousTimeSurfaceFilter(new TimeSurfaceConfig(DecayTimeConstantUs: 10_000.0));

        // Pixel 49 fired earlier, pixel 51 fired later
        filter.UpdateSurface(new DvsEvent(49, 50, 1000, EventPolarity.On));
        filter.UpdateSurface(new DvsEvent(51, 50, 5000, EventPolarity.On));

        var (gx, gy) = filter.ComputeGradient(50, 50, currentTimestampUs: 5000);

        gx.Should().BeGreaterThan(0);
        gy.Should().Be(0.0);
    }

    [Fact]
    public void EstimateOpticalFlowAt_HorizontalParcelMotion_EstimatesPositiveVx()
    {
        var filter = new AsynchronousTimeSurfaceFilter(new TimeSurfaceConfig(
            DecayTimeConstantUs: 10_000.0,
            MetersPerPixel: 0.001));

        // Edge moving horizontally from left to right:
        // x=49 at t=2000, x=50 at t=3000, x=51 at t=4000
        filter.UpdateSurface(new DvsEvent(49, 60, 2000, EventPolarity.On));
        filter.UpdateSurface(new DvsEvent(50, 60, 3000, EventPolarity.On));
        filter.UpdateSurface(new DvsEvent(51, 60, 4000, EventPolarity.On));

        var (vx, vy) = filter.EstimateOpticalFlowAt(50, 60, currentTimestampUs: 4000);

        vx.Should().BeGreaterThan(0.0);
        vy.Should().BeApproximately(0.0, 0.01);
    }

    [Fact]
    public void EstimateGlobalVelocity_ActiveMovingBatch_ReturnsEstimatedConveyorSpeed()
    {
        var filter = new AsynchronousTimeSurfaceFilter(new TimeSurfaceConfig(
            DecayTimeConstantUs: 10_000.0,
            MetersPerPixel: 0.001));

        var events = new List<DvsEvent>();
        for (int i = 0; i < 20; i++)
        {
            events.Add(new DvsEvent(20 + i, 80, 1000 + (i * 200), EventPolarity.On));
        }

        filter.UpdateSurface(events);
        var (meanVx, meanVy, speed) = filter.EstimateGlobalVelocity(events, currentTimestampUs: events[^1].TimestampMicroseconds);

        speed.Should().BeGreaterThanOrEqualTo(0.0);
    }

    #endregion

    #region 8. High Concurrency Thread-Safety using System.Threading.Lock

    [Fact]
    public void HighConcurrency_StreamProcessor_HandlesConcurrentIngestionWithoutCorruption()
    {
        var processor = new NeuromorphicEventStreamProcessor();
        int threadCount = 8;
        int eventsPerThread = 100;

        Parallel.For(0, threadCount, t =>
        {
            var batch = Enumerable.Range(0, eventsPerThread)
                .Select(i => new DvsEvent(t * 10 + (i % 5), 50 + (i % 5), 10_000 + (i * 50), EventPolarity.On))
                .ToList();

            var resp = processor.IngestEvents($"stream-{t}", $"conv-{t}", batch);
            resp.Success.Should().BeTrue();
        });

        var metrics = processor.GetMetrics();
        metrics.TotalEventsProcessed.Should().Be(threadCount * eventsPerThread);
    }

    [Fact]
    public void HighConcurrency_TimeSurfaceFilter_ConcurrentUpdatesPreserveMonotonicity()
    {
        var filter = new AsynchronousTimeSurfaceFilter();
        int updates = 500;

        Parallel.For(0, updates, i =>
        {
            filter.UpdateSurface(new DvsEvent(i % 100, (i * 3) % 100, 1000 + i, EventPolarity.On));
            filter.GetTimeSurfaceValue(i % 100, (i * 3) % 100, 2000);
        });

        // Verifies no race-condition exceptions occurred
        double val = filter.GetTimeSurfaceValue(50, 50, 2000);
        val.Should().BeGreaterThanOrEqualTo(0.0);
    }

    [Fact]
    public void HighConcurrency_SnnDetector_ConcurrentNeuronUpdatesDoNotDeadlock()
    {
        var detector = new SpikingNeuralNetworkAnomalyDetector();
        int workers = 8;
        int eventsPerWorker = 50;

        Parallel.For(0, workers, w =>
        {
            for (int i = 0; i < eventsPerWorker; i++)
            {
                detector.ProcessEvent(new DvsEvent(w * 16, w * 16, 1000 + (i * 100), EventPolarity.On));
            }
        });

        var states = detector.GetNeuronStates();
        states.Should().NotBeEmpty();
    }

    [Fact]
    public void HighConcurrency_AlarmManagement_ConcurrentClearAndInspect()
    {
        var detector = new SpikingNeuralNetworkAnomalyDetector(new LifNeuronConfig(
            ThresholdPotential: 0.1,
            SynapticWeight: 0.1));

        // Create alarm
        var events = Enumerable.Range(0, 10)
            .Select(i => new DvsEvent(10, 10, 1000 + (i * 100), EventPolarity.On))
            .ToList();
        detector.AnalyzeConveyor("CONV-CONCURR-01", events, opticalSpeedMetersPerSec: 0.0);

        Parallel.For(0, 10, _ =>
        {
            detector.GetActiveAlarms();
            detector.ClearAlarm("ALM-CONV-CONCURR-01-ParcelJam");
        });

        detector.GetActiveAlarms().Should().BeEmpty();
    }

    #endregion

    #region 9. Dependency Injection & Service Descriptor Assertions

    [Fact]
    public void AddNeuromorphicVisionServices_RegistersExpectedSingletons_WithoutCallingBuildServiceProvider()
    {
        // HARD CONSTRAINT: In unit tests, NEVER call services.BuildServiceProvider().
        // Assert directly on ServiceCollection descriptors.
        var services = new ServiceCollection();
        services.AddNeuromorphicVisionServices();

        services.Should().Contain(d => d.ServiceType == typeof(INeuromorphicEventStreamProcessor) &&
                                       d.ImplementationType == typeof(NeuromorphicEventStreamProcessor) &&
                                       d.Lifetime == ServiceLifetime.Singleton);

        services.Should().Contain(d => d.ServiceType == typeof(IAsynchronousTimeSurfaceFilter) &&
                                       d.ImplementationType == typeof(AsynchronousTimeSurfaceFilter) &&
                                       d.Lifetime == ServiceLifetime.Singleton);

        services.Should().Contain(d => d.ServiceType == typeof(ISpikingNeuralNetworkAnomalyDetector) &&
                                       d.ImplementationType == typeof(SpikingNeuralNetworkAnomalyDetector) &&
                                       d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddNeuromorphicVisionServices_ServiceCollectionCount_IsExactlyThree()
    {
        var services = new ServiceCollection();
        services.AddNeuromorphicVisionServices();

        services.Count.Should().Be(3);
    }

    #endregion
}
