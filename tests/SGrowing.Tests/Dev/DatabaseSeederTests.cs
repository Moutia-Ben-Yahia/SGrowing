using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Auth;
using SGrowing.Api.Modules.Dev;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Dev;

public class DatabaseSeederTests
{
    private (SGrowingDbContext db, DatabaseSeeder seeder) CreateContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var db = new SGrowingDbContext(options);
        var hasher = new Pbkdf2PasswordHasher();
        var seeder = new DatabaseSeeder(db, hasher);
        return (db, seeder);
    }

    [Fact]
    public async Task SeedEnterpriseDemoAsync_CreatesCompleteMultiTenantDataset()
    {
        // Arrange
        var (db, seeder) = CreateContext();
        var targetTenant = Guid.NewGuid();

        // Act
        var seededTenantId = await seeder.SeedEnterpriseDemoAsync(targetTenant);

        // Assert
        Assert.Equal(targetTenant, seededTenantId);

        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == targetTenant);
        Assert.NotNull(tenant);

        var users = await db.Users.Where(u => u.TenantId == targetTenant).ToListAsync();
        Assert.True(users.Count >= 2);

        var contacts = await db.Contacts.Where(c => c.TenantId == targetTenant).ToListAsync();
        Assert.True(contacts.Count >= 4);

        var orders = await db.CodOrders.Where(o => o.TenantId == targetTenant).ToListAsync();
        Assert.True(orders.Count >= 5);

        var envelopes = await db.DocumentEnvelopes.Where(e => e.TenantId == targetTenant).ToListAsync();
        Assert.Single(envelopes);
        Assert.Equal(EnvelopeStatus.Completed, envelopes[0].Status);

        var dwellRecords = await db.DocumentDwellTelemetries.Where(d => d.TenantId == targetTenant).ToListAsync();
        Assert.Equal(3, dwellRecords.Count);

        var articles = await db.PublicationArticles.Where(a => a.TenantId == targetTenant).ToListAsync();
        Assert.Equal(2, articles.Count);

        var forecasts = await db.SalesForecastPeriods.Where(p => p.TenantId == targetTenant).ToListAsync();
        Assert.Single(forecasts);

        var feedback = await db.FeatureFeedbackPosts.Where(f => f.TenantId == targetTenant).ToListAsync();
        Assert.Equal(4, feedback.Count);

        var budget = await db.AiAgentBudgets.FirstOrDefaultAsync(b => b.TenantId == targetTenant);
        Assert.NotNull(budget);
        Assert.Equal(5000000, budget.MonthlyTokenLimit);

        var toolLogs = await db.AiToolExecutionLogs.Where(l => l.TenantId == targetTenant).ToListAsync();
        Assert.Equal(2, toolLogs.Count);
    }

    [Fact]
    public async Task SeedEnterpriseDemoAsync_IsIdempotentOnSubsequentCalls()
    {
        // Arrange
        var (db, seeder) = CreateContext();
        var targetTenant = Guid.NewGuid();

        // Act: Run twice
        await seeder.SeedEnterpriseDemoAsync(targetTenant);
        await seeder.SeedEnterpriseDemoAsync(targetTenant);

        // Assert: No duplicate articles or envelopes
        var articlesCount = await db.PublicationArticles.CountAsync(a => a.TenantId == targetTenant);
        Assert.Equal(2, articlesCount);

        var envelopeCount = await db.DocumentEnvelopes.CountAsync(e => e.TenantId == targetTenant);
        Assert.Equal(1, envelopeCount);
    }
}
