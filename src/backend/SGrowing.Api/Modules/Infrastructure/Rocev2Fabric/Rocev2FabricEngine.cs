using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Infrastructure.Rocev2Fabric;

public interface IRocev2FabricEngine
{
    RoceNodeEndpoint RegisterNode(RegisterRoceNodeRequest request);
    IReadOnlyList<RoceNodeEndpoint> GetNodes();
    FabricTelemetrySample IngestTelemetry(string nodeId, double throughputGbps, double bufferOccupancy, int pfcTx, int pfcRx);
    IReadOnlyList<PfcDeadlockAlert> GetActiveDeadlockAlerts();
    PfcDeadlockAlert TriggerWatchdogQueueDrain(string nodeId);
    DcqcnTuningProfile GetDcqcnTuningProfile();
    RoceFabricSummary GetFabricSummary();
}

public class Rocev2FabricEngine : IRocev2FabricEngine
{
    private readonly ConcurrentDictionary<string, RoceNodeEndpoint> _nodes = new();
    private readonly ConcurrentBag<FabricTelemetrySample> _telemetrySamples = new();
    private readonly ConcurrentBag<PfcDeadlockAlert> _deadlockAlerts = new();

    public Rocev2FabricEngine()
    {
        SeedInitialFabric();
    }

    private void SeedInitialFabric()
    {
        var node1 = new RoceNodeEndpoint(
            NodeId: "ROCE-GPU-01",
            Hostname: "gpu-worker-us-east-h100-01",
            CloudProvider: "BareMetal",
            ClusterRegion: "us-east-fabric-01",
            NicModel: "NVIDIA Mellanox ConnectX-7 400Gbps",
            GpuCount: 8,
            PfcPriorityQueue: 3,
            LosslessRoceEnabled: true,
            RegisteredAtUtc: DateTime.UtcNow.AddDays(-10)
        );

        var node2 = new RoceNodeEndpoint(
            NodeId: "ROCE-GPU-02",
            Hostname: "gpu-worker-azure-west-b200-02",
            CloudProvider: "Azure",
            ClusterRegion: "westus2-ai-zone",
            NicModel: "NVIDIA Quantum-2 CX7 400G",
            GpuCount: 8,
            PfcPriorityQueue: 3,
            LosslessRoceEnabled: true,
            RegisteredAtUtc: DateTime.UtcNow.AddDays(-8)
        );

        _nodes[node1.NodeId] = node1;
        _nodes[node2.NodeId] = node2;
    }

    public RoceNodeEndpoint RegisterNode(RegisterRoceNodeRequest request)
    {
        var nodeId = $"ROCE-GPU-{FabricSeed()}";
        var node = new RoceNodeEndpoint(
            NodeId: nodeId,
            Hostname: request.Hostname,
            CloudProvider: request.CloudProvider,
            ClusterRegion: request.ClusterRegion,
            NicModel: string.IsNullOrWhiteSpace(request.NicModel) ? "ConnectX-7 400Gbps" : request.NicModel,
            GpuCount: request.GpuCount > 0 ? request.GpuCount : 8,
            PfcPriorityQueue: request.PfcPriorityQueue > 0 ? request.PfcPriorityQueue : 3,
            LosslessRoceEnabled: true,
            RegisteredAtUtc: DateTime.UtcNow
        );

        _nodes[nodeId] = node;
        return node;
    }

    public IReadOnlyList<RoceNodeEndpoint> GetNodes()
    {
        return _nodes.Values.OrderBy(n => n.NodeId).ToList();
    }

    public FabricTelemetrySample IngestTelemetry(string nodeId, double throughputGbps, double bufferOccupancy, int pfcTx, int pfcRx)
    {
        int ecn = bufferOccupancy > 65.0 ? (int)((bufferOccupancy - 65.0) * 120) : 0;
        double tailLatency = bufferOccupancy > 80.0 ? 28.5 : 4.2;

        var sample = new FabricTelemetrySample(
            SampleId: $"SMPL-{FabricSeed()}",
            NodeId: nodeId,
            ThroughputGbps: throughputGbps,
            BufferOccupancyPercentage: bufferOccupancy,
            EcnMarkedPacketsPerSec: ecn,
            PfcPauseFramesTxPerSec: pfcTx,
            PfcPauseFramesRxPerSec: pfcRx,
            TailLatencyMicros: tailLatency,
            CapturedAtUtc: DateTime.UtcNow
        );

        _telemetrySamples.Add(sample);

        // Detect deadlock if PFC TX and RX frames both exceed threshold and buffer is locked > 90%
        if (pfcTx > 1000 && pfcRx > 1000 && bufferOccupancy >= 90.0)
        {
            var alert = new PfcDeadlockAlert(
                AlertId: $"DEADLOCK-{FabricSeed()}",
                NodeId: nodeId,
                SpineSwitchId: "SPINE-LEAF-TOR-04",
                StuckQueuePriority: 3,
                DeadlockDurationMs: 450,
                AutomatedMitigationAction: "Watchdog Auto-Drain: Injected transient packet drop on Queue 3 to break PFC pause loop",
                QueueDrainedSuccessfully: true,
                DetectedAtUtc: DateTime.UtcNow
            );
            _deadlockAlerts.Add(alert);
        }

        return sample;
    }

    public IReadOnlyList<PfcDeadlockAlert> GetActiveDeadlockAlerts()
    {
        return _deadlockAlerts.OrderByDescending(a => a.DetectedAtUtc).ToList();
    }

    public PfcDeadlockAlert TriggerWatchdogQueueDrain(string nodeId)
    {
        var alert = new PfcDeadlockAlert(
            AlertId: $"DEADLOCK-MANUAL-{FabricSeed()}",
            NodeId: nodeId,
            SpineSwitchId: "SPINE-FABRIC-DIRECTOR-01",
            StuckQueuePriority: 3,
            DeadlockDurationMs: 120,
            AutomatedMitigationAction: "Manual PFC Watchdog Triggered: Flushed stuck buffer frames & restored credit loop",
            QueueDrainedSuccessfully: true,
            DetectedAtUtc: DateTime.UtcNow
        );
        _deadlockAlerts.Add(alert);
        return alert;
    }

    public DcqcnTuningProfile GetDcqcnTuningProfile()
    {
        return new DcqcnTuningProfile(
            ProfileId: "DCQCN-AI-OPTIMAL-01",
            AlphaIncreaseStep: 0.05,
            BetaDecreaseFactor: 0.5,
            TargetRateBps: 400_000_000,
            MinRateBps: 10_000_000,
            RecommendationNotes: "Optimized for AllReduce collective communications in 175B+ parameter LLM distributed training clusters.",
            GeneratedAtUtc: DateTime.UtcNow
        );
    }

    public RoceFabricSummary GetFabricSummary()
    {
        int totalNodes = _nodes.Count;
        int totalGpus = _nodes.Values.Sum(n => n.GpuCount);
        double throughput = Math.Max(totalNodes * 0.38, 3.2); // Tbps
        int activeAlerts = _deadlockAlerts.Count(a => !a.QueueDrainedSuccessfully);

        var state = activeAlerts > 0 ? CongestionState.PfcDeadlockCritical : CongestionState.NormalLossless;

        return new RoceFabricSummary(
            TotalGpuNodesConnected: totalNodes,
            TotalActiveGpus: totalGpus,
            AggregateThroughputTbps: throughput,
            GlobalCongestionState: state,
            ActiveDeadlockAlerts: activeAlerts,
            AverageFabricLatencyMicros: 3.8,
            LastAuditedUtc: DateTime.UtcNow
        );
    }

    private static int _seedCounter = 7000;
    private static int FabricSeed() => Interlocked.Increment(ref _seedCounter);
}
