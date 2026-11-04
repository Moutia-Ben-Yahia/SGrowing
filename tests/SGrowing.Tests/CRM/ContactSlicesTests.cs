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

    [Fact]
    public async Task CreateContact_WithEmptyTenantId_ShouldReturnFailure()
    {
        var db = CreateDbContext();
        var handler = new CreateContactHandler(db);

        var request = new CreateContactRequest(
            TenantId: Guid.Empty,
            FirstName: "No",
            LastName: "Tenant",
            Email: "notenant@example.com",
            Phone: "+1000",
            LifecycleStage: LifecycleStage.Lead
        );

        var result = await handler.HandleAsync(request);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("TenantId is required");
    }

    [Fact]
    public async Task CreateContact_WithNoEmailAndNoPhone_ShouldReturnFailure()
    {
        var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var handler = new CreateContactHandler(db);

        var request = new CreateContactRequest(
            TenantId: tenantId,
            FirstName: "Anonymous",
            LastName: "Visitor",
            Email: null,
            Phone: "",
            LifecycleStage: LifecycleStage.Lead
        );

        var result = await handler.HandleAsync(request);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("At least one contact method");
    }

    [Fact]
    public async Task GetContact_WithExistingContact_ShouldReturnDto()
    {
        var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var contact = Contact.Create(
            tenantId: tenantId,
            firstName: "Ada",
            lastName: "Lovelace",
            email: "ada@computing.org",
            phone: "+44201234567",
            stage: LifecycleStage.Prospect
        );
        db.Contacts.Add(contact);
        await db.SaveChangesAsync();

        var handler = new GetContactHandler(db);
        var result = await handler.HandleAsync(tenantId, contact.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.FirstName.Should().Be("Ada");
        result.Value.LastName.Should().Be("Lovelace");
        result.Value.Email.Should().Be("ada@computing.org");
        result.Value.LifecycleStage.Should().Be(LifecycleStage.Prospect);
    }

    [Fact]
    public async Task GetContact_WithNonExistentId_ShouldReturnFailure()
    {
        var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var missingId = Guid.NewGuid();

        var handler = new GetContactHandler(db);
        var result = await handler.HandleAsync(tenantId, missingId);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Contact not found");
    }

    [Fact]
    public async Task GetContact_WithDifferentTenant_ShouldReturnFailure()
    {
        var db = CreateDbContext();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var contact = Contact.Create(
            tenantId: tenantA,
            firstName: "Alan",
            lastName: "Turing",
            email: "alan@enigma.gov",
            phone: "+44123456",
            stage: LifecycleStage.Customer
        );
        db.Contacts.Add(contact);
        await db.SaveChangesAsync();

        var handler = new GetContactHandler(db);
        // Tenant B requests Tenant A's contact
        var result = await handler.HandleAsync(tenantB, contact.Id);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Contact not found");
    }

    [Fact]
    public async Task SearchContacts_WithEmptyTenantId_ShouldReturnFailure()
    {
        var db = CreateDbContext();
        var handler = new SearchContactsHandler(db);

        var result = await handler.HandleAsync(new SearchContactsQuery(Guid.Empty, "query"));

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("TenantId is required");
    }

    [Fact]
    public async Task SearchContacts_WithEmptySearchTerm_ShouldReturnAllTenantContactsUpTo50()
    {
        var db = CreateDbContext();
        var tenantId = Guid.NewGuid();

        for (int i = 0; i < 5; i++)
        {
            var contact = Contact.Create(tenantId, $"First{i}", $"Last{i}", $"contact{i}@domain.com", $"+2160000{i}", LifecycleStage.Lead);
            db.Contacts.Add(contact);
        }
        await db.SaveChangesAsync();

        var handler = new SearchContactsHandler(db);
        var result = await handler.HandleAsync(new SearchContactsQuery(tenantId, ""));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(5);
    }
}
