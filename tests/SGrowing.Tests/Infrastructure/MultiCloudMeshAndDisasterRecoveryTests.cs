using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SGrowing.Api.Modules.Infrastructure.MultiCloud;

namespace SGrowing.Tests.Infrastructure;

/// <summary>
/// Comprehensive unit and integration test suite for Phase 45:
/// Global Sovereign Multi-Cloud Mesh, Anycast Disaster Recovery &amp; Database Split-Brain Arbitrator.
/// </summary>
public class MultiCloudMeshAndDisasterRecoveryTests
{
    // =========================================================================
    // Category 1: Node Registration & Topology (Tests 1-5)
    // =========================================================================

    [Fact]
    public void RegisterNode_AddsNodeToMesh_Successfully()
    {
        var gateway = new MultiCloudMeshGatewayService(seedDefaultTopology: false);
        var node = new CloudMeshNode(
            NodeId: "dz-algiers-custom",
            Provider: CloudProvider.SovereignPrivateAlgiers,
            RegionCode: "dz-central-1",
            Hostname: "algiers.custom.sgrowing.dz",
            HealthStatus: RegionNodeStatus.Healthy,
            LatencyP99Ms: 14.2,
            Weight: 2.0,
            IsWriteLeader: false);

        gateway.RegisterNode(node);

        var retrieved = gateway.GetNode("dz-algiers-custom");
        retrieved.Should().NotBeNull();
        retrieved!.NodeId.Should().Be("dz-algiers-custom");
        retrieved.Provider.Should().Be(CloudProvider.SovereignPrivateAlgiers);
        retrieved.IsRoutable.Should().BeTrue();
    }

    [Fact]
    public void RegisterNode_DuplicateId_OverwritesExistingNode()
    {
        var gateway = new MultiCloudMeshGatewayService(seedDefaultTopology: false);
        var initial = new CloudMeshNode(
            NodeId: "node-aws-1",
            Provider: CloudProvider.Aws,
            RegionCode: "eu-west-3",
            Hostname: "aws-1.cloud",
            HealthStatus: RegionNodeStatus.Healthy,
            LatencyP99Ms: 25.0,
            Weight: 1.0,
            IsWriteLeader: false);

        gateway.RegisterNode(initial);

        var updated = initial with { LatencyP99Ms = 12.0, Weight = 3.0 };
        gateway.RegisterNode(updated);

        var retrieved = gateway.GetNode("node-aws-1");
        retrieved!.LatencyP99Ms.Should().Be(12.0);
        retrieved.Weight.Should().Be(3.0);
        gateway.GetAllNodes().Should().HaveCount(1);
    }

    [Fact]
    public void RegisterNode_WhenMarkedWriteLeader_DemotesPreviousWriteLeader()
    {
        var gateway = new MultiCloudMeshGatewayService(seedDefaultTopology: false);
        var node1 = new CloudMeshNode(
            NodeId: "node-1",
            Provider: CloudProvider.Azure,
            RegionCode: "westeurope",
            Hostname: "node1.cloud",
            HealthStatus: RegionNodeStatus.Healthy,
            LatencyP99Ms: 20.0,
            Weight: 1.0,
            IsWriteLeader: true);

        var node2 = new CloudMeshNode(
            NodeId: "node-2",
            Provider: CloudProvider.SovereignPrivateAlgiers,
            RegionCode: "dz-central-1",
            Hostname: "node2.cloud",
            HealthStatus: RegionNodeStatus.Healthy,
            LatencyP99Ms: 10.0,
            Weight: 2.0,
            IsWriteLeader: true);

        gateway.RegisterNode(node1);
        gateway.GetNode("node-1")!.IsWriteLeader.Should().BeTrue();

        gateway.RegisterNode(node2);
        gateway.GetNode("node-1")!.IsWriteLeader.Should().BeFalse();
        gateway.GetNode("node-2")!.IsWriteLeader.Should().BeTrue();
    }

    [Fact]
    public void UnregisterNode_RemovesNodeFromMesh()
    {
        var gateway = new MultiCloudMeshGatewayService(seedDefaultTopology: false);
        var node = new CloudMeshNode("node-temp", CloudProvider.Gcp, "europe-west1", "host.cloud", RegionNodeStatus.Healthy, 20.0, 1.0, false);

        gateway.RegisterNode(node);
        gateway.GetAllNodes().Should().HaveCount(1);

        bool removed = gateway.UnregisterNode("node-temp");
        removed.Should().BeTrue();
        gateway.GetNode("node-temp").Should().BeNull();
        gateway.GetAllNodes().Should().BeEmpty();
    }

    [Fact]
    public void GetClusterTopology_ReturnsAccurateCountsAndWeights()
    {
        var gateway = new MultiCloudMeshGatewayService(seedDefaultTopology: true);
        var topology = gateway.GetClusterTopology();

        // Seeded topology has 5 nodes across AWS, Azure, GCP, Algiers, and Dubai
        topology.TotalNodesCount.Should().Be(5);
        topology.HealthyNodesCount.Should().Be(5);
        topology.TotalCapacityWeight.Should().BeGreaterThan(0.0);
        topology.CurrentWriteLeaderId.Should().Be("node-sovereign-algiers-1");
        topology.Nodes.Should().HaveCount(5);
    }

    // =========================================================================
    // Category 2: Latency & Ingress Rerouting (<5000ms detection) (Tests 6-10)
    // =========================================================================

    [Fact]
    public void UpdateNodeHealth_UpdatesStatusAndLatency()
    {
        var gateway = new MultiCloudMeshGatewayService(seedDefaultTopology: true);
        gateway.UpdateNodeHealth("node-aws-eu-west-3", RegionNodeStatus.Healthy, 29.5);

        var node = gateway.GetNode("node-aws-eu-west-3");
        node.Should().NotBeNull();
        node!.HealthStatus.Should().Be(RegionNodeStatus.Healthy);
        node.LatencyP99Ms.Should().Be(29.5);
    }

    [Fact]
    public void UpdateNodeHealth_WhenLatencyOver5000Ms_AutomaticallyDegradesStatus()
    {
        var gateway = new MultiCloudMeshGatewayService(seedDefaultTopology: true);

        // Caller attempts to report Healthy, but latency is >= 5000ms
        gateway.UpdateNodeHealth("node-azure-westeurope", RegionNodeStatus.Healthy, 5200.0);

        var node = gateway.GetNode("node-azure-westeurope");
        node!.HealthStatus.Should().Be(RegionNodeStatus.DegradedLatency);
        node.IsRoutable.Should().BeFalse();
    }

    [Fact]
    public void GetOptimalRoutingNode_IgnoresOfflineNodes()
    {
        var gateway = new MultiCloudMeshGatewayService(seedDefaultTopology: false);
        var nodeOffline = new CloudMeshNode("n-offline", CloudProvider.Aws, "eu-west-1", "host1", RegionNodeStatus.Offline, 5.0, 10.0, false);
        var nodeHealthy = new CloudMeshNode("n-healthy", CloudProvider.Azure, "westeurope", "host2", RegionNodeStatus.Healthy, 25.0, 1.0, false);

        gateway.RegisterNode(nodeOffline);
        gateway.RegisterNode(nodeHealthy);

        var optimal = gateway.GetOptimalRoutingNode();
        optimal.Should().NotBeNull();
        optimal!.NodeId.Should().Be("n-healthy");
    }

    [Fact]
    public void GetOptimalRoutingNode_IgnoresDegradedLatencyNodes()
    {
        var gateway = new MultiCloudMeshGatewayService(seedDefaultTopology: false);
        var nodeDegraded = new CloudMeshNode("n-deg", CloudProvider.Aws, "eu-west-1", "host1", RegionNodeStatus.DegradedLatency, 8.0, 5.0, false);
        var nodeHealthy = new CloudMeshNode("n-healthy", CloudProvider.Azure, "westeurope", "host2", RegionNodeStatus.Healthy, 30.0, 1.0, false);

        gateway.RegisterNode(nodeDegraded);
        gateway.RegisterNode(nodeHealthy);

        var optimal = gateway.GetOptimalRoutingNode();
        optimal!.NodeId.Should().Be("n-healthy");
    }

    [Fact]
    public void GetOptimalRoutingNode_IgnoresPartitionedIsolatedNodes()
    {
        var gateway = new MultiCloudMeshGatewayService(seedDefaultTopology: false);
        var nodePartitioned = new CloudMeshNode("n-iso", CloudProvider.SovereignPrivateDubai, "me-central-1", "host1", RegionNodeStatus.PartitionedIsolated, 4.0, 5.0, false);
        var nodeHealthy = new CloudMeshNode("n-healthy", CloudProvider.SovereignPrivateAlgiers, "dz-central-1", "host2", RegionNodeStatus.Healthy, 15.0, 1.0, false);

        gateway.RegisterNode(nodePartitioned);
        gateway.RegisterNode(nodeHealthy);

        var optimal = gateway.GetOptimalRoutingNode();
        optimal!.NodeId.Should().Be("n-healthy");
    }

    // =========================================================================
    // Category 3: Weighted Lowest Latency Anycast Selection (Tests 11-15)
    // =========================================================================

    [Fact]
    public void GetOptimalRoutingNode_SelectsLowestLatency_WhenWeightsEqual()
    {
        var gateway = new MultiCloudMeshGatewayService(seedDefaultTopology: false);
        var nodeA = new CloudMeshNode("node-a", CloudProvider.Aws, "eu-west-3", "host-a", RegionNodeStatus.Healthy, 15.0, 1.0, false);
        var nodeB = new CloudMeshNode("node-b", CloudProvider.Azure, "westeurope", "host-b", RegionNodeStatus.Healthy, 30.0, 1.0, false);

        gateway.RegisterNode(nodeA);
        gateway.RegisterNode(nodeB);

        var optimal = gateway.GetOptimalRoutingNode();
        optimal!.NodeId.Should().Be("node-a");
    }

    [Fact]
    public void GetOptimalRoutingNode_SelectsHigherWeight_WhenLatenciesEqual()
    {
        var gateway = new MultiCloudMeshGatewayService(seedDefaultTopology: false);
        // Both have 20ms, but node-b has weight 2.0 (effective score: 10ms vs 20ms)
        var nodeA = new CloudMeshNode("node-a", CloudProvider.Aws, "eu-west-3", "host-a", RegionNodeStatus.Healthy, 20.0, 1.0, false);
        var nodeB = new CloudMeshNode("node-b", CloudProvider.SovereignPrivateAlgiers, "dz-central-1", "host-b", RegionNodeStatus.Healthy, 20.0, 2.0, false);

        gateway.RegisterNode(nodeA);
        gateway.RegisterNode(nodeB);

        var optimal = gateway.GetOptimalRoutingNode();
        optimal!.NodeId.Should().Be("node-b");
    }

    [Fact]
    public void GetOptimalRoutingNode_WeightCanCompensateForHigherLatency()
    {
        var gateway = new MultiCloudMeshGatewayService(seedDefaultTopology: false);
        // Node A: 16ms / 1.0 weight = score 16.0
        // Node B: 24ms / 2.0 weight = score 12.0 (preferred)
        var nodeA = new CloudMeshNode("node-a", CloudProvider.Aws, "eu-west-3", "host-a", RegionNodeStatus.Healthy, 16.0, 1.0, false);
        var nodeB = new CloudMeshNode("node-b", CloudProvider.SovereignPrivateAlgiers, "dz-central-1", "host-b", RegionNodeStatus.Healthy, 24.0, 2.0, false);

        gateway.RegisterNode(nodeA);
        gateway.RegisterNode(nodeB);

        var optimal = gateway.GetOptimalRoutingNode();
        optimal!.NodeId.Should().Be("node-b");
    }

    [Fact]
    public void GetOptimalRoutingNode_WhenAllNodesDegradedOrOffline_ReturnsNull()
    {
        var gateway = new MultiCloudMeshGatewayService(seedDefaultTopology: false);
        var nodeA = new CloudMeshNode("node-a", CloudProvider.Aws, "eu-west-3", "host-a", RegionNodeStatus.Offline, 15.0, 1.0, false);
        var nodeB = new CloudMeshNode("node-b", CloudProvider.Azure, "westeurope", "host-b", RegionNodeStatus.DegradedLatency, 30.0, 1.0, false);

        gateway.RegisterNode(nodeA);
        gateway.RegisterNode(nodeB);

        var optimal = gateway.GetOptimalRoutingNode();
        optimal.Should().BeNull();
    }

    [Fact]
    public void GetOptimalRoutingNode_DeterministicTieBreaking_UsesNodeId()
    {
        var gateway = new MultiCloudMeshGatewayService(seedDefaultTopology: false);
        var nodeZ = new CloudMeshNode("node-z", CloudProvider.Aws, "eu-west-3", "host-z", RegionNodeStatus.Healthy, 20.0, 1.0, false);
        var nodeA = new CloudMeshNode("node-a", CloudProvider.Azure, "westeurope", "host-a", RegionNodeStatus.Healthy, 20.0, 1.0, false);

        gateway.RegisterNode(nodeZ);
        gateway.RegisterNode(nodeA);

        var optimal = gateway.GetOptimalRoutingNode();
        optimal!.NodeId.Should().Be("node-a");
    }

    // =========================================================================
    // Category 4: Strict Majority Quorum Calculations (Tests 16-20)
    // =========================================================================

    [Fact]
    public void EvaluateWriteQuorum_OddCluster_5Nodes_Requires3ForMajority()
    {
        var arbitrator = new SplitBrainQuorumArbitratorService();
        arbitrator.RegisterVotingNode("n1");
        arbitrator.RegisterVotingNode("n2");
        arbitrator.RegisterVotingNode("n3");
        arbitrator.RegisterVotingNode("n4");
        arbitrator.RegisterVotingNode("n5");

        // 3 of 5 -> Majority
        var decisionMajority = arbitrator.EvaluateWriteQuorum("n1", ["n1", "n2", "n3"]);
        decisionMajority.GrantedWriteLease.Should().BeTrue();
        decisionMajority.MajorityClusterNodes.Should().HaveCount(3);
        decisionMajority.PartitionDetected.Should().BeTrue();

        // 2 of 5 -> Denied
        var decisionMinority = arbitrator.EvaluateWriteQuorum("n1", ["n1", "n2"]);
        decisionMinority.GrantedWriteLease.Should().BeFalse();
        decisionMinority.MajorityClusterNodes.Should().BeEmpty();
        decisionMinority.PartitionDetected.Should().BeTrue();
    }

    [Fact]
    public void EvaluateWriteQuorum_OddCluster_3Nodes_Requires2ForMajority()
    {
        var arbitrator = new SplitBrainQuorumArbitratorService();
        arbitrator.RegisterVotingNode("n1");
        arbitrator.RegisterVotingNode("n2");
        arbitrator.RegisterVotingNode("n3");

        // 2 of 3 -> Majority
        var decision = arbitrator.EvaluateWriteQuorum("n1", ["n1", "n2"]);
        decision.GrantedWriteLease.Should().BeTrue();
        decision.MajorityClusterNodes.Should().Contain("n1").And.Contain("n2");

        // 1 of 3 -> Denied
        var decisionMinority = arbitrator.EvaluateWriteQuorum("n1", ["n1"]);
        decisionMinority.GrantedWriteLease.Should().BeFalse();
    }

    [Fact]
    public void EvaluateWriteQuorum_OddCluster_7Nodes_Requires4ForMajority()
    {
        var arbitrator = new SplitBrainQuorumArbitratorService();
        for (int i = 1; i <= 7; i++)
        {
            arbitrator.RegisterVotingNode($"n{i}");
        }

        // 4 of 7 -> Majority
        var decision4 = arbitrator.EvaluateWriteQuorum("n1", ["n1", "n2", "n3", "n4"]);
        decision4.GrantedWriteLease.Should().BeTrue();

        // 3 of 7 -> Denied
        var decision3 = arbitrator.EvaluateWriteQuorum("n1", ["n1", "n2", "n3"]);
        decision3.GrantedWriteLease.Should().BeFalse();
    }

    [Fact]
    public void EvaluateWriteQuorum_EvenCluster_4Nodes_Requires3ForMajority()
    {
        // For N=4, strict majority is 4/2 + 1 = 3 (prevents 2 vs 2 split-brain!)
        var arbitrator = new SplitBrainQuorumArbitratorService();
        arbitrator.RegisterVotingNode("n1");
        arbitrator.RegisterVotingNode("n2");
        arbitrator.RegisterVotingNode("n3");
        arbitrator.RegisterVotingNode("n4");

        // 2 of 4 -> Denied (split-brain tie prevention)
        var decision2 = arbitrator.EvaluateWriteQuorum("n1", ["n1", "n2"]);
        decision2.GrantedWriteLease.Should().BeFalse();

        // 3 of 4 -> Granted
        var decision3 = arbitrator.EvaluateWriteQuorum("n1", ["n1", "n2", "n3"]);
        decision3.GrantedWriteLease.Should().BeTrue();
    }

    [Fact]
    public void EvaluateWriteQuorum_SingleNodeCluster_Requires1ForMajority()
    {
        var arbitrator = new SplitBrainQuorumArbitratorService();
        arbitrator.RegisterVotingNode("single-node");

        var decision = arbitrator.EvaluateWriteQuorum("single-node", ["single-node"]);
        decision.GrantedWriteLease.Should().BeTrue();
        decision.PartitionDetected.Should().BeFalse();
    }

    // =========================================================================
    // Category 5: WAN Partition Split-Brain Isolation (Tests 21-25)
    // =========================================================================

    [Fact]
    public void EvaluateWriteQuorum_EuropeAfricaPartition_MajorityEuropeRetainsLease()
    {
        var gateway = new MultiCloudMeshGatewayService(seedDefaultTopology: false);
        // Cluster of 5 nodes: 3 in Europe, 2 in Africa
        gateway.RegisterNode(new CloudMeshNode("eu-aws", CloudProvider.Aws, "eu-west-3", "aws.eu", RegionNodeStatus.Healthy, 15.0, 1.0, false));
        gateway.RegisterNode(new CloudMeshNode("eu-azure", CloudProvider.Azure, "westeurope", "azure.eu", RegionNodeStatus.Healthy, 18.0, 1.0, false));
        gateway.RegisterNode(new CloudMeshNode("eu-gcp", CloudProvider.Gcp, "europe-west1", "gcp.eu", RegionNodeStatus.Healthy, 20.0, 1.0, false));
        gateway.RegisterNode(new CloudMeshNode("af-algiers", CloudProvider.SovereignPrivateAlgiers, "dz-central-1", "algiers.dz", RegionNodeStatus.Healthy, 12.0, 2.0, true));
        gateway.RegisterNode(new CloudMeshNode("af-dubai", CloudProvider.SovereignPrivateDubai, "me-central-1", "dubai.ae", RegionNodeStatus.Healthy, 28.0, 1.5, false));

        var arbitrator = new SplitBrainQuorumArbitratorService(gateway);

        // WAN partition divides Europe (3 nodes) from Africa (2 nodes)
        // Europe partition evaluates quorum: 3 reachable out of 5
        var decision = arbitrator.EvaluateWriteQuorum("eu-aws", ["eu-aws", "eu-azure", "eu-gcp"]);

        decision.GrantedWriteLease.Should().BeTrue();
        decision.MajorityClusterNodes.Should().BeEquivalentTo(["eu-aws", "eu-azure", "eu-gcp"]);
        decision.IsolatedMinorityNodes.Should().BeEquivalentTo(["af-algiers", "af-dubai"]);
        decision.PartitionDetected.Should().BeTrue();

        // Africa nodes must be isolated in gateway to prevent split-brain writes
        gateway.GetNode("af-algiers")!.HealthStatus.Should().Be(RegionNodeStatus.PartitionedIsolated);
        gateway.GetNode("af-dubai")!.HealthStatus.Should().Be(RegionNodeStatus.PartitionedIsolated);
        gateway.GetNode("eu-aws")!.IsWriteLeader.Should().BeTrue();
    }

    [Fact]
    public void EvaluateWriteQuorum_EuropeAfricaPartition_MinorityAfricaDeniedLease()
    {
        var gateway = new MultiCloudMeshGatewayService(seedDefaultTopology: false);
        gateway.RegisterNode(new CloudMeshNode("eu-aws", CloudProvider.Aws, "eu-west-3", "aws.eu", RegionNodeStatus.Healthy, 15.0, 1.0, false));
        gateway.RegisterNode(new CloudMeshNode("eu-azure", CloudProvider.Azure, "westeurope", "azure.eu", RegionNodeStatus.Healthy, 18.0, 1.0, false));
        gateway.RegisterNode(new CloudMeshNode("eu-gcp", CloudProvider.Gcp, "europe-west1", "gcp.eu", RegionNodeStatus.Healthy, 20.0, 1.0, false));
        gateway.RegisterNode(new CloudMeshNode("af-algiers", CloudProvider.SovereignPrivateAlgiers, "dz-central-1", "algiers.dz", RegionNodeStatus.Healthy, 12.0, 2.0, true));
        gateway.RegisterNode(new CloudMeshNode("af-dubai", CloudProvider.SovereignPrivateDubai, "me-central-1", "dubai.ae", RegionNodeStatus.Healthy, 28.0, 1.5, false));

        var arbitrator = new SplitBrainQuorumArbitratorService(gateway);

        // Africa sub-cluster evaluates write quorum: only 2 nodes reachable out of 5
        var decision = arbitrator.EvaluateWriteQuorum("af-algiers", ["af-algiers", "af-dubai"]);

        decision.GrantedWriteLease.Should().BeFalse();
        decision.PartitionDetected.Should().BeTrue();
        decision.Reason.Should().Contain("Split-brain partition detected");
        arbitrator.HasWriteLease.Should().BeFalse();
    }

    [Fact]
    public void EvaluateWriteQuorum_MinorityPartition_RevokesWriteLeaderInGateway()
    {
        var gateway = new MultiCloudMeshGatewayService(seedDefaultTopology: true);
        var arbitrator = new SplitBrainQuorumArbitratorService(gateway);

        // Initial write leader is Algiers
        gateway.GetNode("node-sovereign-algiers-1")!.IsWriteLeader.Should().BeTrue();

        // 1 reachable node out of 5 evaluates quorum -> denied
        arbitrator.EvaluateWriteQuorum("node-sovereign-algiers-1", ["node-sovereign-algiers-1"]);

        // Write leader must be revoked to protect against corrupt partitioned writes
        gateway.GetAllNodes().Should().NotContain(n => n.IsWriteLeader);
    }

    [Fact]
    public void EvaluateWriteQuorum_FullClusterReachable_NoPartitionDetected()
    {
        var gateway = new MultiCloudMeshGatewayService(seedDefaultTopology: true);
        var arbitrator = new SplitBrainQuorumArbitratorService(gateway);

        var allNodeIds = gateway.GetAllNodes().Select(n => n.NodeId).ToList();
        var decision = arbitrator.EvaluateWriteQuorum("node-sovereign-algiers-1", allNodeIds);

        decision.GrantedWriteLease.Should().BeTrue();
        decision.PartitionDetected.Should().BeFalse();
        decision.IsolatedMinorityNodes.Should().BeEmpty();
        decision.Reason.Should().Contain("Full cluster quorum active");
    }

    [Fact]
    public void EvaluateWriteQuorum_IsolatedNodesMarkedPartitionedInGateway()
    {
        var gateway = new MultiCloudMeshGatewayService(seedDefaultTopology: true);
        var arbitrator = new SplitBrainQuorumArbitratorService(gateway);

        // 3 of 5 nodes reachable (AWS, Azure, GCP); Algiers and Dubai are unreachable
        arbitrator.EvaluateWriteQuorum("node-aws-eu-west-3", ["node-aws-eu-west-3", "node-azure-westeurope", "node-gcp-europe-west1"]);

        gateway.GetNode("node-sovereign-algiers-1")!.HealthStatus.Should().Be(RegionNodeStatus.PartitionedIsolated);
        gateway.GetNode("node-sovereign-dubai-1")!.HealthStatus.Should().Be(RegionNodeStatus.PartitionedIsolated);
    }

    // =========================================================================
    // Category 6: Byzantine Faults & Term Progression (Tests 26-30)
    // =========================================================================

    [Fact]
    public void EvaluateWriteQuorum_ByzantineCandidateNotInReachableSet_RejectsLease()
    {
        var arbitrator = new SplitBrainQuorumArbitratorService();
        arbitrator.RegisterVotingNode("n1");
        arbitrator.RegisterVotingNode("n2");
        arbitrator.RegisterVotingNode("n3");

        // Candidate "n-phantom" claims leadership but is not in the reachable set
        var decision = arbitrator.EvaluateWriteQuorum("n-phantom", ["n1", "n2"]);

        decision.GrantedWriteLease.Should().BeFalse();
        decision.Reason.Should().Contain("Byzantine fault detected");
    }

    [Fact]
    public void EvaluateWriteQuorum_ByzantineCandidateOffline_RejectsLease()
    {
        var gateway = new MultiCloudMeshGatewayService(seedDefaultTopology: false);
        gateway.RegisterNode(new CloudMeshNode("n1", CloudProvider.Aws, "eu-west-3", "h1", RegionNodeStatus.Offline, 10.0, 1.0, false));
        gateway.RegisterNode(new CloudMeshNode("n2", CloudProvider.Azure, "westeurope", "h2", RegionNodeStatus.Healthy, 20.0, 1.0, false));
        gateway.RegisterNode(new CloudMeshNode("n3", CloudProvider.Gcp, "europe-west1", "h3", RegionNodeStatus.Healthy, 20.0, 1.0, false));

        var arbitrator = new SplitBrainQuorumArbitratorService(gateway);

        // Offline candidate claims leadership
        var decision = arbitrator.EvaluateWriteQuorum("n1", ["n1", "n2"]);

        decision.GrantedWriteLease.Should().BeFalse();
        decision.Reason.Should().Contain("Byzantine fault detected");
        decision.Reason.Should().Contain("Offline");
    }

    [Fact]
    public void ProcessHeartbeat_HigherTerm_AdvancesClusterTermAndLeader()
    {
        var arbitrator = new SplitBrainQuorumArbitratorService();
        arbitrator.CurrentTerm.Should().Be(1);

        var heartbeat = new QuorumHeartbeat(
            NodeId: "node-aws",
            Term: 5,
            SequenceNumber: 42,
            TimestampUtc: DateTimeOffset.UtcNow,
            KnownLeaderId: "node-aws",
            ActivePeersCount: 4);

        bool accepted = arbitrator.ProcessHeartbeat(heartbeat);

        accepted.Should().BeTrue();
        arbitrator.CurrentTerm.Should().Be(5);
        arbitrator.CurrentLeaderId.Should().Be("node-aws");
    }

    [Fact]
    public void ProcessHeartbeat_StaleTerm_RejectsHeartbeat()
    {
        var arbitrator = new SplitBrainQuorumArbitratorService();

        // Advance to term 10
        arbitrator.ProcessHeartbeat(new QuorumHeartbeat("node-1", 10, 1, DateTimeOffset.UtcNow, "node-1", 3));

        // Submit stale heartbeat from term 5
        var staleHeartbeat = new QuorumHeartbeat("node-2", 5, 200, DateTimeOffset.UtcNow, "node-2", 3);
        bool accepted = arbitrator.ProcessHeartbeat(staleHeartbeat);

        accepted.Should().BeFalse();
        arbitrator.CurrentTerm.Should().Be(10);
        arbitrator.CurrentLeaderId.Should().Be("node-1");
    }

    [Fact]
    public void GetQuorumState_ExcludesTimedOutHeartbeatsFromActiveNodes()
    {
        var arbitrator = new SplitBrainQuorumArbitratorService(gateway: null)
        {
            HeartbeatTtl = TimeSpan.FromMilliseconds(50)
        };

        // Submit heartbeat for node-active
        arbitrator.ProcessHeartbeat(new QuorumHeartbeat("node-active", 1, 1, DateTimeOffset.UtcNow, "node-active", 2));

        // Submit heartbeat for node-stale
        arbitrator.ProcessHeartbeat(new QuorumHeartbeat("node-stale", 1, 1, DateTimeOffset.UtcNow, "node-active", 2));

        // Wait for TTL to expire
        Thread.Sleep(70);

        // Refresh heartbeat only for node-active
        arbitrator.ProcessHeartbeat(new QuorumHeartbeat("node-active", 1, 2, DateTimeOffset.UtcNow, "node-active", 2));

        var state = arbitrator.GetQuorumState();
        state.ActiveNodeIds.Should().Contain("node-active");
        state.IsolatedNodeIds.Should().Contain("node-stale");
    }

    // =========================================================================
    // Category 7: Autonomous DR Failover Drills (RTO < 30s & RPO == 0) (Tests 31-36)
    // =========================================================================

    [Fact]
    public void ExecuteDrill_SimulatedCloudBlackout_AchievesRtoUnder30SecondsAndRpoZero()
    {
        var gateway = new MultiCloudMeshGatewayService(seedDefaultTopology: true);
        var arbitrator = new SplitBrainQuorumArbitratorService(gateway);
        var autopilot = new AutonomousDrAutopilotService(gateway, arbitrator);

        var request = new TriggerDrDrillRequest(
            TargetNodeId: "node-aws-eu-west-3",
            FailoverType: "SimulatedCloudBlackout");

        var report = autopilot.ExecuteDrill(request);

        report.Should().NotBeNull();
        report.Success.Should().BeTrue();
        report.ActualRtoSeconds.Should().BeLessThan(30.0);
        report.ActualRpoSeconds.Should().Be(0.0);
        report.FailoverType.Should().Be("SimulatedCloudBlackout");
        report.TargetNodeId.Should().Be("node-aws-eu-west-3");
    }

    [Fact]
    public void ExecuteDrill_WanPartition_AchievesRtoUnder30SecondsAndRpoZero()
    {
        var gateway = new MultiCloudMeshGatewayService(seedDefaultTopology: true);
        var arbitrator = new SplitBrainQuorumArbitratorService(gateway);
        var autopilot = new AutonomousDrAutopilotService(gateway, arbitrator);

        var request = new TriggerDrDrillRequest(
            TargetNodeId: "node-sovereign-dubai-1",
            FailoverType: "WanPartition");

        var report = autopilot.ExecuteDrill(request);

        report.Success.Should().BeTrue();
        report.ActualRtoSeconds.Should().BeLessThan(30.0);
        report.ActualRpoSeconds.Should().Be(0.0);
    }

    [Fact]
    public void ExecuteDrill_NonDestructive_RestoresOriginalTopology()
    {
        var gateway = new MultiCloudMeshGatewayService(seedDefaultTopology: true);
        var arbitrator = new SplitBrainQuorumArbitratorService(gateway);
        var autopilot = new AutonomousDrAutopilotService(gateway, arbitrator);

        var originalTargetNode = gateway.GetNode("node-sovereign-algiers-1")!;
        originalTargetNode.HealthStatus.Should().Be(RegionNodeStatus.Healthy);
        originalTargetNode.IsWriteLeader.Should().BeTrue();

        var request = new TriggerDrDrillRequest(
            TargetNodeId: "node-sovereign-algiers-1",
            FailoverType: "SimulatedCloudBlackout");

        var report = autopilot.ExecuteDrill(request);
        report.Success.Should().BeTrue();

        // Non-destructive verification: target node must be restored to its pre-drill healthy state
        var restoredNode = gateway.GetNode("node-sovereign-algiers-1");
        restoredNode.Should().NotBeNull();
        restoredNode!.HealthStatus.Should().Be(RegionNodeStatus.Healthy);
        restoredNode.LatencyP99Ms.Should().Be(originalTargetNode.LatencyP99Ms);
        restoredNode.IsWriteLeader.Should().BeTrue();
    }

    [Fact]
    public void ExecuteDrill_TargetNodeNotFound_ReturnsFailedReport()
    {
        var gateway = new MultiCloudMeshGatewayService(seedDefaultTopology: true);
        var arbitrator = new SplitBrainQuorumArbitratorService(gateway);
        var autopilot = new AutonomousDrAutopilotService(gateway, arbitrator);

        var request = new TriggerDrDrillRequest(TargetNodeId: "non-existent-node");
        var report = autopilot.ExecuteDrill(request);

        report.Success.Should().BeFalse();
        report.Message.Should().Contain("does not exist");
    }

    [Fact]
    public void ExecuteDrill_StoresReportInHistoryAndTopology()
    {
        var gateway = new MultiCloudMeshGatewayService(seedDefaultTopology: true);
        var arbitrator = new SplitBrainQuorumArbitratorService(gateway);
        var autopilot = new AutonomousDrAutopilotService(gateway, arbitrator);

        autopilot.GetDrillHistory().Should().BeEmpty();

        var report = autopilot.ExecuteDrill(new TriggerDrDrillRequest("node-azure-westeurope"));

        autopilot.GetDrillHistory().Should().ContainSingle();
        autopilot.GetLatestReport().Should().Be(report);

        var topology = gateway.GetClusterTopology();
        topology.LastDrillReport.Should().NotBeNull();
        topology.LastDrillReport!.DrillId.Should().Be(report.DrillId);
    }

    [Fact]
    public void ExecuteDrill_WhenTargetWasLeader_FailsOverToBestHealthyNodeDuringDrill()
    {
        var gateway = new MultiCloudMeshGatewayService(seedDefaultTopology: true);
        var arbitrator = new SplitBrainQuorumArbitratorService(gateway);
        var autopilot = new AutonomousDrAutopilotService(gateway, arbitrator);

        // Algiers is default write leader
        gateway.GetNode("node-sovereign-algiers-1")!.IsWriteLeader.Should().BeTrue();

        var report = autopilot.ExecuteDrill(new TriggerDrDrillRequest("node-sovereign-algiers-1", "SimulatedCloudBlackout"));

        report.Success.Should().BeTrue();
        report.Message.Should().Contain("Temporary leader");
    }

    // =========================================================================
    // Category 8: Concurrency & Thread-Safety (Tests 37-39)
    // =========================================================================

    [Fact]
    public void Concurrency_SimultaneousHeartbeats_ThreadSafe()
    {
        var arbitrator = new SplitBrainQuorumArbitratorService();
        const int concurrentTasks = 40;
        var tasks = new Task[concurrentTasks];

        for (int i = 0; i < concurrentTasks; i++)
        {
            int index = i;
            tasks[i] = Task.Run(() =>
            {
                var heartbeat = new QuorumHeartbeat(
                    NodeId: $"node-{index % 5}",
                    Term: 1 + (index / 10),
                    SequenceNumber: index,
                    TimestampUtc: DateTimeOffset.UtcNow,
                    KnownLeaderId: "node-0",
                    ActivePeersCount: 5);

                arbitrator.ProcessHeartbeat(heartbeat);
            });
        }

        var act = () => Task.WaitAll(tasks);
        act.Should().NotThrow();

        arbitrator.CurrentTerm.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void Concurrency_SimultaneousRoutingAndHealthUpdates_ThreadSafe()
    {
        var gateway = new MultiCloudMeshGatewayService(seedDefaultTopology: true);
        const int concurrentTasks = 40;
        var tasks = new Task[concurrentTasks];

        for (int i = 0; i < concurrentTasks; i++)
        {
            int index = i;
            tasks[i] = Task.Run(() =>
            {
                if (index % 2 == 0)
                {
                    gateway.UpdateNodeHealth("node-aws-eu-west-3", RegionNodeStatus.Healthy, 15.0 + (index % 10));
                }
                else
                {
                    gateway.GetOptimalRoutingNode();
                }

                gateway.GetClusterTopology();
            });
        }

        var act = () => Task.WaitAll(tasks);
        act.Should().NotThrow();

        gateway.GetClusterTopology().Nodes.Should().NotBeEmpty();
    }

    [Fact]
    public void Concurrency_SimultaneousQuorumEvaluations_ThreadSafe()
    {
        var gateway = new MultiCloudMeshGatewayService(seedDefaultTopology: true);
        var arbitrator = new SplitBrainQuorumArbitratorService(gateway);
        const int concurrentTasks = 30;
        var tasks = new Task[concurrentTasks];

        for (int i = 0; i < concurrentTasks; i++)
        {
            int index = i;
            tasks[i] = Task.Run(() =>
            {
                arbitrator.EvaluateWriteQuorum(
                    "node-aws-eu-west-3",
                    ["node-aws-eu-west-3", "node-azure-westeurope", "node-gcp-europe-west1"]);
                arbitrator.GetQuorumState();
            });
        }

        var act = () => Task.WaitAll(tasks);
        act.Should().NotThrow();
    }

    // =========================================================================
    // Category 9: Edge Cases, Input Validation & DTO Mappings (Tests 40-47)
    // =========================================================================

    [Fact]
    public void EvaluateWriteQuorum_EmptyReachableNodes_DeniesLease()
    {
        var arbitrator = new SplitBrainQuorumArbitratorService();
        arbitrator.RegisterVotingNode("n1");
        arbitrator.RegisterVotingNode("n2");

        var decision = arbitrator.EvaluateWriteQuorum("n1", []);

        decision.GrantedWriteLease.Should().BeFalse();
        decision.MajorityClusterNodes.Should().BeEmpty();
        decision.PartitionDetected.Should().BeTrue();
    }

    [Fact]
    public void EvaluateWriteQuorum_WithWhitespaceAndEmptyNodeIds_FiltersCleanly()
    {
        var arbitrator = new SplitBrainQuorumArbitratorService();
        arbitrator.RegisterVotingNode("n1");
        arbitrator.RegisterVotingNode("n2");
        arbitrator.RegisterVotingNode("n3");

        var decision = arbitrator.EvaluateWriteQuorum("n1", ["n1", "", "   ", "n2"]);

        // 2 of 3 valid nodes reachable -> Majority achieved
        decision.GrantedWriteLease.Should().BeTrue();
        decision.MajorityClusterNodes.Should().BeEquivalentTo(["n1", "n2"]);
    }

    [Fact]
    public void RegisterNodeRequest_ToNode_MapsAllPropertiesAccurately()
    {
        var request = new RegisterNodeRequest(
            NodeId: "node-test-dto",
            Provider: CloudProvider.SovereignPrivateDubai,
            RegionCode: "me-central-1",
            Hostname: "dubai.test.cloud",
            HealthStatus: RegionNodeStatus.Healthy,
            LatencyP99Ms: 19.8,
            Weight: 2.5,
            IsWriteLeader: true);

        var node = request.ToNode();

        node.NodeId.Should().Be("node-test-dto");
        node.Provider.Should().Be(CloudProvider.SovereignPrivateDubai);
        node.RegionCode.Should().Be("me-central-1");
        node.Hostname.Should().Be("dubai.test.cloud");
        node.HealthStatus.Should().Be(RegionNodeStatus.Healthy);
        node.LatencyP99Ms.Should().Be(19.8);
        node.Weight.Should().Be(2.5);
        node.IsWriteLeader.Should().BeTrue();
        node.IsRoutable.Should().BeTrue();
    }

    [Fact]
    public void NodeHeartbeatRequest_ToHeartbeat_MapsAllPropertiesAccurately()
    {
        var now = DateTimeOffset.UtcNow;
        var request = new NodeHeartbeatRequest(
            NodeId: "node-hb-1",
            Term: 4,
            SequenceNumber: 101,
            TimestampUtc: now,
            KnownLeaderId: "node-leader-1",
            ActivePeersCount: 3);

        var heartbeat = request.ToHeartbeat();

        heartbeat.NodeId.Should().Be("node-hb-1");
        heartbeat.Term.Should().Be(4);
        heartbeat.SequenceNumber.Should().Be(101);
        heartbeat.TimestampUtc.Should().Be(now);
        heartbeat.KnownLeaderId.Should().Be("node-leader-1");
        heartbeat.ActivePeersCount.Should().Be(3);
    }

    [Fact]
    public void ProcessHeartbeat_SameTerm_UpdatesKnownLeader()
    {
        var arbitrator = new SplitBrainQuorumArbitratorService();

        arbitrator.ProcessHeartbeat(new QuorumHeartbeat("n1", 1, 1, DateTimeOffset.UtcNow, "n1", 2));
        arbitrator.CurrentLeaderId.Should().Be("n1");

        // Same term, but new leader acknowledged
        arbitrator.ProcessHeartbeat(new QuorumHeartbeat("n2", 1, 2, DateTimeOffset.UtcNow, "n2", 2));
        arbitrator.CurrentLeaderId.Should().Be("n2");
    }

    [Fact]
    public void UpdateNodeHealth_NonExistentNode_DoesNotThrow()
    {
        var gateway = new MultiCloudMeshGatewayService(seedDefaultTopology: false);

        var act = () => gateway.UpdateNodeHealth("unknown-node", RegionNodeStatus.Healthy, 20.0);
        act.Should().NotThrow();
    }

    [Fact]
    public void GetNode_NullOrWhiteSpace_ReturnsNull()
    {
        var gateway = new MultiCloudMeshGatewayService(seedDefaultTopology: true);

        gateway.GetNode("").Should().BeNull();
        gateway.GetNode("   ").Should().BeNull();
    }

    [Fact]
    public void UnregisterNode_NullOrWhiteSpace_ReturnsFalse()
    {
        var gateway = new MultiCloudMeshGatewayService(seedDefaultTopology: true);

        gateway.UnregisterNode("").Should().BeFalse();
        gateway.UnregisterNode("   ").Should().BeFalse();
    }

    // =========================================================================
    // Category 10: ServiceCollection Registration (NO BuildServiceProvider) (Test 48)
    // =========================================================================

    [Fact]
    public void ServiceCollectionRegistration_RegistersAllRequiredServicesWithoutCallingBuildServiceProvider()
    {
        // Rule: In tests, NEVER call 'services.BuildServiceProvider()'.
        // Instantiate services directly or assert on ServiceCollection descriptors.
        var services = new ServiceCollection();

        services.AddMultiCloudServices();

        services.Should().Contain(d =>
            d.ServiceType == typeof(IMultiCloudMeshGatewayService) &&
            d.ImplementationType == typeof(MultiCloudMeshGatewayService) &&
            d.Lifetime == ServiceLifetime.Singleton);

        services.Should().Contain(d =>
            d.ServiceType == typeof(ISplitBrainQuorumArbitratorService) &&
            d.ImplementationType == typeof(SplitBrainQuorumArbitratorService) &&
            d.Lifetime == ServiceLifetime.Singleton);

        services.Should().Contain(d =>
            d.ServiceType == typeof(IAutonomousDrAutopilotService) &&
            d.ImplementationType == typeof(AutonomousDrAutopilotService) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }
}
