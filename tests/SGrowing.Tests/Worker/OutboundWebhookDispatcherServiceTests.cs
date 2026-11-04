using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;
using SGrowing.Worker.Services;
using Xunit;

namespace SGrowing.Tests.Worker;

public class OutboundWebhookDispatcherServiceTests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public void ComputeHmacSignature_CalculatesValidHexDigestWithPrefix()
    {
        var payload = "{\"orderId\":\"123\",\"total\":45.0}";
        var secret = "whsec_test_secret_key_99";

        var signature = OutboundWebhookDispatcherService.ComputeHmacSignature(payload, secret);

        signature.Should().StartWith("sha256=");
        signature.Length.Should().Be(71); // "sha256=" (7 chars) + 64 hex chars
    }

    [Fact]
    public async Task DispatchOutboundWebhooksAsync_WhenNoSubscriptions_ReturnsZero()
    {
        using var db = CreateDbContext();
        var service = new OutboundWebhookDispatcherService(db);
        var tenantId = Guid.NewGuid();

        var dispatched = await service.DispatchOutboundWebhooksAsync(tenantId, "order.confirmed", "{}");

        dispatched.Should().Be(0);
    }

    [Fact]
    public async Task DispatchOutboundWebhooksAsync_WhenActiveAndSubscribed_DispatchesAndLogsDelivery()
    {
        using var db = CreateDbContext();
        var service = new OutboundWebhookDispatcherService(db);
        var tenantId = Guid.NewGuid();

        var sub = WebhookSubscription.Create(
            tenantId,
            "https://partner-erp.com/webhooks/orders",
            "whsec_topsecret123",
            "order.confirmed,order.delivered"
        );
        db.WebhookSubscriptions.Add(sub);
        await db.SaveChangesAsync();

        var dispatched = await service.DispatchOutboundWebhooksAsync(
            tenantId,
            "order.confirmed",
            "{\"orderId\":\"ORD-771\"}"
        );

        dispatched.Should().Be(1);

        var log = await db.WebhookDeliveryLogs.FirstOrDefaultAsync(l => l.SubscriptionId == sub.Id);
        log.Should().NotBeNull();
        log!.EventType.Should().Be("order.confirmed");
        log.IsSuccess.Should().BeTrue();
        log.ResponseStatusCode.Should().Be(200);
    }

    [Fact]
    public async Task DispatchOutboundWebhooksAsync_WhenInactive_ExcludesFromDispatch()
    {
        using var db = CreateDbContext();
        var service = new OutboundWebhookDispatcherService(db);
        var tenantId = Guid.NewGuid();

        var sub = WebhookSubscription.Create(
            tenantId,
            "https://partner-erp.com/webhooks/disabled",
            "whsec_topsecret123",
            "*"
        );
        sub.IsActive = false;
        db.WebhookSubscriptions.Add(sub);
        await db.SaveChangesAsync();

        var dispatched = await service.DispatchOutboundWebhooksAsync(
            tenantId,
            "order.created",
            "{}"
        );

        dispatched.Should().Be(0);
        var logs = await db.WebhookDeliveryLogs.ToListAsync();
        logs.Should().BeEmpty();
    }

    [Fact]
    public async Task DispatchOutboundWebhooksAsync_WhenEventNotSubscribed_ExcludesFromDispatch()
    {
        using var db = CreateDbContext();
        var service = new OutboundWebhookDispatcherService(db);
        var tenantId = Guid.NewGuid();

        var sub = WebhookSubscription.Create(
            tenantId,
            "https://partner-erp.com/webhooks/invoices",
            "whsec_topsecret123",
            "invoice.created,invoice.paid"
        );
        db.WebhookSubscriptions.Add(sub);
        await db.SaveChangesAsync();

        var dispatched = await service.DispatchOutboundWebhooksAsync(
            tenantId,
            "order.cancelled",
            "{}"
        );

        dispatched.Should().Be(0);
    }

    [Fact]
    public async Task DispatchOutboundWebhooksAsync_WhenDifferentTenant_EnforcesTenantBoundary()
    {
        using var db = CreateDbContext();
        var service = new OutboundWebhookDispatcherService(db);
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var subA = WebhookSubscription.Create(
            tenantA,
            "https://tenantA.com/webhook",
            "whsec_secret_A",
            "*"
        );
        db.WebhookSubscriptions.Add(subA);
        await db.SaveChangesAsync();

        var dispatched = await service.DispatchOutboundWebhooksAsync(
            tenantB,
            "customer.created",
            "{}"
        );

        dispatched.Should().Be(0);
    }

    [Fact]
    public async Task DispatchOutboundWebhooksAsync_MultipleSubscriptionsMatch_DispatchesToAll()
    {
        using var db = CreateDbContext();
        var service = new OutboundWebhookDispatcherService(db);
        var tenantId = Guid.NewGuid();

        var sub1 = WebhookSubscription.Create(tenantId, "https://partner1.com/hook", "secret1", "invoice.paid");
        var sub2 = WebhookSubscription.Create(tenantId, "https://partner2.com/hook", "secret2", "invoice.paid");
        db.WebhookSubscriptions.AddRange(sub1, sub2);
        await db.SaveChangesAsync();

        var dispatched = await service.DispatchOutboundWebhooksAsync(tenantId, "invoice.paid", "{\"amount\":150.0}");

        dispatched.Should().Be(2);
        var logs = await db.WebhookDeliveryLogs.Where(l => l.EventType == "invoice.paid").ToListAsync();
        logs.Count.Should().Be(2);
    }

    [Fact]
    public void ComputeHmacSignature_DifferentSecretOrPayload_ProducesDistinctSignatures()
    {
        var sig1 = OutboundWebhookDispatcherService.ComputeHmacSignature("payload-A", "secret-1");
        var sig2 = OutboundWebhookDispatcherService.ComputeHmacSignature("payload-A", "secret-2");
        var sig3 = OutboundWebhookDispatcherService.ComputeHmacSignature("payload-B", "secret-1");

        sig1.Should().NotBe(sig2);
        sig1.Should().NotBe(sig3);
        sig2.Should().NotBe(sig3);
    }
}
