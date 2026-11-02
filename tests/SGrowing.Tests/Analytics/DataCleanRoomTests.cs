using SGrowing.Api.Modules.Analytics.DataCleanRoom;
using Xunit;

namespace SGrowing.Tests.Analytics;

public sealed class DataCleanRoomTests
{
    private readonly DataCleanRoomEngine _engine = new();

    [Fact]
    public async Task GetParticipantsAndDatasets_ReturnsValidPartiesAndCohorts()
    {
        var participants = await _engine.GetParticipantsAsync();
        var datasets = await _engine.GetDatasetsAsync();

        Assert.NotNull(participants);
        Assert.NotEmpty(participants);
        Assert.Contains(participants, p => p.Role == "Advertiser");
        Assert.Contains(participants, p => p.Role == "Publisher");

        Assert.NotNull(datasets);
        Assert.Contains(datasets, d => d.DatasetId == "ds_acme_high_ltv");
    }

    [Fact]
    public async Task ExecuteOverlapMeasurement_CalculatesPrivateSetIntersectionWithNoise()
    {
        var request = new RunOverlapQueryRequest(
            AdvertiserDatasetId: "ds_acme_high_ltv",
            PublisherDatasetId: "ds_pub_tech_readers",
            DifferentialPrivacyEpsilon: 0.5
        );

        var result = await _engine.ExecuteOverlapMeasurementAsync(request);

        Assert.NotNull(result);
        Assert.StartsWith("query_zk_", result.QueryId);
        Assert.True(result.RawOverlapMatchCount > 0);
        Assert.True(result.PrivacyNoisedOverlapCount > 0);
        Assert.True(result.MatchRatePercent > 20.0);
        Assert.True(result.PrivacyPreservingRoas >= 1.0);
        Assert.Equal(0.5, result.EpsilonCost);
    }

    [Fact]
    public async Task GetSummary_ReturnsAggregatedPrivacyBudget()
    {
        await _engine.ExecuteOverlapMeasurementAsync(new RunOverlapQueryRequest("ds_acme_high_ltv", "ds_pub_tech_readers", 0.5));
        var summary = await _engine.GetSummaryAsync();

        Assert.NotNull(summary);
        Assert.True(summary.TotalParticipants >= 2);
        Assert.True(summary.TotalHashedIdentities > 0);
        Assert.True(summary.TotalQueriesExecuted >= 1);
        Assert.True(summary.TotalEpsilonConsumed >= 0.5);
    }
}
