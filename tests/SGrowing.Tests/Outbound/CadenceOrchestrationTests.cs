using FluentAssertions;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Outbound;
using SGrowing.Core.Entities;
using SGrowing.Core.Tenancy;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace SGrowing.Tests.Outbound;

public class CadenceOrchestrationTests
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
    public async Task CreateCampaign_WithSteps_ShouldPersistCorrectly()
    {
        var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var handler = new CreateCampaignHandler(db);

        var request = new CreateCampaignRequest(
            TenantId: tenantId,
            Name: "Enterprise SaaS Outbound Q4",
            SenderPoolIds: new List<Guid> { Guid.NewGuid() },
            Steps: new List<CreateCadenceStepDto>
            {
                new(StepNumber: 1, DelayDays: 0, Channel: OutboundChannel.Email, SubjectTemplate: "Quick question regarding {{company}}", BodyTemplate: "Hi {{first_name}}, {{icebreaker}}"),
                new(StepNumber: 2, DelayDays: 3, Channel: OutboundChannel.Email, SubjectTemplate: "Following up on my note", BodyTemplate: "Hi {{first_name}}, wanted to make sure you saw my previous note.")
            }
        );

        var result = await handler.HandleAsync(request);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Enterprise SaaS Outbound Q4");
        result.Value.Steps.Should().HaveCount(2);

        var stepsInDb = await db.CadenceSteps.Where(s => s.CampaignId == result.Value.Id).ToListAsync();
        stepsInDb.Should().HaveCount(2);
        stepsInDb[1].DelayDays.Should().Be(3);
    }

    [Fact]
    public async Task EnrollProspects_ShouldInitializeEnrollmentWithStep1DueNow()
    {
        var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var campaign = OutboundCampaign.Create(tenantId, "Q4 Push", new List<Guid> { Guid.NewGuid() });
        db.OutboundCampaigns.Add(campaign);
        var contact = Contact.Create(tenantId, "Bruce", "Wayne", "bruce@waynecorp.com");
        db.Contacts.Add(contact);
        await db.SaveChangesAsync();

        var handler = new EnrollProspectsHandler(db);
        var result = await handler.HandleAsync(new EnrollProspectsRequest(tenantId, campaign.Id, new List<Guid> { contact.Id }));

        result.IsSuccess.Should().BeTrue();
        result.Value.EnrolledCount.Should().Be(1);

        var enrollment = await db.ProspectEnrollments.FirstOrDefaultAsync(e => e.ContactId == contact.Id);
        enrollment.Should().NotBeNull();
        enrollment!.CurrentStepNumber.Should().Be(1);
        enrollment.Status.Should().Be(EnrollmentStatus.Active);
        enrollment.NextRunAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateCampaign_WithoutTenantId_ReturnsFailure()
    {
        var db = CreateDbContext();
        var handler = new CreateCampaignHandler(db);

        var request = new CreateCampaignRequest(
            TenantId: Guid.Empty,
            Name: "No Tenant",
            SenderPoolIds: new List<Guid>(),
            Steps: new List<CreateCadenceStepDto>
            {
                new(1, 0, OutboundChannel.Email, "Subject", "Body")
            }
        );

        var result = await handler.HandleAsync(request);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("TenantId is required");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task CreateCampaign_WithEmptyName_ReturnsFailure(string? name)
    {
        var db = CreateDbContext();
        var handler = new CreateCampaignHandler(db);

        var request = new CreateCampaignRequest(
            TenantId: Guid.NewGuid(),
            Name: name!,
            SenderPoolIds: new List<Guid>(),
            Steps: new List<CreateCadenceStepDto>
            {
                new(1, 0, OutboundChannel.Email, "Subject", "Body")
            }
        );

        var result = await handler.HandleAsync(request);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Campaign name is required");
    }

    [Fact]
    public async Task CreateCampaign_WithNoSteps_ReturnsFailure()
    {
        var db = CreateDbContext();
        var handler = new CreateCampaignHandler(db);

        var request = new CreateCampaignRequest(
            TenantId: Guid.NewGuid(),
            Name: "Empty Steps Campaign",
            SenderPoolIds: new List<Guid>(),
            Steps: new List<CreateCadenceStepDto>()
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

        var result = await handler.HandleAsync(new EnrollProspectsRequest(
            Guid.NewGuid(), Guid.NewGuid(), new List<Guid> { Guid.NewGuid() }));

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Campaign not found");
    }

    [Fact]
    public async Task EnrollProspects_DoesNotDuplicateExistingEnrollments()
    {
        var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var campaign = OutboundCampaign.Create(tenantId, "Retention Campaign", new List<Guid>());
        db.OutboundCampaigns.Add(campaign);
        var contact = Contact.Create(tenantId, "Clark", "Kent", "clark@planet.com");
        db.Contacts.Add(contact);
        await db.SaveChangesAsync();

        var handler = new EnrollProspectsHandler(db);
        var res1 = await handler.HandleAsync(new EnrollProspectsRequest(tenantId, campaign.Id, new List<Guid> { contact.Id }));
        res1.Value.EnrolledCount.Should().Be(1);

        // Re-enrolling same contact
        var res2 = await handler.HandleAsync(new EnrollProspectsRequest(tenantId, campaign.Id, new List<Guid> { contact.Id }));
        res2.Value.EnrolledCount.Should().Be(0);

        var total = await db.ProspectEnrollments.CountAsync(e => e.CampaignId == campaign.Id && e.ContactId == contact.Id);
        total.Should().Be(1);
    }
}
