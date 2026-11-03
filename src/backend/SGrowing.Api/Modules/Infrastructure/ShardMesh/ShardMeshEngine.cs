using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace SGrowing.Api.Modules.Infrastructure.ShardMesh;

public interface IShardMeshEngine
{
    ShardMeshTopology GetTopology();
    TenantShardRouting RouteTenant(string tenantId);
    RebalanceResult RebalanceBuckets(RebalanceShardRequest request);
}

public class ShardMeshEngine : IShardMeshEngine
{
    private const int TotalBuckets = 1024;
    private readonly ConcurrentDictionary<string, DatabaseShardNode> _shards = new(StringComparer.OrdinalIgnoreCase);
    private DateTime _lastRebalanced = DateTime.UtcNow.AddDays(-7);

    public ShardMeshEngine()
    {
        // Seed 3 regional active-active shards
        _shards["shard_us_east"] = new DatabaseShardNode("shard_us_east", ShardRegion.UsEastPrimary, "pg-shard-us.internal:5432", 84.5, 4200, 342, false);
        _shards["shard_eu_west"] = new DatabaseShardNode("shard_eu_west", ShardRegion.EuropeWestPrimary, "pg-shard-eu.internal:5432", 52.0, 2800, 341, false);
        _shards["shard_apac_south"] = new DatabaseShardNode("shard_apac_south", ShardRegion.ApacSouthPrimary, "pg-shard-apac.internal:5432", 48.0, 2100, 341, false);
    }

    public ShardMeshTopology GetTopology()
    {
        var list = _shards.Values.OrderBy(s => s.ShardId).ToList();
        var maxCap = list.Max(s => s.StorageUtilizationPercentage);
        var minCap = list.Min(s => s.StorageUtilizationPercentage);
        var balanced = (maxCap - minCap) < 20.0; // Balanced if within 20% variance

        return new ShardMeshTopology(
            list,
            TotalBuckets,
            TotalClusterStorageTb: 48.5,
            balanced,
            _lastRebalanced
        );
    }

    public TenantShardRouting RouteTenant(string tenantId)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(tenantId));
        int hashVal = Math.Abs(BitConverter.ToInt32(hashBytes, 0));
        int bucket = hashVal % TotalBuckets;

        // Route bucket deterministically
        var shardList = _shards.Values.OrderBy(s => s.ShardId).ToList();
        int cumulative = 0;
        DatabaseShardNode assigned = shardList.First();

        foreach (var shard in shardList)
        {
            cumulative += shard.AssignedVirtualBuckets;
            if (bucket < cumulative)
            {
                assigned = shard;
                break;
            }
        }

        return new TenantShardRouting(
            tenantId,
            assigned.ShardId,
            bucket,
            assigned.HostEndpoint
        );
    }

    public RebalanceResult RebalanceBuckets(RebalanceShardRequest request)
    {
        if (!_shards.TryGetValue(request.SourceShardId, out var source) ||
            !_shards.TryGetValue(request.TargetShardId, out var target))
        {
            return new RebalanceResult(false, request.SourceShardId, request.TargetShardId, 0, 0.0, "Shard node not found.");
        }

        int migrate = Math.Clamp(request.BucketsToMigrate, 1, source.AssignedVirtualBuckets - 100);

        _shards[request.SourceShardId] = source with
        {
            AssignedVirtualBuckets = source.AssignedVirtualBuckets - migrate,
            StorageUtilizationPercentage = Math.Round(source.StorageUtilizationPercentage - (migrate * 0.15), 1)
        };

        _shards[request.TargetShardId] = target with
        {
            AssignedVirtualBuckets = target.AssignedVirtualBuckets + migrate,
            StorageUtilizationPercentage = Math.Round(target.StorageUtilizationPercentage + (migrate * 0.15), 1)
        };

        _lastRebalanced = DateTime.UtcNow;

        return new RebalanceResult(
            Success: true,
            request.SourceShardId,
            request.TargetShardId,
            migrate,
            ElapsedSeconds: 3.4,
            Message: $"Seamlessly migrated {migrate} virtual hash buckets from {request.SourceShardId} to {request.TargetShardId} without downtime."
        );
    }
}
