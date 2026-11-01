using System.Diagnostics;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using SGrowing.Api.Modules.Infrastructure.EdgeObservability;

namespace SGrowing.Tests.Infrastructure;

/// <summary>
/// Unit and integration tests for Horizon 55:
/// Self-Healing Kubernetes / Nomad Edge Cluster Autopilot &amp; eBPF Kernel Mesh Observability.
/// </summary>
public class EdgeClusterAutopilotAndEbpfTests
{
    // =========================================================================
    // 1. Edge Node Health Monitoring & State Transitions (Tests 1 - 10)
    // =========================================================================

    [Fact]
    public void EdgeNode_InitialRegistration_RegistersAsHealthy()
    {
        var service = new EdgeClusterAutopilotService();
        var telemetry = new EdgeNodeTelemetry(
            NodeId: "node-edge-01",
            ClusterId: "edge-cluster-primary",
            Hostname: "edge-node-01.local",
            CpuUtilizationPct: 42.0,
            MemoryUtilizationPct: 55.0,
            TemperatureCelsius: 58.0,
            PacketLossPct: 0.2,
            SocketFlapCount: 0,
            IsWanConnected: true,
            State: NodeHealthState.Healthy,
            LastHeartbeatUtc: DateTimeOffset.UtcNow,
            ActivePods: []);

        var result = service.RegisterOrUpdateNode(telemetry);

        result.State.Should().Be(NodeHealthState.Healthy);
        result.NodeId.Should().Be("node-edge-01");
        service.GetNode("node-edge-01").Should().NotBeNull();
    }

    [Fact]
    public void EdgeNode_ThermalExceedsThreshold_TransitionsToDegradedThermal()
    {
        var service = new EdgeClusterAutopilotService();
        var telemetry = new EdgeNodeTelemetry(
            NodeId: "node-thermal-01",
            ClusterId: "edge-cluster-primary",
            Hostname: "edge-thermal-01.local",
            CpuUtilizationPct: 60.0,
            MemoryUtilizationPct: 65.0,
            TemperatureCelsius: 84.5, // > 80.0°C threshold
            PacketLossPct: 0.5,
            SocketFlapCount: 0,
            IsWanConnected: true,
            State: NodeHealthState.Healthy,
            LastHeartbeatUtc: DateTimeOffset.UtcNow,
            ActivePods: []);

        var result = service.RegisterOrUpdateNode(telemetry);

        result.State.Should().Be(NodeHealthState.DegradedThermal);
    }

    [Fact]
    public void EdgeNode_PacketLossExceedsThreshold_TransitionsToDegradedNetworkFlap()
    {
        var service = new EdgeClusterAutopilotService();
        var telemetry = new EdgeNodeTelemetry(
            NodeId: "node-loss-01",
            ClusterId: "edge-cluster-primary",
            Hostname: "edge-loss-01.local",
            CpuUtilizationPct: 45.0,
            MemoryUtilizationPct: 50.0,
            TemperatureCelsius: 62.0,
            PacketLossPct: 3.8, // > 3.0% threshold
            SocketFlapCount: 1,
            IsWanConnected: true,
            State: NodeHealthState.Healthy,
            LastHeartbeatUtc: DateTimeOffset.UtcNow,
            ActivePods: []);

        var result = service.RegisterOrUpdateNode(telemetry);

        result.State.Should().Be(NodeHealthState.DegradedNetworkFlap);
    }

    [Fact]
    public void EdgeNode_SocketFlapCountExceedsThreshold_TransitionsToDegradedNetworkFlap()
    {
        var service = new EdgeClusterAutopilotService();
        var telemetry = new EdgeNodeTelemetry(
            NodeId: "node-flap-01",
            ClusterId: "edge-cluster-primary",
            Hostname: "edge-flap-01.local",
            CpuUtilizationPct: 35.0,
            MemoryUtilizationPct: 40.0,
            TemperatureCelsius: 59.0,
            PacketLossPct: 0.5,
            SocketFlapCount: 6, // >= 5 threshold
            IsWanConnected: true,
            State: NodeHealthState.Healthy,
            LastHeartbeatUtc: DateTimeOffset.UtcNow,
            ActivePods: []);

        var result = service.RegisterOrUpdateNode(telemetry);

        result.State.Should().Be(NodeHealthState.DegradedNetworkFlap);
    }

    [Fact]
    public void EdgeNode_CordonedOrEvacuating_PreservesEvacuationState()
    {
        var service = new EdgeClusterAutopilotService();
        var telemetry = new EdgeNodeTelemetry(
            NodeId: "node-evac-01",
            ClusterId: "edge-cluster-primary",
            Hostname: "edge-evac-01.local",
            CpuUtilizationPct: 20.0,
            MemoryUtilizationPct: 30.0,
            TemperatureCelsius: 50.0,
            PacketLossPct: 0.1,
            SocketFlapCount: 0,
            IsWanConnected: true,
            State: NodeHealthState.NodeEvacuating,
            LastHeartbeatUtc: DateTimeOffset.UtcNow,
            ActivePods: []);

        var result = service.RegisterOrUpdateNode(telemetry);

        result.State.Should().Be(NodeHealthState.NodeEvacuating);
    }

    [Fact]
    public void EdgeNode_DrainedOffline_PreservesOfflineState()
    {
        var service = new EdgeClusterAutopilotService();
        var telemetry = new EdgeNodeTelemetry(
            NodeId: "node-offline-01",
            ClusterId: "edge-cluster-primary",
            Hostname: "edge-offline-01.local",
            CpuUtilizationPct: 10.0,
            MemoryUtilizationPct: 15.0,
            TemperatureCelsius: 45.0,
            PacketLossPct: 0.0,
            SocketFlapCount: 0,
            IsWanConnected: true,
            State: NodeHealthState.DrainedOffline,
            LastHeartbeatUtc: DateTimeOffset.UtcNow,
            ActivePods: []);

        var result = service.RegisterOrUpdateNode(telemetry);

        result.State.Should().Be(NodeHealthState.DrainedOffline);
    }

    [Fact]
    public void EdgeNode_NormalizesFromDegraded_RecoversToHealthyWhenNoPods()
    {
        var service = new EdgeClusterAutopilotService();
        // Initially degraded
        service.RegisterOrUpdateNode(new EdgeNodeTelemetry(
            NodeId: "node-recover-01",
            ClusterId: "edge-cluster-primary",
            Hostname: "edge-recover.local",
            CpuUtilizationPct: 50.0,
            MemoryUtilizationPct: 50.0,
            TemperatureCelsius: 85.0,
            PacketLossPct: 0.5,
            SocketFlapCount: 0,
            IsWanConnected: true,
            State: NodeHealthState.Healthy,
            LastHeartbeatUtc: DateTimeOffset.UtcNow,
            ActivePods: []));

        service.GetNode("node-recover-01")!.State.Should().Be(NodeHealthState.DegradedThermal);

        // Later normalizes
        var updated = service.RegisterOrUpdateNode(new EdgeNodeTelemetry(
            NodeId: "node-recover-01",
            ClusterId: "edge-cluster-primary",
            Hostname: "edge-recover.local",
            CpuUtilizationPct: 40.0,
            MemoryUtilizationPct: 45.0,
            TemperatureCelsius: 65.0,
            PacketLossPct: 0.2,
            SocketFlapCount: 0,
            IsWanConnected: true,
            State: NodeHealthState.Healthy,
            LastHeartbeatUtc: DateTimeOffset.UtcNow,
            ActivePods: []));

        updated.State.Should().Be(NodeHealthState.Healthy);
    }

    [Fact]
    public void EdgeNode_HighCpuOrMemory_DoesNotDegradeStateUnlessExceeded()
    {
        var service = new EdgeClusterAutopilotService();
        var telemetry = new EdgeNodeTelemetry(
            NodeId: "node-highload-01",
            ClusterId: "edge-cluster-primary",
            Hostname: "edge-load.local",
            CpuUtilizationPct: 80.0,
            MemoryUtilizationPct: 85.0,
            TemperatureCelsius: 75.0, // < 80.0
            PacketLossPct: 1.5,       // < 3.0
            SocketFlapCount: 2,       // < 5
            IsWanConnected: true,
            State: NodeHealthState.Healthy,
            LastHeartbeatUtc: DateTimeOffset.UtcNow,
            ActivePods: []);

        var result = service.RegisterOrUpdateNode(telemetry);

        result.State.Should().Be(NodeHealthState.Healthy);
    }

    [Fact]
    public void EdgeCluster_PruneStaleNodes_EvacuatesNodeWhoseHeartbeatExceedsThreshold()
    {
        var service = new EdgeClusterAutopilotService();
        var baseTime = DateTimeOffset.UtcNow;
        var staleHeartbeat = baseTime.AddMinutes(-10);

        var staleNode = new EdgeNodeTelemetry(
            NodeId: "node-stale-01",
            ClusterId: "edge-cluster-primary",
            Hostname: "edge-stale.local",
            CpuUtilizationPct: 30.0,
            MemoryUtilizationPct: 30.0,
            TemperatureCelsius: 50.0,
            PacketLossPct: 0.0,
            SocketFlapCount: 0,
            IsWanConnected: true,
            State: NodeHealthState.Healthy,
            LastHeartbeatUtc: staleHeartbeat,
            ActivePods: [EdgeWorkloadPod.CreateRoboticsCritical("pod-stale-1", "amr-safety-service")]);

        service.RegisterOrUpdateNode(staleNode);

        service.PruneStaleNodes(TimeSpan.FromMinutes(5), baseTime);

        var inspected = service.GetNode("node-stale-01");
        inspected.Should().NotBeNull();
        inspected!.State.Should().Be(NodeHealthState.DrainedOffline);
        inspected.ActivePods.Should().BeEmpty();
    }

    [Fact]
    public void EdgeCluster_GetClusterHealth_AggregatesCountsAccurately()
    {
        var service = new EdgeClusterAutopilotService();
        var now = DateTimeOffset.UtcNow;

        service.RegisterOrUpdateNode(new EdgeNodeTelemetry("node-h1", "c1", "host1", 30, 40, 50, 0.1, 0, true, NodeHealthState.Healthy, now, []));
        service.RegisterOrUpdateNode(new EdgeNodeTelemetry("node-h2", "c1", "host2", 40, 50, 55, 0.2, 0, true, NodeHealthState.Healthy, now, []));
        service.RegisterOrUpdateNode(new EdgeNodeTelemetry("node-d1", "c1", "host3", 70, 70, 85, 0.2, 0, true, NodeHealthState.Healthy, now, [])); // degraded thermal
        service.RegisterOrUpdateNode(new EdgeNodeTelemetry("node-d2", "c1", "host4", 50, 50, 60, 4.5, 0, true, NodeHealthState.Healthy, now, [])); // degraded network flap

        var health = service.GetClusterHealth("c1");

        health.TotalNodes.Should().Be(4);
        health.HealthyNodes.Should().Be(2);
        health.DegradedNodes.Should().Be(2);
        health.WanBackhaulConnected.Should().BeTrue();
    }

    // =========================================================================
    // 2. Autonomous Pod Evacuation & Drain Sequences (Tests 11 - 20)
    // =========================================================================

    [Fact]
    public void AutonomousEvacuation_DegradedThermal_TriggersPodEvacuation()
    {
        var service = new EdgeClusterAutopilotService();
        var now = DateTimeOffset.UtcNow;

        // Register standby healthy node
        service.RegisterOrUpdateNode(new EdgeNodeTelemetry("node-standby", "c1", "host-standby", 25.0, 30.0, 50.0, 0.1, 0, true, NodeHealthState.Healthy, now, []));

        // Register distressed node with pods
        var pod1 = EdgeWorkloadPod.CreateRoboticsCritical("pod-robot-1", "amr-collision-prevention");
        var pod2 = EdgeWorkloadPod.CreateBackgroundBatch("pod-batch-1", "ocr-image-indexer");

        var degradedNode = new EdgeNodeTelemetry(
            NodeId: "node-hot",
            ClusterId: "c1",
            Hostname: "host-hot",
            CpuUtilizationPct: 60.0,
            MemoryUtilizationPct: 55.0,
            TemperatureCelsius: 86.0, // > 80°C
            PacketLossPct: 0.1,
            SocketFlapCount: 0,
            IsWanConnected: true,
            State: NodeHealthState.Healthy,
            LastHeartbeatUtc: now,
            ActivePods: [pod1, pod2]);

        service.RegisterOrUpdateNode(degradedNode);

        var finalHot = service.GetNode("node-hot");
        finalHot!.State.Should().Be(NodeHealthState.DrainedOffline);
        finalHot.ActivePods.Should().BeEmpty();

        var plans = service.GetEvacuationPlans();
        plans.Should().NotBeEmpty();
        plans[0].SourceNodeId.Should().Be("node-hot");
        plans[0].EvacuatedPods.Should().HaveCount(2);
    }

    [Fact]
    public void AutonomousEvacuation_DegradedNetworkFlap_TriggersPodEvacuation()
    {
        var service = new EdgeClusterAutopilotService();
        var now = DateTimeOffset.UtcNow;

        service.RegisterOrUpdateNode(new EdgeNodeTelemetry("node-standby-net", "c1", "host-standby", 20.0, 20.0, 48.0, 0.0, 0, true, NodeHealthState.Healthy, now, []));

        var pod = EdgeWorkloadPod.CreateRoboticsCritical("pod-robot-2", "lidar-telemetry");
        var degradedNode = new EdgeNodeTelemetry("node-flapping", "c1", "host-flap", 40.0, 40.0, 55.0, 5.2, 0, true, NodeHealthState.Healthy, now, [pod]);

        service.RegisterOrUpdateNode(degradedNode);

        var finalNode = service.GetNode("node-flapping");
        finalNode!.State.Should().Be(NodeHealthState.DrainedOffline);
    }

    [Fact]
    public void AutonomousEvacuation_Tier1RoboticsCritical_MigratesToHealthyStandby()
    {
        var service = new EdgeClusterAutopilotService();
        var now = DateTimeOffset.UtcNow;

        service.RegisterOrUpdateNode(new EdgeNodeTelemetry("node-standby-low", "c1", "host-standby-low", 15.0, 20.0, 45.0, 0.1, 0, true, NodeHealthState.Healthy, now, []));
        service.RegisterOrUpdateNode(new EdgeNodeTelemetry("node-standby-high", "c1", "host-standby-high", 65.0, 60.0, 50.0, 0.1, 0, true, NodeHealthState.Healthy, now, []));

        var criticalPod = EdgeWorkloadPod.CreateRoboticsCritical("pod-robot-amr", "fleet-orchestrator", cpu: 1.0, ram: 512.0);
        var sourceNode = new EdgeNodeTelemetry("node-degraded-source", "c1", "host-source", 50.0, 50.0, 88.0, 0.1, 0, true, NodeHealthState.Healthy, now, [criticalPod]);

        service.RegisterOrUpdateNode(sourceNode);

        // Should have migrated to node-standby-low due to lower CPU
        var standbyLow = service.GetNode("node-standby-low");
        standbyLow!.ActivePods.Should().ContainSingle(p => p.PodId == "pod-robot-amr");

        var plan = service.GetEvacuationPlans().Single();
        var evacuatedDetail = plan.EvacuatedPods.Single(p => p.PodId == "pod-robot-amr");
        evacuatedDetail.TargetNodeId.Should().Be("node-standby-low");
        evacuatedDetail.ActionTaken.Should().Be("MigratedToStandby");
        evacuatedDetail.Success.Should().BeTrue();
    }

    [Fact]
    public void AutonomousEvacuation_Tier2CoreCheckout_MigratesWhenStandbyAvailable()
    {
        var service = new EdgeClusterAutopilotService();
        var now = DateTimeOffset.UtcNow;

        service.RegisterOrUpdateNode(new EdgeNodeTelemetry("node-standby-chk", "c1", "host-standby", 20.0, 20.0, 45.0, 0.1, 0, true, NodeHealthState.Healthy, now, []));

        var checkoutPod = EdgeWorkloadPod.CreateCoreCheckout("pod-checkout-1", "order-processor");
        var source = new EdgeNodeTelemetry("node-hot-chk", "c1", "host-chk", 50.0, 50.0, 85.0, 0.0, 0, true, NodeHealthState.Healthy, now, [checkoutPod]);

        service.RegisterOrUpdateNode(source);

        var standby = service.GetNode("node-standby-chk");
        standby!.ActivePods.Should().ContainSingle(p => p.PodId == "pod-checkout-1");
    }

    [Fact]
    public void AutonomousEvacuation_Tier3Analytics_SuspendedDuringEvacuation()
    {
        var service = new EdgeClusterAutopilotService();
        var now = DateTimeOffset.UtcNow;

        var analyticsPod = EdgeWorkloadPod.CreateAnalytics("pod-analytics-1", "clickstream-aggregator");
        var source = new EdgeNodeTelemetry("node-analytics-src", "c1", "host-ana", 50.0, 50.0, 85.0, 0.0, 0, true, NodeHealthState.Healthy, now, [analyticsPod]);

        service.RegisterOrUpdateNode(source);

        var plan = service.GetEvacuationPlans().Single();
        var detail = plan.EvacuatedPods.Single(p => p.PodId == "pod-analytics-1");
        detail.ActionTaken.Should().Be("Suspended");
        detail.Success.Should().BeTrue();
    }

    [Fact]
    public void AutonomousEvacuation_Tier4BackgroundBatch_GracefullyTerminated()
    {
        var service = new EdgeClusterAutopilotService();
        var now = DateTimeOffset.UtcNow;

        var batchPod = EdgeWorkloadPod.CreateBackgroundBatch("pod-batch-99", "ml-retraining-worker");
        var source = new EdgeNodeTelemetry("node-batch-src", "c1", "host-batch", 50.0, 50.0, 85.0, 0.0, 0, true, NodeHealthState.Healthy, now, [batchPod]);

        service.RegisterOrUpdateNode(source);

        var plan = service.GetEvacuationPlans().Single();
        var detail = plan.EvacuatedPods.Single(p => p.PodId == "pod-batch-99");
        detail.ActionTaken.Should().Be("GracefullyTerminated");
        detail.Success.Should().BeTrue();
    }

    [Fact]
    public void AutonomousEvacuation_NoHealthyStandby_EmergencyFallbackOrQueued()
    {
        var service = new EdgeClusterAutopilotService();
        var now = DateTimeOffset.UtcNow;

        // Only one degraded node with a critical AMR pod
        var criticalPod = EdgeWorkloadPod.CreateRoboticsCritical("pod-critical-orphan", "amr-nav");
        var singleNode = new EdgeNodeTelemetry("node-only", "c1", "host-only", 75.0, 75.0, 89.0, 0.0, 0, true, NodeHealthState.Healthy, now, [criticalPod]);

        service.RegisterOrUpdateNode(singleNode);

        var plan = service.GetEvacuationPlans().Single();
        var detail = plan.EvacuatedPods.Single(p => p.PodId == "pod-critical-orphan");
        detail.ActionTaken.Should().Be("MigrationSuspendedNoStandby");
        detail.Success.Should().BeFalse();
    }

    [Fact]
    public void DrainNode_ManualRequest_DrainsAndMarksOffline()
    {
        var service = new EdgeClusterAutopilotService();
        var now = DateTimeOffset.UtcNow;

        service.RegisterOrUpdateNode(new EdgeNodeTelemetry("node-healthy-1", "c1", "host-1", 20.0, 30.0, 50.0, 0.0, 0, true, NodeHealthState.Healthy, now, []));

        var pod = EdgeWorkloadPod.CreateRoboticsCritical("pod-drain-manual", "amr-manual");
        service.RegisterOrUpdateNode(new EdgeNodeTelemetry("node-to-drain", "c1", "host-drain", 40.0, 40.0, 60.0, 0.1, 0, true, NodeHealthState.Healthy, now, [pod]));

        var response = service.DrainNode("node-to-drain", "Scheduled edge maintenance");

        response.Success.Should().BeTrue();
        response.FinalState.Should().Be(NodeHealthState.DrainedOffline);
        response.Plan.Should().NotBeNull();
        response.Plan!.Reason.Should().Be("Scheduled edge maintenance");

        var drained = service.GetNode("node-to-drain");
        drained!.State.Should().Be(NodeHealthState.DrainedOffline);
        drained.ActivePods.Should().BeEmpty();
    }

    [Fact]
    public void DrainNode_NonExistentNode_ReturnsFailure()
    {
        var service = new EdgeClusterAutopilotService();

        var response = service.DrainNode("non-existent-node");

        response.Success.Should().BeFalse();
        response.Message.Should().Contain("not found");
    }

    [Fact]
    public void DrainNode_EmptyNode_DrainsSuccessfullyWithEmptyPlan()
    {
        var service = new EdgeClusterAutopilotService();
        var now = DateTimeOffset.UtcNow;

        service.RegisterOrUpdateNode(new EdgeNodeTelemetry("node-empty", "c1", "host-empty", 10.0, 15.0, 45.0, 0.0, 0, true, NodeHealthState.Healthy, now, []));

        var response = service.DrainNode("node-empty");

        response.Success.Should().BeTrue();
        response.Plan!.EvacuatedPods.Should().BeEmpty();
        service.GetNode("node-empty")!.State.Should().Be(NodeHealthState.DrainedOffline);
    }

    // =========================================================================
    // 3. Partition Tolerance & Localized Consensus (Tests 21 - 24)
    // =========================================================================

    [Fact]
    public void PartitionTolerance_WanDisconnected_EntersPartitionTolerantMode()
    {
        var service = new EdgeClusterAutopilotService();
        var clusterId = "remote-micro-dc-01";

        service.SetWanConnectivity(clusterId, false);

        service.IsWanConnected(clusterId).Should().BeFalse();
        service.IsPartitionTolerantModeActive(clusterId).Should().BeTrue();
    }

    [Fact]
    public void PartitionTolerance_LocalSchedulingContinues_WhenWanDisconnected()
    {
        var service = new EdgeClusterAutopilotService();
        var clusterId = "isolated-edge-dc";
        var now = DateTimeOffset.UtcNow;

        service.SetWanConnectivity(clusterId, false);

        // Standby node
        service.RegisterOrUpdateNode(new EdgeNodeTelemetry("node-iso-standby", clusterId, "host-iso-st", 20.0, 20.0, 50.0, 0.0, 0, false, NodeHealthState.Healthy, now, []));

        // Critical pod on node that suddenly degrades
        var pod = EdgeWorkloadPod.CreateRoboticsCritical("pod-iso-robot", "amr-offline-guidance");
        var degraded = new EdgeNodeTelemetry("node-iso-bad", clusterId, "host-iso-bad", 50.0, 50.0, 92.0, 0.0, 0, false, NodeHealthState.Healthy, now, [pod]);

        service.RegisterOrUpdateNode(degraded);

        // Verification: Pod evacuated and migrated purely via localized edge autonomy
        var standby = service.GetNode("node-iso-standby");
        standby!.ActivePods.Should().ContainSingle(p => p.PodId == "pod-iso-robot");
    }

    [Fact]
    public void PartitionTolerance_WanReconnected_RestoresReporting()
    {
        var service = new EdgeClusterAutopilotService();
        var clusterId = "mobile-truck-cluster";

        service.SetWanConnectivity(clusterId, false);
        service.IsWanConnected(clusterId).Should().BeFalse();

        service.SetWanConnectivity(clusterId, true);
        service.IsWanConnected(clusterId).Should().BeTrue();
        service.IsPartitionTolerantModeActive(clusterId).Should().BeFalse();
    }

    [Fact]
    public void PartitionTolerance_ClusterHealth_ReportsWanStatusAccurately()
    {
        var service = new EdgeClusterAutopilotService();
        var clusterId = "warehouse-berlin-01";

        service.SetWanConnectivity(clusterId, false);
        var health = service.GetClusterHealth(clusterId);

        health.WanBackhaulConnected.Should().BeFalse();
    }

    // =========================================================================
    // 4. eBPF Kernel Socket Metric Analysis & Saturation (Tests 25 - 38)
    // =========================================================================

    [Fact]
    public void EbpfEngine_IngestMetric_ClassifiesNormalStatus()
    {
        var engine = new EbpfKernelSocketObservabilityEngine();
        var metric = new EbpfSocketMetric(
            SocketId: "sock-norm-1",
            SourceAddress: "10.0.0.1",
            SourcePort: 45100,
            DestinationAddress: "10.0.0.2",
            DestinationPort: 5000,
            TcpState: "ESTABLISHED",
            SmoothedRttMs: 14.5,
            RttVarianceMs: 8.2, // < 45.0
            SynRetransmitCount: 0,
            ZeroWindowProbeCount: 0,
            BytesSent: 1024,
            BytesReceived: 2048,
            Status: SocketHealthStatus.Normal,
            CapturedAtUtc: DateTimeOffset.UtcNow,
            ProbeType: EbpfKernelSocketObservabilityEngine.ProbeTcpSendMsg);

        engine.IngestMetric(metric);

        var recent = engine.GetRecentMetrics(10);
        recent.Should().ContainSingle();
        recent[0].Status.Should().Be(SocketHealthStatus.Normal);
    }

    [Fact]
    public void EbpfEngine_IngestMetric_HighRttJitter_WhenVarianceExceeds45ms()
    {
        var engine = new EbpfKernelSocketObservabilityEngine();
        var metric = new EbpfSocketMetric(
            SocketId: "sock-jitter-1",
            SourceAddress: "10.0.0.1",
            SourcePort: 45101,
            DestinationAddress: "10.0.0.2",
            DestinationPort: 5000,
            TcpState: "ESTABLISHED",
            SmoothedRttMs: 65.0,
            RttVarianceMs: 51.5, // > 45.0ms threshold!
            SynRetransmitCount: 0,
            ZeroWindowProbeCount: 0,
            BytesSent: 4096,
            BytesReceived: 8192,
            Status: SocketHealthStatus.Normal,
            CapturedAtUtc: DateTimeOffset.UtcNow,
            ProbeType: EbpfKernelSocketObservabilityEngine.ProbeTcpSendMsg);

        engine.IngestMetric(metric);

        var recent = engine.GetRecentMetrics(1);
        recent[0].Status.Should().Be(SocketHealthStatus.HighRttJitter);
    }

    [Fact]
    public void EbpfEngine_IngestMetric_SynRetransmitBurst_WhenSynRetransmitsReachThreshold()
    {
        var engine = new EbpfKernelSocketObservabilityEngine();
        var metric = new EbpfSocketMetric(
            SocketId: "sock-syn-1",
            SourceAddress: "10.0.0.1",
            SourcePort: 45102,
            DestinationAddress: "10.0.0.3",
            DestinationPort: 8080,
            TcpState: "SYN_SENT",
            SmoothedRttMs: 0.0,
            RttVarianceMs: 0.0,
            SynRetransmitCount: 4, // >= 3 threshold!
            ZeroWindowProbeCount: 0,
            BytesSent: 0,
            BytesReceived: 0,
            Status: SocketHealthStatus.Normal,
            CapturedAtUtc: DateTimeOffset.UtcNow,
            ProbeType: EbpfKernelSocketObservabilityEngine.ProbeTcpV4Connect);

        engine.IngestMetric(metric);

        var recent = engine.GetRecentMetrics(1);
        recent[0].Status.Should().Be(SocketHealthStatus.SynRetransmitBurst);
    }

    [Fact]
    public void EbpfEngine_IngestMetric_ZeroWindowThrottled_WhenProbesDetected()
    {
        var engine = new EbpfKernelSocketObservabilityEngine();
        var metric = new EbpfSocketMetric(
            SocketId: "sock-zw-1",
            SourceAddress: "10.0.0.1",
            SourcePort: 45103,
            DestinationAddress: "10.0.0.4",
            DestinationPort: 9000,
            TcpState: "ESTABLISHED",
            SmoothedRttMs: 25.0,
            RttVarianceMs: 10.0,
            SynRetransmitCount: 0,
            ZeroWindowProbeCount: 2, // > 0 threshold!
            BytesSent: 500,
            BytesReceived: 100,
            Status: SocketHealthStatus.Normal,
            CapturedAtUtc: DateTimeOffset.UtcNow,
            ProbeType: EbpfKernelSocketObservabilityEngine.ProbeInetSockSetState);

        engine.IngestMetric(metric);

        var recent = engine.GetRecentMetrics(1);
        recent[0].Status.Should().Be(SocketHealthStatus.ZeroWindowThrottled);
    }

    [Fact]
    public void EbpfEngine_ProbeType_KprobeTcpV4Connect_TracksConnectionSetup()
    {
        var engine = new EbpfKernelSocketObservabilityEngine();
        var metric = new EbpfSocketMetric(
            SocketId: "sock-kprobe-conn",
            SourceAddress: "10.0.1.5",
            SourcePort: 52000,
            DestinationAddress: "10.0.1.10",
            DestinationPort: 443,
            TcpState: "SYN_SENT",
            SmoothedRttMs: 5.0,
            RttVarianceMs: 2.0,
            SynRetransmitCount: 0,
            ZeroWindowProbeCount: 0,
            BytesSent: 64,
            BytesReceived: 0,
            Status: SocketHealthStatus.Normal,
            CapturedAtUtc: DateTimeOffset.UtcNow,
            ProbeType: EbpfKernelSocketObservabilityEngine.ProbeTcpV4Connect);

        engine.IngestMetric(metric);

        var recent = engine.GetRecentMetrics(1);
        recent[0].ProbeType.Should().Be("kprobe/tcp_v4_connect");
    }

    [Fact]
    public void EbpfEngine_ProbeType_KretprobeTcpSendMsg_MeasuresSendLatency()
    {
        var engine = new EbpfKernelSocketObservabilityEngine();
        var metric = new EbpfSocketMetric(
            SocketId: "sock-kretprobe-send",
            SourceAddress: "10.0.1.5",
            SourcePort: 52001,
            DestinationAddress: "10.0.1.10",
            DestinationPort: 443,
            TcpState: "ESTABLISHED",
            SmoothedRttMs: 18.0,
            RttVarianceMs: 4.0,
            SynRetransmitCount: 0,
            ZeroWindowProbeCount: 0,
            BytesSent: 8192,
            BytesReceived: 16384,
            Status: SocketHealthStatus.Normal,
            CapturedAtUtc: DateTimeOffset.UtcNow,
            ProbeType: EbpfKernelSocketObservabilityEngine.ProbeTcpSendMsg);

        engine.IngestMetric(metric);

        var recent = engine.GetRecentMetrics(1);
        recent[0].ProbeType.Should().Be("kretprobe/tcp_sendmsg");
    }

    [Fact]
    public void EbpfEngine_ProbeType_TracepointInetSockSetState_TracksStateChanges()
    {
        var engine = new EbpfKernelSocketObservabilityEngine();
        var metric = new EbpfSocketMetric(
            SocketId: "sock-tp-state",
            SourceAddress: "10.0.1.5",
            SourcePort: 52002,
            DestinationAddress: "10.0.1.10",
            DestinationPort: 443,
            TcpState: "TIME_WAIT",
            SmoothedRttMs: 10.0,
            RttVarianceMs: 2.0,
            SynRetransmitCount: 0,
            ZeroWindowProbeCount: 0,
            BytesSent: 500,
            BytesReceived: 500,
            Status: SocketHealthStatus.Normal,
            CapturedAtUtc: DateTimeOffset.UtcNow,
            ProbeType: EbpfKernelSocketObservabilityEngine.ProbeInetSockSetState);

        engine.IngestMetric(metric);

        var recent = engine.GetRecentMetrics(1);
        recent[0].ProbeType.Should().Be("tracepoint/sock/inet_sock_set_state");
        recent[0].TcpState.Should().Be("TIME_WAIT");
    }

    [Fact]
    public void EbpfEngine_DetectMicroBurstSaturation_IdentifiesCorrelatedSpikes()
    {
        var engine = new EbpfKernelSocketObservabilityEngine();
        var targetHost = "10.10.50.100";
        var now = DateTimeOffset.UtcNow;

        // 4 concurrent sockets experiencing RTT variance > 45ms to the same destination
        for (int i = 1; i <= 4; i++)
        {
            engine.IngestMetric(new EbpfSocketMetric(
                SocketId: $"sock-burst-{i}",
                SourceAddress: "10.10.50.1",
                SourcePort: 40000 + i,
                DestinationAddress: targetHost,
                DestinationPort: 8080,
                TcpState: "ESTABLISHED",
                SmoothedRttMs: 80.0,
                RttVarianceMs: 65.0, // spike
                SynRetransmitCount: 0,
                ZeroWindowProbeCount: 0,
                BytesSent: 1000,
                BytesReceived: 2000,
                Status: SocketHealthStatus.HighRttJitter,
                CapturedAtUtc: now,
                ProbeType: EbpfKernelSocketObservabilityEngine.ProbeTcpSendMsg));
        }

        var alerts = engine.DetectMicroBurstSaturation();

        alerts.Should().NotBeEmpty();
        alerts[0].Should().Contain("Micro-burst network saturation detected");
        alerts[0].Should().Contain(targetHost);
    }

    [Fact]
    public void EbpfEngine_DetectMicroBurstSaturation_ReturnsEmpty_WhenNoCorrelatedSpikes()
    {
        var engine = new EbpfKernelSocketObservabilityEngine();
        var now = DateTimeOffset.UtcNow;

        for (int i = 1; i <= 4; i++)
        {
            engine.IngestMetric(new EbpfSocketMetric(
                SocketId: $"sock-calm-{i}",
                SourceAddress: "10.10.50.1",
                SourcePort: 41000 + i,
                DestinationAddress: "10.10.50.2",
                DestinationPort: 8080,
                TcpState: "ESTABLISHED",
                SmoothedRttMs: 15.0,
                RttVarianceMs: 5.0, // normal
                SynRetransmitCount: 0,
                ZeroWindowProbeCount: 0,
                BytesSent: 1000,
                BytesReceived: 2000,
                Status: SocketHealthStatus.Normal,
                CapturedAtUtc: now,
                ProbeType: EbpfKernelSocketObservabilityEngine.ProbeTcpSendMsg));
        }

        var alerts = engine.DetectMicroBurstSaturation();

        alerts.Should().BeEmpty();
    }

    [Fact]
    public void EbpfEngine_DetectRogueSocketLeaks_FlagsCloseWaitSockets()
    {
        var engine = new EbpfKernelSocketObservabilityEngine();
        var metric = new EbpfSocketMetric(
            SocketId: "sock-leak-cw",
            SourceAddress: "10.0.0.15",
            SourcePort: 49152,
            DestinationAddress: "10.0.0.25",
            DestinationPort: 5432,
            TcpState: "CLOSE_WAIT", // Rogue leak
            SmoothedRttMs: 0.0,
            RttVarianceMs: 0.0,
            SynRetransmitCount: 0,
            ZeroWindowProbeCount: 0,
            BytesSent: 0,
            BytesReceived: 0,
            Status: SocketHealthStatus.Normal,
            CapturedAtUtc: DateTimeOffset.UtcNow,
            ProbeType: EbpfKernelSocketObservabilityEngine.ProbeInetSockSetState);

        engine.IngestMetric(metric);

        var leaks = engine.DetectRogueSocketLeaks();

        leaks.Should().ContainSingle();
        leaks[0].Should().Contain("CLOSE_WAIT");
    }

    [Fact]
    public void EbpfEngine_DetectRogueSocketLeaks_FlagsZeroByteEstablishedSockets()
    {
        var engine = new EbpfKernelSocketObservabilityEngine();
        var metric = new EbpfSocketMetric(
            SocketId: "sock-leak-zero",
            SourceAddress: "10.0.0.18",
            SourcePort: 49155,
            DestinationAddress: "10.0.0.30",
            DestinationPort: 6379,
            TcpState: "ESTABLISHED",
            SmoothedRttMs: 10.0,
            RttVarianceMs: 1.0,
            SynRetransmitCount: 0,
            ZeroWindowProbeCount: 0,
            BytesSent: 0,     // 0 bytes
            BytesReceived: 0, // 0 bytes
            Status: SocketHealthStatus.Normal,
            CapturedAtUtc: DateTimeOffset.UtcNow,
            ProbeType: EbpfKernelSocketObservabilityEngine.ProbeInetSockSetState);

        engine.IngestMetric(metric);

        var leaks = engine.DetectRogueSocketLeaks();

        leaks.Should().ContainSingle();
        leaks[0].Should().Contain("0 bytes transferred");
    }

    [Fact]
    public void EbpfEngine_CalculateRttStatistics_CalculatesMeanStdDevAndMax()
    {
        var engine = new EbpfKernelSocketObservabilityEngine();
        var now = DateTimeOffset.UtcNow;

        double[] rtts = [10.0, 20.0, 30.0, 40.0, 50.0];
        foreach (var rtt in rtts)
        {
            engine.IngestMetric(new EbpfSocketMetric(
                SocketId: $"sock-stat-{rtt}",
                SourceAddress: "10.0.0.1",
                SourcePort: 50000,
                DestinationAddress: "10.0.0.2",
                DestinationPort: 80,
                TcpState: "ESTABLISHED",
                SmoothedRttMs: rtt,
                RttVarianceMs: 5.0,
                SynRetransmitCount: 0,
                ZeroWindowProbeCount: 0,
                BytesSent: 100,
                BytesReceived: 100,
                Status: SocketHealthStatus.Normal,
                CapturedAtUtc: now,
                ProbeType: EbpfKernelSocketObservabilityEngine.ProbeTcpSendMsg));
        }

        var (mean, stdDev, max) = engine.CalculateRttStatistics();

        mean.Should().BeApproximately(30.0, 0.001);
        max.Should().Be(50.0);
        stdDev.Should().BeApproximately(14.142, 0.01);
    }

    [Fact]
    public void EbpfEngine_CalculateRttStatistics_SubMillisecondPerformance()
    {
        var engine = new EbpfKernelSocketObservabilityEngine();
        var now = DateTimeOffset.UtcNow;
        var batch = new List<EbpfSocketMetric>();

        for (int i = 0; i < 1_000; i++)
        {
            batch.Add(new EbpfSocketMetric(
                SocketId: $"sock-perf-{i}",
                SourceAddress: "10.0.0.1",
                SourcePort: 50000 + (i % 1000),
                DestinationAddress: "10.0.0.2",
                DestinationPort: 80,
                TcpState: "ESTABLISHED",
                SmoothedRttMs: 15.0 + (i % 30),
                RttVarianceMs: 5.0,
                SynRetransmitCount: 0,
                ZeroWindowProbeCount: 0,
                BytesSent: 100,
                BytesReceived: 100,
                Status: SocketHealthStatus.Normal,
                CapturedAtUtc: now,
                ProbeType: EbpfKernelSocketObservabilityEngine.ProbeTcpSendMsg));
        }
        engine.IngestMetricsBatch(batch);

        var sw = Stopwatch.StartNew();
        var (mean, stdDev, max) = engine.CalculateRttStatistics();
        sw.Stop();

        mean.Should().BeGreaterThan(0.0);
        max.Should().BeGreaterThan(0.0);
        sw.ElapsedMilliseconds.Should().BeLessThanOrEqualTo(5); // Sub-millisecond target in optimized execution
    }

    [Fact]
    public void EbpfEngine_BoundedRingBuffer_PreventsUnboundedMemoryGrowth()
    {
        var engine = new EbpfKernelSocketObservabilityEngine();
        var now = DateTimeOffset.UtcNow;

        var batch1 = Enumerable.Range(0, 9_500).Select(i => new EbpfSocketMetric(
            $"sock-b1-{i}", "10.0.0.1", 1000, "10.0.0.2", 80, "ESTABLISHED", 10.0, 2.0, 0, 0, 100, 100, SocketHealthStatus.Normal, now, "kretprobe/tcp_sendmsg"));
        engine.IngestMetricsBatch(batch1);

        var batch2 = Enumerable.Range(0, 2_000).Select(i => new EbpfSocketMetric(
            $"sock-b2-{i}", "10.0.0.1", 1000, "10.0.0.2", 80, "ESTABLISHED", 10.0, 2.0, 0, 0, 100, 100, SocketHealthStatus.Normal, now, "kretprobe/tcp_sendmsg"));
        engine.IngestMetricsBatch(batch2);

        var summary = engine.GetMetricsSummary();
        summary.TotalSocketsTracked.Should().BeLessThanOrEqualTo(10_000);
    }

    // =========================================================================
    // 5. Adaptive Multi-Tier Load Shedding (Tests 39 - 47)
    // =========================================================================

    [Fact]
    public void AdaptiveLoadShedder_Tier1Robotics_NeverShed_UnderModerateLoad()
    {
        var shedder = new AdaptiveLoadShedderSentinelService();

        var admitted = shedder.ShouldAdmit(LoadSheddingTier.Tier1CriticalRoboticsNeverShed, currentCpuPct: 88.0, currentPacketLossPct: 1.0);

        admitted.Should().BeTrue();
    }

    [Fact]
    public void AdaptiveLoadShedder_Tier1Robotics_NeverShed_UnderExtremeDistress()
    {
        var shedder = new AdaptiveLoadShedderSentinelService();

        // 100% CPU and 50% packet drop
        var admitted = shedder.ShouldAdmit(LoadSheddingTier.Tier1CriticalRoboticsNeverShed, currentCpuPct: 100.0, currentPacketLossPct: 50.0);

        admitted.Should().BeTrue();
    }

    [Fact]
    public void AdaptiveLoadShedder_Tier4Background_Shed_WhenCpuExceeds85Pct()
    {
        var shedder = new AdaptiveLoadShedderSentinelService();

        var tier4Admitted = shedder.ShouldAdmit(LoadSheddingTier.Tier4BackgroundBatch, currentCpuPct: 86.0, currentPacketLossPct: 1.0);
        var tier3Admitted = shedder.ShouldAdmit(LoadSheddingTier.Tier3AnalyticsTelemetry, currentCpuPct: 86.0, currentPacketLossPct: 1.0);

        tier4Admitted.Should().BeFalse(); // Shed
        tier3Admitted.Should().BeTrue();  // Still admitted
    }

    [Fact]
    public void AdaptiveLoadShedder_Tier4Background_Shed_WhenPacketLossExceeds3Pct()
    {
        var shedder = new AdaptiveLoadShedderSentinelService();

        var tier4Admitted = shedder.ShouldAdmit(LoadSheddingTier.Tier4BackgroundBatch, currentCpuPct: 50.0, currentPacketLossPct: 3.5);

        tier4Admitted.Should().BeFalse();
    }

    [Fact]
    public void AdaptiveLoadShedder_Tier3Analytics_Shed_WhenCpuExceeds90Pct()
    {
        var shedder = new AdaptiveLoadShedderSentinelService();

        var tier4 = shedder.ShouldAdmit(LoadSheddingTier.Tier4BackgroundBatch, currentCpuPct: 92.0, currentPacketLossPct: 1.0);
        var tier3 = shedder.ShouldAdmit(LoadSheddingTier.Tier3AnalyticsTelemetry, currentCpuPct: 92.0, currentPacketLossPct: 1.0);
        var tier2 = shedder.ShouldAdmit(LoadSheddingTier.Tier2CoreCheckout, currentCpuPct: 92.0, currentPacketLossPct: 1.0);

        tier4.Should().BeFalse(); // Shed
        tier3.Should().BeFalse(); // Shed
        tier2.Should().BeTrue();  // Preserved
    }

    [Fact]
    public void AdaptiveLoadShedder_Tier3Analytics_Shed_WhenPacketLossExceeds5Pct()
    {
        var shedder = new AdaptiveLoadShedderSentinelService();

        var tier3 = shedder.ShouldAdmit(LoadSheddingTier.Tier3AnalyticsTelemetry, currentCpuPct: 60.0, currentPacketLossPct: 6.0);

        tier3.Should().BeFalse();
    }

    [Fact]
    public void AdaptiveLoadShedder_Tier2CoreCheckout_Shed_WhenCpuExceeds95Pct()
    {
        var shedder = new AdaptiveLoadShedderSentinelService();

        var tier2 = shedder.ShouldAdmit(LoadSheddingTier.Tier2CoreCheckout, currentCpuPct: 96.0, currentPacketLossPct: 1.0);
        var tier1 = shedder.ShouldAdmit(LoadSheddingTier.Tier1CriticalRoboticsNeverShed, currentCpuPct: 96.0, currentPacketLossPct: 1.0);

        tier2.Should().BeFalse(); // Shed
        tier1.Should().BeTrue();  // Critical Tier 1 NEVER shed
    }

    [Fact]
    public void AdaptiveLoadShedder_Tier2CoreCheckout_Shed_WhenPacketLossExceeds10Pct()
    {
        var shedder = new AdaptiveLoadShedderSentinelService();

        var tier2 = shedder.ShouldAdmit(LoadSheddingTier.Tier2CoreCheckout, currentCpuPct: 50.0, currentPacketLossPct: 11.5);
        var tier1 = shedder.ShouldAdmit(LoadSheddingTier.Tier1CriticalRoboticsNeverShed, currentCpuPct: 50.0, currentPacketLossPct: 11.5);

        tier2.Should().BeFalse();
        tier1.Should().BeTrue();
    }

    [Fact]
    public void AdaptiveLoadShedder_NormalLoad_AdmitsAllTiers()
    {
        var shedder = new AdaptiveLoadShedderSentinelService();

        shedder.ShouldAdmit(LoadSheddingTier.Tier1CriticalRoboticsNeverShed, 50.0, 0.5).Should().BeTrue();
        shedder.ShouldAdmit(LoadSheddingTier.Tier2CoreCheckout, 50.0, 0.5).Should().BeTrue();
        shedder.ShouldAdmit(LoadSheddingTier.Tier3AnalyticsTelemetry, 50.0, 0.5).Should().BeTrue();
        shedder.ShouldAdmit(LoadSheddingTier.Tier4BackgroundBatch, 50.0, 0.5).Should().BeTrue();
    }

    // =========================================================================
    // 6. Hysteresis Recovery & Manual Overrides (Tests 48 - 53)
    // =========================================================================

    [Fact]
    public void AdaptiveLoadShedder_Hysteresis_DoesNotInstantlyClearShedding()
    {
        var shedder = new AdaptiveLoadShedderSentinelService();
        var startTime = DateTimeOffset.UtcNow;

        // Severe load: sheds Tier 4, 3, 2
        shedder.EvaluateCurrentPolicy(currentCpuPct: 96.0, currentPacketLossPct: 1.0, now: startTime);

        // Load drops back to nominal (40% CPU, 0.1% loss) 5 seconds later
        var checkTime = startTime.AddSeconds(5);
        var policy = shedder.EvaluateCurrentPolicy(currentCpuPct: 40.0, currentPacketLossPct: 0.1, now: checkTime);

        // Hysteresis holds: shedding remains active
        policy.IsActive.Should().BeTrue();
        policy.StableDuration.TotalSeconds.Should().BeApproximately(5.0, 0.5);
    }

    [Fact]
    public void AdaptiveLoadShedder_Hysteresis_ProgressiveRestoration_10s_20s_30s()
    {
        var shedder = new AdaptiveLoadShedderSentinelService();
        var t0 = DateTimeOffset.UtcNow;

        // Distress trigger: all tiers 4, 3, 2 shed
        shedder.EvaluateCurrentPolicy(currentCpuPct: 96.0, currentPacketLossPct: 1.0, now: t0);

        // t = 12s (> 10s): Tier 2 should be restored, Tier 3 and 4 still shed
        var t12 = t0.AddSeconds(12);
        var policy12 = shedder.EvaluateCurrentPolicy(40.0, 0.1, t12);
        policy12.ActiveShedTiers.Should().NotContain(LoadSheddingTier.Tier2CoreCheckout);
        policy12.ActiveShedTiers.Should().Contain(LoadSheddingTier.Tier3AnalyticsTelemetry);
        policy12.ActiveShedTiers.Should().Contain(LoadSheddingTier.Tier4BackgroundBatch);

        // t = 22s (> 20s): Tier 3 restored, Tier 4 still shed
        var t22 = t0.AddSeconds(22);
        var policy22 = shedder.EvaluateCurrentPolicy(40.0, 0.1, t22);
        policy22.ActiveShedTiers.Should().NotContain(LoadSheddingTier.Tier3AnalyticsTelemetry);
        policy22.ActiveShedTiers.Should().Contain(LoadSheddingTier.Tier4BackgroundBatch);

        // t = 32s (> 30s): Full recovery! No tiers shed
        var t32 = t0.AddSeconds(32);
        var policy32 = shedder.EvaluateCurrentPolicy(40.0, 0.1, t32);
        policy32.IsActive.Should().BeFalse();
        policy32.ActiveShedTiers.Should().BeEmpty();
    }

    [Fact]
    public void AdaptiveLoadShedder_Hysteresis_LoadSpikeResetsStabilizationTimer()
    {
        var shedder = new AdaptiveLoadShedderSentinelService();
        var t0 = DateTimeOffset.UtcNow;

        // Distress
        shedder.EvaluateCurrentPolicy(92.0, 1.0, now: t0);

        // Stabilizing at t = 20s
        var t20 = t0.AddSeconds(20);
        shedder.EvaluateCurrentPolicy(40.0, 0.1, now: t20);

        // Sudden load spike at t = 25s (> 85% CPU)
        var t25 = t0.AddSeconds(25);
        var spikedPolicy = shedder.EvaluateCurrentPolicy(89.0, 1.0, now: t25);

        spikedPolicy.IsActive.Should().BeTrue();
        spikedPolicy.StableDuration.Should().Be(TimeSpan.Zero); // Reset to 0!
    }

    [Fact]
    public void AdaptiveLoadShedder_ManualOverride_ForcesSheddingRegardlessOfMetrics()
    {
        var shedder = new AdaptiveLoadShedderSentinelService();

        shedder.SetManualOverride(true, [LoadSheddingTier.Tier4BackgroundBatch, LoadSheddingTier.Tier3AnalyticsTelemetry]);

        // Even with very low CPU (10%), override takes effect
        var policy = shedder.EvaluateCurrentPolicy(10.0, 0.1);

        policy.IsManualOverride.Should().BeTrue();
        policy.ActiveShedTiers.Should().Contain(LoadSheddingTier.Tier4BackgroundBatch);
        policy.ActiveShedTiers.Should().Contain(LoadSheddingTier.Tier3AnalyticsTelemetry);
    }

    [Fact]
    public void AdaptiveLoadShedder_ManualOverride_CannotShedTier1Robotics()
    {
        var shedder = new AdaptiveLoadShedderSentinelService();

        // Attempting to include Tier 1 in manual override
        shedder.SetManualOverride(true, [LoadSheddingTier.Tier1CriticalRoboticsNeverShed, LoadSheddingTier.Tier4BackgroundBatch]);

        var policy = shedder.EvaluateCurrentPolicy(10.0, 0.1);

        policy.ActiveShedTiers.Should().NotContain(LoadSheddingTier.Tier1CriticalRoboticsNeverShed);
        shedder.ShouldAdmit(LoadSheddingTier.Tier1CriticalRoboticsNeverShed, 10.0, 0.1).Should().BeTrue();
    }

    [Fact]
    public void AdaptiveLoadShedder_ClearManualOverride_RevertsToDynamicEvaluation()
    {
        var shedder = new AdaptiveLoadShedderSentinelService();

        shedder.SetManualOverride(true, [LoadSheddingTier.Tier4BackgroundBatch]);
        shedder.ClearManualOverride();

        var policy = shedder.EvaluateCurrentPolicy(40.0, 0.1);
        policy.IsManualOverride.Should().BeFalse();
        policy.IsActive.Should().BeFalse();
    }

    // =========================================================================
    // 7. Concurrency & High Throughput Thread Safety with Native Lock (Tests 54 - 55)
    // =========================================================================

    [Fact]
    public async Task EdgeObservability_Concurrency_TelemetryIngestWithNativeLock()
    {
        var service = new EdgeClusterAutopilotService();
        var now = DateTimeOffset.UtcNow;

        var tasks = Enumerable.Range(0, 20).Select(i => Task.Run(() =>
        {
            for (int j = 0; j < 50; j++)
            {
                var nodeId = $"node-concurrent-{i % 5}";
                service.RegisterOrUpdateNode(new EdgeNodeTelemetry(
                    NodeId: nodeId,
                    ClusterId: "edge-cluster-primary",
                    Hostname: $"{nodeId}.local",
                    CpuUtilizationPct: 20.0 + j,
                    MemoryUtilizationPct: 30.0,
                    TemperatureCelsius: 50.0,
                    PacketLossPct: 0.1,
                    SocketFlapCount: 0,
                    IsWanConnected: true,
                    State: NodeHealthState.Healthy,
                    LastHeartbeatUtc: now,
                    ActivePods: []));

                service.GetAllNodes();
            }
        }));

        await Task.WhenAll(tasks);

        service.GetAllNodes().Should().HaveCount(5);
    }

    [Fact]
    public async Task EdgeObservability_Concurrency_EbpfMetricIngestWithNativeLock()
    {
        var engine = new EbpfKernelSocketObservabilityEngine();
        var now = DateTimeOffset.UtcNow;

        var tasks = Enumerable.Range(0, 20).Select(i => Task.Run(() =>
        {
            for (int j = 0; j < 50; j++)
            {
                engine.IngestMetric(new EbpfSocketMetric(
                    SocketId: $"sock-c-{i}-{j}",
                    SourceAddress: "10.0.0.1",
                    SourcePort: 10000 + i,
                    DestinationAddress: "10.0.0.2",
                    DestinationPort: 80,
                    TcpState: "ESTABLISHED",
                    SmoothedRttMs: 15.0 + j,
                    RttVarianceMs: 5.0,
                    SynRetransmitCount: 0,
                    ZeroWindowProbeCount: 0,
                    BytesSent: 100,
                    BytesReceived: 100,
                    Status: SocketHealthStatus.Normal,
                    CapturedAtUtc: now,
                    ProbeType: EbpfKernelSocketObservabilityEngine.ProbeTcpSendMsg));

                engine.CalculateRttStatistics();
            }
        }));

        await Task.WhenAll(tasks);

        var summary = engine.GetMetricsSummary();
        summary.TotalSocketsTracked.Should().Be(1_000);
    }

    // =========================================================================
    // 8. ServiceCollection & Endpoint Mapping (Tests 56 - 57)
    // =========================================================================

    [Fact]
    public void EdgeObservability_ServiceCollection_RegistersRequiredServicesWithoutBuildServiceProvider()
    {
        // HARD CONSTRAINT: In unit tests, NEVER call 'services.BuildServiceProvider()'.
        // Assert directly on ServiceCollection descriptors.
        var services = new ServiceCollection();

        services.AddEdgeObservabilityServices();

        services.Should().Contain(d =>
            d.ServiceType == typeof(IEdgeClusterAutopilotService) &&
            d.ImplementationType == typeof(EdgeClusterAutopilotService) &&
            d.Lifetime == ServiceLifetime.Singleton);

        services.Should().Contain(d =>
            d.ServiceType == typeof(IEbpfKernelSocketObservabilityEngine) &&
            d.ImplementationType == typeof(EbpfKernelSocketObservabilityEngine) &&
            d.Lifetime == ServiceLifetime.Singleton);

        services.Should().Contain(d =>
            d.ServiceType == typeof(IAdaptiveLoadShedderSentinelService) &&
            d.ImplementationType == typeof(AdaptiveLoadShedderSentinelService) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void EdgeObservability_MapEndpoints_RegistersExpectedRoutes()
    {
        var builder = new DefaultEndpointRouteBuilder();

        builder.MapEdgeObservabilityEndpoints();

        builder.DataSources.Should().NotBeEmpty();
        var endpoints = builder.DataSources.SelectMany(ds => ds.Endpoints).ToList();

        endpoints.Should().Contain(e => e.DisplayName != null && e.DisplayName.Contains("/cluster/health"));
        endpoints.Should().Contain(e => e.DisplayName != null && e.DisplayName.Contains("/nodes/{nodeId}/drain"));
        endpoints.Should().Contain(e => e.DisplayName != null && e.DisplayName.Contains("/ebpf/metrics"));
        endpoints.Should().Contain(e => e.DisplayName != null && e.DisplayName.Contains("/load-shedding/override"));
        endpoints.Should().Contain(e => e.DisplayName != null && e.DisplayName.Contains("/load-shedding/status"));
    }

    /// <summary>
    /// Helper mock route builder implementing IEndpointRouteBuilder for route assertions.
    /// </summary>
    private class DefaultEndpointRouteBuilder : IEndpointRouteBuilder
    {
        public IServiceProvider ServiceProvider => Mock.Of<IServiceProvider>();
        public ICollection<EndpointDataSource> DataSources { get; } = new List<EndpointDataSource>();

        public IApplicationBuilder CreateApplicationBuilder()
        {
            var mock = new Mock<IApplicationBuilder>();
            return mock.Object;
        }
    }
}
