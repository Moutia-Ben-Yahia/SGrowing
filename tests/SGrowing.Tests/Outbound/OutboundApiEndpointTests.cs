using FluentAssertions;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Outbound;
using SGrowing.Core.Entities;
using SGrowing.Core.Tenancy;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace SGrowing.Tests.Outbound;

public class OutboundApiEndpointTests
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
    public async Task CreateCampaign_And_EnrollProspects_PersistsDataCorrectly()
    {
        var db = CreateDbContext();
        var tenantId = Guid.NewGuid();

        // 1. Create Contacts in DB
        var contact1 = Contact.Create(tenantId, "Alice", "Smith", "alice@fintech.io");
        var contact2 = Contact.Create(tenantId, "Bob", "Jones", "bob@healthtech.com");
        db.Contacts.AddRange(contact1, contact2);
        await db.SaveChangesAsync();

        // 2. Create Campaign Slice
        var createHandler = new CreateCampaignHandler(db);
        var createRequest = new CreateCampaignRequest(
            tenantId,
            "Q4 High-Growth FinTech Outreach",
            new List<Guid> { Guid.NewGuid() },
            new List<CreateCadenceStepDto>
            {
                new(1, 0, OutboundChannel.Email, "AI Hyper-personalized Intro", "Hi {{first_name}}, loved your focus on scaling..."),
                new(2, 3, OutboundChannel.Email, "Follow-up ROI Value Prop", "Hi {{first_name}}, following up on my previous note regarding ROI metrics...")
            }
        );

        var campaignResult = await createHandler.HandleAsync(createRequest);
        campaignResult.IsSuccess.Should().BeTrue();
        campaignResult.Value.Steps.Should().HaveCount(2);

        // 3. Enroll Prospects Slice
        var enrollHandler = new EnrollProspectsHandler(db);
        var enrollRequest = new EnrollProspectsRequest(
            tenantId,
            campaignResult.Value.Id,
            new List<Guid> { contact1.Id, contact2.Id }
        );

        var enrollResult = await enrollHandler.HandleAsync(enrollRequest);
        enrollResult.IsSuccess.Should().BeTrue();
        enrollResult.Value.EnrolledCount.Should().Be(2);

        // 4. Verify Enrollments
        var enrollments = await db.ProspectEnrollments
            .Where(e => e.CampaignId == campaignResult.Value.Id)
            .ToListAsync();

        enrollments.Should().HaveCount(2);
        enrollments.All(e => e.Status == EnrollmentStatus.Active).Should().BeTrue();
        enrollments.All(e => e.CurrentStepNumber == 1).Should().BeTrue();
    }

    [Fact]
    public async Task MailboxPoolManagement_AddsAndQueriesSenderAccounts()
    {
        var db = CreateDbContext();
        var tenantId = Guid.NewGuid();

        var sender = SenderAccount.Create(
            tenantId: tenantId,
            email: "outbound-sdr@trysgrowing.com",
            displayName: "Alex from SGrowing",
            provider: MailboxProvider.GoogleWorkspace,
            targetDailyQuota: 50,
            rampDailyIncrement: 5
        );

        db.SenderAccounts.Add(sender);
        await db.SaveChangesAsync();

        var mailboxes = await db.SenderAccounts.Where(s => s.TenantId == tenantId).ToListAsync();
        mailboxes.Should().ContainSingle();
        mailboxes.First().Email.Should().Be("outbound-sdr@trysgrowing.com");
        mailboxes.First().HealthScore.Should().Be(95);
    }
}
