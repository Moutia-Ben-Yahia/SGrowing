using FluentAssertions;
using MarketingStudio.Api.Data;
using MarketingStudio.Api.Modules.Outbound;
using MarketingStudio.Api.Services;
using MarketingStudio.Core.Entities;
using MarketingStudio.Core.Tenancy;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MarketingStudio.Tests.Outbound;

public class CadenceDispatcherTests
{
    private MarketingStudioDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MarketingStudioDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(Guid.NewGuid());
        return new MarketingStudioDbContext(options, tenantContext);
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
}
