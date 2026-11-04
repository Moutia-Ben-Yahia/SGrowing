using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;
using SGrowing.Worker.Services;
using Xunit;

namespace SGrowing.Tests.Workers;

public class DripCampaignCadenceDaemonTests
{
    private SGrowingDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new SGrowingDbContext(options);
    }

    [Fact]
    public async Task ProcessDueCadencesAsync_AdvancesLeadAndCreatesSdrTask()
    {
        // Arrange
        using var db = CreateContext();
        var tenantId = Guid.NewGuid();
        var cadenceId = Guid.NewGuid();

        var step1 = new SalesCadenceStepItem
        {
            Id = Guid.NewGuid(),
            CadenceId = cadenceId,
            StepNumber = 1,
            DayOffset = 0,
            Channel = CadenceChannel.Email,
            BodyTemplate = "Welcome to Inspire! Here is your quickstart guide."
        };

        var step2 = new SalesCadenceStepItem
        {
            Id = Guid.NewGuid(),
            CadenceId = cadenceId,
            StepNumber = 2,
            DayOffset = 3,
            Channel = CadenceChannel.PhoneCallTask,
            BodyTemplate = "Call prospect to discuss customized multi-channel setup."
        };

        db.SalesCadenceStepItems.AddRange(step1, step2);

        var enrollment = new SalesCadenceLeadEnrollment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CadenceId = cadenceId,
            LeadName = "Yassine Mansour",
            Email = "yassine@tunisiamail.tn",
            PhoneNumber = "+21622334455",
            CurrentStepNumber = 1,
            State = CadenceEnrollmentState.Active,
            NextExecutionAtUtc = DateTime.UtcNow.AddMinutes(-5) // Due now
        };
        db.SalesCadenceLeadEnrollments.Add(enrollment);

        await db.SaveChangesAsync();

        // Act
        var processed = await DripCampaignCadenceDaemon.ProcessDueCadencesAsync(db);

        // Assert
        Assert.True(processed >= 1);

        var updatedEnrollment = await db.SalesCadenceLeadEnrollments.FindAsync(enrollment.Id);
        Assert.NotNull(updatedEnrollment);
        Assert.Equal(2, updatedEnrollment.CurrentStepNumber);
        Assert.True(updatedEnrollment.NextExecutionAtUtc > DateTime.UtcNow);

        var task = await db.SdrTaskItems.FirstOrDefaultAsync(t => t.EnrollmentId == enrollment.Id);
        Assert.NotNull(task);
        Assert.Equal("PhoneCall", task.TaskType);
        Assert.Equal("Call prospect to discuss customized multi-channel setup.", task.Instruction);
    }

    [Fact]
    public async Task ProcessDueCadencesAsync_CompletesEnrollmentWhenNoMoreSteps()
    {
        // Arrange
        using var db = CreateContext();
        var tenantId = Guid.NewGuid();
        var cadenceId = Guid.NewGuid();

        var enrollment = new SalesCadenceLeadEnrollment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CadenceId = cadenceId,
            LeadName = "Amina Bouazizi",
            Email = "amina@algertech.dz",
            PhoneNumber = "+213550123456",
            CurrentStepNumber = 5, // No step 6 exists
            State = CadenceEnrollmentState.Active,
            NextExecutionAtUtc = DateTime.UtcNow.AddMinutes(-5)
        };
        db.SalesCadenceLeadEnrollments.Add(enrollment);

        await db.SaveChangesAsync();

        // Act
        var processed = await DripCampaignCadenceDaemon.ProcessDueCadencesAsync(db);

        // Assert
        Assert.Equal(1, processed);

        var updatedEnrollment = await db.SalesCadenceLeadEnrollments.FindAsync(enrollment.Id);
        Assert.NotNull(updatedEnrollment);
        Assert.Equal(CadenceEnrollmentState.Completed, updatedEnrollment.State);
    }

    [Fact]
    public async Task ProcessDueCadencesAsync_IgnoresPausedOrRepliedEnrollments()
    {
        using var db = CreateContext();
        var tenantId = Guid.NewGuid();
        var cadenceId = Guid.NewGuid();

        var pausedEnrollment = new SalesCadenceLeadEnrollment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CadenceId = cadenceId,
            LeadName = "Paused Lead",
            Email = "paused@example.com",
            PhoneNumber = "+21611111111",
            CurrentStepNumber = 1,
            State = CadenceEnrollmentState.Paused,
            NextExecutionAtUtc = DateTime.UtcNow.AddMinutes(-10) // Due, but paused
        };

        var repliedEnrollment = new SalesCadenceLeadEnrollment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CadenceId = cadenceId,
            LeadName = "Replied Lead",
            Email = "replied@example.com",
            PhoneNumber = "+21622222222",
            CurrentStepNumber = 1,
            State = CadenceEnrollmentState.RepliedAndStopped,
            NextExecutionAtUtc = DateTime.UtcNow.AddMinutes(-10) // Due, but replied
        };

        db.SalesCadenceLeadEnrollments.AddRange(pausedEnrollment, repliedEnrollment);
        await db.SaveChangesAsync();

        var processed = await DripCampaignCadenceDaemon.ProcessDueCadencesAsync(db);
        Assert.Equal(0, processed);

        var refreshedPaused = await db.SalesCadenceLeadEnrollments.FindAsync(pausedEnrollment.Id);
        Assert.Equal(CadenceEnrollmentState.Paused, refreshedPaused!.State);
        Assert.Equal(1, refreshedPaused.CurrentStepNumber);
    }

    [Fact]
    public async Task ProcessDueCadencesAsync_IgnoresEnrollmentsNotYetDue()
    {
        using var db = CreateContext();
        var tenantId = Guid.NewGuid();
        var cadenceId = Guid.NewGuid();

        var futureEnrollment = new SalesCadenceLeadEnrollment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CadenceId = cadenceId,
            LeadName = "Future Lead",
            Email = "future@example.com",
            PhoneNumber = "+21633333333",
            CurrentStepNumber = 1,
            State = CadenceEnrollmentState.Active,
            NextExecutionAtUtc = DateTime.UtcNow.AddHours(2) // 2 hours in future
        };

        db.SalesCadenceLeadEnrollments.Add(futureEnrollment);
        await db.SaveChangesAsync();

        var processed = await DripCampaignCadenceDaemon.ProcessDueCadencesAsync(db);
        Assert.Equal(0, processed);

        var refreshed = await db.SalesCadenceLeadEnrollments.FindAsync(futureEnrollment.Id);
        Assert.Equal(1, refreshed!.CurrentStepNumber);
    }

    [Fact]
    public async Task ProcessDueCadencesAsync_ProcessesMailboxWarmupCadences_IncrementsVolumeAndAddsLog()
    {
        using var db = CreateContext();
        var tenantId = Guid.NewGuid();

        var warmup = new MailboxWarmupCadence
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SenderEmail = "outreach@sgrowing.io",
            CurrentDailyVolume = 2,
            DailyTargetLimit = 10,
            TotalSimulatedSent = 15,
            IsActive = true
        };

        db.MailboxWarmupCadences.Add(warmup);
        await db.SaveChangesAsync();

        var processed = await DripCampaignCadenceDaemon.ProcessDueCadencesAsync(db);
        Assert.Equal(1, processed);

        var refreshed = await db.MailboxWarmupCadences.FindAsync(warmup.Id);
        Assert.NotNull(refreshed);
        Assert.Equal(3, refreshed.CurrentDailyVolume);
        Assert.Equal(16, refreshed.TotalSimulatedSent);

        var log = await db.WarmupExchangeLogs.FirstOrDefaultAsync(l => l.WarmupCadenceId == warmup.Id);
        Assert.NotNull(log);
        Assert.Equal("Sent", log.Direction);
        Assert.Equal(tenantId, log.TenantId);
    }

    [Fact]
    public async Task ProcessDueCadencesAsync_MailboxWarmup_SkipsWhenDailyLimitReachedOrInactive()
    {
        using var db = CreateContext();
        var tenantId = Guid.NewGuid();

        var maxedWarmup = new MailboxWarmupCadence
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SenderEmail = "maxed@sgrowing.io",
            CurrentDailyVolume = 20,
            DailyTargetLimit = 20, // Reached limit
            IsActive = true
        };

        var inactiveWarmup = new MailboxWarmupCadence
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SenderEmail = "inactive@sgrowing.io",
            CurrentDailyVolume = 5,
            DailyTargetLimit = 20,
            IsActive = false // Inactive
        };

        db.MailboxWarmupCadences.AddRange(maxedWarmup, inactiveWarmup);
        await db.SaveChangesAsync();

        var processed = await DripCampaignCadenceDaemon.ProcessDueCadencesAsync(db);
        Assert.Equal(0, processed);

        var refreshedMaxed = await db.MailboxWarmupCadences.FindAsync(maxedWarmup.Id);
        Assert.Equal(20, refreshedMaxed!.CurrentDailyVolume);
    }
}
