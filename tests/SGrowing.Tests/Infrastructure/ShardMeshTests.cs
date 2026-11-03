using System;
using System.Linq;
using FluentAssertions;
using SGrowing.Api.Modules.Infrastructure.ShardMesh;
using Xunit;

namespace SGrowing.Tests.Infrastructure;

public class ShardMeshTests
{
    private readonly ShardMeshEngine _engine = new();

    [Fact]
    public void GetTopology_ReturnsActiveActiveShardsAndMetrics()
    {
        var topology = _engine.GetTopology();

        topology.Should().NotBeNull();
        topology.Shards.Should().HaveCount(3);
        topology.TotalVirtualBuckets.Should().Be(1024);
        topology.TotalClusterStorageTb.Should().BeGreaterThan(0);
        topology.Shards.Select(s => s.ShardId).Should().Contain(new[] { "shard_us_east", "shard_eu_west", "shard_apac_south" });
    }

    [Fact]
    public void RouteTenant_RoutesConsistentlyToShardAndBucket()
    {
        var tenantId = "tenant_enterprise_aurora_99";
        var route1 = _engine.RouteTenant(tenantId);
        var route2 = _engine.RouteTenant(tenantId);

        route1.TenantId.Should().Be(tenantId);
        route1.VirtualHashBucket.Should().BeInRange(0, 1023);
        route1.AssignedShardId.Should().NotBeNullOrWhiteSpace();
        route1.RegionEndpoint.Should().NotBeNullOrWhiteSpace();

        // Deterministic routing
        route2.AssignedShardId.Should().Be(route1.AssignedShardId);
        route2.VirtualHashBucket.Should().Be(route1.VirtualHashBucket);
    }

    [Fact]
    public void RebalanceBuckets_TransfersBucketsAndUpdatesUtilization()
    {
        var beforeTopology = _engine.GetTopology();
        var sourceBefore = beforeTopology.Shards.First(s => s.ShardId == "shard_us_east");
        var targetBefore = beforeTopology.Shards.First(s => s.ShardId == "shard_eu_west");

        var request = new RebalanceShardRequest("shard_us_east", "shard_eu_west", 30);
        var result = _engine.RebalanceBuckets(request);

        result.Success.Should().BeTrue();
        result.BucketsMigrated.Should().Be(30);

        var afterTopology = _engine.GetTopology();
        var sourceAfter = afterTopology.Shards.First(s => s.ShardId == "shard_us_east");
        var targetAfter = afterTopology.Shards.First(s => s.ShardId == "shard_eu_west");

        sourceAfter.AssignedVirtualBuckets.Should().Be(sourceBefore.AssignedVirtualBuckets - 30);
        targetAfter.AssignedVirtualBuckets.Should().Be(targetBefore.AssignedVirtualBuckets + 30);
        sourceAfter.StorageUtilizationPercentage.Should().BeLessThan(sourceBefore.StorageUtilizationPercentage);
        targetAfter.StorageUtilizationPercentage.Should().BeGreaterThan(targetBefore.StorageUtilizationPercentage);
    }

    [Fact]
    public void RebalanceBuckets_ReturnsError_WhenShardNotFound()
    {
        var request = new RebalanceShardRequest("non_existent_shard", "shard_eu_west", 20);
        var result = _engine.RebalanceBuckets(request);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not found");
    }
}
