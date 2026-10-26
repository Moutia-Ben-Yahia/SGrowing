using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Automation;
using SGrowing.Core.Entities;
using SGrowing.Worker.Services;
using Xunit;

namespace SGrowing.Tests.Automation;

public class AdvancedWorkflowTests
{
    private readonly SGrowingDbContext _db;
    private readonly QuietHoursGuardService _guardService;
    private readonly AdvancedWorkflowSchedulerService _schedulerService;

    public AdvancedWorkflowTests()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new SGrowingDbContext(options);
        _guardService = new QuietHoursGuardService(_db);
        _schedulerService = new AdvancedWorkflowSchedulerService(_db);
    }

    [Fact]
    public async Task QuietHours_BlocksNighttimePromotionsAndPermitsUrgentBypass()
    {
        var tenantId = Guid.NewGuid();
        var policy = QuietHoursPolicy.CreateDefault(tenantId); // 21:00 to 08:00
        await _db.QuietHoursPolicies.AddAsync(policy);
        await _db.SaveChangesAsync();

        // 1. Daytime (14:30) -> Allowed
        var afternoon = new DateTime(2026, 10, 26, 14, 30, 0, DateTimeKind.Utc);
        var checkDay = await _guardService.EvaluateDispatchTimeAsync(tenantId, afternoon, isUrgent: false);
        Assert.True(checkDay.CanSendNow);
        Assert.False(checkDay.InQuietHours);

        // 2. Late Night (23:15) Non-urgent -> Blocked
        var lateNight = new DateTime(2026, 10, 26, 23, 15, 0, DateTimeKind.Utc);
        var checkNight = await _guardService.EvaluateDispatchTimeAsync(tenantId, lateNight, isUrgent: false);
        Assert.False(checkNight.CanSendNow);
        Assert.True(checkNight.InQuietHours);
        Assert.NotNull(checkNight.NextAvailableWindowUtc);

        // 3. Late Night (23:15) Urgent Alert -> Bypassed
        var checkUrgent = await _guardService.EvaluateDispatchTimeAsync(tenantId, lateNight, isUrgent: true);
        Assert.True(checkUrgent.CanSendNow);
        Assert.Contains("Bypassed", checkUrgent.Reason);
    }

    [Fact]
    public async Task DeadLetterQueue_EnqueuesFailedStepAndReplaysSuccessfully()
    {
        var tenantId = Guid.NewGuid();
        var wfId = Guid.NewGuid();
        var execId = Guid.NewGuid();

        var dlqItem = await _schedulerService.EnqueueDeadLetterAsync(
            tenantId,
            wfId,
            execId,
            failedNodeId: "node_send_webhook_4",
            failedNodeType: "Action:Webhook",
            inputPayload: "{\"url\":\"https://broken.endpoint.com\"}",
            errorMessage: "Connection timed out after 5000ms");

        Assert.NotNull(dlqItem);
        Assert.False(dlqItem.IsResolved);
        Assert.Equal(0, dlqItem.RetryCount);

        // Replay step
        var replay = await _schedulerService.ReplayDeadLetterStepAsync(tenantId, dlqItem.Id);
        Assert.True(replay.Success);

        var refreshed = await _db.WorkflowDeadLetterLogs.FindAsync(dlqItem.Id);
        Assert.True(refreshed!.IsResolved);
        Assert.Equal(1, refreshed.RetryCount);
        Assert.NotNull(refreshed.ResolvedAtUtc);
    }
}
