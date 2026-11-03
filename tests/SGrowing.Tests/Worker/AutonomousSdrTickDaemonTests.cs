using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;
using SGrowing.Worker.Services;
using Xunit;

namespace SGrowing.Tests.Worker;

public class AutonomousSdrTickDaemonTests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public async Task ProcessSdrTicksAsync_WhenNoDueEnrollments_ReturnsZero()
    {
        using var db = CreateDbContext();

        var processed = await AutonomousSdrTickDaemon.ProcessSdrTicksAsync(db);

        processed.Should().Be(0);
    }

    [Fact]
    public async Task ProcessSdrTicksAsync_WhenEnrollmentDueInFuture_DoesNotProcess()
    {
        using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var campaignId = Guid.NewGuid();
        var contactId = Guid.NewGuid();

        var enrollment = ProspectEnrollment.Create(tenantId, campaignId, contactId, "Great interview on TechCrunch");
        enrollment.NextRunAtUtc = DateTime.UtcNow.AddHours(4); // Future

        db.ProspectEnrollments.Add(enrollment);
        await db.SaveChangesAsync();

        var processed = await AutonomousSdrTickDaemon.ProcessSdrTicksAsync(db);

        processed.Should().Be(0);
        enrollment.CurrentStepNumber.Should().Be(1);
        enrollment.Status.Should().Be(EnrollmentStatus.Active);
    }

    [Fact]
    public async Task ProcessSdrTicksAsync_WhenDueEnrollmentStep1_AdvancesToStep2AndSetsFutureNextRun()
    {
        using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var campaignId = Guid.NewGuid();
        var contactId = Guid.NewGuid();

        var enrollment = ProspectEnrollment.Create(tenantId, campaignId, contactId, "Saw your Series B funding announcement");
        enrollment.NextRunAtUtc = DateTime.UtcNow.AddMinutes(-5); // Past, due now
        enrollment.CurrentStepNumber = 1;

        db.ProspectEnrollments.Add(enrollment);
        await db.SaveChangesAsync();

        var processed = await AutonomousSdrTickDaemon.ProcessSdrTicksAsync(db);

        processed.Should().Be(1);

        var updated = await db.ProspectEnrollments.FindAsync(enrollment.Id);
        updated.Should().NotBeNull();
        updated!.CurrentStepNumber.Should().Be(2);
        updated.Status.Should().Be(EnrollmentStatus.Active);
        updated.NextRunAtUtc.Should().BeAfter(DateTime.UtcNow.AddDays(1));
        updated.LastDispatchedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task ProcessSdrTicksAsync_WhenDueEnrollmentStep4_CompletesEnrollment()
    {
        using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var campaignId = Guid.NewGuid();
        var contactId = Guid.NewGuid();

        var enrollment = ProspectEnrollment.Create(tenantId, campaignId, contactId, "Final follow-up before closing file");
        enrollment.NextRunAtUtc = DateTime.UtcNow.AddMinutes(-10);
        enrollment.CurrentStepNumber = 4; // Final step

        db.ProspectEnrollments.Add(enrollment);
        await db.SaveChangesAsync();

        var processed = await AutonomousSdrTickDaemon.ProcessSdrTicksAsync(db);

        processed.Should().Be(1);

        var updated = await db.ProspectEnrollments.FindAsync(enrollment.Id);
        updated.Should().NotBeNull();
        updated!.Status.Should().Be(EnrollmentStatus.Completed);
        updated.NextRunAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task ProcessSdrTicksAsync_WhenEnrollmentNotActive_IgnoresEnrollment()
    {
        using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var campaignId = Guid.NewGuid();

        var repliedEnrollment = ProspectEnrollment.Create(tenantId, campaignId, Guid.NewGuid());
        repliedEnrollment.MarkReplied();
        repliedEnrollment.NextRunAtUtc = DateTime.UtcNow.AddMinutes(-10);

        var unsubscribedEnrollment = ProspectEnrollment.Create(tenantId, campaignId, Guid.NewGuid());
        unsubscribedEnrollment.MarkUnsubscribed();
        unsubscribedEnrollment.NextRunAtUtc = DateTime.UtcNow.AddMinutes(-10);

        var completedEnrollment = ProspectEnrollment.Create(tenantId, campaignId, Guid.NewGuid());
        completedEnrollment.Complete();
        completedEnrollment.NextRunAtUtc = DateTime.UtcNow.AddMinutes(-10);

        db.ProspectEnrollments.AddRange(repliedEnrollment, unsubscribedEnrollment, completedEnrollment);
        await db.SaveChangesAsync();

        var processed = await AutonomousSdrTickDaemon.ProcessSdrTicksAsync(db);

        processed.Should().Be(0);
    }
}
