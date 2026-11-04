using System;
using System.Collections.Generic;
using System.Linq;
using SGrowing.Api.Modules.Advertising.CrossDeviceSync;
using Xunit;

namespace SGrowing.Tests.Advertising;

public class CrossDeviceSyncTests
{
    private readonly CrossDeviceSyncEngine _engine = new();

    [Fact]
    public void SeededClusters_ContainLinkedNodes()
    {
        var clusters = _engine.GetClusters();
        Assert.NotEmpty(clusters);
        var cluster1 = clusters.FirstOrDefault(c => c.ClusterId == "CLUSTER-CD-901");
        Assert.NotNull(cluster1);
        Assert.Equal(3, cluster1.TotalLinkedDevices);
        Assert.True(cluster1.OverallGraphConfidence > 90m);
    }

    [Fact]
    public void MatchOfflineConversion_AttributesToClusterAndForwardsToCapi()
    {
        var receipt = new OfflineConversionReceipt(
            TransactionId: "POS-TX-99881",
            StoreOrPosId: "STORE-NYC-SOHO-01",
            CustomerHashedEmail: "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            CustomerHashedPhone: null,
            PurchaseAmount: 450.00m,
            Currency: "USD",
            PurchasedSkuCodes: new List<string> { "SKU-PREM-01", "SKU-ACC-02" },
            TransactionTimestampUtc: DateTime.UtcNow
        );

        var match = _engine.MatchOfflineConversion(receipt);

        Assert.NotNull(match);
        Assert.Equal("POS-TX-99881", match.TransactionId);
        Assert.Equal("CLUSTER-CD-901", match.ClusterId);
        Assert.True(match.ForwardedToMetaCapi);
        Assert.True(match.ForwardedToGoogleOci);
        Assert.True(match.ForwardedToTikTokEventsApi); // PurchaseAmount > 50
    }

    [Fact]
    public void MatchOfflineConversion_Should_Create_New_Cluster_When_Not_Found()
    {
        var unknownEmailSha = "a1b2c3d4e5f678901234567890abcdef1234567890abcdef1234567890abcdef";
        var receipt = new OfflineConversionReceipt(
            TransactionId: "POS-TX-NEW-01",
            StoreOrPosId: "STORE-TUN-LAC-01",
            CustomerHashedEmail: unknownEmailSha,
            CustomerHashedPhone: null,
            PurchaseAmount: 35.00m, // <= 50m -> TikTok false
            Currency: "TND",
            PurchasedSkuCodes: new List<string> { "SKU-BASIC-01" },
            TransactionTimestampUtc: DateTime.UtcNow
        );

        var match = _engine.MatchOfflineConversion(receipt);

        Assert.NotNull(match);
        Assert.StartsWith("CLUSTER-CD-", match.ClusterId);
        Assert.False(match.ForwardedToTikTokEventsApi); // <= 50m
        Assert.True(match.ForwardedToMetaCapi);
        Assert.True(match.ForwardedToGoogleOci);

        var allClusters = _engine.GetClusters();
        var createdCluster = allClusters.FirstOrDefault(c => c.ClusterId == match.ClusterId);
        Assert.NotNull(createdCluster);
        Assert.Single(createdCluster.Nodes);
        Assert.Equal(unknownEmailSha, createdCluster.Nodes[0].IdentifierValue);
    }

    [Fact]
    public void ResolveIdentityCluster_Should_Resolve_Custom_Household_And_Associated_Identifiers()
    {
        var request = new ResolveIdentityRequest(
            PrimaryIdentifier: "hashed-email-primary-123456789012345678901234567890123456789012",
            PrimaryType: IdentifierType.HashedEmailSha256,
            AssociatedIdentifiers: new List<string>
            {
                "contact@sgrowing.com",
                "uid2-token-active-7766"
            },
            HouseholdHint: "HH-CUSTOM-NORD-01"
        );

        var cluster = _engine.ResolveIdentityCluster(request);

        Assert.NotNull(cluster);
        Assert.Equal("HH-CUSTOM-NORD-01", cluster.PrimaryHouseholdId);
        Assert.Equal(3, cluster.TotalLinkedDevices);
        Assert.Contains(cluster.Nodes, n => n.Type == IdentifierType.HashedEmailSha256);
        Assert.Contains(cluster.Nodes, n => n.Type == IdentifierType.Uid2Token);
    }

    [Fact]
    public void ResolveIdentityCluster_Should_Auto_Generate_Household_When_Hint_Is_Empty()
    {
        var request = new ResolveIdentityRequest(
            PrimaryIdentifier: "gaid-device-mobile-99",
            PrimaryType: IdentifierType.MobileAdIdGaid,
            AssociatedIdentifiers: new List<string>(),
            HouseholdHint: null
        );

        var cluster = _engine.ResolveIdentityCluster(request);

        Assert.NotNull(cluster);
        Assert.StartsWith("HH-AUTO-", cluster.PrimaryHouseholdId);
        Assert.Single(cluster.Nodes);
    }

    [Fact]
    public void GetMatchedEvents_Should_Return_Logged_Matches_Chronologically()
    {
        var initialCount = _engine.GetMatchedEvents().Count;

        var receipt = new OfflineConversionReceipt(
            TransactionId: $"POS-TX-CHRONO-{Guid.NewGuid():N}",
            StoreOrPosId: "STORE-01",
            CustomerHashedEmail: "test-email-hash-chrono",
            CustomerHashedPhone: null,
            PurchaseAmount: 120.00m,
            Currency: "USD",
            PurchasedSkuCodes: new List<string> { "SKU-01" },
            TransactionTimestampUtc: DateTime.UtcNow
        );

        _engine.MatchOfflineConversion(receipt);

        var events = _engine.GetMatchedEvents();
        Assert.Equal(initialCount + 1, events.Count);
    }

    [Fact]
    public void GetMetrics_CalculatesMatchRateAndAttributedRevenue()
    {
        var metrics = _engine.GetMetrics();

        Assert.NotNull(metrics);
        Assert.True(metrics.TotalReceiptsIngested > 0);
        Assert.True(metrics.TotalMatchedConversions > 0);
        Assert.True(metrics.MatchRatePercentage >= 50m);
        Assert.True(metrics.TotalAttributedGrossRevenue > 0m);
    }
}
