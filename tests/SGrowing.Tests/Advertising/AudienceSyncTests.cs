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
}
