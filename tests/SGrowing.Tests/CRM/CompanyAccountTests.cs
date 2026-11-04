using FluentAssertions;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.CRM;
using SGrowing.Core.Entities;
using SGrowing.Core.Tenancy;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace SGrowing.Tests.CRM;

public class CompanyAccountTests
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
    public async Task CreateCompany_ShouldPersistCompanyDetailsAndAnnualRevenue()
    {
        var db = CreateDbContext();
        var service = new CompanyAccountService(db);
        var tenantId = Guid.NewGuid();

        var company = await service.CreateCompanyAsync(
            tenantId: tenantId,
            name: "Wayne Enterprises",
            domain: "wayne-enterprises.com",
            industry: "Defense & Technology",
            revenue: 500_000_000m
        );

        company.Should().NotBeNull();
        company.Id.Should().NotBeEmpty();
        company.TenantId.Should().Be(tenantId);
        company.Name.Should().Be("Wayne Enterprises");
        company.Domain.Should().Be("wayne-enterprises.com");
        company.Industry.Should().Be("Defense & Technology");
        company.AnnualRevenue.Should().Be(500_000_000m);
        company.CreatedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        var inDb = await db.Companies.FirstOrDefaultAsync(c => c.Id == company.Id);
        inDb.Should().NotBeNull();
        inDb!.AnnualRevenue.Should().Be(500_000_000m);
    }

    [Fact]
    public async Task AssociateContactByDomain_ShouldMatchEmailDomainAndSetCustomField()
    {
        var db = CreateDbContext();
        var service = new CompanyAccountService(db);
        var tenantId = Guid.NewGuid();

        // 1. Create company account
        var company = await service.CreateCompanyAsync(
            tenantId: tenantId,
            name: "Stark Industries",
            domain: "starkindustries.com",
            industry: "Aerospace",
            revenue: 1_200_000_000m
        );

        // 2. Create unassigned contact
        var contact = Contact.Create(
            tenantId: tenantId,
            firstName: "Pepper",
            lastName: "Potts",
            email: "pepper.potts@starkindustries.com",
            phone: "+1-212-555-0100"
        );
        db.Contacts.Add(contact);
        await db.SaveChangesAsync();

        // 3. Automated association by email domain
        var matchedCompany = await service.AssociateContactByDomainAsync(tenantId, contact.Id, contact.Email!);

        // Assertions
        matchedCompany.Should().NotBeNull();
        matchedCompany!.Id.Should().Be(company.Id);
        matchedCompany.Name.Should().Be("Stark Industries");

        // Verify contact was updated in database
        var updatedContact = await db.Contacts.FirstOrDefaultAsync(c => c.Id == contact.Id);
        updatedContact.Should().NotBeNull();
        updatedContact!.CompanyId.Should().Be(company.Id);

        var customFields = updatedContact.GetCustomFields();
        customFields.Should().ContainKey("companyId");
        customFields["companyId"].ToString().Should().Be(company.Id.ToString());
    }

    [Fact]
    public async Task AssociateContactByDomain_ShouldReturnNull_WhenNoMatchingDomain()
    {
        var db = CreateDbContext();
        var service = new CompanyAccountService(db);
        var tenantId = Guid.NewGuid();

        await service.CreateCompanyAsync(tenantId, "Cyberdyne Systems", "cyberdyne.com");

        var contact = Contact.Create(tenantId, "John", "Connor", "john@resistance.org");
        db.Contacts.Add(contact);
        await db.SaveChangesAsync();

        var result = await service.AssociateContactByDomainAsync(tenantId, contact.Id, contact.Email!);

        result.Should().BeNull();

        var unchangedContact = await db.Contacts.FirstOrDefaultAsync(c => c.Id == contact.Id);
        unchangedContact!.CompanyId.Should().BeNull();
        unchangedContact.GetCustomFields().Should().NotContainKey("companyId");
    }

    [Fact]
    public async Task AssociateContactByDomain_ShouldPreserveExistingCustomFields()
    {
        var db = CreateDbContext();
        var service = new CompanyAccountService(db);
        var tenantId = Guid.NewGuid();

        var company = await service.CreateCompanyAsync(tenantId, "Acme Corp", "acme.com");

        var initialFields = new Dictionary<string, object>
        {
            { "lead_score", 85 },
            { "target_tier", "Enterprise" }
        };

        var contact = Contact.Create(
            tenantId: tenantId,
            firstName: "Wile",
            lastName: "Coyote",
            email: "wile.e@acme.com",
            customFields: initialFields
        );
        db.Contacts.Add(contact);
        await db.SaveChangesAsync();

        var matched = await service.AssociateContactByDomainAsync(tenantId, contact.Id, contact.Email!);
        matched.Should().NotBeNull();

        var updated = await db.Contacts.FirstOrDefaultAsync(c => c.Id == contact.Id);
        var fields = updated!.GetCustomFields();
        fields.Should().ContainKey("lead_score");
        fields.Should().ContainKey("target_tier");
        fields.Should().ContainKey("companyId");
        fields["companyId"].ToString().Should().Be(company.Id.ToString());
    }

    [Fact]
    public async Task CompanyAccountHierarchy_ShouldSupportParentAndChildAccounts()
    {
        var db = CreateDbContext();
        var service = new CompanyAccountService(db);
        var tenantId = Guid.NewGuid();

        // Create Parent Holding
        var parentHolding = await service.CreateCompanyAsync(
            tenantId: tenantId,
            name: "Alphabet Inc",
            domain: "abc.xyz",
            industry: "Conglomerate",
            revenue: 300_000_000_000m
        );

        // Create Subsidiary
        var subsidiary = await service.CreateCompanyAsync(
            tenantId: tenantId,
            name: "Google DeepMind",
            domain: "deepmind.google",
            industry: "Artificial Intelligence",
            revenue: 5_000_000_000m,
            parentCompanyId: parentHolding.Id
        );

        subsidiary.ParentCompanyId.Should().Be(parentHolding.Id);

        var children = await service.GetChildCompaniesAsync(tenantId, parentHolding.Id);
        children.Should().ContainSingle();
        children[0].Name.Should().Be("Google DeepMind");
    }

    [Fact]
    public async Task SetParentCompanyAsync_UpdatesParentCompanyId_AndPersists()
    {
        var db = CreateDbContext();
        var service = new CompanyAccountService(db);
        var tenantId = Guid.NewGuid();

        var parent = await service.CreateCompanyAsync(tenantId, "Meta Platforms", "meta.com");
        var child = await service.CreateCompanyAsync(tenantId, "Instagram", "instagram.com");

        child.ParentCompanyId.Should().BeNull();

        var updated = await service.SetParentCompanyAsync(tenantId, child.Id, parent.Id);
        updated.Should().NotBeNull();
        updated!.ParentCompanyId.Should().Be(parent.Id);

        var persisted = await service.GetCompanyByIdAsync(tenantId, child.Id);
        persisted!.ParentCompanyId.Should().Be(parent.Id);
    }

    [Fact]
    public async Task GetContactsForCompanyAsync_ReturnsOrderedContacts_ForAssignedCompany()
    {
        var db = CreateDbContext();
        var service = new CompanyAccountService(db);
        var tenantId = Guid.NewGuid();

        var company = await service.CreateCompanyAsync(tenantId, "Microsoft Corp", "microsoft.com");

        var c1 = Contact.Create(tenantId, "Satya", "Nadella", "satya@microsoft.com");
        var c2 = Contact.Create(tenantId, "Bill", "Gates", "bill@microsoft.com");
        c1.CompanyId = company.Id;
        c2.CompanyId = company.Id;

        // Unassociated contact
        var c3 = Contact.Create(tenantId, "Elon", "Musk", "elon@tesla.com");

        db.Contacts.AddRange(c1, c2, c3);
        await db.SaveChangesAsync();

        var contacts = await service.GetContactsForCompanyAsync(tenantId, company.Id);

        contacts.Should().HaveCount(2);
        // Ordered by LastName, then FirstName: Gates, then Nadella
        contacts[0].LastName.Should().Be("Gates");
        contacts[1].LastName.Should().Be("Nadella");
    }

    [Fact]
    public async Task AssociateContactByDomainAsync_HandlesWwwPrefixEquivalence()
    {
        var db = CreateDbContext();
        var service = new CompanyAccountService(db);
        var tenantId = Guid.NewGuid();

        // Company registered with www.apple.com
        var company = await service.CreateCompanyAsync(tenantId, "Apple Inc", "www.apple.com");

        // Contact email without www: tim.cook@apple.com
        var contact = Contact.Create(tenantId, "Tim", "Cook", "tim.cook@apple.com");
        db.Contacts.Add(contact);
        await db.SaveChangesAsync();

        var matched = await service.AssociateContactByDomainAsync(tenantId, contact.Id, contact.Email!);

        matched.Should().NotBeNull();
        matched!.Id.Should().Be(company.Id);
        matched.Name.Should().Be("Apple Inc");

        var updatedContact = await db.Contacts.FindAsync(contact.Id);
        updatedContact!.CompanyId.Should().Be(company.Id);
    }
}
