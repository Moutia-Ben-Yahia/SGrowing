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
        // tenantContext.SetTenant(Guid.NewGuid());
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

    [Fact]
    public async Task IngestEvent_WhenJourneyNotActive_DoesNotEnrollContact()
    {
        var db = CreateDbContext();
        var outbox = new OutboxService(db);
        var service = new JourneyExecutionService(db, outbox);
        var tenantId = Guid.NewGuid();

        var contact = Contact.Create(tenantId, "Marco", "Polo", "marco@silkroad.cn");
        db.Contacts.Add(contact);

        var pausedJourney = CustomerJourney.Create(tenantId, "Paused Campaign", "lead.generated");
        pausedJourney.Status = JourneyStatus.Paused;
        db.CustomerJourneys.Add(pausedJourney);
        await db.SaveChangesAsync();

        var res = await service.IngestEventAsync(tenantId, contact.Id, "lead.generated");

        res.IsSuccess.Should().BeTrue();
        var enrollments = await db.JourneyEnrollments.Where(e => e.ContactId == contact.Id).ToListAsync();
        enrollments.Should().BeEmpty();
    }

    [Fact]
    public async Task IngestEvent_AvoidsDuplicateActiveEnrollment_WhenContactAlreadyEnrolled()
    {
        var db = CreateDbContext();
        var outbox = new OutboxService(db);
        var service = new JourneyExecutionService(db, outbox);
        var tenantId = Guid.NewGuid();

        var contact = Contact.Create(tenantId, "Galileo", "Galilei", "galileo@pisa.it");
        db.Contacts.Add(contact);

        var journey = CustomerJourney.Create(tenantId, "Astro Newsletter", "telescope.ordered");
        db.CustomerJourneys.Add(journey);
        await db.SaveChangesAsync();

        // Fire event twice
        await service.IngestEventAsync(tenantId, contact.Id, "telescope.ordered");
        await service.IngestEventAsync(tenantId, contact.Id, "telescope.ordered");

        var enrollments = await db.JourneyEnrollments.Where(e => e.ContactId == contact.Id).ToListAsync();
        enrollments.Should().HaveCount(1);
    }

    [Fact]
    public async Task ProcessDueSteps_WithWhatsAppStep_DispatchesWhatsAppOutboxEvent()
    {
        var db = CreateDbContext();
        var outbox = new OutboxService(db);
        var service = new JourneyExecutionService(db, outbox);
        var tenantId = Guid.NewGuid();

        var contact = Contact.Create(tenantId, "Dante", "Alighieri", "dante@florence.it", phone: "+39055123456");
        db.Contacts.Add(contact);

        var journey = CustomerJourney.Create(tenantId, "WhatsApp Welcome", "user.registered");
        db.CustomerJourneys.Add(journey);
        await db.SaveChangesAsync();

        var step = JourneyStep.Create(tenantId, journey.Id, 1, JourneyStepType.SendWhatsApp, "Ciao {{first_name}}, benvenuto!");
        db.JourneySteps.Add(step);

        var enrollment = JourneyEnrollment.Create(tenantId, journey.Id, contact.Id);
        db.JourneyEnrollments.Add(enrollment);
        await db.SaveChangesAsync();

        var processed = await service.ProcessDueStepsAsync(tenantId);
        processed.Should().Be(1);

        var outboxMessages = await db.OutboxMessages.ToListAsync();
        outboxMessages.Should().ContainSingle();
        outboxMessages.First().EventType.Should().Be("Journey.WhatsApp.Send");
        outboxMessages.First().Payload.Should().Contain("Ciao Dante");
    }

    [Fact]
    public async Task ProcessDueSteps_WhenMultiStepJourney_AdvancesCurrentStepOrderAndDelaysNextExecution()
    {
        var db = CreateDbContext();
        var outbox = new OutboxService(db);
        var service = new JourneyExecutionService(db, outbox);
        var tenantId = Guid.NewGuid();

        var contact = Contact.Create(tenantId, "Niccolo", "Machiavelli", "niccolo@prince.it");
        db.Contacts.Add(contact);

        var journey = CustomerJourney.Create(tenantId, "Multi Step Sequence", "plan.activated");
        db.CustomerJourneys.Add(journey);
        await db.SaveChangesAsync();

        var step1 = JourneyStep.Create(tenantId, journey.Id, 1, JourneyStepType.SendEmail, "Step 1 Content", delaySeconds: 7200);
        var step2 = JourneyStep.Create(tenantId, journey.Id, 2, JourneyStepType.SendEmail, "Step 2 Content", delaySeconds: 3600);
        db.JourneySteps.AddRange(step1, step2);

        var enrollment = JourneyEnrollment.Create(tenantId, journey.Id, contact.Id);
        db.JourneyEnrollments.Add(enrollment);
        await db.SaveChangesAsync();

        // Process step 1
        var processed = await service.ProcessDueStepsAsync(tenantId);
        processed.Should().Be(1);

        var updatedEnrollment = await db.JourneyEnrollments.FindAsync(enrollment.Id);
        updatedEnrollment!.CurrentStepOrder.Should().Be(2);
        updatedEnrollment.Status.Should().Be(JourneyEnrollmentStatus.Active);
        updatedEnrollment.NextExecutionAtUtc.Should().BeAfter(DateTime.UtcNow.AddMinutes(60));
    }
}
