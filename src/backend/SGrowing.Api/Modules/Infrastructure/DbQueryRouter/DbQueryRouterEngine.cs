using System;
using System.Collections.Generic;
using System.Linq;

namespace SGrowing.Api.Modules.Infrastructure.DbQueryRouter;

public interface IDbQueryRouterEngine
{
    DbQueryRouterOverview GetOverview();
    ProbeLagResult ProbeLag(ProbeLagCommand command);
    RouteQueryResult RouteQuery(RouteQueryCommand command);
}

public class DbQueryRouterEngine : IDbQueryRouterEngine
{
    private readonly List<DbReplicaNode> _replicas =
    [
        new("DB-PRIMARY-IAD", "us-east-1", "db-primary.iad.sgrowing.internal", "PostgreSQL-17", 0, 4, 120, 42.5, "Healthy_InPool"),
        new("DB-REPLICA-FRA", "eu-central-1", "db-rep.fra.sgrowing.internal", "PostgreSQL-17", 18, 12, 65, 31.0, "Healthy_InPool"),
        new("DB-REPLICA-SIN", "ap-southeast-1", "db-rep.sin.sgrowing.internal", "PostgreSQL-17", 45, 22, 48, 28.4, "Healthy_InPool"),
        new("DB-REPLICA-GRU", "sa-east-1", "db-rep.gru.sgrowing.internal", "PostgreSQL-17", 125, 75, 20, 68.2, "Lagging_Degraded")
    ];

    public DbQueryRouterOverview GetOverview()
    {
        var totalReplicas = _replicas.Count;
        var healthy = _replicas.Count(r => r.NodeStatus == "Healthy_InPool");
        var maxLag = 100; // ms threshold before circuit breaker trips

        var metrics = new DbQueryRoutingMetrics(
            14200000L,
            12638000L,
            1562000L,
            89.0,
            45, // 45 microseconds
            1
        );

        return new DbQueryRouterOverview(
            totalReplicas,
            healthy,
            maxLag,
            metrics,
            _replicas
        );
    }

    public ProbeLagResult ProbeLag(ProbeLagCommand command)
    {
        var receiptId = $"PROBE-DB-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
        var isLagging = command.MeasuredLagMs > 100 || command.CpuPct > 85.0;
        var status = isLagging ? "Lagging_Degraded" : "Healthy_InPool";
        var circuitTrip = isLagging;
        var recommendation = isLagging
            ? $"Remove {command.ReplicaId} from active read pool; failover read queries to nearest peer replica"
            : "Replication lag within SLA bounds (<100ms); node eligible for geo-routing";

        return new ProbeLagResult(
            receiptId,
            command.ReplicaId,
            status,
            circuitTrip,
            recommendation
        );
    }

    public RouteQueryResult RouteQuery(RouteQueryCommand command)
    {
        var decisionId = $"ROUTE-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
        var isWrite = command.SqlStatement.TrimStart().StartsWith("INSERT", StringComparison.OrdinalIgnoreCase) ||
                      command.SqlStatement.TrimStart().StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase) ||
                      command.SqlStatement.TrimStart().StartsWith("DELETE", StringComparison.OrdinalIgnoreCase);

        if (isWrite || command.RequireStrongConsistency)
        {
            return new RouteQueryResult(
                decisionId,
                "DB-PRIMARY-IAD",
                "Primary_ReadWrite",
                0,
                "Write transaction or strong consistency constraint requires Primary IAD node"
            );
        }

        // Read query routed to nearest healthy replica
        var target = command.ClientRegion switch
        {
            "eu-central-1" or "Europe" => "DB-REPLICA-FRA",
            "ap-southeast-1" or "AsiaPacific" => "DB-REPLICA-SIN",
            _ => "DB-PRIMARY-IAD"
        };

        var matched = _replicas.FirstOrDefault(r => r.ReplicaId == target) ?? _replicas[0];

        return new RouteQueryResult(
            decisionId,
            matched.ReplicaId,
            "Replica_ReadOnly",
            matched.ReplicationLagMs,
            $"Read-only query routed to lowest latency healthy replica in {matched.RegionName}"
        );
    }
}
