using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Infrastructure.ShardMesh;

public enum ShardRegion
{
    UsEastPrimary,
    EuropeWestPrimary,
    ApacSouthPrimary
}

public record DatabaseShardNode(
    string ShardId,
    ShardRegion Region,
    string HostEndpoint,
    double StorageUtilizationPercentage,
    int QueriesPerSecondIops,
    int AssignedVirtualBuckets,
    bool IsRebalancing
);

public record TenantShardRouting(
    string TenantId,
    string AssignedShardId,
    int VirtualHashBucket,
    string RegionEndpoint
);

public record ShardMeshTopology(
    List<DatabaseShardNode> Shards,
    int TotalVirtualBuckets,
    double TotalClusterStorageTb,
    bool MeshBalanced,
    DateTime LastRebalancedAtUtc
);

public record RebalanceShardRequest(
    string SourceShardId,
    string TargetShardId,
    int BucketsToMigrate
);

public record RebalanceResult(
    bool Success,
    string SourceShardId,
    string TargetShardId,
    int BucketsMigrated,
    double ElapsedSeconds,
    string Message
);
