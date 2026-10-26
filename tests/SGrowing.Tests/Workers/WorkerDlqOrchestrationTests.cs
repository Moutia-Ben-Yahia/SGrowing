using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;
using SGrowing.Worker.Services;
using Xunit;

namespace SGrowing.Tests.Workers;

public class WorkerDlqOrchestrationTests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: $"WorkerDlqTestDb_{Guid.NewGuid()}")
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public async Task Worker_DeadLetterQueue_ExponentialBackoff_And_Replay_Should_Succeed()
    {
        var db = CreateDbContext();
        var tenantId = Guid.NewGuid();

        // 1. Validate Exponential Backoff Cadences
        Assert.Equal(TimeSpan.FromSeconds(10), ResilientDlqDaemon.GetBackoffInterval(1));
        Assert.Equal(TimeSpan.FromMinutes(1), ResilientDlqDaemon.GetBackoffInterval(2));
        Assert.Equal(TimeSpan.FromMinutes(5), ResilientDlqDaemon.GetBackoffInterval(3));
        Assert.Equal(TimeSpan.FromMinutes(30), ResilientDlqDaemon.GetBackoffInterval(4));
        Assert.Equal(TimeSpan.FromHours(2), ResilientDlqDaemon.GetBackoffInterval(5));

        // 2. Enqueue Failed Workflow Node to Dead-Letter Queue (e.g. Courier Webhook Network Timeout)
        var scheduler = new AdvancedWorkflowSchedulerService(db);
        var deadLog = await scheduler.EnqueueDeadLetterAsync(
            tenantId,
            workflowId: Guid.NewGuid(),
            executionId: Guid.NewGuid(),
            failedNodeId: "node-http-courier",
            failedNodeType: "HttpWebhookPost",
            inputPayload: "{\"tracking\":\"YAL-1029\"}",
            errorMessage: "Connection timed out after 30000ms");

        Assert.NotNull(deadLog);
        Assert.False(deadLog.IsResolved);
        Assert.Equal(0, deadLog.RetryCount);

        // 3. Simulate elapsed time and process eligible retries
        deadLog.FailedAtUtc = DateTime.UtcNow.AddMinutes(-5); // 5 min ago
        await db.SaveChangesAsync();

        int processed = await ResilientDlqDaemon.ProcessEligibleRetriesAsync(db);
        Assert.Equal(1, processed);
        Assert.Equal(1, deadLog.RetryCount);

        // 4. Manually Replay and resolve Dead Letter
        var replayResult = await scheduler.ReplayDeadLetterStepAsync(tenantId, deadLog.Id);
        Assert.True(replayResult.Success);
        Assert.True(deadLog.IsResolved);
        Assert.NotNull(deadLog.ResolvedAtUtc);
    }
}
