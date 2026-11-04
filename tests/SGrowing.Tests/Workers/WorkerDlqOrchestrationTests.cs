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

    [Theory]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(10)]
    [InlineData(100)]
    public void ResilientDlqDaemon_GetBackoffInterval_ForHighRetryCounts_CapsAtTwoHours(int retryCount)
    {
        var backoff = ResilientDlqDaemon.GetBackoffInterval(retryCount);
        Assert.Equal(TimeSpan.FromHours(2), backoff);
    }

    [Fact]
    public async Task ResilientDlqDaemon_ProcessEligibleRetriesAsync_IgnoresLogsAtMaxRetryCount()
    {
        var db = CreateDbContext();
        var tenantId = Guid.NewGuid();

        var exhaustedLog = new WorkflowDeadLetterLog
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WorkflowId = Guid.NewGuid(),
            ExecutionId = Guid.NewGuid(),
            FailedNodeId = "node-exhausted",
            FailedNodeType = "HttpRequest",
            InputPayload = "{}",
            ErrorMessage = "Persistent failure",
            RetryCount = 5, // Reached max retries
            IsResolved = false,
            FailedAtUtc = DateTime.UtcNow.AddDays(-1)
        };

        db.WorkflowDeadLetterLogs.Add(exhaustedLog);
        await db.SaveChangesAsync();

        int processed = await ResilientDlqDaemon.ProcessEligibleRetriesAsync(db);
        Assert.Equal(0, processed);

        var refreshed = await db.WorkflowDeadLetterLogs.FindAsync(exhaustedLog.Id);
        Assert.Equal(5, refreshed!.RetryCount);
    }

    [Fact]
    public async Task ResilientDlqDaemon_ProcessEligibleRetriesAsync_IgnoresLogsNotYetEligible()
    {
        var db = CreateDbContext();
        var tenantId = Guid.NewGuid();

        var recentLog = new WorkflowDeadLetterLog
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WorkflowId = Guid.NewGuid(),
            ExecutionId = Guid.NewGuid(),
            FailedNodeId = "node-recent",
            FailedNodeType = "HttpRequest",
            InputPayload = "{}",
            ErrorMessage = "Temporary glitch",
            RetryCount = 0,
            IsResolved = false,
            FailedAtUtc = DateTime.UtcNow.AddSeconds(-2) // Only 2s ago, backoff for retry 1 is 10s
        };

        db.WorkflowDeadLetterLogs.Add(recentLog);
        await db.SaveChangesAsync();

        int processed = await ResilientDlqDaemon.ProcessEligibleRetriesAsync(db);
        Assert.Equal(0, processed);

        var refreshed = await db.WorkflowDeadLetterLogs.FindAsync(recentLog.Id);
        Assert.Equal(0, refreshed!.RetryCount);
    }

    [Fact]
    public async Task ResilientDlqDaemon_ProcessEligibleRetriesAsync_IgnoresAlreadyResolvedLogs()
    {
        var db = CreateDbContext();
        var tenantId = Guid.NewGuid();

        var resolvedLog = new WorkflowDeadLetterLog
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WorkflowId = Guid.NewGuid(),
            ExecutionId = Guid.NewGuid(),
            FailedNodeId = "node-resolved",
            FailedNodeType = "HttpRequest",
            InputPayload = "{}",
            ErrorMessage = "Fixed glitch",
            RetryCount = 1,
            IsResolved = true,
            ResolvedAtUtc = DateTime.UtcNow.AddHours(-1),
            FailedAtUtc = DateTime.UtcNow.AddHours(-2)
        };

        db.WorkflowDeadLetterLogs.Add(resolvedLog);
        await db.SaveChangesAsync();

        int processed = await ResilientDlqDaemon.ProcessEligibleRetriesAsync(db);
        Assert.Equal(0, processed);

        var refreshed = await db.WorkflowDeadLetterLogs.FindAsync(resolvedLog.Id);
        Assert.Equal(1, refreshed!.RetryCount);
    }

    [Fact]
    public async Task AdvancedWorkflowSchedulerService_EnqueueDeadLetterAsync_MultipleTenants_IsolatesCorrectly()
    {
        var db = CreateDbContext();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var scheduler = new AdvancedWorkflowSchedulerService(db);

        var logA = await scheduler.EnqueueDeadLetterAsync(
            tenantA,
            workflowId: Guid.NewGuid(),
            executionId: Guid.NewGuid(),
            failedNodeId: "node-a",
            failedNodeType: "HttpWebhookPost",
            inputPayload: "{\"tenant\":\"A\"}",
            errorMessage: "Error Tenant A"
        );

        var logB = await scheduler.EnqueueDeadLetterAsync(
            tenantB,
            workflowId: Guid.NewGuid(),
            executionId: Guid.NewGuid(),
            failedNodeId: "node-b",
            failedNodeType: "HttpWebhookPost",
            inputPayload: "{\"tenant\":\"B\"}",
            errorMessage: "Error Tenant B"
        );

        Assert.Equal(tenantA, logA.TenantId);
        Assert.Equal(tenantB, logB.TenantId);

        var tenantALogs = await db.WorkflowDeadLetterLogs.Where(l => l.TenantId == tenantA).ToListAsync();
        var tenantBLogs = await db.WorkflowDeadLetterLogs.Where(l => l.TenantId == tenantB).ToListAsync();

        Assert.Single(tenantALogs);
        Assert.Single(tenantBLogs);
        Assert.Equal("Error Tenant A", tenantALogs[0].ErrorMessage);
        Assert.Equal("Error Tenant B", tenantBLogs[0].ErrorMessage);
    }

    [Fact]
    public async Task ReplayDeadLetterStepAsync_WhenNotFound_ReturnsFailure()
    {
        var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var scheduler = new AdvancedWorkflowSchedulerService(db);

        var result = await scheduler.ReplayDeadLetterStepAsync(tenantId, Guid.NewGuid());

        Assert.False(result.Success);
        Assert.Equal("Dead letter item not found.", result.Message);
    }

    [Fact]
    public async Task ReplayDeadLetterStepAsync_WhenCrossTenantAccess_ReturnsFailureAndPreservesState()
    {
        var db = CreateDbContext();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var scheduler = new AdvancedWorkflowSchedulerService(db);

        var logA = await scheduler.EnqueueDeadLetterAsync(
            tenantA,
            workflowId: Guid.NewGuid(),
            executionId: Guid.NewGuid(),
            failedNodeId: "node-cross-tenant",
            failedNodeType: "HttpWebhookPost",
            inputPayload: "{}",
            errorMessage: "Cross tenant isolation test"
        );

        // Tenant B attempts to replay Tenant A's dead letter
        var result = await scheduler.ReplayDeadLetterStepAsync(tenantB, logA.Id);

        Assert.False(result.Success);
        Assert.Equal("Dead letter item not found.", result.Message);

        var refreshed = await db.WorkflowDeadLetterLogs.FindAsync(logA.Id);
        Assert.NotNull(refreshed);
        Assert.False(refreshed!.IsResolved);
        Assert.Equal(0, refreshed.RetryCount);
    }
}
