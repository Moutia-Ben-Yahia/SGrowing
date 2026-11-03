using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Infrastructure.DbQueryRouter;

public record DbReplicaNode(
    string ReplicaId,
    string RegionName, // "us-east-1", "eu-central-1", "ap-southeast-1", "sa-east-1"
    string Hostname,
    string DatabaseEngine, // "PostgreSQL-17", "CockroachDB"
    int ReplicationLagMs,
    int RttLatencyMs,
    int ActiveConnectionsCount,
    double CpuUtilizationPct,
    string NodeStatus // "Healthy_InPool", "Lagging_Degraded", "Draining", "Offline"
);

public record DbQueryRoutingMetrics(
    long TotalQueriesRoutedToday,
    long ReadQueriesCount,
    long WriteQueriesCount,
    double ReadTrafficPct,
    int AverageRoutingDecisionMicroseconds,
    int CircuitBreakerTripsCount
);

public record DbQueryRouterOverview(
    int TotalReplicasCount,
    int HealthyReplicasCount,
    int MaxToleratedLagMs,
    DbQueryRoutingMetrics Metrics,
    IReadOnlyList<DbReplicaNode> Replicas
);

public record ProbeLagCommand(
    string ReplicaId,
    int MeasuredLagMs,
    int RttLatencyMs,
    double CpuPct
);

public record ProbeLagResult(
    string ProbeReceiptId,
    string ReplicaId,
    string NewNodeStatus,
    bool CircuitBreakerTriggered,
    string RoutingRecommendation
);

public record RouteQueryCommand(
    string SqlStatement,
    string ClientRegion,
    bool RequireStrongConsistency
);

public record RouteQueryResult(
    string RoutingDecisionId,
    string TargetNodeId,
    string TargetNodeType, // "Primary_ReadWrite", "Replica_ReadOnly"
    int EstimatedLagMs,
    string RouteRationale
);
