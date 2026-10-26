using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Billing;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Billing;

public class SaasBillingTests
{
    private readonly SGrowingDbContext _db;
    private readonly SaasBillingService _service;

    public SaasBillingTests()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new SGrowingDbContext(options);
        _service = new SaasBillingService(_db);
    }

    [Fact]
    public async Task GetSubscriptionDetails_InitializesDefaultGrowthPlanIfNotExists()
    {
        var tenantId = Guid.NewGuid();

        var details = await _service.GetSubscriptionDetailsAsync(tenantId);

        Assert.NotNull(details);
        Assert.Equal(PlanTier.Growth, details.PlanTier);
        Assert.Equal(249m, details.MonthlyPrice);
        Assert.NotNull(details.Entitlements);
        Assert.NotNull(details.UsageSummary);
    }

    [Fact]
    public async Task UpgradePlan_UpdatesSubscriptionTierAndEntitlements()
    {
        var tenantId = Guid.NewGuid();

        // 1. Initial details
        await _service.GetSubscriptionDetailsAsync(tenantId);

        // 2. Upgrade to Enterprise ($799)
        var upgraded = await _service.UpgradePlanAsync(tenantId, PlanTier.Enterprise);

        Assert.NotNull(upgraded);
        Assert.Equal(PlanTier.Enterprise, upgraded.PlanTier);
        Assert.Equal(799m, upgraded.MonthlyPrice);

        // 3. Verify upgraded entitlements reflected
        var details = await _service.GetSubscriptionDetailsAsync(tenantId);
        Assert.Equal(PlanTier.Enterprise, details.PlanTier);
        Assert.Equal(5_000_000, details.Entitlements.MonthlyIncludedAiTokens);
        Assert.Equal(2_500, details.Entitlements.MonthlyIncludedVoiceMinutes);
    }

    [Fact]
    public async Task VerifyQuota_AllowsWithinLimitsAndRejectsWhenExceeded()
    {
        var tenantId = Guid.NewGuid();

        // Initialize Growth tier: MaxVoiceMinutesPerMonth = 500
        await _service.GetSubscriptionDetailsAsync(tenantId);

        // Check 100 minutes -> allowed
        var check1 = await _service.VerifyQuotaAsync(tenantId, UsageMetricType.VoiceMinutes, 100);
        Assert.True(check1.Allowed);

        // Record 490 minutes usage
        await _service.RecordUsageAsync(tenantId, UsageMetricType.VoiceMinutes, 490, "Automated voice dialer confirmation");

        // Check 20 more minutes (490 + 20 = 510 > 500) -> rejected
        var check2 = await _service.VerifyQuotaAsync(tenantId, UsageMetricType.VoiceMinutes, 20);
        Assert.False(check2.Allowed);
    }

    [Fact]
    public async Task ProcessStripePaymentSuccessWebhook_GeneratesInvoiceRecord()
    {
        var tenantId = Guid.NewGuid();
        var invoiceId = "in_stripe_mock_99214";

        var invoice = await _service.ProcessStripePaymentSuccessWebhookAsync(
            tenantId,
            invoiceId,
            249m,
            "USD"
        );

        Assert.NotNull(invoice);
        Assert.Equal(invoiceId, invoice.StripeInvoiceId);
        Assert.Equal(249m, invoice.AmountDue);
        Assert.Equal("Paid", invoice.Status);

        var invoices = await _service.GetInvoicesAsync(tenantId);
        Assert.Single(invoices);
        Assert.Equal(invoice.Id, invoices[0].Id);
    }

    [Fact]
    public async Task CreateCheckoutSessionUrl_ReturnsStripeDirectSessionLink()
    {
        var tenantId = Guid.NewGuid();
        var successUrl = "https://app.sgrowing.io/billing/success";

        var checkoutUrl = await _service.CreateCheckoutSessionUrlAsync(tenantId, PlanTier.Enterprise, successUrl);

        Assert.NotNull(checkoutUrl);
        Assert.Contains("checkout.stripe.com", checkoutUrl);
        Assert.Contains("plan=Enterprise", checkoutUrl);
    }
}
