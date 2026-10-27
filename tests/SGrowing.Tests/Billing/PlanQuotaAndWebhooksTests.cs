using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Billing;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Billing;

public class PlanQuotaAndWebhooksTests
{
    private readonly SGrowingDbContext _db;
    private readonly SaasBillingService _billingService;
    private readonly PlanQuotaEnforcementService _quotaService;

    public PlanQuotaAndWebhooksTests()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new SGrowingDbContext(options);
        _billingService = new SaasBillingService(_db);
        _quotaService = new PlanQuotaEnforcementService(_db);
    }

    [Fact]
    public async Task CheckQuota_OnStarterPlan_ExceedingLimit_ReturnsHardBlock()
    {
        var tenantId = Guid.NewGuid();

        // 1. Setup Starter subscription with max 1,000 monthly cold emails
        await _billingService.GetSubscriptionDetailsAsync(tenantId);
        await _billingService.UpgradePlanAsync(tenantId, PlanTier.Starter);

        // 2. Simulate usage already at 1,050 emails
        _db.UsageMeterRecords.Add(UsageMeterRecord.Create(tenantId, UsageMetricType.MonthlyColdEmails, 1050));
        await _db.SaveChangesAsync();

        // 3. Check quota for 50 additional emails
        var result = await _quotaService.CheckQuotaAsync(tenantId, UsageMetricType.MonthlyColdEmails, 50);

        result.Allowed.Should().BeFalse();
        result.IsHardBlocked.Should().BeTrue();
        result.RequiresOverageWarning.Should().BeFalse();
        result.PlanTier.Should().Be(PlanTier.Starter);
        result.Message.Should().Contain("Starter plan hard quota");
    }

    [Fact]
    public async Task CheckQuota_OnGrowthPlan_ExceedingLimit_ReturnsSoftWarning()
    {
        var tenantId = Guid.NewGuid();

        // Default plan is Growth (10,000 cold emails limit)
        await _billingService.GetSubscriptionDetailsAsync(tenantId);

        // Set usage to 10,200
        _db.UsageMeterRecords.Add(UsageMeterRecord.Create(tenantId, UsageMetricType.MonthlyColdEmails, 10200));
        await _db.SaveChangesAsync();

        var result = await _quotaService.CheckQuotaAsync(tenantId, UsageMetricType.MonthlyColdEmails, 100);

        result.Allowed.Should().BeTrue();
        result.IsHardBlocked.Should().BeFalse();
        result.RequiresOverageWarning.Should().BeTrue();
        result.PlanTier.Should().Be(PlanTier.Growth);
    }

    [Fact]
    public async Task CheckQuota_OnEnterprisePlan_ExceedingLimit_AllowsElasticUsage()
    {
        var tenantId = Guid.NewGuid();

        await _billingService.GetSubscriptionDetailsAsync(tenantId);
        await _billingService.UpgradePlanAsync(tenantId, PlanTier.Enterprise);

        // Enterprise allows elastic overage
        _db.UsageMeterRecords.Add(UsageMeterRecord.Create(tenantId, UsageMetricType.MonthlyColdEmails, 600_000));
        await _db.SaveChangesAsync();

        var result = await _quotaService.CheckQuotaAsync(tenantId, UsageMetricType.MonthlyColdEmails, 1000);

        result.Allowed.Should().BeTrue();
        result.IsHardBlocked.Should().BeFalse();
        result.RequiresOverageWarning.Should().BeFalse();
        result.PlanTier.Should().Be(PlanTier.Enterprise);
    }

    [Fact]
    public async Task ProcessWebhookEvent_StripeSubscriptionCreated_ActivatesSubscription()
    {
        var tenantId = Guid.NewGuid();
        await _billingService.GetSubscriptionDetailsAsync(tenantId);

        var payload = new BillingWebhookPayloadDto(
            Provider: "Stripe",
            EventType: "customer.subscription.created",
            TenantId: tenantId,
            ExternalSubscriptionId: "sub_stripe_12345",
            TargetPlanTier: PlanTier.Enterprise
        );

        var result = await _billingService.ProcessWebhookEventAsync(payload);

        result.Success.Should().BeTrue();

        var sub = await _db.TenantSubscriptions.FirstAsync(s => s.TenantId == tenantId);
        sub.PlanTier.Should().Be(PlanTier.Enterprise);
        sub.Status.Should().Be("Active");
    }

    [Fact]
    public async Task ProcessWebhookEvent_LemonSqueezyPaymentSucceeded_RecordsPayment()
    {
        var tenantId = Guid.NewGuid();
        await _billingService.GetSubscriptionDetailsAsync(tenantId);

        var payload = new BillingWebhookPayloadDto(
            Provider: "LemonSqueezy",
            EventType: "invoice.payment_succeeded",
            TenantId: tenantId,
            ExternalInvoiceId: "inv_ls_9876",
            Amount: 249.00m
        );

        var result = await _billingService.ProcessWebhookEventAsync(payload);

        result.Success.Should().BeTrue();

        var sub = await _db.TenantSubscriptions.FirstAsync(s => s.TenantId == tenantId);
        sub.Status.Should().Be("Active");

        var invoices = await _db.BillingInvoiceRecords.Where(i => i.TenantId == tenantId).ToListAsync();
        invoices.Should().HaveCount(1);
        invoices[0].AmountDue.Should().Be(249.00m);
    }

    [Fact]
    public async Task ProcessWebhookEvent_ChargilyPaymentFailed_MarksSubscriptionPastDue()
    {
        var tenantId = Guid.NewGuid();
        await _billingService.GetSubscriptionDetailsAsync(tenantId);

        var payload = new BillingWebhookPayloadDto(
            Provider: "Chargily",
            EventType: "invoice.payment_failed",
            TenantId: tenantId,
            ExternalInvoiceId: "inv_chargily_441"
        );

        var result = await _billingService.ProcessWebhookEventAsync(payload);

        result.Success.Should().BeTrue();

        var sub = await _db.TenantSubscriptions.FirstAsync(s => s.TenantId == tenantId);
        sub.Status.Should().Be("PastDue");
    }

    [Fact]
    public async Task VerifyQuotaAsync_ForNewMetrics_EnforcesStarterTierLimits()
    {
        var tenantId = Guid.NewGuid();
        await _billingService.GetSubscriptionDetailsAsync(tenantId);
        await _billingService.UpgradePlanAsync(tenantId, PlanTier.Starter);

        // Starter limits: WarmupMailboxes=2, Waybills=50
        _db.UsageMeterRecords.Add(UsageMeterRecord.Create(tenantId, UsageMetricType.ConnectedWarmupMailboxes, 3));
        _db.UsageMeterRecords.Add(UsageMeterRecord.Create(tenantId, UsageMetricType.CarrierWaybills, 55));
        await _db.SaveChangesAsync();

        var mailboxCheck = await _billingService.VerifyQuotaAsync(tenantId, UsageMetricType.ConnectedWarmupMailboxes, 1);
        mailboxCheck.Allowed.Should().BeFalse();
        mailboxCheck.QuotaLimit.Should().Be(2);

        var waybillCheck = await _billingService.VerifyQuotaAsync(tenantId, UsageMetricType.CarrierWaybills, 1);
        waybillCheck.Allowed.Should().BeFalse();
        waybillCheck.QuotaLimit.Should().Be(50);
    }

    [Fact]
    public async Task ProcessWebhookEvent_WithNullProvider_DefaultsGracefullyWithoutThrowing()
    {
        var tenantId = Guid.NewGuid();
        await _billingService.GetSubscriptionDetailsAsync(tenantId);

        var payload = new BillingWebhookPayloadDto(
            Provider: null!,
            EventType: "invoice.payment_succeeded",
            TenantId: tenantId,
            ExternalInvoiceId: "inv_generic_101",
            Amount: 99m
        );

        var result = await _billingService.ProcessWebhookEventAsync(payload);
        result.Success.Should().BeTrue();
        result.Provider.Should().Be("Stripe");
    }

    [Fact]
    public async Task ProcessWebhookEvent_SubscriptionDeleted_CancelsSubscription()
    {
        var tenantId = Guid.NewGuid();
        await _billingService.GetSubscriptionDetailsAsync(tenantId);

        var payload = new BillingWebhookPayloadDto(
            Provider: "Stripe",
            EventType: "customer.subscription.deleted",
            TenantId: tenantId
        );

        var result = await _billingService.ProcessWebhookEventAsync(payload);
        result.Success.Should().BeTrue();

        var sub = await _db.TenantSubscriptions.FirstAsync(s => s.TenantId == tenantId);
        sub.Status.Should().Be("Canceled");
    }
}
