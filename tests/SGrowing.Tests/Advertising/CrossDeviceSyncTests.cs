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
        Assert.True(match.ForwardedToTikTokEventsApi);
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
