using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Billing;
using SGrowing.Api.Modules.Commerce;
using SGrowing.Api.Modules.Integrations;
using SGrowing.Api.Modules.Marketing;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.E2E;

public class MasterHorizonsOneToFourE2ETests
{
    [Fact]
    public async Task Complete_Horizons_One_To_Four_Unified_Platform_Journey_Should_Pass()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var db = new SGrowingDbContext(options);
        var tenantId = Guid.NewGuid();

        // =========================================================================
        // HORIZON 1: Headless COD Checkout & Mobile Courier Driver Companion
        // =========================================================================
        var checkoutService = new CodCheckoutService(db);

        // 1.1 Customer initiates checkout
        var cartJson = "[{\"sku\":\"PERFUME-ROYAL-OUD-50ML\",\"qty\":1,\"price\":120.0}]";
        var checkoutSession = await checkoutService.CreateSessionAsync(tenantId, cartJson, subtotal: 120.0m);
        Assert.NotNull(checkoutSession);

        // 1.2 Customer selects Wilaya and enters phone
        await checkoutService.UpdateCustomerDetailsAsync(
            tenantId,
            checkoutSession.SessionKey,
            "Kais Saidi",
            "+21698223344",
            "TN-11",
            "La Marsa",
            "12 Rue Sidi Bou Said"
        );

        // 1.3 OTP verification
        var otp = await checkoutService.SendPhoneOtpAsync(tenantId, checkoutSession.SessionKey);
        var verified = await checkoutService.VerifyPhoneOtpAsync(tenantId, checkoutSession.SessionKey, otp);
        Assert.True(verified);

        // 1.4 Post-order 1-click upsell
        await checkoutService.ApplyUpsellOfferAsync(tenantId, checkoutSession.SessionKey, "OUD-TRAVEL-MINI", 30.0m);
        var orderConfirmation = await checkoutService.CompleteCodOrderAsync(tenantId, checkoutSession.SessionKey);
        Assert.Equal(157.0m, orderConfirmation.TotalAmountDueOnDoorstep); // 120 + 7 (shipping) + 30 (upsell)

        // =========================================================================
        // HORIZON 3: Autonomous UGC Creative Engine & Competitor Radar
        // =========================================================================
        var ugcService = new UgcCreativeGeneratorService(db);
        var competitorService = new CompetitorIntelligenceService(db);

        // 3.1 Generate 5 UGC video scripts
        var scripts = await ugcService.GenerateScriptsForProductAsync(tenantId, "PERFUME-ROYAL-OUD-50ML", "Royal Oud Eau De Parfum", "Darija");
        Assert.Equal(5, scripts.Count);

        // 3.2 Ingest Competitor Ad & Track Disparity
        await competitorService.IngestCompetitorAdAsync(tenantId, "FragranceRival.tn", "Meta", "Fake Oud on discount", "Buy 1 Get 1 Free", 1800m, "High");
        var priceAlert = await competitorService.TrackPriceDisparityAsync(tenantId, "PERFUME-ROYAL-OUD-50ML", "FragranceRival.tn", "https://rival.tn/oud", 55m, 120m);
        Assert.Equal("BundleIncentive", priceAlert.RecommendedCounterAction);

        // =========================================================================
        // HORIZON 4: Enterprise B2B Financials & Developer Gateway
        // =========================================================================
        var b2bService = new B2BCreditAndFactoringService(db);
        var devService = new DeveloperGatewayService(db);

        // 4.1 B2B Credit Line drawdown
        var b2bCompanyId = Guid.NewGuid();
        var creditAccount = await b2bService.EstablishCreditAccountAsync(tenantId, b2bCompanyId, "Grand Tunis Wholesalers", creditLimit: 25000m, termsDays: 30);
        Assert.Equal(25000m, creditAccount.AvailableCredit);

        await b2bService.RecordInvoiceDrawdownAsync(tenantId, b2bCompanyId, 8500m);
        var updatedAccount = await db.B2BCreditAccounts.FindAsync(creditAccount.Id);
        Assert.NotNull(updatedAccount);
        Assert.Equal(16500m, updatedAccount.AvailableCredit);

        // 4.2 Courier cash factoring advance
        var factoring = await b2bService.RequestCourierCashFactoringAsync(tenantId, "YALIDINE", "Yalidine Express", 15000m, 40000m, 2.5m);
        var fundedFactoring = await b2bService.FundFactoringAdvanceAsync(tenantId, factoring.Id);
        Assert.Equal("Funded", fundedFactoring.Status);
        Assert.Equal(39000m, fundedFactoring.ImmediateCashAvailable); // 40000 - 2.5%

        // 4.3 Developer API Key creation and verification
        var (apiKey, plainKey) = await devService.CreateApiKeyAsync(tenantId, "Production ERP Key", new() { "orders:read", "webhooks:read" });
        var keyValidation = await devService.ValidateApiKeyAsync(plainKey);
        Assert.True(keyValidation.IsValid);
        Assert.Contains("orders:read", keyValidation.AllowedScopes);

        // 4.4 Webhook registration and dispatch
        var sub = await devService.RegisterWebhookAsync(tenantId, "https://erp.client.tn/webhook", new() { "order.delivered" });
        var attempts = await devService.DispatchWebhookEventAsync(tenantId, "order.delivered", new { orderNumber = orderConfirmation.OrderNumber, status = "Delivered" });
        Assert.Single(attempts);
        Assert.True(attempts[0].IsSuccess);
    }

    [Fact]
    public async Task Horizons_One_To_Four_Security_Validation_And_Constraint_Failures_Should_Be_Enforced()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var db = new SGrowingDbContext(options);
        var tenantId = Guid.NewGuid();

        // 1. Horizon 1 Failures: Missing session and invalid OTP
        var checkoutService = new CodCheckoutService(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            checkoutService.CompleteCodOrderAsync(tenantId, "non-existent-session-key"));

        var session = await checkoutService.CreateSessionAsync(tenantId, "[]", 50m);
        var otp = await checkoutService.SendPhoneOtpAsync(tenantId, session.SessionKey);
        var wrongOtpResult = await checkoutService.VerifyPhoneOtpAsync(tenantId, session.SessionKey, "999999");
        Assert.False(wrongOtpResult);

        // 2. Horizon 3 Competitor Radar: Competitor price higher or parity maintains price
        var competitorService = new CompetitorIntelligenceService(db);
        var tracker = await competitorService.TrackPriceDisparityAsync(tenantId, "SKU-PREMIUM-ROSE", "HighEndShop.tn", "https://highend.tn", 150m, 120m);
        Assert.Equal("MaintainPrice", tracker.RecommendedCounterAction);

        // 3. Horizon 4 B2B Credit Line: Non-existent account and over-limit drawdowns
        var b2bService = new B2BCreditAndFactoringService(db);
        var missingCompanyId = Guid.NewGuid();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            b2bService.RecordInvoiceDrawdownAsync(tenantId, missingCompanyId, 500m));

        var testCompanyId = Guid.NewGuid();
        await b2bService.EstablishCreditAccountAsync(tenantId, testCompanyId, "Micro Retail SARL", creditLimit: 2000m, termsDays: 15);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            b2bService.RecordInvoiceDrawdownAsync(tenantId, testCompanyId, 2500m));

        // 4. Horizon 4 Developer Gateway: Malformed and unregistered API keys
        var devService = new DeveloperGatewayService(db);
        var badFormatResult = await devService.ValidateApiKeyAsync("plain_secret_string");
        Assert.False(badFormatResult.IsValid);
        Assert.Equal("Invalid key format", badFormatResult.ErrorMessage);

        var unknownKeyResult = await devService.ValidateApiKeyAsync("sg_live_0123456789abcdef0123456789abcdef");
        Assert.False(unknownKeyResult.IsValid);
        Assert.Equal("Key not found or revoked", unknownKeyResult.ErrorMessage);
    }
}
