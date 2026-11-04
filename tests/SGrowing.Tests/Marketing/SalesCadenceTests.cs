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

    [Fact]
    public async Task EnrollLeadAsync_WithNonExistentCadence_ThrowsInvalidOperationException()
    {
        var db = CreateDbContext();
        var engine = new SalesCadenceEngine(db);
        var tenantId = Guid.NewGuid();
        var missingCadenceId = Guid.NewGuid();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.EnrollLeadAsync(tenantId, missingCadenceId, "Lead", "lead@domain.com", "+216000"));

        Assert.Contains("not found", ex.Message);
    }

    [Fact]
    public async Task AddCadenceStepAsync_WithNonExistentCadence_ThrowsInvalidOperationException()
    {
        var db = CreateDbContext();
        var engine = new SalesCadenceEngine(db);
        var missingCadenceId = Guid.NewGuid();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.AddCadenceStepAsync(missingCadenceId, 1, 0, CadenceChannel.Email, "Subject", "Body"));

        Assert.Contains("not found", ex.Message);
    }

    [Fact]
    public async Task CompleteSdrTaskAsync_WithNonExistentTaskId_ThrowsInvalidOperationException()
    {
        var db = CreateDbContext();
        var engine = new SalesCadenceEngine(db);
        var tenantId = Guid.NewGuid();
        var missingTaskId = Guid.NewGuid();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.CompleteSdrTaskAsync(tenantId, missingTaskId, "Outcome"));

        Assert.Contains("not found", ex.Message);
    }

    [Fact]
    public async Task GetPendingSdrTasksAsync_ShouldFilterByTenantId()
    {
        var db = CreateDbContext();
        var engine = new SalesCadenceEngine(db);
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var cadenceA = await engine.CreateCadenceAsync(tenantA, "Cadence A", "Desc", "Audience");
        await engine.AddCadenceStepAsync(cadenceA.Id, 1, 0, CadenceChannel.PhoneCallTask, null, "Call A");
        await engine.EnrollLeadAsync(tenantA, cadenceA.Id, "Lead A", "a@corp.com", "+216111");

        var cadenceB = await engine.CreateCadenceAsync(tenantB, "Cadence B", "Desc", "Audience");
        await engine.AddCadenceStepAsync(cadenceB.Id, 1, 0, CadenceChannel.LinkedInTask, null, "Connect B");
        await engine.EnrollLeadAsync(tenantB, cadenceB.Id, "Lead B", "b@corp.com", "+216222");

        await engine.ProcessDueCadenceStepsAsync(tenantA);
        await engine.ProcessDueCadenceStepsAsync(tenantB);

        var tasksA = await engine.GetPendingSdrTasksAsync(tenantA);
        var tasksB = await engine.GetPendingSdrTasksAsync(tenantB);

        Assert.Single(tasksA);
        Assert.Equal("Lead A", tasksA[0].LeadName);

        Assert.Single(tasksB);
        Assert.Equal("Lead B", tasksB[0].LeadName);
    }

    [Fact]
    public async Task HandleProspectReplyAsync_WhenNoEnrollmentMatches_ReturnsFalse()
    {
        var db = CreateDbContext();
        var engine = new SalesCadenceEngine(db);
        var tenantId = Guid.NewGuid();

        var handled = await engine.HandleProspectReplyAsync(tenantId, "unknown@nobody.com", "Email", "Please unsubscribe me");
        Assert.False(handled);
    }

    [Fact]
    public async Task ProcessDueCadenceStepsAsync_WhenLinkedInTaskChannel_CreatesSdrTaskWithLinkedInType()
    {
        var db = CreateDbContext();
        var engine = new SalesCadenceEngine(db);
        var tenantId = Guid.NewGuid();

        var cadence = await engine.CreateCadenceAsync(tenantId, "Social Selling", "LinkedIn connect", "B2B Executives");
        await engine.AddCadenceStepAsync(cadence.Id, 1, 0, CadenceChannel.LinkedInTask, null, "Connect with CEO on LinkedIn");

        var enrollment = await engine.EnrollLeadAsync(tenantId, cadence.Id, "Yassine Mansour", "yassine@fintech.tn", "+21655667788", "Fintech TN");

        var processed = await engine.ProcessDueCadenceStepsAsync(tenantId);
        Assert.Equal(1, processed);

        var tasks = await engine.GetPendingSdrTasksAsync(tenantId);
        Assert.Single(tasks);
        Assert.Equal("LinkedInTask", tasks[0].TaskType);
        Assert.Equal("Yassine Mansour", tasks[0].LeadName);
        Assert.Equal("Connect with CEO on LinkedIn", tasks[0].Instruction);
    }
}
