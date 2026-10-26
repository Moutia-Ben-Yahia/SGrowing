using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Marketing;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Marketing;

public class SalesCadenceTests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: $"CadenceTestDb_{Guid.NewGuid()}")
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public async Task SalesCadence_Should_Progress_Steps_And_Create_Sdr_Tasks()
    {
        var db = CreateDbContext();
        var engine = new SalesCadenceEngine(db);
        var tenantId = Guid.NewGuid();

        // 1. Create Cadence
        var cadence = await engine.CreateCadenceAsync(tenantId, "B2B SaaS Outbound", "Outbound sequence for CTOs", "Enterprise B2B");
        await engine.AddCadenceStepAsync(cadence.Id, 1, 0, CadenceChannel.Email, "Quick question", "Hey {{name}}, saw your work at {{company}}.");
        await engine.AddCadenceStepAsync(cadence.Id, 2, 2, CadenceChannel.PhoneCallTask, null, "Call prospect regarding cloud migration.");
        await engine.AddCadenceStepAsync(cadence.Id, 3, 3, CadenceChannel.WhatsApp, null, "Sharing our recent case study on WhatsApp.");

        Assert.Equal(3, cadence.StepsCount);

        // 2. Enroll lead
        var enrollment = await engine.EnrollLeadAsync(tenantId, cadence.Id, "Karim Ayari", "karim@techcorp.tn", "+21698765432", "TechCorp TN");
        Assert.Equal(CadenceEnrollmentState.Active, enrollment.State);
        Assert.Equal(1, enrollment.CurrentStepNumber);

        // 3. Process Step 1 (Due immediately)
        var count = await engine.ProcessDueCadenceStepsAsync(tenantId);
        Assert.Equal(1, count);
        Assert.Equal(2, enrollment.CurrentStepNumber); // Advanced to Step 2

        // Fast-forward next run to now and process Step 2 (Phone call task)
        enrollment.NextExecutionAtUtc = DateTime.UtcNow.AddMinutes(-5);
        await db.SaveChangesAsync();

        var count2 = await engine.ProcessDueCadenceStepsAsync(tenantId);
        Assert.Equal(1, count2);

        var sdrTasks = await engine.GetPendingSdrTasksAsync(tenantId);
        Assert.Single(sdrTasks);
        Assert.Equal("PhoneCallTask", sdrTasks[0].TaskType);
        Assert.Equal("Karim Ayari", sdrTasks[0].LeadName);

        // Complete SDR task
        var completed = await engine.CompleteSdrTaskAsync(tenantId, sdrTasks[0].Id, "Spoke with assistant, requested email info");
        Assert.True(completed.IsCompleted);
    }

    [Fact]
    public async Task HandleProspectReply_Should_Halt_Active_Cadence_And_Cancel_Open_Tasks()
    {
        var db = CreateDbContext();
        var engine = new SalesCadenceEngine(db);
        var tenantId = Guid.NewGuid();

        var cadence = await engine.CreateCadenceAsync(tenantId, "Outreach Cadence", "Description", "Audience");
        await engine.AddCadenceStepAsync(cadence.Id, 1, 0, CadenceChannel.PhoneCallTask, null, "Initial discovery call");
        await engine.AddCadenceStepAsync(cadence.Id, 2, 3, CadenceChannel.WhatsApp, null, "Follow-up WhatsApp note");

        var enrollment = await engine.EnrollLeadAsync(tenantId, cadence.Id, "Dorra Jlassi", "dorra@enterprise.tn", "+21622334455", "Enterprise TN");
        await engine.ProcessDueCadenceStepsAsync(tenantId);

        var pendingTasks = await engine.GetPendingSdrTasksAsync(tenantId);
        Assert.Single(pendingTasks);

        // Prospect replies via email -> Stop on reply engine triggers
        var stopped = await engine.HandleProspectReplyAsync(tenantId, "dorra@enterprise.tn", "Email", "Thanks, let's schedule a call on Thursday.");
        Assert.True(stopped);
        Assert.Equal(CadenceEnrollmentState.RepliedAndStopped, enrollment.State);

        // SDR task should be auto-completed
        var tasksAfterReply = await engine.GetPendingSdrTasksAsync(tenantId);
        Assert.Empty(tasksAfterReply);
    }
}
