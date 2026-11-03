using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;
using SGrowing.Worker.Services;
using Xunit;

namespace SGrowing.Tests.Worker;

public class AdvancedWorkflowSchedulerServiceTests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public async Task EnqueueDeadLetterAsync_PersistsDeadLetterLogWithCorrectProperties()
    {
        using var db = CreateDbContext();
        var service = new AdvancedWorkflowSchedulerService(db);
        var tenantId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        var executionId = Guid.NewGuid();

        var log = await service.EnqueueDeadLetterAsync(
            tenantId,
            workflowId,
            executionId,
            "webhook-dispatch-node",
            "HttpAction",
            "{\"url\":\"https://partner.api/webhook\"}",
            "Connection reset by peer"
        );

        log.Should().NotBeNull();
        log.TenantId.Should().Be(tenantId);
        log.WorkflowId.Should().Be(workflowId);
        log.ExecutionId.Should().Be(executionId);
        log.FailedNodeId.Should().Be("webhook-dispatch-node");
        log.FailedNodeType.Should().Be("HttpAction");
        log.ErrorMessage.Should().Be("Connection reset by peer");
        log.IsResolved.Should().BeFalse();
        log.RetryCount.Should().Be(0);

        var persisted = await db.WorkflowDeadLetterLogs.FindAsync(log.Id);
        persisted.Should().NotBeNull();
    }

    [Fact]
    public async Task ReplayDeadLetterStepAsync_WhenNotFound_ReturnsFailedResult()
    {
        using var db = CreateDbContext();
        var service = new AdvancedWorkflowSchedulerService(db);
        var tenantId = Guid.NewGuid();
        var nonExistentId = Guid.NewGuid();

        var result = await service.ReplayDeadLetterStepAsync(tenantId, nonExistentId);

        result.Success.Should().BeFalse();
        result.DeadLetterId.Should().Be(nonExistentId);
        result.Message.Should().Contain("not found");
    }

    [Fact]
    public async Task ReplayDeadLetterStepAsync_WhenFound_IncrementsRetryCountAndMarksResolved()
    {
        using var db = CreateDbContext();
        var service = new AdvancedWorkflowSchedulerService(db);
        var tenantId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        var executionId = Guid.NewGuid();

        var log = await service.EnqueueDeadLetterAsync(
            tenantId,
            workflowId,
            executionId,
            "charge-customer-card",
            "PaymentGatewayAction",
            "{\"amount\":150}",
            "Payment gateway timeout"
        );

        var result = await service.ReplayDeadLetterStepAsync(tenantId, log.Id);

        result.Success.Should().BeTrue();
        result.DeadLetterId.Should().Be(log.Id);
        result.Message.Should().Contain("successfully replayed");

        var updated = await db.WorkflowDeadLetterLogs.FindAsync(log.Id);
        updated!.RetryCount.Should().Be(1);
        updated.IsResolved.Should().BeTrue();
        updated.ResolvedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task ReplayDeadLetterStepAsync_WhenDifferentTenant_ReturnsNotFound()
    {
        using var db = CreateDbContext();
        var service = new AdvancedWorkflowSchedulerService(db);
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var log = await service.EnqueueDeadLetterAsync(
            tenantA,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "notify-slack",
            "SlackAction",
            "{}",
            "Unauthorized"
        );

        var result = await service.ReplayDeadLetterStepAsync(tenantB, log.Id);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not found");
    }
}
