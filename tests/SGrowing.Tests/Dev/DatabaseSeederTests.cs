using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Auth;
using SGrowing.Api.Modules.Dev;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Dev;

public class DatabaseSeederTests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public async Task SeedEnterpriseDemoAsync_PopulatesCompleteEnterpriseDataset()
    {
        // Arrange
        using var db = CreateDbContext();
        var hasher = new Pbkdf2PasswordHasher();
        var seeder = new DatabaseSeeder(db, hasher);

        // Act
        var seededTenantId = await seeder.SeedEnterpriseDemoAsync();

        // Assert
        Assert.NotEqual(Guid.Empty, seededTenantId);

        // Verify Tenant
        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == seededTenantId);
        Assert.NotNull(tenant);
        Assert.Equal("SGrowing Enterprise Labs", tenant.Name);

        // Verify Roles
        var roles = await db.SystemRoles.Where(r => r.TenantId == seededTenantId).ToListAsync();
        Assert.Equal(6, roles.Count);

        // Verify Users
        var admin = await db.Users.FirstOrDefaultAsync(u => u.TenantId == seededTenantId && u.Email == "admin@sgrowing.io");
        var agent = await db.Users.FirstOrDefaultAsync(u => u.TenantId == seededTenantId && u.Email == "agent.sarah@sgrowing.io");
        Assert.NotNull(admin);
        Assert.NotNull(agent);
        Assert.True(hasher.VerifyPassword("SecurePassword999!", admin.PasswordHash, admin.PasswordSalt));

        // Verify Contacts & Companies
        var companies = await db.Companies.Where(c => c.TenantId == seededTenantId).ToListAsync();
        var contacts = await db.Contacts.Where(c => c.TenantId == seededTenantId).ToListAsync();
        Assert.Equal(2, companies.Count);
        Assert.Equal(4, contacts.Count);

        // Verify Inboxes, Conversations & AI Drafts
        var inboxes = await db.Inboxes.Where(i => i.TenantId == seededTenantId).ToListAsync();
        var convs = await db.Conversations.Where(c => c.TenantId == seededTenantId).ToListAsync();
        var drafts = await db.AiDrafts.Where(d => d.TenantId == seededTenantId).ToListAsync();
        Assert.Equal(2, inboxes.Count);
        Assert.Equal(2, convs.Count);
        Assert.Single(drafts);

        // Verify COD Orders
        var orders = await db.CodOrders.Where(o => o.TenantId == seededTenantId).ToListAsync();
        Assert.Equal(5, orders.Count);
        Assert.Contains(orders, o => o.RiskLevel == FraudRiskLevel.Fraudulent);

        // Verify WMS Inventory
        var inventory = await db.WarehouseInventories.Where(w => w.TenantId == seededTenantId).ToListAsync();
        Assert.Equal(6, inventory.Count);

        // Verify Affiliates
        var creators = await db.CreatorProfiles.Where(c => c.TenantId == seededTenantId).ToListAsync();
        var refCodes = await db.AffiliateReferralCodes.Where(r => r.TenantId == seededTenantId).ToListAsync();
        Assert.Equal(2, creators.Count);
        Assert.Equal(2, refCodes.Count);

        // Verify Cross-Border Trade
        var countries = await db.CountryTradeConfigs.Where(c => c.TenantId == seededTenantId).ToListAsync();
        Assert.Equal(4, countries.Count);

        // Verify Security Audit Trail
        var auditLogs = await db.SecurityAuditLogEntries.Where(a => a.TenantId == seededTenantId).ToListAsync();
        Assert.Single(auditLogs);
        Assert.Equal("SeedEnterpriseDemo", auditLogs[0].Action);
    }
}
