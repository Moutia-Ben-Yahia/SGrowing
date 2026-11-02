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
    public void GetConnectorStatuses_ReturnsAllMajorDspsWithMetrics()
    {
        // Act
        var connectors = _engine.GetConnectorStatuses();

        // Assert
        Assert.NotNull(connectors);
        Assert.True(connectors.Count >= 4);
        Assert.Contains(connectors, c => c.Platform == DspPlatform.GoogleCustomerMatch && c.IsConnected);
        Assert.Contains(connectors, c => c.Platform == DspPlatform.MetaCustomAudience && c.IsConnected);
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
}
