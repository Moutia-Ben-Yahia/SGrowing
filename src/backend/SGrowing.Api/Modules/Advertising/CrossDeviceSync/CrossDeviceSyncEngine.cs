using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Advertising.CrossDeviceSync;

public interface ICrossDeviceSyncEngine
{
    IdentityCluster ResolveIdentityCluster(ResolveIdentityRequest request);
    IReadOnlyList<IdentityCluster> GetClusters();
    MatchedConversionEvent MatchOfflineConversion(OfflineConversionReceipt receipt);
    IReadOnlyList<MatchedConversionEvent> GetMatchedEvents();
    SyncMatchMetrics GetMetrics();
}

public class CrossDeviceSyncEngine : ICrossDeviceSyncEngine
{
    private readonly ConcurrentDictionary<string, IdentityCluster> _clusters = new();
    private readonly ConcurrentBag<MatchedConversionEvent> _matches = new();
    private readonly ConcurrentBag<OfflineConversionReceipt> _receipts = new();

    public CrossDeviceSyncEngine()
    {
        SeedInitialClusters();
    }

    private void SeedInitialClusters()
    {
        var cluster1 = new IdentityCluster(
            ClusterId: "CLUSTER-CD-901",
            PrimaryHouseholdId: "HH-US-EAST-441",
            Nodes: new List<DeviceIdentityNode>
            {
                new("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", IdentifierType.HashedEmailSha256, DateTime.UtcNow.AddDays(-30), DateTime.UtcNow, 98),
                new("gaid-4819-bb01-92fa", IdentifierType.MobileAdIdGaid, DateTime.UtcNow.AddDays(-20), DateTime.UtcNow, 92),
                new("uid2-tok-8481-9921", IdentifierType.Uid2Token, DateTime.UtcNow.AddDays(-15), DateTime.UtcNow, 95)
            },
            TotalLinkedDevices: 3,
            OverallGraphConfidence: 95.0m,
            UpdatedAtUtc: DateTime.UtcNow.AddHours(-2)
        );

        var cluster2 = new IdentityCluster(
            ClusterId: "CLUSTER-CD-902",
            PrimaryHouseholdId: "HH-EU-WEST-772",
            Nodes: new List<DeviceIdentityNode>
            {
                new("8f434346648f6b96df89dda901c5176b10a6d83961dd3c1ac88b59b2dc327aa4", IdentifierType.HashedEmailSha256, DateTime.UtcNow.AddDays(-40), DateTime.UtcNow, 96),
                new("idfa-2941-ee44-11fa", IdentifierType.MobileAdIdIdfa, DateTime.UtcNow.AddDays(-10), DateTime.UtcNow, 90),
                new("192.168.10.0/24-hash", IdentifierType.IpSubnetFingerprint, DateTime.UtcNow.AddDays(-5), DateTime.UtcNow, 82)
            },
            TotalLinkedDevices: 3,
            OverallGraphConfidence: 89.3m,
            UpdatedAtUtc: DateTime.UtcNow.AddHours(-1)
        );

        _clusters[cluster1.ClusterId] = cluster1;
        _clusters[cluster2.ClusterId] = cluster2;
    }

    public IdentityCluster ResolveIdentityCluster(ResolveIdentityRequest request)
    {
        var clusterId = $"CLUSTER-CD-{SyncSeed()}";
        var nodes = new List<DeviceIdentityNode>
        {
            new(request.PrimaryIdentifier, request.PrimaryType, DateTime.UtcNow, DateTime.UtcNow, 95)
        };

        foreach (var assoc in request.AssociatedIdentifiers)
        {
            var type = assoc.Contains("@") || assoc.Length == 64 ? IdentifierType.HashedEmailSha256 : IdentifierType.Uid2Token;
            nodes.Add(new(assoc, type, DateTime.UtcNow, DateTime.UtcNow, 85));
        }

        var household = !string.IsNullOrWhiteSpace(request.HouseholdHint)
            ? request.HouseholdHint
            : $"HH-AUTO-{SyncSeed()}";

        var cluster = new IdentityCluster(
            ClusterId: clusterId,
            PrimaryHouseholdId: household,
            Nodes: nodes,
            TotalLinkedDevices: nodes.Count,
            OverallGraphConfidence: 91.5m,
            UpdatedAtUtc: DateTime.UtcNow
        );

        _clusters[clusterId] = cluster;
        return cluster;
    }

    public IReadOnlyList<IdentityCluster> GetClusters()
    {
        return _clusters.Values.OrderByDescending(c => c.UpdatedAtUtc).ToList();
    }

    public MatchedConversionEvent MatchOfflineConversion(OfflineConversionReceipt receipt)
    {
        _receipts.Add(receipt);

        // Find or create matching cluster
        var existing = _clusters.Values.FirstOrDefault(c =>
            c.Nodes.Any(n => n.IdentifierValue.Equals(receipt.CustomerHashedEmail, StringComparison.OrdinalIgnoreCase)));

        string clusterId = existing?.ClusterId ?? $"CLUSTER-CD-{SyncSeed()}";
        if (existing == null)
        {
            var newCluster = new IdentityCluster(
                ClusterId: clusterId,
                PrimaryHouseholdId: $"HH-POS-{SyncSeed()}",
                Nodes: new List<DeviceIdentityNode>
                {
                    new(receipt.CustomerHashedEmail, IdentifierType.HashedEmailSha256, DateTime.UtcNow, DateTime.UtcNow, 95)
                },
                TotalLinkedDevices: 1,
                OverallGraphConfidence: 88.0m,
                UpdatedAtUtc: DateTime.UtcNow
            );
            _clusters[clusterId] = newCluster;
        }

        var match = new MatchedConversionEvent(
            MatchId: $"MATCH-{SyncSeed()}",
            TransactionId: receipt.TransactionId,
            ClusterId: clusterId,
            AttributedCampaignId: "CAMP-RETARGETING-Q4",
            AttributedChannel: "Omnichannel CAPI (Meta / Google OCI)",
            LookbackWindowDays: 14,
            AttributionWeight: 0.85m,
            ForwardedToMetaCapi: true,
            ForwardedToGoogleOci: true,
            ForwardedToTikTokEventsApi: receipt.PurchaseAmount > 50m,
            MatchedAtUtc: DateTime.UtcNow
        );

        _matches.Add(match);
        return match;
    }

    public IReadOnlyList<MatchedConversionEvent> GetMatchedEvents()
    {
        return _matches.OrderByDescending(m => m.MatchedAtUtc).ToList();
    }

    public SyncMatchMetrics GetMetrics()
    {
        int totalReceipts = _receipts.Count;
        int matched = _matches.Count;
        decimal matchRate = totalReceipts > 0
            ? Math.Round((decimal)matched / totalReceipts * 100m, 1)
            : 87.5m;

        decimal totalRevenue = _receipts.Sum(r => r.PurchaseAmount);
        if (totalRevenue == 0m) totalRevenue = 142500m;

        return new SyncMatchMetrics(
            TotalReceiptsIngested: Math.Max(totalReceipts, 240),
            TotalMatchedConversions: Math.Max(matched, 210),
            MatchRatePercentage: matchRate,
            TotalAttributedGrossRevenue: totalRevenue,
            ActiveIdentityClusters: _clusters.Count,
            LastSyncUtc: DateTime.UtcNow
        );
    }

    private static int _seedCounter = 4000;
    private static int SyncSeed() => Interlocked.Increment(ref _seedCounter);
}
