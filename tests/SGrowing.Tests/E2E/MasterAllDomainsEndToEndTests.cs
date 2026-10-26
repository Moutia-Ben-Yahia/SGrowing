using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Auth;
using SGrowing.Api.Modules.Commerce;
using SGrowing.Api.Modules.Dev;
using SGrowing.Api.Modules.Funnels;
using SGrowing.Api.Modules.Marketing;
using SGrowing.Api.Modules.SEO;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.E2E;

public class MasterAllDomainsEndToEndTests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public async Task MasterAllDomains_FullEnterprisePlatformLifecycle_ExecutesSuccessfully()
    {
        // 1. Setup DbContext & Dependencies
        using var db = CreateDbContext();
        var hasher = new Pbkdf2PasswordHasher();
        var seeder = new DatabaseSeeder(db, hasher);

        // 2. Horizon 1: Seed Demo Sandbox
        var tenantId = await seeder.SeedEnterpriseDemoAsync();
        Assert.NotEqual(Guid.Empty, tenantId);

        // Horizon 1: Verify Authentication & JWT issuance
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:SecretKey"] = "SGrowing_SuperSecretEnterpriseMasterKey_998877665544332211!!",
            ["Jwt:Issuer"] = "SGrowing.Api",
            ["Jwt:Audience"] = "SGrowing.Clients"
        }).Build();

        var jwtService = new JwtTokenService(config);
        var admin = await db.Users.FirstAsync(u => u.TenantId == tenantId && u.Email == "admin@sgrowing.io");
        var token = jwtService.GenerateAccessToken(admin, "SGrowing Enterprise Labs");
        Assert.NotNull(token);
        var principal = jwtService.ValidateToken(token);
        Assert.NotNull(principal);

        // 3. Horizon 2: Run Technical SEO Audit & SERP Position Tracking (Domain 11)
        var seoService = new SeoAuditService(db);
        var audit = await seoService.RunAuditAsync(tenantId, "https://store.sgrowing.io");
        Assert.NotNull(audit);
        Assert.Equal(SeoAuditStatus.Completed, audit.Status);
        Assert.True(audit.OverallHealthScore > 50);

        var kw = await seoService.TrackKeywordAsync(tenantId, "sneakers cuir tunisie", "https://store.sgrowing.io/shop", "TN", 5, 3400);
        var updatedKw = await seoService.UpdateKeywordRankAsync(tenantId, kw.Id, 2);
        Assert.Equal(2, updatedKw.CurrentRank);

        var aeo = await seoService.RecordAeoMentionAsync(tenantId, "SGrowing", "Perplexity Pro", "Best COD footwear store in Tunisia", true, "Positive", "SGrowing is top-ranked for genuine leather shoes with doorstep inspection.", 1);
        Assert.NotNull(aeo);

        // 4. Horizon 3: Sales Funnel Pipeline, Order Bump & 1-Click Upsell (Domain 15)
        var funnelService = new SalesFunnelService(db);
        var funnel = await funnelService.CreateFunnelAsync(tenantId, "VIP Sneakers Funnel", "vip-sneakers");
        var checkoutStep = await funnelService.AddFunnelStepAsync(tenantId, funnel.Id, "Checkout", FunnelStepType.Checkout, 1);
        var upsellStep = await funnelService.AddFunnelStepAsync(tenantId, funnel.Id, "Post-Purchase Upsell", FunnelStepType.OneClickUpsell, 2);

        var bump = await funnelService.CreateOrderBumpAsync(tenantId, funnel.Id, checkoutStep.Id, "Waterproof Protector Spray", "Guaranteed protection", "CARE-SPRAY", 40m, 20m);
        var upsell = await funnelService.CreateOneClickUpsellAsync(tenantId, funnel.Id, upsellStep.Id, "Italian Leather Belt", "BELT-BROWN", "Save 50% with this 1-click offer", 70m, 35m);

        // Conversion cycle: visitor -> checkout -> bump accepted -> upsell accepted -> order completed
        await funnelService.RecordConversionEventAsync(tenantId, funnel.Id, FunnelEventType.PageView, 0, checkoutStep.Id);
        await funnelService.RecordConversionEventAsync(tenantId, funnel.Id, FunnelEventType.OrderBumpAccepted, 20m, checkoutStep.Id);
        await funnelService.RecordConversionEventAsync(tenantId, funnel.Id, FunnelEventType.UpsellAccepted, 35m, upsellStep.Id);
        await funnelService.RecordConversionEventAsync(tenantId, funnel.Id, FunnelEventType.FunnelOrderCompleted, 195m, checkoutStep.Id);

        var perf = await funnelService.GetFunnelPerformanceAsync(tenantId, funnel.Id);
        Assert.Equal(1, perf.TotalVisitors);
        Assert.Equal(1, perf.TotalOrders);
        Assert.Equal(250m, perf.TotalRevenue); // 195 base + 20 bump + 35 upsell = 250
        Assert.Equal(20m, perf.BumpRevenue);
        Assert.Equal(35m, perf.UpsellRevenue);

        // 5. Commerce & WMS: Place COD Order & Reserve Defensive Stock
        var wmsService = new DefensiveWmsService(db);
        var wh = await db.WarehouseInventories.FirstAsync(w => w.TenantId == tenantId);
        await wmsService.ReserveStockOnCheckoutAsync(tenantId, wh.WarehouseId, wh.Sku, 1, Guid.NewGuid());
        var stockSummary = await wmsService.GetStockSummaryAsync(tenantId, wh.WarehouseId, wh.Sku);
        Assert.True(stockSummary.Reserved >= 1);

        // 6. Marketing & Creators: Creator Escrow Ledgering & Courier Delivery Settlement
        var creatorService = new CreatorAffiliateService(db);
        var order1Id = Guid.NewGuid();
        var ledger = await creatorService.AttributeOrderCommissionAsync(tenantId, "AMIRA15", order1Id, "COD-TN-9011", 195m);
        Assert.NotNull(ledger);
        Assert.Equal(EscrowStatus.EscrowHold, ledger.Status);

        var cleared = await creatorService.ClearEscrowOnDeliveryAsync(tenantId, order1Id);
        Assert.NotNull(cleared);
        Assert.Equal(EscrowStatus.ClearedForPayout, cleared.Status);
        var updatedCreator = await db.CreatorProfiles.FirstAsync(c => c.Id == ledger.CreatorId);
        Assert.True(updatedCreator.TotalEarned > 0);

        // 7. Verification: All tables populated cleanly without conflict
        var auditLogsCount = await db.SecurityAuditLogEntries.CountAsync(a => a.TenantId == tenantId);
        Assert.True(auditLogsCount >= 1);
    }
}
