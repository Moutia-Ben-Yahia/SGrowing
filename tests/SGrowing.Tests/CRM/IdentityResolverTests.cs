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

    [Fact]
    public async Task ResolveOrCreateContactAsync_WithNewContact_CreatesAndSetsAnonymousDefaults()
    {
        var db = CreateDbContext();
        var service = new IdentityResolverService(db);
        var tenantId = Guid.NewGuid();

        // No name provided -> Anonymous User defaults
        var res = await service.ResolveOrCreateContactAsync(tenantId, "ghost@domain.com", null, null, null);

        res.IsSuccess.Should().BeTrue();
        res.Value.FirstName.Should().Be("Anonymous");
        res.Value.LastName.Should().Be("User");
        res.Value.Email.Should().Be("ghost@domain.com");
    }

    [Fact]
    public async Task ResolveOrCreateContactAsync_NormalizesPunctuationAndWhitespaceInPhone()
    {
        var db = CreateDbContext();
        var service = new IdentityResolverService(db);
        var tenantId = Guid.NewGuid();

        // Phone with parentheses, spaces, dots and dashes: "+1 (555) 123-4567"
        var res = await service.ResolveOrCreateContactAsync(tenantId, null, "+1 (555) 123-4567", "Peter", "Parker");

        res.IsSuccess.Should().BeTrue();
        res.Value.Phone.Should().Be("15551234567");

        // Subsequent call with stripped format matches existing contact
        var matchRes = await service.ResolveOrCreateContactAsync(tenantId, "peter@dailybugle.com", "15551234567", "Peter", "Parker");
        matchRes.Value.Id.Should().Be(res.Value.Id);
        matchRes.Value.Email.Should().Be("peter@dailybugle.com");
    }

    [Fact]
    public async Task ResolveOrCreateContactAsync_WhenMatchingByEmail_EnrichesMissingPhone()
    {
        var db = CreateDbContext();
        var service = new IdentityResolverService(db);
        var tenantId = Guid.NewGuid();

        // 1. Initial contact created with email only
        var initial = await service.ResolveOrCreateContactAsync(tenantId, "clark@kent.com", null, "Clark", "Kent");
        initial.Value.Phone.Should().BeNull();

        // 2. Incoming touchpoint matching email provides phone
        var enriched = await service.ResolveOrCreateContactAsync(tenantId, "clark@kent.com", "+1-800-METROPOLIS", "Clark", "Kent");
        enriched.Value.Id.Should().Be(initial.Value.Id);
        enriched.Value.Phone.Should().Be("1800METROPOLIS");
    }

    [Fact]
    public async Task GetUnifiedCustomerTimelineAsync_WithNonExistentContactId_ReturnsFailure()
    {
        var db = CreateDbContext();
        var service = new IdentityResolverService(db);
        var tenantId = Guid.NewGuid();
        var missingContactId = Guid.NewGuid();

        var res = await service.GetUnifiedCustomerTimelineAsync(tenantId, missingContactId);

        res.IsSuccess.Should().BeFalse();
        res.Error.Should().Contain("Contact not found");
    }

    [Fact]
    public async Task ResolveOrCreateContactAsync_WhenDifferentTenantsShareSamePhone_CreatesSeparateContacts()
    {
        var db = CreateDbContext();
        var service = new IdentityResolverService(db);
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var sharedPhone = "+216-99-888-777";

        var resA = await service.ResolveOrCreateContactAsync(tenantA, "userA@acme.com", sharedPhone, "Alice", "TenantA");
        var resB = await service.ResolveOrCreateContactAsync(tenantB, "userB@globex.com", sharedPhone, "Bob", "TenantB");

        resA.IsSuccess.Should().BeTrue();
        resB.IsSuccess.Should().BeTrue();
        resA.Value.Id.Should().NotBe(resB.Value.Id);
        resA.Value.TenantId.Should().Be(tenantA);
        resB.Value.TenantId.Should().Be(tenantB);

        var totalContacts = await db.Contacts.CountAsync();
        totalContacts.Should().Be(2);
    }

    [Fact]
    public async Task GetUnifiedCustomerTimelineAsync_WhenContactHasNoEvents_ReturnsEmptyTimelineCollections()
    {
        var db = CreateDbContext();
        var service = new IdentityResolverService(db);
        var tenantId = Guid.NewGuid();

        var contact = Contact.Create(tenantId, "Solo", "User", "solo@domain.com", "99887766");
        db.Contacts.Add(contact);
        await db.SaveChangesAsync();

        var res = await service.GetUnifiedCustomerTimelineAsync(tenantId, contact.Id);

        res.IsSuccess.Should().BeTrue();
        res.Value.Contact.FirstName.Should().Be("Solo");
        res.Value.Conversations.Should().BeEmpty();
        res.Value.Deals.Should().BeEmpty();
        res.Value.Events.Should().BeEmpty();
    }
}
