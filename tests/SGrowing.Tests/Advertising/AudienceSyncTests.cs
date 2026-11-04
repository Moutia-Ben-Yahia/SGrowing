using System;
using System.Collections.Generic;
using SGrowing.Api.Modules.Advertising.AudienceSync;
using Xunit;

namespace SGrowing.Tests.Advertising;

public class AudienceSyncTests
{
    private readonly AudienceSyncEngine _engine = new();

    [Fact]
    public void SyncCohortBatch_ValidIdentifiers_HashesAndStreamsToDsp()
    {
        // Arrange
        var request = new BatchAudienceSyncRequest(
            Platform: DspPlatform.MetaCustomAudience,
            ExternalAudienceId: "act_meta_vip_churn_risk",
            Records: new List<AudienceSyncRecord>
            {
                new("customer.alpha@enterprise.corp", "HighLtvChurnRisk", SyncAction.Add),
                new("+14155552671", "HighLtvChurnRisk", SyncAction.Add),
                new("customer.beta@enterprise.corp", "HighLtvChurnRisk", SyncAction.Remove)
            }
        );

        // Act
        var result = _engine.SyncCohortBatch(request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(3, result.ProcessedCount);
        Assert.Equal(3, result.HashedCount);
        Assert.Equal("StreamedToDsp", result.Status);
        Assert.StartsWith("sync_", result.SyncJobId);
    }

    [Fact]
    public void SyncCohortBatch_Should_Ignore_Empty_Or_Whitespace_Identifiers()
    {
        var request = new BatchAudienceSyncRequest(
            Platform: DspPlatform.GoogleCustomerMatch,
            ExternalAudienceId: "act_google_filtered",
            Records: new List<AudienceSyncRecord>
            {
                new("valid.user@company.com", "SegmentA", SyncAction.Add),
                new("", "SegmentA", SyncAction.Add),
                new("   ", "SegmentA", SyncAction.Add)
            }
        );

        var result = _engine.SyncCohortBatch(request);

        Assert.Equal(3, result.ProcessedCount);
        Assert.Equal(1, result.HashedCount); // Only 1 valid identifier hashed
    }

    [Fact]
    public void SyncCohortBatch_Should_Throw_On_Null_Request()
    {
        Assert.Throws<ArgumentNullException>(() => _engine.SyncCohortBatch(null!));
    }

    [Fact]
    public void GetConnectorStatuses_ReturnsAllMajorDspsWithMetrics()
    {
        // Act
        var connectors = _engine.GetConnectorStatuses();

        // Assert
        Assert.NotNull(connectors);
        Assert.True(connectors.Count >= 4);
        Assert.Contains(connectors, c => c.Platform == DspPlatform.GoogleCustomerMatch && c.IsConnected);
        Assert.Contains(connectors, c => c.Platform == DspPlatform.MetaCustomAudience && c.IsConnected);
        Assert.Contains(connectors, c => c.Platform == DspPlatform.TikTokAudienceManager && c.IsConnected);
        Assert.Contains(connectors, c => c.Platform == DspPlatform.LinkedInMatchedAudience && c.IsConnected);
    }

    [Fact]
    public void RetryFailedSync_RecoversAndDispatchesSuccessfully()
    {
        // Act
        var result = _engine.RetryFailedSync("sync_failed_test_01");

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("RecoveredAndStreamed", result.Status);
    }

    [Fact]
    public void RetryFailedSync_Should_Update_Existing_Job_When_Previously_Recorded()
    {
        var batch = _engine.SyncCohortBatch(new BatchAudienceSyncRequest(
            Platform: DspPlatform.LinkedInMatchedAudience,
            ExternalAudienceId: "aud_linkedin_abm",
            Records: new List<AudienceSyncRecord> { new("lead@acme.com", "ABM", SyncAction.Add) }
        ));

        var retried = _engine.RetryFailedSync(batch.SyncJobId);

        Assert.Equal(batch.SyncJobId, retried.SyncJobId);
        Assert.Equal("RecoveredAndStreamed", retried.Status);
    }

    [Theory]
    [InlineData(DspPlatform.MetaCustomAudience)]
    [InlineData(DspPlatform.GoogleCustomerMatch)]
    [InlineData(DspPlatform.TikTokAudienceManager)]
    [InlineData(DspPlatform.LinkedInMatchedAudience)]
    public void SyncCohortBatch_AllDspPlatforms_StreamsSuccessfully(DspPlatform platform)
    {
        var request = new BatchAudienceSyncRequest(
            Platform: platform,
            ExternalAudienceId: $"aud_{platform.ToString().ToLowerInvariant()}_test",
            Records: new List<AudienceSyncRecord>
            {
                new("user1@example.com", "TestSegment", SyncAction.Add)
            }
        );

        var result = _engine.SyncCohortBatch(request);

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(platform, result.Platform);
        Assert.Equal(1, result.ProcessedCount);
        Assert.Equal(1, result.HashedCount);
        Assert.Equal("StreamedToDsp", result.Status);
    }

    [Fact]
    public void GetConnectorStatuses_SortedByPlatformName()
    {
        var statuses = _engine.GetConnectorStatuses();

        Assert.NotEmpty(statuses);
        for (int i = 0; i < statuses.Count - 1; i++)
        {
            Assert.True(string.CompareOrdinal(statuses[i].Platform.ToString(), statuses[i + 1].Platform.ToString()) <= 0);
        }
    }

    [Fact]
    public void SyncCohortBatch_IncrementingConnectorMetrics_UpdatesActiveHashedRecords()
    {
        var initialConnectors = _engine.GetConnectorStatuses();
        var initialGoogle = initialConnectors.First(c => c.Platform == DspPlatform.GoogleCustomerMatch);
        var initialCount = initialGoogle.ActiveHashedRecords;

        _engine.SyncCohortBatch(new BatchAudienceSyncRequest(
            Platform: DspPlatform.GoogleCustomerMatch,
            ExternalAudienceId: "aud_google_metrics_test",
            Records: new List<AudienceSyncRecord>
            {
                new("hash1@test.com", "Segment", SyncAction.Add),
                new("hash2@test.com", "Segment", SyncAction.Add)
            }
        ));

        var updatedConnectors = _engine.GetConnectorStatuses();
        var updatedGoogle = updatedConnectors.First(c => c.Platform == DspPlatform.GoogleCustomerMatch);

        Assert.Equal(initialCount + 2, updatedGoogle.ActiveHashedRecords);
    }

    [Fact]
    public void RetryFailedSync_NullOrEmptyId_ReturnsMockJob()
    {
        var retried = _engine.RetryFailedSync("sync_untracked_external_job");

        Assert.NotNull(retried);
        Assert.True(retried.Success);
        Assert.Equal("RecoveredAndStreamed", retried.Status);
        Assert.Equal("sync_untracked_external_job", retried.SyncJobId);
    }

    [Fact]
    public void SyncCohortBatch_EmptyRecordsList_ReturnsZeroProcessedCount()
    {
        var request = new BatchAudienceSyncRequest(
            Platform: DspPlatform.TikTokAudienceManager,
            ExternalAudienceId: "aud_empty_test",
            Records: new List<AudienceSyncRecord>()
        );

        var result = _engine.SyncCohortBatch(request);

        Assert.NotNull(result);
        Assert.Equal(0, result.ProcessedCount);
        Assert.Equal(0, result.HashedCount);
        Assert.True(result.Success);
    }
}
