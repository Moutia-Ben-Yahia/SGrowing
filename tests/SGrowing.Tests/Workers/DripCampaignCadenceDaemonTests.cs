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
}
