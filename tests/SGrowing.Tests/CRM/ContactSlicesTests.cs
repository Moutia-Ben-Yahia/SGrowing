using FluentAssertions;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.CRM;
using SGrowing.Core.Entities;
using SGrowing.Core.Tenancy;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace SGrowing.Tests.CRM;

public class ContactSlicesTests
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
    public async Task CreateContact_ShouldPersistContactAndCustomFields()
    {
        var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var handler = new CreateContactHandler(db);

        var request = new CreateContactRequest(
            TenantId: tenantId,
            FirstName: "Sarah",
            LastName: "Connor",
            Email: "sarah@cyberdyne.com",
            Phone: "+15550199",
            LifecycleStage: LifecycleStage.Lead,
            CustomFields: new Dictionary<string, object>
            {
                { "lead_score", 95 },
                { "preferred_channel", "WhatsApp" }
            },
            Tags: new List<string> { "vip", "cyberdyne" }
        );

        var result = await handler.HandleAsync(request);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Email.Should().Be("sarah@cyberdyne.com");
        result.Value.Tags.Should().Contain("vip");
        result.Value.CustomFields.Should().ContainKey("lead_score");

        var inDb = await db.Contacts.FirstOrDefaultAsync(c => c.Id == result.Value.Id);
        inDb.Should().NotBeNull();
        inDb!.FirstName.Should().Be("Sarah");
    }

    [Fact]
    public async Task SearchContacts_ShouldFilterByEmailOrPhone()
    {
        var db = CreateDbContext();
        var tenantId = Guid.NewGuid();

        var contact = Contact.Create(
            tenantId: tenantId,
            firstName: "John",
            lastName: "Doe",
            email: "john@example.com",
            phone: "+1234567890",
            stage: LifecycleStage.Customer
        );
        db.Contacts.Add(contact);
        await db.SaveChangesAsync();

        var handler = new SearchContactsHandler(db);
        var searchResult = await handler.HandleAsync(new SearchContactsQuery(tenantId, "john"));

        searchResult.IsSuccess.Should().BeTrue();
        searchResult.Value.Should().HaveCount(1);
        searchResult.Value[0].Email.Should().Be("john@example.com");
    }
}
