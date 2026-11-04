using FluentAssertions;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Outbound;
using SGrowing.Api.Services;
using SGrowing.Core.Entities;
using SGrowing.Core.Tenancy;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace SGrowing.Tests.Outbound;

public class CadenceDispatcherTests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var tenantContext = new TenantContext();
        return new SGrowingDbContext(options, tenantContext);
    }

    [Fact]
    public async Task ProcessDueCadences_ShouldDispatchStepAndAdvanceToNextStep()
    {
        var db = CreateDbContext();
        var outbox = new OutboxService(db);
        var mailboxService = new MailboxService(db);
        var aiSdr = new AiSdrService();
        var tenantId = Guid.NewGuid();

        // 1. Setup Mailbox
        var sender = SenderAccount.Create(tenantId, "sdr@hypergrowth.ai", "SDR Alex", MailboxProvider.GoogleWorkspace, 40);
        db.SenderAccounts.Add(sender);

        // 2. Setup Campaign with 2 Steps
        var campaign = OutboundCampaign.Create(tenantId, "AI SDR Outbound", new List<Guid> { sender.Id });
        db.OutboundCampaigns.Add(campaign);

        var step1 = CadenceStep.Create(tenantId, campaign.Id, 1, 0, OutboundChannel.Email, "Intro to {{company}}", "Hi {{first_name}}, {{icebreaker}}");
        var step2 = CadenceStep.Create(tenantId, campaign.Id, 2, 4, OutboundChannel.Email, "Quick follow up", "Checking in, {{first_name}}.");
        db.CadenceSteps.AddRange(step1, step2);

        // 3. Setup Contact and Enrollment
        var contact = Contact.Create(tenantId, "Tony", "Stark", "tony@starkindustries.com");
        db.Contacts.Add(contact);
        var company = new Company { TenantId = tenantId, Name = "Stark Industries", Industry = "Defense & Tech" };
        db.Companies.Add(company);
        contact.CompanyId = company.Id;

        var enrollment = ProspectEnrollment.Create(tenantId, campaign.Id, contact.Id);
        enrollment.NextRunAtUtc = DateTime.UtcNow.AddMinutes(-5); // due now!
        db.ProspectEnrollments.Add(enrollment);
        await db.SaveChangesAsync();

        // 4. Run Dispatcher
        var dispatcher = new CadenceDispatcherService(db, mailboxService, aiSdr, outbox);
        var processedCount = await dispatcher.ProcessDueCadencesAsync(tenantId);

        processedCount.Should().Be(1);

        // Verify sender count incremented
        sender.SentTodayCount.Should().Be(1);

        // Verify enrollment advanced to step 2 with delay
        enrollment.CurrentStepNumber.Should().Be(2);
        enrollment.NextRunAtUtc.Should().BeAfter(DateTime.UtcNow.AddDays(3));

        // Verify outbox message recorded
        var outboxEvents = await db.OutboxMessages.Where(o => o.TenantId == tenantId).ToListAsync();
        outboxEvents.Should().HaveCount(1);
        outboxEvents[0].EventType.Should().Be("OutboundMessageDispatched");
    }

    [Fact]
    public async Task ProcessDueCadences_WhenCampaignPaused_ShouldSkipEnrollment()
    {
        var db = CreateDbContext();
        var outbox = new OutboxService(db);
        var mailboxService = new MailboxService(db);
        var aiSdr = new AiSdrService();
        var tenantId = Guid.NewGuid();

        var sender = SenderAccount.Create(tenantId, "sales@domain.com", "Sales", MailboxProvider.GoogleWorkspace, 50);
        db.SenderAccounts.Add(sender);

        var campaign = OutboundCampaign.Create(tenantId, "Paused Campaign", new List<Guid> { sender.Id });
        campaign.Status = CampaignStatus.Paused; // Paused!
        db.OutboundCampaigns.Add(campaign);

        var step1 = CadenceStep.Create(tenantId, campaign.Id, 1, 0, OutboundChannel.Email, "Hello", "Body");
        db.CadenceSteps.Add(step1);

        var contact = Contact.Create(tenantId, "Bruce", "Wayne", "bruce@wayne.com");
        db.Contacts.Add(contact);

        var enrollment = ProspectEnrollment.Create(tenantId, campaign.Id, contact.Id);
        enrollment.NextRunAtUtc = DateTime.UtcNow.AddMinutes(-10);
        db.ProspectEnrollments.Add(enrollment);
        await db.SaveChangesAsync();

        var dispatcher = new CadenceDispatcherService(db, mailboxService, aiSdr, outbox);
        var processed = await dispatcher.ProcessDueCadencesAsync(tenantId);

        processed.Should().Be(0);
        sender.SentTodayCount.Should().Be(0);
        enrollment.CurrentStepNumber.Should().Be(1);
    }

    [Fact]
    public async Task ProcessDueCadences_WhenSenderDailyQuotaExceeded_ShouldHaltBatch()
    {
        var db = CreateDbContext();
        var outbox = new OutboxService(db);
        var mailboxService = new MailboxService(db);
        var aiSdr = new AiSdrService();
        var tenantId = Guid.NewGuid();

        // Sender has reached daily limit
        var sender = SenderAccount.Create(tenantId, "sdr@domain.com", "SDR", MailboxProvider.GoogleWorkspace, 10);
        sender.SentTodayCount = 10; // Quota full!
        db.SenderAccounts.Add(sender);

        var campaign = OutboundCampaign.Create(tenantId, "Active Campaign", new List<Guid> { sender.Id });
        db.OutboundCampaigns.Add(campaign);

        var step1 = CadenceStep.Create(tenantId, campaign.Id, 1, 0, OutboundChannel.Email, "Subject", "Body");
        db.CadenceSteps.Add(step1);

        var contact = Contact.Create(tenantId, "Diana", "Prince", "diana@themyscira.gov");
        db.Contacts.Add(contact);

        var enrollment = ProspectEnrollment.Create(tenantId, campaign.Id, contact.Id);
        enrollment.NextRunAtUtc = DateTime.UtcNow.AddMinutes(-5);
        db.ProspectEnrollments.Add(enrollment);
        await db.SaveChangesAsync();

        var dispatcher = new CadenceDispatcherService(db, mailboxService, aiSdr, outbox);
        var processed = await dispatcher.ProcessDueCadencesAsync(tenantId);

        processed.Should().Be(0);
        enrollment.CurrentStepNumber.Should().Be(1);
    }

    [Fact]
    public async Task ProcessDueCadences_WhenLastStepCompleted_ShouldMarkEnrollmentCompleted()
    {
        var db = CreateDbContext();
        var outbox = new OutboxService(db);
        var mailboxService = new MailboxService(db);
        var aiSdr = new AiSdrService();
        var tenantId = Guid.NewGuid();

        var sender = SenderAccount.Create(tenantId, "sdr@domain.com", "SDR", MailboxProvider.GoogleWorkspace, 50);
        db.SenderAccounts.Add(sender);

        // Single-step campaign
        var campaign = OutboundCampaign.Create(tenantId, "Single Step Campaign", new List<Guid> { sender.Id });
        db.OutboundCampaigns.Add(campaign);

        var step1 = CadenceStep.Create(tenantId, campaign.Id, 1, 0, OutboundChannel.Email, "Final Subject", "Final Body");
        db.CadenceSteps.Add(step1);

        var contact = Contact.Create(tenantId, "Clark", "Kent", "clark@dailyplanet.com");
        db.Contacts.Add(contact);

        var enrollment = ProspectEnrollment.Create(tenantId, campaign.Id, contact.Id);
        enrollment.NextRunAtUtc = DateTime.UtcNow.AddMinutes(-1);
        db.ProspectEnrollments.Add(enrollment);
        await db.SaveChangesAsync();

        var dispatcher = new CadenceDispatcherService(db, mailboxService, aiSdr, outbox);
        var processed = await dispatcher.ProcessDueCadencesAsync(tenantId);

        processed.Should().Be(1);
        enrollment.Status.Should().Be(EnrollmentStatus.Completed);
        enrollment.NextRunAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task ProcessDueCadences_WhenContactMissing_ShouldSkipWithoutAdvancing()
    {
        var db = CreateDbContext();
        var outbox = new OutboxService(db);
        var mailboxService = new MailboxService(db);
        var aiSdr = new AiSdrService();
        var tenantId = Guid.NewGuid();

        var sender = SenderAccount.Create(tenantId, "sdr@domain.com", "SDR", MailboxProvider.GoogleWorkspace, 50);
        db.SenderAccounts.Add(sender);

        var campaign = OutboundCampaign.Create(tenantId, "Ghost Campaign", new List<Guid> { sender.Id });
        db.OutboundCampaigns.Add(campaign);

        var step1 = CadenceStep.Create(tenantId, campaign.Id, 1, 0, OutboundChannel.Email, "Sub", "Body");
        db.CadenceSteps.Add(step1);

        // Enrollment references missing non-existent ContactId
        var missingContactId = Guid.NewGuid();
        var enrollment = ProspectEnrollment.Create(tenantId, campaign.Id, missingContactId);
        enrollment.NextRunAtUtc = DateTime.UtcNow.AddMinutes(-5);
        db.ProspectEnrollments.Add(enrollment);
        await db.SaveChangesAsync();

        var dispatcher = new CadenceDispatcherService(db, mailboxService, aiSdr, outbox);
        var processed = await dispatcher.ProcessDueCadencesAsync(tenantId);

        processed.Should().Be(0);
        sender.SentTodayCount.Should().Be(0);
    }

    [Fact]
    public async Task ProcessDueCadences_WhenMultipleEnrollmentsAcrossTenants_ShouldIsolateByTenant()
    {
        var db = CreateDbContext();
        var outbox = new OutboxService(db);
        var mailboxService = new MailboxService(db);
        var aiSdr = new AiSdrService();

        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        // Tenant A Setup
        var senderA = SenderAccount.Create(tenantA, "a@domain.com", "A", MailboxProvider.GoogleWorkspace, 50);
        db.SenderAccounts.Add(senderA);
        var campaignA = OutboundCampaign.Create(tenantA, "Camp A", new List<Guid> { senderA.Id });
        db.OutboundCampaigns.Add(campaignA);
        var stepA = CadenceStep.Create(tenantA, campaignA.Id, 1, 0, OutboundChannel.Email, "Sub A", "Body A");
        db.CadenceSteps.Add(stepA);
        var contactA = Contact.Create(tenantA, "Alice", "Smith", "alice@example.com");
        db.Contacts.Add(contactA);
        var enrollA = ProspectEnrollment.Create(tenantA, campaignA.Id, contactA.Id);
        enrollA.NextRunAtUtc = DateTime.UtcNow.AddMinutes(-2);
        db.ProspectEnrollments.Add(enrollA);

        // Tenant B Setup
        var senderB = SenderAccount.Create(tenantB, "b@domain.com", "B", MailboxProvider.GoogleWorkspace, 50);
        db.SenderAccounts.Add(senderB);
        var campaignB = OutboundCampaign.Create(tenantB, "Camp B", new List<Guid> { senderB.Id });
        db.OutboundCampaigns.Add(campaignB);
        var stepB = CadenceStep.Create(tenantB, campaignB.Id, 1, 0, OutboundChannel.Email, "Sub B", "Body B");
        db.CadenceSteps.Add(stepB);
        var contactB = Contact.Create(tenantB, "Bob", "Jones", "bob@example.com");
        db.Contacts.Add(contactB);
        var enrollB = ProspectEnrollment.Create(tenantB, campaignB.Id, contactB.Id);
        enrollB.NextRunAtUtc = DateTime.UtcNow.AddMinutes(-2);
        db.ProspectEnrollments.Add(enrollB);

        await db.SaveChangesAsync();

        var dispatcher = new CadenceDispatcherService(db, mailboxService, aiSdr, outbox);

        // Process only Tenant A
        var processedA = await dispatcher.ProcessDueCadencesAsync(tenantA);

        processedA.Should().Be(1);
        senderA.SentTodayCount.Should().Be(1);
        senderB.SentTodayCount.Should().Be(0); // Tenant B untainted!
        enrollB.CurrentStepNumber.Should().Be(1);
    }
}
