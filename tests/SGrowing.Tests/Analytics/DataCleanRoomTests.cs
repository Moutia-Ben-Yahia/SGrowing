using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
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

    [Fact]
    public async Task ExecuteOverlapMeasurement_NullRequest_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _engine.ExecuteOverlapMeasurementAsync(null!));
    }

    [Fact]
    public async Task ExecuteOverlapMeasurement_MissingAdvertiserDataset_ThrowsKeyNotFoundException()
    {
        var request = new RunOverlapQueryRequest(
            AdvertiserDatasetId: "non_existent_adv_ds",
            PublisherDatasetId: "ds_pub_tech_readers",
            DifferentialPrivacyEpsilon: 0.5
        );

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _engine.ExecuteOverlapMeasurementAsync(request));
    }

    [Fact]
    public async Task ExecuteOverlapMeasurement_MissingPublisherDataset_ThrowsKeyNotFoundException()
    {
        var request = new RunOverlapQueryRequest(
            AdvertiserDatasetId: "ds_acme_high_ltv",
            PublisherDatasetId: "non_existent_pub_ds",
            DifferentialPrivacyEpsilon: 0.5
        );

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _engine.ExecuteOverlapMeasurementAsync(request));
    }

    [Fact]
    public async Task ExecuteOverlapMeasurement_LowerEpsilon_InjectsHigherDifferentialPrivacyNoise()
    {
        // Low epsilon (0.1) -> higher privacy -> higher noise
        var lowEpsRequest = new RunOverlapQueryRequest("ds_acme_high_ltv", "ds_pub_tech_readers", 0.1);
        var lowEpsResult = await _engine.ExecuteOverlapMeasurementAsync(lowEpsRequest);

        // High epsilon (1.0) -> lower privacy -> lower noise
        var highEpsRequest = new RunOverlapQueryRequest("ds_acme_high_ltv", "ds_pub_tech_readers", 1.0);
        var highEpsResult = await _engine.ExecuteOverlapMeasurementAsync(highEpsRequest);

        var lowEpsNoise = lowEpsResult.PrivacyNoisedOverlapCount - lowEpsResult.RawOverlapMatchCount;
        var highEpsNoise = highEpsResult.PrivacyNoisedOverlapCount - highEpsResult.RawOverlapMatchCount;

        lowEpsNoise.Should().BeGreaterThan(highEpsNoise);
    }

    [Fact]
    public async Task GetDatasets_ReturnsDatasetsSortedByHashedIdentityCountDescending()
    {
        var datasets = await _engine.GetDatasetsAsync();

        datasets.Count.Should().BeGreaterThanOrEqualTo(2);
        for (var i = 0; i < datasets.Count - 1; i++)
        {
            datasets[i].HashedIdentityCount.Should().BeGreaterThanOrEqualTo(datasets[i + 1].HashedIdentityCount);
        }
    }
}
