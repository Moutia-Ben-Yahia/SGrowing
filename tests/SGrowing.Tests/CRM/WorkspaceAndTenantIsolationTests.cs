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

    [Fact]
    public void TenantContext_SetTenantEmptyGuid_ThrowsArgumentException()
    {
        var tenantContext = new TenantContext();
        var act = () => tenantContext.SetTenant(Guid.Empty);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*TenantId cannot be empty*");
    }

    [Fact]
    public void TenantContext_SetWorkspace_PersistsWorkspaceIdAndTenantId()
    {
        var tenantContext = new TenantContext();
        var tenantId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();

        tenantContext.SetTenant(tenantId);
        tenantContext.SetWorkspace(workspaceId);

        tenantContext.HasTenant.Should().BeTrue();
        tenantContext.TenantId.Should().Be(tenantId);
        tenantContext.WorkspaceId.Should().Be(workspaceId);

        // Reset workspace
        tenantContext.SetWorkspace(null);
        tenantContext.WorkspaceId.Should().BeNull();
    }

    [Fact]
    public async Task Workspace_StateTransitions_ShouldPersistCorrectly()
    {
        var tenantId = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();
        var db = CreateDbContext(tenantId, dbName);

        var workspace = new Workspace
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Sales EMEA",
            Slug = "sales-emea",
            Status = "Active",
            IsActive = true
        };

        db.Workspaces.Add(workspace);
        await db.SaveChangesAsync();

        // Update status to Suspended and deactivate
        workspace.Status = "Suspended";
        workspace.IsActive = false;
        await db.SaveChangesAsync();

        var refreshed = await db.Workspaces.FindAsync(workspace.Id);
        refreshed.Should().NotBeNull();
        refreshed!.Status.Should().Be("Suspended");
        refreshed.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task MultiTenantDataIsolation_MultipleEntities_StayStrictlyIsolated()
    {
        var tenant1 = Guid.NewGuid();
        var tenant2 = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();

        var seedDb = CreateDbContext(Guid.Empty, dbName);

        var ws1 = new Workspace { TenantId = tenant1, Name = "Workspace 1", Slug = "ws-1" };
        var ws2 = new Workspace { TenantId = tenant2, Name = "Workspace 2", Slug = "ws-2" };
        seedDb.Workspaces.AddRange(ws1, ws2);
        await seedDb.SaveChangesAsync();

        var dbTenant1 = CreateDbContext(tenant1, dbName);
        var workspaces = await dbTenant1.Workspaces.Where(w => w.TenantId == tenant1).ToListAsync();

        workspaces.Should().ContainSingle();
        workspaces[0].Slug.Should().Be("ws-1");
    }

    [Fact]
    public void TenantContext_DefaultState_HasTenantIsFalse()
    {
        var tenantContext = new TenantContext();

        tenantContext.HasTenant.Should().BeFalse();
        tenantContext.TenantId.Should().Be(Guid.Empty);
        tenantContext.WorkspaceId.Should().BeNull();
    }

    [Fact]
    public async Task GlobalQueryFilter_Deals_AreStrictlyIsolatedByTenant()
    {
        var tenant1 = Guid.NewGuid();
        var tenant2 = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();

        // Seed deals across tenants using context without filter
        var seedDb = CreateDbContext(Guid.Empty, dbName);
        var deal1 = Deal.Create(tenant1, Guid.NewGuid(), "Deal Alpha", 1000m, DealStage.Prospecting);
        var deal2 = Deal.Create(tenant2, Guid.NewGuid(), "Deal Beta", 2000m, DealStage.Qualified);
        seedDb.Deals.AddRange(deal1, deal2);
        await seedDb.SaveChangesAsync();

        // Query with Tenant 1 scope
        var dbTenant1 = CreateDbContext(tenant1, dbName);
        var dealsTenant1 = await dbTenant1.Deals.ToListAsync();
        dealsTenant1.Should().ContainSingle();
        dealsTenant1[0].Title.Should().Be("Deal Alpha");

        // Query with Tenant 2 scope
        var dbTenant2 = CreateDbContext(tenant2, dbName);
        var dealsTenant2 = await dbTenant2.Deals.ToListAsync();
        dealsTenant2.Should().ContainSingle();
        dealsTenant2[0].Title.Should().Be("Deal Beta");
    }
}
