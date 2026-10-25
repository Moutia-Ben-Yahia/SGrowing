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
        tenantContext.SetTenant(Guid.NewGuid());
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
}
