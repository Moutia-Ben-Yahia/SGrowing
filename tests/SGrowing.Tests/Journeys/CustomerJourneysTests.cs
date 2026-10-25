using FluentAssertions;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Journeys;
using SGrowing.Api.Services;
using SGrowing.Core.Entities;
using SGrowing.Core.Tenancy;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace SGrowing.Tests.Journeys;

public class CustomerJourneysTests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(Guid.NewGuid());
        return new SGrowingDbContext(options, tenantContext);
    }

    [Fact]
    public async Task IngestEvent_TriggersActiveJourney_AndEnrollsContact()
    {
        var db = CreateDbContext();
        var outbox = new OutboxService(db);
        var service = new JourneyExecutionService(db, outbox);
        var tenantId = Guid.NewGuid();

        var contact = Contact.Create(tenantId, "Sophia", "Loren", "sophia@cinema.it");
        db.Contacts.Add(contact);

        var journey = CustomerJourney.Create(tenantId, "Onboarding Journey", "user.signed_up");
        db.CustomerJourneys.Add(journey);
        await db.SaveChangesAsync();

        var step1 = JourneyStep.Create(tenantId, journey.Id, 1, JourneyStepType.SendEmail, "Welcome {{first_name}}!", "Welcome aboard");
        var step2 = JourneyStep.Create(tenantId, journey.Id, 2, JourneyStepType.SendWhatsApp, "Hi {{first_name}}, did you try our AI inbox?", delaySeconds: 86400);
        db.JourneySteps.AddRange(step1, step2);
        await db.SaveChangesAsync();

        // Act: Ingest user.signed_up event
        var res = await service.IngestEventAsync(tenantId, contact.Id, "user.signed_up", "{\"plan\": \"pro\"}");

        // Assert
        res.IsSuccess.Should().BeTrue();
        var enrollments = await db.JourneyEnrollments.Where(e => e.ContactId == contact.Id).ToListAsync();
        enrollments.Should().ContainSingle();
        enrollments.First().JourneyId.Should().Be(journey.Id);
        enrollments.First().CurrentStepOrder.Should().Be(1);
        enrollments.First().Status.Should().Be(JourneyEnrollmentStatus.Active);
    }

    [Fact]
    public async Task ProcessDueSteps_ExecutesStep_EnqueuesOutbox_AndAdvancesNextStep()
    {
        var db = CreateDbContext();
        var outbox = new OutboxService(db);
        var service = new JourneyExecutionService(db, outbox);
        var tenantId = Guid.NewGuid();

        var contact = Contact.Create(tenantId, "Leonardo", "DaVinci", "leo@renaissance.org");
        db.Contacts.Add(contact);

        var journey = CustomerJourney.Create(tenantId, "Trial Retention", "trial.expiring");
        db.CustomerJourneys.Add(journey);
        await db.SaveChangesAsync();

        var step1 = JourneyStep.Create(tenantId, journey.Id, 1, JourneyStepType.SendEmail, "Hello {{first_name}}, your trial ends soon.", "Trial Ending", delaySeconds: 3600);
        db.JourneySteps.Add(step1);

        var enrollment = JourneyEnrollment.Create(tenantId, journey.Id, contact.Id);
        db.JourneyEnrollments.Add(enrollment);
        await db.SaveChangesAsync();

        // Process step
        var processed = await service.ProcessDueStepsAsync(tenantId);
        processed.Should().Be(1);

        // Verify outbox
        var outboxMessages = await db.OutboxMessages.ToListAsync();
        outboxMessages.Should().ContainSingle();
        outboxMessages.First().EventType.Should().Be("Journey.Email.Send");
        outboxMessages.First().Payload.Should().Contain("Leonardo");

        // Verify enrollment is marked completed because there was no next step
        var updatedEnrollment = await db.JourneyEnrollments.FindAsync(enrollment.Id);
        updatedEnrollment!.Status.Should().Be(JourneyEnrollmentStatus.Completed);
    }
}
