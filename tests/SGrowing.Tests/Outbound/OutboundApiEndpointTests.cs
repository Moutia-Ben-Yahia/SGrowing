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
        // tenantContext.SetTenant(Guid.NewGuid());
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

    [Fact]
    public async Task CreateCampaign_WithMultipleSenderPools_AssignsAllSenderPools()
    {
        var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var pool1 = Guid.NewGuid();
        var pool2 = Guid.NewGuid();

        var handler = new CreateCampaignHandler(db);
        var request = new CreateCampaignRequest(
            tenantId,
            "Multi-Pool Outbound",
            new List<Guid> { pool1, pool2 },
            new List<CreateCadenceStepDto>
            {
                new(1, 0, OutboundChannel.Email, "Hello", "Content")
            }
        );

        var result = await handler.HandleAsync(request);

        result.IsSuccess.Should().BeTrue();
        var campaign = await db.OutboundCampaigns.FindAsync(result.Value.Id);
        campaign.Should().NotBeNull();
        var poolIds = campaign!.GetSenderPoolIds();
        poolIds.Should().HaveCount(2);
        poolIds.Should().Contain(pool1);
        poolIds.Should().Contain(pool2);
    }

    [Fact]
    public async Task CreateCampaign_WhenNameMissing_ReturnsFailure()
    {
        var db = CreateDbContext();
        var handler = new CreateCampaignHandler(db);
        var request = new CreateCampaignRequest(
            Guid.NewGuid(),
            "  ",
            new List<Guid>(),
            new List<CreateCadenceStepDto> { new(1, 0, OutboundChannel.Email, "S", "B") }
        );

        var result = await handler.HandleAsync(request);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Campaign name is required");
    }

    [Fact]
    public async Task MailboxPoolManagement_FilterByActive_ReturnsOnlyActiveSenders()
    {
        var db = CreateDbContext();
        var tenantId = Guid.NewGuid();

        var activeSender = SenderAccount.Create(tenantId, "active@pool.io", "Active", MailboxProvider.GoogleWorkspace, 40);
        var inactiveSender = SenderAccount.Create(tenantId, "inactive@pool.io", "Inactive", MailboxProvider.GoogleWorkspace, 40);
        inactiveSender.IsActive = false;

        db.SenderAccounts.AddRange(activeSender, inactiveSender);
        await db.SaveChangesAsync();

        var activeList = await db.SenderAccounts.Where(s => s.TenantId == tenantId && s.IsActive).ToListAsync();
        activeList.Should().ContainSingle();
        activeList.First().Email.Should().Be("active@pool.io");
    }

    [Fact]
    public async Task CreateCampaign_WhenStepsEmpty_ReturnsFailure()
    {
        var db = CreateDbContext();
        var handler = new CreateCampaignHandler(db);
        var request = new CreateCampaignRequest(
            Guid.NewGuid(),
            "Empty Steps Campaign",
            new List<Guid>(),
            new List<CreateCadenceStepDto>()
        );

        var result = await handler.HandleAsync(request);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Campaign must contain at least one step");
    }

    [Fact]
    public async Task EnrollProspects_WhenCampaignNotFound_ReturnsFailure()
    {
        var db = CreateDbContext();
        var handler = new EnrollProspectsHandler(db);
        var request = new EnrollProspectsRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new List<Guid> { Guid.NewGuid() }
        );

        var result = await handler.HandleAsync(request);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Campaign not found");
    }

    [Fact]
    public async Task EnrollProspects_WhenAlreadyEnrolled_DeduplicatesAndEnrollsOnlyOnce()
    {
        var db = CreateDbContext();
        var tenantId = Guid.NewGuid();

        var campaign = OutboundCampaign.Create(tenantId, "Dedupe Campaign", new List<Guid>());
        db.OutboundCampaigns.Add(campaign);

        var contact = Contact.Create(tenantId, "Salma", "Masmoudi", "salma@agro.tn");
        db.Contacts.Add(contact);
        await db.SaveChangesAsync();

        var handler = new EnrollProspectsHandler(db);
        var firstEnroll = await handler.HandleAsync(new EnrollProspectsRequest(tenantId, campaign.Id, new List<Guid> { contact.Id }));
        firstEnroll.IsSuccess.Should().BeTrue();
        firstEnroll.Value.EnrolledCount.Should().Be(1);

        // Attempt second enrollment with same contact
        var secondEnroll = await handler.HandleAsync(new EnrollProspectsRequest(tenantId, campaign.Id, new List<Guid> { contact.Id }));
        secondEnroll.IsSuccess.Should().BeTrue();
        secondEnroll.Value.EnrolledCount.Should().Be(0);

        var totalEnrollments = await db.ProspectEnrollments
            .Where(e => e.TenantId == tenantId && e.CampaignId == campaign.Id && e.ContactId == contact.Id)
            .CountAsync();
        totalEnrollments.Should().Be(1);
    }
}
