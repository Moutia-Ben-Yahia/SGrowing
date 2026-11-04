using System;
using System.Collections.Generic;
using FluentAssertions;
using SGrowing.Api.Modules.Infrastructure.DbQueryRouter;
using Xunit;

namespace SGrowing.Tests.Infrastructure;

public class DbQueryRouterTests
{
    private readonly DbQueryRouterEngine _engine = new();

    [Fact]
    public void GetOverview_ReturnsReplicasAndRoutingMetrics()
    {
        var overview = _engine.GetOverview();

        overview.Should().NotBeNull();
        overview.TotalReplicasCount.Should().Be(4);
        overview.HealthyReplicasCount.Should().Be(3);
        overview.MaxToleratedLagMs.Should().Be(100);
        overview.Metrics.TotalQueriesRoutedToday.Should().BeGreaterThan(10000000L);
        overview.Metrics.ReadTrafficPct.Should().Be(89.0);
        overview.Replicas.Should().HaveCount(4);
    }

    [Fact]
    public void ProbeLag_DetectsLaggingReplicaAndTripsCircuitBreaker()
    {
        var command = new ProbeLagCommand("DB-REPLICA-GRU", 145, 80, 72.0);
        var result = _engine.ProbeLag(command);

        result.Should().NotBeNull();
        result.ReplicaId.Should().Be("DB-REPLICA-GRU");
        result.NewNodeStatus.Should().Be("Lagging_Degraded");
        result.CircuitBreakerTriggered.Should().BeTrue();
        result.RoutingRecommendation.Should().Contain("Remove DB-REPLICA-GRU from active read pool");
    }

    [Fact]
    public void ProbeLag_HighCpuUsage_TripsCircuitBreakerEvenWithLowLag()
    {
        // Arrange: lag is low (25ms) but CPU is saturated (89.5%)
        var command = new ProbeLagCommand("DB-REPLICA-FRA", 25, 15, 89.5);

        // Act
        var result = _engine.ProbeLag(command);

        // Assert
        result.NewNodeStatus.Should().Be("Lagging_Degraded");
        result.CircuitBreakerTriggered.Should().BeTrue();
        result.RoutingRecommendation.Should().Contain("Remove DB-REPLICA-FRA from active read pool");
    }

    [Fact]
    public void ProbeLag_HealthyNode_ReturnsNoCircuitTrip()
    {
        // Arrange: normal lag (30ms) and normal CPU (45%)
        var command = new ProbeLagCommand("DB-REPLICA-SIN", 30, 10, 45.0);

        // Act
        var result = _engine.ProbeLag(command);

        // Assert
        result.NewNodeStatus.Should().Be("Healthy_InPool");
        result.CircuitBreakerTriggered.Should().BeFalse();
        result.RoutingRecommendation.Should().Contain("<100ms");
    }

    [Fact]
    public void RouteQuery_DirectsWriteTransactionToPrimary()
    {
        var command = new RouteQueryCommand("UPDATE accounts SET status = 'active' WHERE id = 101;", "eu-central-1", false);
        var result = _engine.RouteQuery(command);

        result.Should().NotBeNull();
        result.TargetNodeId.Should().Be("DB-PRIMARY-IAD");
        result.TargetNodeType.Should().Be("Primary_ReadWrite");
        result.EstimatedLagMs.Should().Be(0);
    }

    [Fact]
    public void RouteQuery_DirectsReadOnlyQueryToNearestHealthyReplica()
    {
        var command = new RouteQueryCommand("SELECT id, name, balance FROM accounts WHERE region = 'EMEA';", "eu-central-1", false);
        var result = _engine.RouteQuery(command);

        result.Should().NotBeNull();
        result.TargetNodeId.Should().Be("DB-REPLICA-FRA");
        result.TargetNodeType.Should().Be("Replica_ReadOnly");
        result.EstimatedLagMs.Should().Be(18);
    }

    [Fact]
    public void RouteQuery_DirectsStrongConsistencyReadToPrimary()
    {
        // Arrange: read query with RequireStrongConsistency = true
        var command = new RouteQueryCommand("SELECT balance FROM wallets WHERE user_id = 'usr_123';", "eu-central-1", true);

        // Act
        var result = _engine.RouteQuery(command);

        // Assert
        result.TargetNodeId.Should().Be("DB-PRIMARY-IAD");
        result.TargetNodeType.Should().Be("Primary_ReadWrite");
        result.EstimatedLagMs.Should().Be(0);
        result.RouteRationale.Should().Contain("strong consistency");
    }

    [Theory]
    [InlineData("INSERT INTO orders (id, total) VALUES ('ord_1', 100);")]
    [InlineData("DELETE FROM sessions WHERE expires_at < NOW();")]
    public void RouteQuery_DirectsDeleteOrInsertToWriteNode(string sql)
    {
        // Arrange
        var command = new RouteQueryCommand(sql, "ap-southeast-1", false);

        // Act
        var result = _engine.RouteQuery(command);

        // Assert
        result.TargetNodeId.Should().Be("DB-PRIMARY-IAD");
        result.TargetNodeType.Should().Be("Primary_ReadWrite");
    }
}
