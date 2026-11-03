using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;
using SGrowing.Core.Tenancy;
using Xunit;

namespace SGrowing.Tests.CRM;

public class WorkspaceAndTenantIsolationTests
{
    private SGrowingDbContext CreateDbContext(Guid tenantId, string dbName)
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var tenantContext = new TenantContext();
        if (tenantId != Guid.Empty)
        {
            tenantContext.SetTenant(tenantId);
        }
        return new SGrowingDbContext(options, tenantContext);
    }

    [Fact]
    public async Task CreateWorkspace_ShouldPersistWorkspaceWithTenantIdAndSlug()
    {
        var tenantId = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();
        var db = CreateDbContext(tenantId, dbName);

        var workspace = new Workspace
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Marketing Operations",
            Slug = "marketing-ops",
            Status = "Active"
        };

        db.Workspaces.Add(workspace);
        await db.SaveChangesAsync();

        var retrieved = await db.Workspaces.FirstOrDefaultAsync(w => w.Id == workspace.Id);
        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be("Marketing Operations");
        retrieved.TenantId.Should().Be(tenantId);
        retrieved.Status.Should().Be("Active");
    }

    [Fact]
    public async Task GlobalQueryFilter_ShouldAutomaticallyFilterDataByTenantId()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();

        // 1. Seed data for both tenants using a context without filter
        var seedDb = CreateDbContext(Guid.Empty, dbName);

        var contactA = Contact.Create(tenantA, "Alice", "Smith", "alice@tenant-a.com");
        var contactB = Contact.Create(tenantB, "Bob", "Jones", "bob@tenant-b.com");

        seedDb.Contacts.AddRange(contactA, contactB);
        await seedDb.SaveChangesAsync();

        // 2. Query as Tenant A
        var dbTenantA = CreateDbContext(tenantA, dbName);
        var contactsA = await dbTenantA.Contacts.ToListAsync();

        contactsA.Should().HaveCount(1);
        contactsA[0].Email.Should().Be("alice@tenant-a.com");

        // 3. Query as Tenant B
        var dbTenantB = CreateDbContext(tenantB, dbName);
        var contactsB = await dbTenantB.Contacts.ToListAsync();

        contactsB.Should().HaveCount(1);
        contactsB[0].Email.Should().Be("bob@tenant-b.com");

        // 4. Super-admin bypass via IgnoreQueryFilters
        var allContacts = await dbTenantA.Contacts.IgnoreQueryFilters().ToListAsync();
        allContacts.Should().HaveCount(2);
    }
}
