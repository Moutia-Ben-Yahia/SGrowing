using System.Threading;

namespace SGrowing.Api.Modules.Commerce.Neuromorphic;

/// <summary>
/// Spiking Neural Network (SNN) based on Leaky Integrate-and-Fire (LIF) neuron array
/// for detecting conveyor belt anomalies and triggering microsecond emergency stops.
/// </summary>
public sealed class SpikingNeuralNetworkAnomalyDetector : ISpikingNeuralNetworkAnomalyDetector
{
    private const int SensorResolution = 256;

    private readonly Lock _lock = new();
    private readonly LifNeuronConfig _config;
    private readonly int _gridDimension;
    private readonly int _totalNeurons;
    private readonly int _receptiveFieldSize;

    // Neuron internal state arrays
    private readonly double[] _membranePotentials;
    private readonly long[] _lastUpdateTimesUs;
    private readonly long[] _lastSpikeTimesUs;
    private readonly int[] _totalSpikeCounts;

    // Moving spike statistics (Welford's algorithm)
    private double _runningMeanSpikes = 2.0;
    private double _runningVarianceSpikes = 1.0;
    private long _welfordCount = 10;

    // Active alarms & Jam tracking
    private readonly Dictionary<string, NeuromorphicAlarmRecord> _activeAlarms = [];
    private readonly Dictionary<int, long> _clusterJamStartTimesUs = [];

    public SpikingNeuralNetworkAnomalyDetector(LifNeuronConfig? config = null)
    {
        _config = config ?? new LifNeuronConfig();
        _gridDimension = _config.GridDimension > 0 ? _config.GridDimension : 16;
        _totalNeurons = _gridDimension * _gridDimension;
        _receptiveFieldSize = Math.Max(1, SensorResolution / _gridDimension);

        _membranePotentials = new double[_totalNeurons];
        _lastUpdateTimesUs = new long[_totalNeurons];
        _lastSpikeTimesUs = new long[_totalNeurons];
        _totalSpikeCounts = new int[_totalNeurons];

        Reset();
    }

    /// <inheritdoc />
    public bool ProcessEvent(DvsEvent evt)
    {
        int neuronIndex = MapEventToNeuron(evt.X, evt.Y);
        long t = evt.TimestampMicroseconds;

        lock (_lock)
        {
            return UpdateNeuronInternal(neuronIndex, t, _config.SynapticWeight);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<int> ProcessEventBatch(IReadOnlyList<DvsEvent> events)
    {
        if (events == null || events.Count == 0)
        {
            return [];
        }

        var spikedNeurons = new List<int>();

        lock (_lock)
        {
            foreach (var evt in events)
            {
                int neuronIndex = MapEventToNeuron(evt.X, evt.Y);
                if (UpdateNeuronInternal(neuronIndex, evt.TimestampMicroseconds, _config.SynapticWeight))
                {
                    spikedNeurons.Add(neuronIndex);
                }
            }

            // Update running spike distribution statistics
            UpdateMovingStatisticsInternal(spikedNeurons.Count);
        }

        return spikedNeurons;
    }

    /// <inheritdoc />
    public NeuromorphicInspectionResult AnalyzeConveyor(
        string conveyorId,
        IReadOnlyList<DvsEvent> events,
        double opticalSpeedMetersPerSec,
        ConveyorBeltTelemetry? telemetry = null)
    {
        if (events == null || events.Count == 0)
        {
            return new NeuromorphicInspectionResult(
                AnomalyDetected: false,
                AnomalyType: ConveyorAnomalyType.None,
                Severity: AnomalySeverity.Normal,
                Confidence: 1.0,
                Description: "Nominal conveyor status. No active optical events.",
                DetectionTimestampMicroseconds: telemetry?.TimestampMicroseconds ?? 0,
                SustainedJamDurationMs: 0.0,
                EmergencyStopTriggered: false,
                HotspotEvents: [],
                EstimatedOpticalSpeedMetersPerSec: opticalSpeedMetersPerSec);
        }

        var spikedNeurons = ProcessEventBatch(events);
        long latestTimestamp = events[^1].TimestampMicroseconds;

        lock (_lock)
        {
            // Count spikes per neuron in this batch
            var neuronBatchSpikes = new Dictionary<int, int>();
            foreach (var nid in spikedNeurons)
            {
                neuronBatchSpikes[nid] = neuronBatchSpikes.GetValueOrDefault(nid) + 1;
            }

            double runningStdDev = Math.Sqrt(Math.Max(0.1, _runningVarianceSpikes));
            double anomalyThreshold = Math.Clamp(_runningMeanSpikes + (_config.AnomalySigmaThreshold * runningStdDev), 3.0, 5.0);

            // Find clusters exceeding 3.5 sigma
            var hotspotClusters = neuronBatchSpikes
                .Where(kvp => kvp.Value >= anomalyThreshold)
                .OrderByDescending(kvp => kvp.Value)
                .ToList();

            // 1. Check for Belt Slip or Tear (Speed Mismatch or High Vibration)
            if (telemetry != null)
            {
                double speedDiff = Math.Abs(telemetry.BeltSpeedMetersPerSec - opticalSpeedMetersPerSec);
                bool slipDetected = speedDiff > 1.2 && telemetry.BeltSpeedMetersPerSec > 0.5;
                bool highVibration = telemetry.VibrationLevelG > 1.5;

                // Check for transverse line of spikes (row/column pattern across belt)
                bool transversePattern = CheckTransverseSpikeLine(neuronBatchSpikes);

                if (slipDetected || (highVibration && transversePattern))
                {
                    var severity = (telemetry.VibrationLevelG > 3.0 || speedDiff > 2.5)
                        ? AnomalySeverity.CriticalEStop
                        : AnomalySeverity.Warning;

                    string desc = $"Belt slip/tear detected on conveyor '{conveyorId}'. Telemetry speed: {telemetry.BeltSpeedMetersPerSec:F2} m/s, Optical speed: {opticalSpeedMetersPerSec:F2} m/s (Diff: {speedDiff:F2} m/s). Vibration: {telemetry.VibrationLevelG:F2}G.";
                    RecordAlarm(conveyorId, ConveyorAnomalyType.BeltSlipTear, severity, desc, latestTimestamp);

                    return new NeuromorphicInspectionResult(
                        AnomalyDetected: true,
                        AnomalyType: ConveyorAnomalyType.BeltSlipTear,
                        Severity: severity,
                        Confidence: Math.Min(1.0, 0.70 + (speedDiff / 5.0)),
                        Description: desc,
                        DetectionTimestampMicroseconds: latestTimestamp,
                        SustainedJamDurationMs: 0.0,
                        EmergencyStopTriggered: severity == AnomalySeverity.CriticalEStop,
                        HotspotEvents: [.. events.Take(20)],
                        EstimatedOpticalSpeedMetersPerSec: opticalSpeedMetersPerSec);
                }
            }

            // 2. Check for High Speed Overhang (spikes at edge boundaries)
            bool edgeOverhang = CheckEdgeOverhang(neuronBatchSpikes);
            if (edgeOverhang && opticalSpeedMetersPerSec > 2.0)
            {
                string desc = $"Payload lateral overhang detected at belt boundary on conveyor '{conveyorId}' travelling at {opticalSpeedMetersPerSec:F2} m/s.";
                RecordAlarm(conveyorId, ConveyorAnomalyType.HighSpeedOverhang, AnomalySeverity.Warning, desc, latestTimestamp);

                return new NeuromorphicInspectionResult(
                    AnomalyDetected: true,
                    AnomalyType: ConveyorAnomalyType.HighSpeedOverhang,
                    Severity: AnomalySeverity.Warning,
                    Confidence: 0.88,
                    Description: desc,
                    DetectionTimestampMicroseconds: latestTimestamp,
                    SustainedJamDurationMs: 0.0,
                    EmergencyStopTriggered: false,
                    HotspotEvents: [.. events.Take(20)],
                    EstimatedOpticalSpeedMetersPerSec: opticalSpeedMetersPerSec);
            }

            // 3. Check for Barcode Motion Smear (excessive speed with alternating polarity spikes)
            if (opticalSpeedMetersPerSec > 3.5 && events.Count > 150)
            {
                int onCount = events.Count(e => e.Polarity == EventPolarity.On);
                int offCount = events.Count(e => e.Polarity == EventPolarity.Off);
                double balance = (double)Math.Min(onCount, offCount) / Math.Max(1, Math.Max(onCount, offCount));
                if (balance > 0.65)
                {
                    string desc = $"Barcode motion smear detected on high-speed conveyor '{conveyorId}' at {opticalSpeedMetersPerSec:F2} m/s.";
                    RecordAlarm(conveyorId, ConveyorAnomalyType.BarcodeMotionSmear, AnomalySeverity.Warning, desc, latestTimestamp);

                    return new NeuromorphicInspectionResult(
                        AnomalyDetected: true,
                        AnomalyType: ConveyorAnomalyType.BarcodeMotionSmear,
                        Severity: AnomalySeverity.Warning,
                        Confidence: 0.85,
                        Description: desc,
                        DetectionTimestampMicroseconds: latestTimestamp,
                        SustainedJamDurationMs: 0.0,
                        EmergencyStopTriggered: false,
                        HotspotEvents: [.. events.Take(20)],
                        EstimatedOpticalSpeedMetersPerSec: opticalSpeedMetersPerSec);
                }
            }

            // 4. Check for Packaging Rupture (localized explosion of high-frequency events across multiple neighbors)
            if (hotspotClusters.Count >= 4 && opticalSpeedMetersPerSec > 0.5)
            {
                bool isScattered = CheckSpatialDispersion(hotspotClusters.Select(c => c.Key).ToList());
                if (isScattered)
                {
                    string desc = $"Packaging rupture detected on conveyor '{conveyorId}'. Radial payload scatter detected across {hotspotClusters.Count} spiking zones.";
                    RecordAlarm(conveyorId, ConveyorAnomalyType.PackagingRupture, AnomalySeverity.Warning, desc, latestTimestamp);

                    return new NeuromorphicInspectionResult(
                        AnomalyDetected: true,
                        AnomalyType: ConveyorAnomalyType.PackagingRupture,
                        Severity: AnomalySeverity.Warning,
                        Confidence: 0.91,
                        Description: desc,
                        DetectionTimestampMicroseconds: latestTimestamp,
                        SustainedJamDurationMs: 0.0,
                        EmergencyStopTriggered: false,
                        HotspotEvents: [.. events.Take(20)],
                        EstimatedOpticalSpeedMetersPerSec: opticalSpeedMetersPerSec);
                }
            }

            // 5. Check for Parcel Jam (localized stationary spike cluster exceeding 3.5 sigma)
            if (hotspotClusters.Count > 0)
            {
                var topCluster = hotspotClusters[0];
                int clusterId = topCluster.Key;

                // Stalled motion condition: optical speed is near zero (< 0.2 m/s)
                bool isStalled = opticalSpeedMetersPerSec < 0.20;

                if (isStalled)
                {
                    if (!_clusterJamStartTimesUs.TryGetValue(clusterId, out long jamStartUs))
                    {
                        jamStartUs = latestTimestamp;
                        _clusterJamStartTimesUs[clusterId] = jamStartUs;
                    }

                    double sustainedMs = Math.Max(0.0, (latestTimestamp - jamStartUs) / 1000.0);
                    bool eStop = sustainedMs >= _config.CriticalJamDurationMs;
                    var severity = eStop ? AnomalySeverity.CriticalEStop : AnomalySeverity.Warning;

                    string desc = eStop
                        ? $"CRITICAL EMERGENCY STOP: Parcel jam sustained for {sustainedMs:F1}ms (> {_config.CriticalJamDurationMs}ms threshold) at neuron cluster {clusterId} on conveyor '{conveyorId}'."
                        : $"Warning: Localized parcel jam accumulation ({sustainedMs:F1}ms) at neuron cluster {clusterId} on conveyor '{conveyorId}'.";

                    RecordAlarm(conveyorId, ConveyorAnomalyType.ParcelJam, severity, desc, latestTimestamp);

                    return new NeuromorphicInspectionResult(
                        AnomalyDetected: true,
                        AnomalyType: ConveyorAnomalyType.ParcelJam,
                        Severity: severity,
                        Confidence: Math.Min(0.99, 0.80 + (sustainedMs / 100.0)),
                        Description: desc,
                        DetectionTimestampMicroseconds: latestTimestamp,
                        SustainedJamDurationMs: sustainedMs,
                        EmergencyStopTriggered: eStop,
                        HotspotEvents: [.. events.Where(e => MapEventToNeuron(e.X, e.Y) == clusterId).Take(30)],
                        EstimatedOpticalSpeedMetersPerSec: opticalSpeedMetersPerSec);
                }
            }

            // If no jam in active clusters, prune stale jam timers
            _clusterJamStartTimesUs.Clear();

            // Nominal inspection state
            return new NeuromorphicInspectionResult(
                AnomalyDetected: false,
                AnomalyType: ConveyorAnomalyType.None,
                Severity: AnomalySeverity.Normal,
                Confidence: 0.95,
                Description: "Conveyor status nominal. Spiking activity within normal operational distribution.",
                DetectionTimestampMicroseconds: latestTimestamp,
                SustainedJamDurationMs: 0.0,
                EmergencyStopTriggered: false,
                HotspotEvents: [],
                EstimatedOpticalSpeedMetersPerSec: opticalSpeedMetersPerSec);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<SpikeNeuronState> GetNeuronStates()
    {
        lock (_lock)
        {
            var states = new SpikeNeuronState[_totalNeurons];
            for (int i = 0; i < _totalNeurons; i++)
            {
                int nx = i % _gridDimension;
                int ny = i / _gridDimension;
                long lastSpike = _lastSpikeTimesUs[i];
                long lastUpdate = _lastUpdateTimesUs[i];
                bool inRefractory = lastSpike >= 0 && (lastUpdate - lastSpike) < _config.RefractoryPeriodUs;

                states[i] = new SpikeNeuronState(
                    NeuronId: i,
                    X: nx * _receptiveFieldSize + (_receptiveFieldSize / 2),
                    Y: ny * _receptiveFieldSize + (_receptiveFieldSize / 2),
                    MembranePotential: _membranePotentials[i],
                    LastSpikeTimeMicroseconds: lastSpike,
                    IsInRefractory: inRefractory,
                    TotalSpikes: _totalSpikeCounts[i]);
            }
            return states;
        }
    }

    /// <inheritdoc />
    public SpikeNeuronState? GetNeuronState(int neuronId)
    {
        lock (_lock)
        {
            if (neuronId < 0 || neuronId >= _totalNeurons)
            {
                return null;
            }

            int nx = neuronId % _gridDimension;
            int ny = neuronId / _gridDimension;
            long lastSpike = _lastSpikeTimesUs[neuronId];
            long lastUpdate = _lastUpdateTimesUs[neuronId];
            bool inRefractory = lastSpike >= 0 && (lastUpdate - lastSpike) < _config.RefractoryPeriodUs;

            return new SpikeNeuronState(
                NeuronId: neuronId,
                X: nx * _receptiveFieldSize + (_receptiveFieldSize / 2),
                Y: ny * _receptiveFieldSize + (_receptiveFieldSize / 2),
                MembranePotential: _membranePotentials[neuronId],
                LastSpikeTimeMicroseconds: lastSpike,
                IsInRefractory: inRefractory,
                TotalSpikes: _totalSpikeCounts[neuronId]);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<NeuromorphicAlarmRecord> GetActiveAlarms()
    {
        lock (_lock)
        {
            return [.. _activeAlarms.Values.Where(a => a.IsActive)];
        }
    }

    /// <inheritdoc />
    public bool ClearAlarm(string alarmId)
    {
        lock (_lock)
        {
            if (_activeAlarms.TryGetValue(alarmId, out var alarm))
            {
                _activeAlarms[alarmId] = alarm with { IsActive = false };
                return true;
            }
            return false;
        }
    }

    /// <inheritdoc />
    public void Reset()
    {
        lock (_lock)
        {
            Array.Clear(_membranePotentials, 0, _totalNeurons);
            Array.Fill(_lastUpdateTimesUs, -1L);
            Array.Fill(_lastSpikeTimesUs, -1L);
            Array.Clear(_totalSpikeCounts, 0, _totalNeurons);

            _runningMeanSpikes = 2.0;
            _runningVarianceSpikes = 1.0;
            _welfordCount = 10;

            _activeAlarms.Clear();
            _clusterJamStartTimesUs.Clear();
        }
    }

    private bool UpdateNeuronInternal(int index, long timestampUs, double weight)
    {
        if (index < 0 || index >= _totalNeurons)
        {
            return false;
        }

        long lastSpike = _lastSpikeTimesUs[index];
        long lastUpdate = _lastUpdateTimesUs[index];

        // 1. Refractory period check: Vi(t) = V_reset during refractory period
        if (lastSpike >= 0 && (timestampUs - lastSpike) < _config.RefractoryPeriodUs)
        {
            _lastUpdateTimesUs[index] = timestampUs;
            return false;
        }

        // 2. Exponential membrane potential decay: Vi(t + dt) = Vi(t) * exp(-dt / tau_m)
        double v = _membranePotentials[index];
        if (lastUpdate >= 0 && timestampUs > lastUpdate)
        {
            double dt = timestampUs - lastUpdate;
            v *= Math.Exp(-dt / _config.MembraneTimeConstantUs);
        }

        // 3. Integration of incoming event current
        v += weight;
        _lastUpdateTimesUs[index] = timestampUs;

        // 4. Threshold firing condition: Vi >= V_th
        if (v >= _config.ThresholdPotential)
        {
            _membranePotentials[index] = _config.ResetPotential;
            _lastSpikeTimesUs[index] = timestampUs;
            _totalSpikeCounts[index]++;
            return true;
        }

        _membranePotentials[index] = v;
        return false;
    }

    private void UpdateMovingStatisticsInternal(int batchSpikeCount)
    {
        _welfordCount++;
        double delta = batchSpikeCount - _runningMeanSpikes;
        _runningMeanSpikes += delta / _welfordCount;
        double delta2 = batchSpikeCount - _runningMeanSpikes;
        _runningVarianceSpikes = ((_runningVarianceSpikes * (_welfordCount - 1)) + (delta * delta2)) / _welfordCount;
    }

    private void RecordAlarm(
        string conveyorId,
        ConveyorAnomalyType anomalyType,
        AnomalySeverity severity,
        string message,
        long timestampUs)
    {
        string alarmId = $"ALM-{conveyorId}-{anomalyType}";
        _activeAlarms[alarmId] = new NeuromorphicAlarmRecord(
            AlarmId: alarmId,
            ConveyorId: conveyorId,
            AnomalyType: anomalyType,
            Severity: severity,
            Message: message,
            TimestampMicroseconds: timestampUs,
            IsActive: true);
    }

    private int MapEventToNeuron(int x, int y)
    {
        int clampedX = Math.Clamp(x, 0, SensorResolution - 1);
        int clampedY = Math.Clamp(y, 0, SensorResolution - 1);

        int nx = clampedX / _receptiveFieldSize;
        int ny = clampedY / _receptiveFieldSize;

        if (nx >= _gridDimension) nx = _gridDimension - 1;
        if (ny >= _gridDimension) ny = _gridDimension - 1;

        return (ny * _gridDimension) + nx;
    }

    private bool CheckTransverseSpikeLine(Dictionary<int, int> spikes)
    {
        // Check if an entire horizontal row of neurons is spiking concurrently
        var rowCounts = new int[_gridDimension];
        foreach (var (nid, count) in spikes)
        {
            if (count > 0)
            {
                int ny = nid / _gridDimension;
                rowCounts[ny]++;
            }
        }
        return rowCounts.Any(c => c >= (_gridDimension * 0.70));
    }

    private bool CheckEdgeOverhang(Dictionary<int, int> spikes)
    {
        int edgeSpikeCount = 0;
        foreach (var (nid, count) in spikes)
        {
            int nx = nid % _gridDimension;
            if ((nx == 0 || nx == _gridDimension - 1) && count >= 3)
            {
                edgeSpikeCount += count;
            }
        }
        return edgeSpikeCount >= 6;
    }

    private bool CheckSpatialDispersion(IReadOnlyList<int> clusterIds)
    {
        if (clusterIds.Count < 3) return false;

        var xs = clusterIds.Select(id => id % _gridDimension).ToList();
        var ys = clusterIds.Select(id => id / _gridDimension).ToList();

        double spreadX = xs.Max() - xs.Min();
        double spreadY = ys.Max() - ys.Min();

        return spreadX >= 3 && spreadY >= 3;
    }
}
