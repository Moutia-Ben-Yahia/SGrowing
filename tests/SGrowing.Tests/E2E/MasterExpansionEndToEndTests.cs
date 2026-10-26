using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Billing;
using SGrowing.Api.Modules.Calendar;
using SGrowing.Api.Modules.Deliverability;
using SGrowing.Api.Modules.Telemetry;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.E2E;

public class MasterExpansionEndToEndTests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: $"MasterExpansionE2EDb_{Guid.NewGuid()}")
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public async Task Master_Expansion_Horizons_A_to_D_Unified_Operator_Journey_Should_Pass()
    {
        var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var hostUserId = Guid.NewGuid();

        // =========================================================================
        // HORIZON A: Calendar Scheduling & Multi-Host Booking Engine
        // =========================================================================
        var calendarService = new CalendarSchedulingService(db);

        // 1. Create Event Type
        var eventType = await calendarService.CreateEventTypeAsync(
            tenantId,
            "Enterprise Architecture Strategy",
            "arch-strategy",
            "1-on-1 COD multi-carrier routing & return optimization",
            duration: 45,
            isRoundRobin: true,
            color: "#6366f1",
            hosts: new List<Guid> { hostUserId }
        );
        Assert.NotNull(eventType);
        Assert.Equal("arch-strategy", eventType.Slug);

        // 2. Set Host Availability
        var fridayHours = new List<DayAvailability>
        {
            new("Friday", "10:00", "16:00")
        };
        await calendarService.SetHostAvailabilityAsync(tenantId, hostUserId, "Chief Architect", "Africa/Tunis", fridayHours);

        // 3. Compute Available Slots
        var targetFriday = new DateTime(2026, 11, 6, 0, 0, 0, DateTimeKind.Utc); // Friday
        var availableSlots = await calendarService.ComputeAvailableSlotsAsync(tenantId, eventType.Id, targetFriday);
        Assert.NotEmpty(availableSlots);

        // 4. Book Appointment
        var bookingTime = availableSlots.First().StartTimeUtc;
        var booking = await calendarService.BookAppointmentAsync(
            tenantId,
            eventType.Id,
            "Mohamed Salah",
            "mohamed@enterprise-client.com",
            "+21620112233",
            bookingTime,
            "Africa/Tunis",
            "Interested in automated drop-and-swap returns and courier remittance clearing"
        );
        Assert.NotNull(booking);
        Assert.Equal(CalendarBookingStatus.Scheduled, booking.Status);

        // 5. Generate RFC 5545 .ics Calendar Invite
        var icsPayload = await calendarService.GenerateIcsContentAsync(tenantId, booking.Id);
        Assert.Contains("BEGIN:VCALENDAR", icsPayload);
        Assert.Contains("Enterprise Architecture Strategy", icsPayload);
        Assert.Contains("mohamed@enterprise-client.com", icsPayload);

        // =========================================================================
        // HORIZON B: In-House Email Deliverability Guardian & Warmup Engine
        // =========================================================================
        var deliverabilityService = new EmailDeliverabilityService(db);

        // 1. Audit Domain DNS Health
        var dnsRecord = await deliverabilityService.CheckDomainDnsHealthAsync(tenantId, "sgrowing.io");
        Assert.True(dnsRecord.OverallHealthScore >= 80);
        Assert.True(dnsRecord.HasSpf);
        Assert.True(dnsRecord.HasDkim);
        Assert.True(dnsRecord.HasDmarc);

        // 2. Spam Risk Analysis
        var spamReport = await deliverabilityService.AnalyzeEmailSpamRiskAsync(
            tenantId,
            "Meeting Confirmation: Enterprise Architecture Strategy Call",
            "<p>Hello Mohamed, your session has been scheduled with our lead engineer.</p><p><a href='https://sgrowing.io/unsub'>Unsubscribe</a></p>"
        );
        Assert.Equal("Safe", spamReport.Verdict);
        Assert.True(spamReport.SpamScore < 20);

        // 3. Start Peer Mailbox Warmup & Simulate Ramp Steps
        var warmup = await deliverabilityService.StartWarmupCadenceAsync(tenantId, "sales@sgrowing.io", dailyTarget: 60);
        Assert.Equal(60, warmup.DailyTargetLimit);

        var updatedWarmup = await deliverabilityService.SimulateWarmupStepAsync(tenantId, warmup.Id);
        Assert.True(updatedWarmup.TotalSimulatedSent > 0);
        Assert.True(updatedWarmup.SpamRescueCount >= 0);

        var warmupLogs = await deliverabilityService.GetWarmupLogsAsync(tenantId, warmup.Id);
        Assert.NotEmpty(warmupLogs);

        // =========================================================================
        // HORIZON C: Sovereign First-Party Product Telemetry & Live Analytics
        // =========================================================================
        var telemetryService = new ProductTelemetryService(db);

        // 1. Ingest Ingress Events from Multiple Devices and Geos
        await telemetryService.IngestTelemetryEventAsync(
            tenantId, "sess_tn_1", "vis_101", "page_view", "/calendar/book/arch-strategy",
            deviceType: "Desktop", browser: "Chrome", countryCode: "TN", city: "Tunis");

        await telemetryService.IngestTelemetryEventAsync(
            tenantId, "sess_tn_1", "vis_101", "appointment_booked", "/calendar/booking-confirmed",
            deviceType: "Desktop", browser: "Chrome", countryCode: "TN", city: "Tunis");

        await telemetryService.IngestTelemetryEventAsync(
            tenantId, "sess_fr_1", "vis_202", "page_view", "/pricing",
            deviceType: "Mobile", browser: "Mobile Safari", countryCode: "FR", city: "Paris");

        // 2. Query Live Pulse Summary (15 min window)
        var liveSummary = await telemetryService.GetLiveSummaryAsync(tenantId, windowMinutes: 15);
        Assert.Equal(2, liveSummary.ActiveVisitorsCount);
        Assert.Equal(2, liveSummary.ActiveSessionsCount);
        Assert.Contains(liveSummary.TopCountries, c => c.CountryCode == "TN");
        Assert.Contains(liveSummary.TopCountries, c => c.CountryCode == "FR");
        Assert.Contains(liveSummary.DeviceBreakdown, d => d.DeviceType == "Desktop");

        var recentEvents = await telemetryService.GetRecentEventsAsync(tenantId, limit: 10);
        Assert.Equal(3, recentEvents.Count);

        // =========================================================================
        // HORIZON D: SaaS Subscription Billing, Quotas & Stripe Webhooks
        // =========================================================================
        var billingService = new SaasBillingService(db);

        // 1. Inspect Default Growth Subscription
        var subscription = await billingService.GetSubscriptionDetailsAsync(tenantId);
        Assert.Equal(PlanTier.Growth, subscription.PlanTier);
        Assert.Equal(249m, subscription.MonthlyPrice);

        // 2. Check Quotas for AI Tokens and Voice Minutes
        var quotaCheck = await billingService.VerifyQuotaAsync(tenantId, UsageMetricType.AiTokens, requestedQuantity: 50_000);
        Assert.True(quotaCheck.Allowed);

        // 3. Record Usage Meter Record
        await billingService.RecordUsageAsync(tenantId, UsageMetricType.AiTokens, 50_000, "Enterprise Architecture RAG Synthesis");

        // 4. Upgrade to Enterprise ($799 / mo)
        var upgradedSub = await billingService.UpgradePlanAsync(tenantId, PlanTier.Enterprise);
        Assert.Equal(PlanTier.Enterprise, upgradedSub.PlanTier);
        Assert.Equal(799m, upgradedSub.MonthlyPrice);

        // 5. Generate Stripe Checkout Session Link
        var checkoutUrl = await billingService.CreateCheckoutSessionUrlAsync(
            tenantId, PlanTier.Enterprise, "https://app.sgrowing.io/billing?status=success");
        Assert.Contains("checkout.stripe.com", checkoutUrl);

        // 6. Process Stripe Webhook for Invoice Payment
        var invoice = await billingService.ProcessStripePaymentSuccessWebhookAsync(
            tenantId, "in_stripe_enterprise_88231", 799m, "USD");
        Assert.Equal("Paid", invoice.Status);
        Assert.Equal(799m, invoice.AmountDue);

        var invoices = await billingService.GetInvoicesAsync(tenantId);
        Assert.Equal(2, invoices.Count);
        Assert.Contains(invoices, inv => inv.StripeInvoiceId == "in_stripe_enterprise_88231");
    }
}
