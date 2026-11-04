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

    [Fact]
    public async Task QuietHours_WhenPolicyDisabledOrMissing_PermitsDispatchAtAnyTime()
    {
        var tenantId = Guid.NewGuid();

        // 1. Missing policy entirely
        var lateNight = new DateTime(2026, 11, 4, 2, 30, 0, DateTimeKind.Utc);
        var resMissing = await _guardService.EvaluateDispatchTimeAsync(tenantId, lateNight, isUrgent: false);
        Assert.True(resMissing.CanSendNow);
        Assert.False(resMissing.InQuietHours);
        Assert.Equal("Quiet hours disabled.", resMissing.Reason);

        // 2. Disabled policy in database
        var policy = QuietHoursPolicy.CreateDefault(tenantId);
        policy.IsEnabled = false;
        await _db.QuietHoursPolicies.AddAsync(policy);
        await _db.SaveChangesAsync();

        var resDisabled = await _guardService.EvaluateDispatchTimeAsync(tenantId, lateNight, isUrgent: false);
        Assert.True(resDisabled.CanSendNow);
        Assert.False(resDisabled.InQuietHours);
        Assert.Equal("Quiet hours disabled.", resDisabled.Reason);
    }

    [Fact]
    public async Task QuietHours_DaytimeWindow_NonOvernightSchedule_EvaluatesCorrectly()
    {
        var tenantId = Guid.NewGuid();
        var policy = new QuietHoursPolicy
        {
            TenantId = tenantId,
            IsEnabled = true,
            StartHourLocal = 12, // 12:00 PM
            EndHourLocal = 14,   // 2:00 PM (lunch quiet period)
            AllowBypassForUrgent = false
        };
        await _db.QuietHoursPolicies.AddAsync(policy);
        await _db.SaveChangesAsync();

        // 13:00 is inside [12, 14) -> Blocked
        var lunchTime = new DateTime(2026, 11, 4, 13, 0, 0, DateTimeKind.Utc);
        var resBlocked = await _guardService.EvaluateDispatchTimeAsync(tenantId, lunchTime, isUrgent: true);
        Assert.False(resBlocked.CanSendNow);
        Assert.True(resBlocked.InQuietHours);

        // 15:00 is outside [12, 14) -> Allowed
        var afternoon = new DateTime(2026, 11, 4, 15, 0, 0, DateTimeKind.Utc);
        var resAllowed = await _guardService.EvaluateDispatchTimeAsync(tenantId, afternoon, isUrgent: false);
        Assert.True(resAllowed.CanSendNow);
        Assert.False(resAllowed.InQuietHours);
    }

    [Fact]
    public async Task QuietHours_WhenUrgentBypassDisallowed_BlocksUrgentMessagesDuringQuietHours()
    {
        var tenantId = Guid.NewGuid();
        var policy = QuietHoursPolicy.CreateDefault(tenantId);
        policy.AllowBypassForUrgent = false; // Strictly no urgent bypass
        await _db.QuietHoursPolicies.AddAsync(policy);
        await _db.SaveChangesAsync();

        var midnight = new DateTime(2026, 11, 4, 0, 30, 0, DateTimeKind.Utc);
        var result = await _guardService.EvaluateDispatchTimeAsync(tenantId, midnight, isUrgent: true);

        Assert.False(result.CanSendNow);
        Assert.True(result.InQuietHours);
        Assert.Contains("Blocked: recipient local time", result.Reason);
        Assert.NotNull(result.NextAvailableWindowUtc);
    }

    [Fact]
    public async Task DeadLetterQueue_ReplayNotFound_ReturnsFailureResult()
    {
        var tenantId = Guid.NewGuid();
        var nonExistentId = Guid.NewGuid();

        var replay = await _schedulerService.ReplayDeadLetterStepAsync(tenantId, nonExistentId);

        Assert.False(replay.Success);
        Assert.Equal(nonExistentId, replay.DeadLetterId);
        Assert.Equal("Dead letter item not found.", replay.Message);
    }

    [Fact]
    public async Task DeadLetterQueue_TenantIsolation_CannotReplayOtherTenantsItem()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var item = await _schedulerService.EnqueueDeadLetterAsync(
            tenantA,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "node_1",
            "Action:Http",
            "{}",
            "Unauthorized");

        var replayAttempt = await _schedulerService.ReplayDeadLetterStepAsync(tenantB, item.Id);
        Assert.False(replayAttempt.Success);
        Assert.Equal("Dead letter item not found.", replayAttempt.Message);

        var refreshed = await _db.WorkflowDeadLetterLogs.FindAsync(item.Id);
        Assert.False(refreshed!.IsResolved);
        Assert.Equal(0, refreshed.RetryCount);
    }
}
