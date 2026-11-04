using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;
using SGrowing.Worker.Services;
using Xunit;

namespace SGrowing.Tests.Worker;

public class DripCampaignCadenceDaemonTests
{
    private static (IServiceProvider Provider, SGrowingDbContext Db) SetupDependencies()
    {
        var services = new ServiceCollection();
        var dbName = $"DripCadenceTests_{Guid.NewGuid()}";
        services.AddDbContext<SGrowingDbContext>(options =>
            options.UseInMemoryDatabase(dbName));

        var sp = services.BuildServiceProvider();
        var db = sp.GetRequiredService<SGrowingDbContext>();
        return (sp, db);
    }

    [Fact]
    public async Task ProcessDueCadencesAsync_WhenNoDueItems_ReturnsZero()
    {
        var (_, db) = SetupDependencies();
        using var scope = db;
        var tenantId = Guid.NewGuid();
        var cadenceId = Guid.NewGuid();

        // Enrollment scheduled for tomorrow (not due)
        db.SalesCadenceLeadEnrollments.Add(new SalesCadenceLeadEnrollment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CadenceId = cadenceId,
            LeadName = "Future Prospect",
            Email = "future@example.com",
            CurrentStepNumber = 1,
            State = CadenceEnrollmentState.Active,
            NextExecutionAtUtc = DateTime.UtcNow.AddDays(1)
        });
        await db.SaveChangesAsync();

        var processed = await DripCampaignCadenceDaemon.ProcessDueCadencesAsync(db);

        processed.Should().Be(0);
        (await db.SdrTaskItems.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ProcessDueCadencesAsync_WhenLeadHasNextEmailStep_CreatesEmailTaskAndAdvancesStep()
    {
        var (_, db) = SetupDependencies();
        using var scope = db;
        var tenantId = Guid.NewGuid();
        var cadenceId = Guid.NewGuid();

        // Step 2 is an email
        db.SalesCadenceStepItems.Add(new SalesCadenceStepItem
        {
            Id = Guid.NewGuid(),
            CadenceId = cadenceId,
            StepNumber = 2,
            DayOffset = 2,
            Channel = CadenceChannel.Email,
            SubjectTemplate = "Quick follow up",
            BodyTemplate = "Hi Sarah, checking in on our discussion."
        });

        var enrollmentId = Guid.NewGuid();
        db.SalesCadenceLeadEnrollments.Add(new SalesCadenceLeadEnrollment
        {
            Id = enrollmentId,
            TenantId = tenantId,
            CadenceId = cadenceId,
            LeadName = "Sarah Connor",
            Email = "sarah@cyberdyne.corp",
            PhoneNumber = "+1555019283",
            Company = "Cyberdyne Systems",
            CurrentStepNumber = 1,
            State = CadenceEnrollmentState.Active,
            NextExecutionAtUtc = DateTime.UtcNow.AddMinutes(-5) // due now
        });
        await db.SaveChangesAsync();

        var processed = await DripCampaignCadenceDaemon.ProcessDueCadencesAsync(db);

        processed.Should().Be(1);

        var task = await db.SdrTaskItems.FirstOrDefaultAsync(t => t.EnrollmentId == enrollmentId);
        task.Should().NotBeNull();
        task!.TaskType.Should().Be("EmailDispatch");
        task.Instruction.Should().Be("Hi Sarah, checking in on our discussion.");
        task.LeadName.Should().Be("Sarah Connor");
        task.Email.Should().Be("sarah@cyberdyne.corp");

        var updatedEnrollment = await db.SalesCadenceLeadEnrollments.FirstAsync(e => e.Id == enrollmentId);
        updatedEnrollment.CurrentStepNumber.Should().Be(2);
        updatedEnrollment.NextExecutionAtUtc.Should().BeAfter(DateTime.UtcNow);
        updatedEnrollment.LastExecutedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task ProcessDueCadencesAsync_WhenLeadHasNextPhoneStep_CreatesPhoneCallTask()
    {
        var (_, db) = SetupDependencies();
        using var scope = db;
        var tenantId = Guid.NewGuid();
        var cadenceId = Guid.NewGuid();

        // Step 2 is a phone call
        db.SalesCadenceStepItems.Add(new SalesCadenceStepItem
        {
            Id = Guid.NewGuid(),
            CadenceId = cadenceId,
            StepNumber = 2,
            DayOffset = 1,
            Channel = CadenceChannel.PhoneCallTask,
            BodyTemplate = "Cold outreach discovery call."
        });

        var enrollmentId = Guid.NewGuid();
        db.SalesCadenceLeadEnrollments.Add(new SalesCadenceLeadEnrollment
        {
            Id = enrollmentId,
            TenantId = tenantId,
            CadenceId = cadenceId,
            LeadName = "John Matrix",
            Email = "jmatrix@commando.net",
            PhoneNumber = "+1555987654",
            CurrentStepNumber = 1,
            State = CadenceEnrollmentState.Active,
            NextExecutionAtUtc = DateTime.UtcNow.AddMinutes(-10)
        });
        await db.SaveChangesAsync();

        var processed = await DripCampaignCadenceDaemon.ProcessDueCadencesAsync(db);

        processed.Should().Be(1);

        var task = await db.SdrTaskItems.FirstOrDefaultAsync(t => t.EnrollmentId == enrollmentId);
        task.Should().NotBeNull();
        task!.TaskType.Should().Be("PhoneCall");
        task.Instruction.Should().Be("Cold outreach discovery call.");
    }

    [Fact]
    public async Task ProcessDueCadencesAsync_WhenNoFurtherSteps_MarksEnrollmentCompleted()
    {
        var (_, db) = SetupDependencies();
        using var scope = db;
        var tenantId = Guid.NewGuid();
        var cadenceId = Guid.NewGuid();

        var enrollmentId = Guid.NewGuid();
        // Currently at step 3, but no step 4 exists in DB
        db.SalesCadenceLeadEnrollments.Add(new SalesCadenceLeadEnrollment
        {
            Id = enrollmentId,
            TenantId = tenantId,
            CadenceId = cadenceId,
            LeadName = "Kyle Reese",
            Email = "kreese@resistance.org",
            CurrentStepNumber = 3,
            State = CadenceEnrollmentState.Active,
            NextExecutionAtUtc = DateTime.UtcNow.AddMinutes(-1)
        });
        await db.SaveChangesAsync();

        var processed = await DripCampaignCadenceDaemon.ProcessDueCadencesAsync(db);

        processed.Should().Be(1);

        var updated = await db.SalesCadenceLeadEnrollments.FirstAsync(e => e.Id == enrollmentId);
        updated.State.Should().Be(CadenceEnrollmentState.Completed);
        updated.LastExecutedAtUtc.Should().NotBeNull();
        (await db.SdrTaskItems.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ProcessDueCadencesAsync_WhenWarmupActiveBelowLimit_IncrementsVolumeAndLogsExchange()
    {
        var (_, db) = SetupDependencies();
        using var scope = db;
        var tenantId = Guid.NewGuid();

        var warmup = MailboxWarmupCadence.Create(tenantId, "sales@sgrowing.io", dailyTarget: 30);
        warmup.CurrentDailyVolume = 5;
        warmup.TotalSimulatedSent = 10;
        db.MailboxWarmupCadences.Add(warmup);
        await db.SaveChangesAsync();

        var processed = await DripCampaignCadenceDaemon.ProcessDueCadencesAsync(db);

        processed.Should().Be(1);

        var updated = await db.MailboxWarmupCadences.FirstAsync(w => w.Id == warmup.Id);
        updated.CurrentDailyVolume.Should().Be(6);
        updated.TotalSimulatedSent.Should().Be(11);

        var exchange = await db.WarmupExchangeLogs.FirstOrDefaultAsync(l => l.WarmupCadenceId == warmup.Id);
        exchange.Should().NotBeNull();
        exchange!.Direction.Should().Be("Sent");
        exchange.PeerInboxEmail.Should().Contain("@warmbly.internal");
    }

    [Fact]
    public async Task ProcessDueCadencesAsync_WhenWarmupReachesLimit_DoesNotIncrement()
    {
        var (_, db) = SetupDependencies();
        using var scope = db;
        var tenantId = Guid.NewGuid();

        var warmup = MailboxWarmupCadence.Create(tenantId, "quota@sgrowing.io", dailyTarget: 15);
        warmup.CurrentDailyVolume = 15; // At limit
        warmup.TotalSimulatedSent = 50;
        db.MailboxWarmupCadences.Add(warmup);
        await db.SaveChangesAsync();

        var processed = await DripCampaignCadenceDaemon.ProcessDueCadencesAsync(db);

        processed.Should().Be(0);

        var updated = await db.MailboxWarmupCadences.FirstAsync(w => w.Id == warmup.Id);
        updated.CurrentDailyVolume.Should().Be(15);
        updated.TotalSimulatedSent.Should().Be(50);
        (await db.WarmupExchangeLogs.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ProcessDueCadencesAsync_WhenEnrollmentStatePaused_IgnoresEnrollment()
    {
        var (_, db) = SetupDependencies();
        using var scope = db;
        var tenantId = Guid.NewGuid();
        var cadenceId = Guid.NewGuid();

        db.SalesCadenceStepItems.Add(new SalesCadenceStepItem
        {
            Id = Guid.NewGuid(),
            CadenceId = cadenceId,
            StepNumber = 2,
            DayOffset = 1,
            Channel = CadenceChannel.Email,
            BodyTemplate = "Checking in"
        });

        var pausedEnrollment = new SalesCadenceLeadEnrollment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CadenceId = cadenceId,
            LeadName = "Paused Lead",
            Email = "paused@example.com",
            CurrentStepNumber = 1,
            State = CadenceEnrollmentState.Paused, // Paused
            NextExecutionAtUtc = DateTime.UtcNow.AddMinutes(-10)
        };
        db.SalesCadenceLeadEnrollments.Add(pausedEnrollment);
        await db.SaveChangesAsync();

        var processed = await DripCampaignCadenceDaemon.ProcessDueCadencesAsync(db);

        processed.Should().Be(0);
        (await db.SdrTaskItems.CountAsync()).Should().Be(0);

        var inDb = await db.SalesCadenceLeadEnrollments.FindAsync(pausedEnrollment.Id);
        inDb!.CurrentStepNumber.Should().Be(1);
    }

    [Fact]
    public async Task ProcessDueCadencesAsync_WhenNextStepHasZeroDayOffset_EnforcesMinimumOneDayOffset()
    {
        var (_, db) = SetupDependencies();
        using var scope = db;
        var tenantId = Guid.NewGuid();
        var cadenceId = Guid.NewGuid();

        // Step with DayOffset = 0
        db.SalesCadenceStepItems.Add(new SalesCadenceStepItem
        {
            Id = Guid.NewGuid(),
            CadenceId = cadenceId,
            StepNumber = 2,
            DayOffset = 0,
            Channel = CadenceChannel.Email,
            BodyTemplate = "Instant follow up"
        });

        var enrollment = new SalesCadenceLeadEnrollment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CadenceId = cadenceId,
            LeadName = "Offset Lead",
            Email = "offset@example.com",
            CurrentStepNumber = 1,
            State = CadenceEnrollmentState.Active,
            NextExecutionAtUtc = DateTime.UtcNow.AddMinutes(-5)
        };
        db.SalesCadenceLeadEnrollments.Add(enrollment);
        await db.SaveChangesAsync();

        var processed = await DripCampaignCadenceDaemon.ProcessDueCadencesAsync(db);

        processed.Should().Be(1);

        var inDb = await db.SalesCadenceLeadEnrollments.FindAsync(enrollment.Id);
        inDb!.CurrentStepNumber.Should().Be(2);
        // Math.Max(1, nextStep.DayOffset) enforces minimum 1 day offset
        inDb.NextExecutionAtUtc.Should().BeAfter(DateTime.UtcNow.AddHours(23));
    }
}
