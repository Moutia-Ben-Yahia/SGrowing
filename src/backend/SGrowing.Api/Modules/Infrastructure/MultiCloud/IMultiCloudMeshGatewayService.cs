namespace SGrowing.Api.Modules.Infrastructure.MultiCloud;

/// <summary>
/// Anycast multi-cloud mesh gateway router managing cluster topology and traffic routing.
/// </summary>
public interface IMultiCloudMeshGatewayService
{
    /// <summary>
    /// Registers or updates a region node in the cluster topology mesh.
    /// </summary>
    void RegisterNode(CloudMeshNode node);

    /// <summary>
    /// Unregisters a node from the cluster topology mesh.
    /// </summary>
    bool UnregisterNode(string nodeId);

    /// <summary>
    /// Updates operational health status and observed latency for a node.
    /// If latency >= 5000ms, status is automatically degraded to protect ingress traffic.
    /// </summary>
    void UpdateNodeHealth(string nodeId, RegionNodeStatus status, double latencyMs);

    /// <summary>
    /// Selects the optimal routing node for ingress traffic based on weighted lowest latency.
    /// Ingress is automatically diverted away from degraded, partitioned, draining, or offline nodes.
    /// Returns null if no healthy nodes are available.
    /// </summary>
    CloudMeshNode? GetOptimalRoutingNode();

    /// <summary>
    /// Retrieves current cluster topology snapshot across all cloud providers and sovereign datacenters.
    /// </summary>
    MultiCloudTopologyResponse GetClusterTopology();

    /// <summary>
    /// Retrieves a specific region node by identifier.
    /// </summary>
    CloudMeshNode? GetNode(string nodeId);

    /// <summary>
    /// Retrieves all nodes currently registered in the mesh.
    /// </summary>
    IReadOnlyList<CloudMeshNode> GetAllNodes();

    /// <summary>
    /// Designates a node as the primary write leader in the mesh.
    /// </summary>
    bool SetWriteLeader(string nodeId);

    /// <summary>
    /// Clears the write leader designation across all nodes.
    /// </summary>
    void ClearWriteLeader();

    /// <summary>
    /// Records the latest disaster recovery drill report in the topology snapshot.
    /// </summary>
    void SetLastDrillReport(DisasterRecoveryDrillReport report);

    /// <summary>
    /// Sets the current consensus term for the topology.
    /// </summary>
    void SetCurrentTerm(long term);
}
