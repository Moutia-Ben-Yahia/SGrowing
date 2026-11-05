using System.Threading.Tasks;
using SGrowing.Api.Common.Streaming;
using SGrowing.Api.Modules.Analytics;
using Xunit;

namespace SGrowing.Tests.Unit;

public class SyntheticTrafficLoadGeneratorTests
{
    [Fact]
    public async Task ExecuteLoadBurstAsync_InjectsAndProjectsEventsWithZeroLoss()
    {
        var mesh = new EventStreamingMeshService();
        var olap = new ClickHouseAnalyticsProjectionService();
        var generator = new SyntheticTrafficLoadGeneratorService();

        int totalEvents = 200;
        int concurrency = 4;

        var result = await generator.ExecuteLoadBurstAsync(mesh, olap, totalEvents, concurrency);

        Assert.Equal(totalEvents, result.TotalEventsInjected);
        Assert.True(result.PartitionsUtilized > 1, $"Expected multiple partitions utilized, got {result.PartitionsUtilized}");
        Assert.True(result.EventsPerSecond > 0);
        Assert.True(result.ZeroMessageLoss, "Expected 0% message loss during high concurrency load burst");
        Assert.True(result.TotalProjectedRevenue > 0);
    }
}
