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

    [Fact]
    public async Task VerifyQuota_EnterpriseTier_AllowsOverageWithWarning()
    {
        var tenantId = Guid.NewGuid();
        await _service.UpgradePlanAsync(tenantId, PlanTier.Enterprise);

        // Enterprise voice minutes quota = 2500
        await _service.RecordUsageAsync(tenantId, UsageMetricType.VoiceMinutes, 2600, "Heavy call volume");

        // Verify quota for 100 more minutes (2600 + 100 > 2500)
        var check = await _service.VerifyQuotaAsync(tenantId, UsageMetricType.VoiceMinutes, 100);

        Assert.True(check.Allowed); // Enterprise tier allows overage
        Assert.True(check.RequiresOverageWarning);
        Assert.Equal(2600, check.CurrentUsage);
        Assert.Equal(2500, check.QuotaLimit);
    }

    [Fact]
    public async Task ProcessWebhookEvent_SubscriptionCreated_ActivatesSubscriptionAndUpdatesTier()
    {
        var tenantId = Guid.NewGuid();
        var payload = new BillingWebhookPayloadDto(
            Provider: "Stripe",
            EventType: "customer.subscription.created",
            TenantId: tenantId,
            TargetPlanTier: PlanTier.Enterprise
        );

        var result = await _service.ProcessWebhookEventAsync(payload);

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("Active", result.SubscriptionStatus);

        var details = await _service.GetSubscriptionDetailsAsync(tenantId);
        Assert.Equal(PlanTier.Enterprise, details.PlanTier);
        Assert.Equal("Active", details.Status);
    }

    [Fact]
    public async Task ProcessWebhookEvent_PaymentSucceeded_AdvancesBillingCycleAndGeneratesInvoice()
    {
        var tenantId = Guid.NewGuid();
        var payload = new BillingWebhookPayloadDto(
            Provider: "Stripe",
            EventType: "invoice.payment_succeeded",
            TenantId: tenantId,
            Amount: 249m,
            Currency: "USD",
            ExternalInvoiceId: "in_ext_test_8877"
        );

        var result = await _service.ProcessWebhookEventAsync(payload);

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.InvoiceNumber);

        var invoices = await _service.GetInvoicesAsync(tenantId);
        Assert.Contains(invoices, i => i.StripeInvoiceId == "in_ext_test_8877");
    }

    [Fact]
    public async Task UpgradePlan_StarterTier_UpdatesPriceTo99AndReflectsEntitlements()
    {
        var tenantId = Guid.NewGuid();

        var upgraded = await _service.UpgradePlanAsync(tenantId, PlanTier.Starter);

        Assert.Equal(PlanTier.Starter, upgraded.PlanTier);
        Assert.Equal(99m, upgraded.MonthlyPrice);

        var details = await _service.GetSubscriptionDetailsAsync(tenantId);
        Assert.Equal(PlanTier.Starter, details.PlanTier);
    }

    [Fact]
    public async Task RecordUsage_AccumulatesMetricUsageAcrossMultipleCalls()
    {
        var tenantId = Guid.NewGuid();
        await _service.GetSubscriptionDetailsAsync(tenantId);

        await _service.RecordUsageAsync(tenantId, UsageMetricType.AiTokens, 50000, "Prompt 1");
        await _service.RecordUsageAsync(tenantId, UsageMetricType.AiTokens, 75000, "Prompt 2");

        var details = await _service.GetSubscriptionDetailsAsync(tenantId);
        Assert.Equal(125000, details.UsageSummary.UsedAiTokens);
    }

    [Fact]
    public async Task GetInvoices_NonExistentTenant_ReturnsEmptyList()
    {
        var unknownTenant = Guid.NewGuid();
        var invoices = await _service.GetInvoicesAsync(unknownTenant);

        Assert.NotNull(invoices);
        Assert.Empty(invoices);
    }
}
