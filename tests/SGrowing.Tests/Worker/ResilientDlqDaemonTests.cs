using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;
using SGrowing.Worker.Services;
using Xunit;

namespace SGrowing.Tests.Worker;

public class ResilientDlqDaemonTests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public void GetBackoffInterval_CalculatesExponentialBackoffProgression()
    {
        ResilientDlqDaemon.GetBackoffInterval(1).Should().Be(TimeSpan.FromSeconds(10));
        ResilientDlqDaemon.GetBackoffInterval(2).Should().Be(TimeSpan.FromMinutes(1));
        ResilientDlqDaemon.GetBackoffInterval(3).Should().Be(TimeSpan.FromMinutes(5));
        ResilientDlqDaemon.GetBackoffInterval(4).Should().Be(TimeSpan.FromMinutes(30));
        ResilientDlqDaemon.GetBackoffInterval(5).Should().Be(TimeSpan.FromHours(2));
        ResilientDlqDaemon.GetBackoffInterval(10).Should().Be(TimeSpan.FromHours(2));
    }

    [Fact]
    public async Task ProcessEligibleRetriesAsync_WhenNoDeadLetters_ReturnsZero()
    {
        using var db = CreateDbContext();

        var processed = await ResilientDlqDaemon.ProcessEligibleRetriesAsync(db);

        processed.Should().Be(0);
    }

    [Fact]
    public async Task ProcessEligibleRetriesAsync_WhenBackoffTimePassed_IncrementsRetryCount()
    {
        using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();

        var log = WorkflowDeadLetterLog.Create(
            tenantId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "webhook-dispatch-node",
            "HttpAction",
            "{}",
            "504 Gateway Timeout"
        );
        log.RetryCount = 0;
        // Backoff for retry 1 is 10s. Set FailedAtUtc to 20 seconds ago.
        log.FailedAtUtc = DateTime.UtcNow.AddSeconds(-20);
        log.IsResolved = false;

        db.WorkflowDeadLetterLogs.Add(log);
        await db.SaveChangesAsync();

        var processed = await ResilientDlqDaemon.ProcessEligibleRetriesAsync(db);

        processed.Should().Be(1);

        var updated = await db.WorkflowDeadLetterLogs.FindAsync(log.Id);
        updated.Should().NotBeNull();
        updated!.RetryCount.Should().Be(1);
    }

    [Fact]
    public async Task ProcessEligibleRetriesAsync_WhenBackoffTimeNotYetPassed_DoesNotRetry()
    {
        using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();

        var log = WorkflowDeadLetterLog.Create(
            tenantId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "webhook-dispatch-node",
            "HttpAction",
            "{}",
            "504 Gateway Timeout"
        );
        log.RetryCount = 1;
        // Backoff for retry 2 is 1 minute. Set FailedAtUtc to 5 seconds ago (not yet passed).
        log.FailedAtUtc = DateTime.UtcNow.AddSeconds(-5);
        log.IsResolved = false;

        db.WorkflowDeadLetterLogs.Add(log);
        await db.SaveChangesAsync();

        var processed = await ResilientDlqDaemon.ProcessEligibleRetriesAsync(db);

        processed.Should().Be(0);

        var updated = await db.WorkflowDeadLetterLogs.FindAsync(log.Id);
        updated!.RetryCount.Should().Be(1);
    }

    [Fact]
    public async Task ProcessEligibleRetriesAsync_WhenRetryCountAtLimit5_ExcludesFromRetry()
    {
        using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();

        var log = WorkflowDeadLetterLog.Create(
            tenantId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "exhausted-node",
            "HttpAction",
            "{}",
            "Fatal Connection Error"
        );
        log.RetryCount = 5;
        log.FailedAtUtc = DateTime.UtcNow.AddDays(-1);
        log.IsResolved = false;

        db.WorkflowDeadLetterLogs.Add(log);
        await db.SaveChangesAsync();

        var processed = await ResilientDlqDaemon.ProcessEligibleRetriesAsync(db);

        processed.Should().Be(0);
    }

    [Fact]
    public async Task ProcessEligibleRetriesAsync_WhenAlreadyResolved_ExcludesFromRetry()
    {
        using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();

        var log = WorkflowDeadLetterLog.Create(
            tenantId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "resolved-node",
            "HttpAction",
            "{}",
            "Transient Socket Failure"
        );
        log.RetryCount = 1;
        log.FailedAtUtc = DateTime.UtcNow.AddHours(-1);
        log.IsResolved = true;
        log.ResolvedAtUtc = DateTime.UtcNow.AddMinutes(-30);

        db.WorkflowDeadLetterLogs.Add(log);
        await db.SaveChangesAsync();

        var processed = await ResilientDlqDaemon.ProcessEligibleRetriesAsync(db);

        processed.Should().Be(0);
    }

    [Fact]
    public async Task ProcessEligibleRetriesAsync_MultipleEligibleLogs_RetriesAllAndIncrementsCounters()
    {
        using var db = CreateDbContext();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var log1 = WorkflowDeadLetterLog.Create(tenantA, Guid.NewGuid(), Guid.NewGuid(), "node-1", "HttpAction", "{}", "Timeout");
        log1.RetryCount = 0;
        log1.FailedAtUtc = DateTime.UtcNow.AddMinutes(-5);

        var log2 = WorkflowDeadLetterLog.Create(tenantB, Guid.NewGuid(), Guid.NewGuid(), "node-2", "EmailAction", "{}", "500 Internal Error");
        log2.RetryCount = 1;
        log2.FailedAtUtc = DateTime.UtcNow.AddMinutes(-10);

        db.WorkflowDeadLetterLogs.AddRange(log1, log2);
        await db.SaveChangesAsync();

        var processed = await ResilientDlqDaemon.ProcessEligibleRetriesAsync(db);

        processed.Should().Be(2);

        var updated1 = await db.WorkflowDeadLetterLogs.FindAsync(log1.Id);
        updated1!.RetryCount.Should().Be(1);

        var updated2 = await db.WorkflowDeadLetterLogs.FindAsync(log2.Id);
        updated2!.RetryCount.Should().Be(2);
    }

    [Fact]
    public async Task ProcessEligibleRetriesAsync_WhenCancellationTokenCancelled_ThrowsOperationCanceledException()
    {
        using var db = CreateDbContext();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await ResilientDlqDaemon.ProcessEligibleRetriesAsync(db, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
