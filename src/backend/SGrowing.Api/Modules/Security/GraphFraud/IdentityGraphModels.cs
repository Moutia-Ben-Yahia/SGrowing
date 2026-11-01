namespace SGrowing.Api.Modules.Security.GraphFraud;

public enum GraphNodeType
{
    Customer,
    DeviceFingerprint,
    PhysicalAddress,
    PhoneNumber,
    PaymentMethod,
    BankRib
}

public enum GraphRelationType
{
    PlacedFromDevice,
    DeliveredToAddress,
    RegisteredWithPhone,
    PaidWithCard,
    TransferredToBank
}

public record GraphNode
{
    public required string NodeId { get; init; }
    public required GraphNodeType NodeType { get; init; }
    public IReadOnlyDictionary<string, string> Properties { get; init; } = new Dictionary<string, string>();
    public double RiskScore { get; init; }
    public bool IsQuarantined { get; init; }
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
    public DateTime? LastSeenAtUtc { get; init; } = DateTime.UtcNow;
}

public record GraphEdge
{
    public required string SourceNodeId { get; init; }
    public required string TargetNodeId { get; init; }
    public required GraphRelationType RelationType { get; init; }
    public double Weight { get; init; } = 1.0;
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
    public IReadOnlyDictionary<string, string> Properties { get; init; } = new Dictionary<string, string>();
}

public record FraudClusterRecord
{
    public required string ClusterId { get; init; }
    public required IReadOnlySet<string> MemberNodeIds { get; init; }
    public double ClusterRiskScore { get; init; }
    public double ModularityScore { get; init; }
    public required IReadOnlyList<string> PrimaryRiskFactors { get; init; }
    public DateTime DetectedAtUtc { get; init; } = DateTime.UtcNow;
    public bool IsContained { get; init; }
}

public record QuarantineActionResponse
{
    public int QuarantinedNodesCount { get; init; }
    public int FrozenOrdersCount { get; init; }
    public decimal FrozenEscrowAmount { get; init; }
    public required IReadOnlyList<string> CancelledWaybills { get; init; }
}

public record ContainmentAuditEvent
{
    public required string EventId { get; init; }
    public required string ClusterId { get; init; }
    public required IReadOnlyList<string> AffectedNodeIds { get; init; }
    public required QuarantineActionResponse ActionSummary { get; init; }
    public string? Reason { get; init; }
    public string? OperatorId { get; init; }
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
}

public record CentralityMetric
{
    public required string NodeId { get; init; }
    public GraphNodeType NodeType { get; init; }
    public int Degree { get; init; }
    public double PageRank { get; init; }
    public bool IsMuleOrBridge { get; init; }
    public int LinkedCustomersCount { get; init; }
}

public record LinkCustomerEntitiesRequest
{
    public required string CustomerId { get; init; }
    public string? DeviceFingerprint { get; init; }
    public string? PhysicalAddress { get; init; }
    public string? PhoneNumber { get; init; }
    public string? PaymentMethod { get; init; }
    public string? BankRib { get; init; }
    public double InitialCustomerRiskScore { get; init; }
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}

public record LinkEntitiesResponse
{
    public required string CustomerNodeId { get; init; }
    public int NodesCreatedOrUpdated { get; init; }
    public int EdgesCreated { get; init; }
    public required IReadOnlyList<string> LinkedNodeIds { get; init; }
}

public record NeighborhoodResponse
{
    public required string CenterNodeId { get; init; }
    public int Depth { get; init; }
    public required IReadOnlyList<GraphNode> Nodes { get; init; }
    public required IReadOnlyList<GraphEdge> Edges { get; init; }
}

public record QuarantineClusterRequest
{
    public string? Reason { get; init; }
    public string? OperatorId { get; init; }
}

public record QuarantineClusterResponse
{
    public required string ClusterId { get; init; }
    public bool Success { get; init; }
    public required QuarantineActionResponse ActionSummary { get; init; }
    public DateTime QuarantinedAtUtc { get; init; }
}

public record GraphFraudEngineHealthResponse
{
    public bool IsHealthy { get; init; } = true;
    public int TotalNodes { get; init; }
    public int TotalEdges { get; init; }
    public int QuarantinedNodesCount { get; init; }
    public int DetectedClustersCount { get; init; }
    public DateTime ServerTimeUtc { get; init; } = DateTime.UtcNow;
}
