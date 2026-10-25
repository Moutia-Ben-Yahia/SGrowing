using FluentAssertions;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.CRM;
using SGrowing.Core.Entities;
using SGrowing.Core.Tenancy;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace SGrowing.Tests.CRM;

public class IdentityResolverTests
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
    public async Task ResolveOrCreateContact_EnrichesExistingContactWhenPhoneMatches()
    {
        var db = CreateDbContext();
        var service = new IdentityResolverService(db);
        var tenantId = Guid.NewGuid();

        // 1. Initial WhatsApp touchpoint creates contact with phone only
        var initialRes = await service.ResolveOrCreateContactAsync(tenantId, null, "+1-555-0199", "Bruce", "Wayne");
        initialRes.IsSuccess.Should().BeTrue();
        initialRes.Value.Email.Should().BeNull();
        initialRes.Value.Phone.Should().Be("15550199");

        // 2. Later, web chat or form arrives with same phone and an email address
        var secondRes = await service.ResolveOrCreateContactAsync(tenantId, "bruce@wayne-enterprises.com", "15550199", "Bruce", "Wayne");
        secondRes.IsSuccess.Should().BeTrue();
        secondRes.Value.Id.Should().Be(initialRes.Value.Id); // Same contact!
        secondRes.Value.Email.Should().Be("bruce@wayne-enterprises.com"); // Enriched!

        var count = await db.Contacts.CountAsync();
        count.Should().Be(1);
    }

    [Fact]
    public async Task GetUnifiedCustomerTimeline_AggregatesConversationsDealsAndEvents()
    {
        var db = CreateDbContext();
        var service = new IdentityResolverService(db);
        var tenantId = Guid.NewGuid();

        var contact = Contact.Create(tenantId, "Diana", "Prince", "diana@themyscira.gov", "123456");
        db.Contacts.Add(contact);

        var conv = Conversation.Create(tenantId, Guid.NewGuid(), contact.Id);
        db.Conversations.Add(conv);

        var deal = Deal.Create(tenantId, contact.Id, "Shield Equipment Order", 15000m, DealStage.Won);
        db.Deals.Add(deal);

        var evt = CustomerEvent.Create(tenantId, contact.Id, "user.upgraded_plan", "{\"plan\": \"vip\"}");
        db.CustomerEvents.Add(evt);
        await db.SaveChangesAsync();

        var timelineRes = await service.GetUnifiedCustomerTimelineAsync(tenantId, contact.Id);

        timelineRes.IsSuccess.Should().BeTrue();
        timelineRes.Value.Contact.FirstName.Should().Be("Diana");
        timelineRes.Value.Conversations.Should().ContainSingle();
        timelineRes.Value.Deals.Should().ContainSingle();
        timelineRes.Value.Events.Should().ContainSingle();
    }
}
