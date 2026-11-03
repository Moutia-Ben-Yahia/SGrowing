using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Moq.Protected;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;
using SGrowing.Worker.Services;
using Xunit;

namespace SGrowing.Tests.Worker;

public class OutwardWebhookDeliveryDaemonTests
{
    private static (IServiceProvider Provider, SGrowingDbContext Db) SetupDependencies()
    {
        var services = new ServiceCollection();
        var dbName = $"OutwardWebhookDaemonTests_{Guid.NewGuid()}";
        services.AddDbContext<SGrowingDbContext>(options =>
            options.UseInMemoryDatabase(dbName));

        var sp = services.BuildServiceProvider();
        var db = sp.GetRequiredService<SGrowingDbContext>();
        return (sp, db);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 5)]
    [InlineData(3, 15)]
    [InlineData(4, 60)]
    [InlineData(5, 360)]
    public void CalculateNextBackoff_ReturnsExponentialTiming(int attempt, int expectedMinutes)
    {
        var delay = OutwardWebhookDeliveryDaemon.CalculateNextBackoff(attempt);
        delay.TotalMinutes.Should().Be(expectedMinutes);
    }

    [Fact]
    public void ComputeAndVerifyHmacSignature_ValidatesAuthenticityAndDetectsTampering()
    {
        var payload = "{\"orderId\":\"ORD-2026-99\"}";
        var secret = "whsec_supersecretkey123456789";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var signature = OutwardWebhookDeliveryDaemon.ComputeHmacSignature(payload, secret, timestamp);

        signature.Should().StartWith("sha256=");

        var isValid = OutwardWebhookDeliveryDaemon.VerifyHmacSignature(payload, signature, secret, timestamp);
        isValid.Should().BeTrue();

        var isTamperedPayload = OutwardWebhookDeliveryDaemon.VerifyHmacSignature("{\"orderId\":\"TAMPERED\"}", signature, secret, timestamp);
        isTamperedPayload.Should().BeFalse();

        var isTamperedSecret = OutwardWebhookDeliveryDaemon.VerifyHmacSignature(payload, signature, "whsec_wrongsecret", timestamp);
        isTamperedSecret.Should().BeFalse();
    }

    [Fact]
    public async Task QueueOutwardEventAsync_WhenActiveSubscriptionsMatch_EnqueuesDeliveryLogs()
    {
        var (_, db) = SetupDependencies();
        using var scope = db;
        var tenantId = Guid.NewGuid();

        var sub1 = WebhookSubscriptionRecord.Create(
            tenantId,
            "https://merchant.tn/webhooks/orders",
            new List<string> { "order.delivered", "fraud.flagged" }
        );

        var sub2 = WebhookSubscriptionRecord.Create(
            tenantId,
            "https://merchant.tn/webhooks/crm",
            new List<string> { "deal.won" }
        );

        db.WebhookSubscriptionRecords.AddRange(sub1, sub2);
        await db.SaveChangesAsync();

        var logs = await OutwardWebhookDeliveryDaemon.QueueOutwardEventAsync(
            db,
            tenantId,
            "order.delivered",
            new { orderId = "ORD-4001", amount = 199.50 }
        );

        logs.Should().HaveCount(1);
        logs[0].SubscriptionId.Should().Be(sub1.Id);
        logs[0].EventType.Should().Be("order.delivered");
        logs[0].SignatureHeader.Should().StartWith("sha256=");
        logs[0].IsSuccess.Should().BeFalse();
        logs[0].IsDeadLetter.Should().BeFalse();

        (await db.WebhookDeliveryAttemptLogs.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task ProcessPendingWebhookDeliveriesAsync_WhenSubscriptionInactive_MarksDeadLetter()
    {
        var (_, db) = SetupDependencies();
        using var scope = db;
        var tenantId = Guid.NewGuid();

        var sub = WebhookSubscriptionRecord.Create(
            tenantId,
            "https://merchant.tn/webhooks/deleted",
            new List<string> { "*" }
        );
        sub.IsActive = false; // Disabled
        db.WebhookSubscriptionRecords.Add(sub);

        var log = WebhookDeliveryAttemptLog.Create(
            tenantId,
            sub.Id,
            "contact.created",
            "{}",
            statusCode: 0,
            isSuccess: false,
            durationMs: 0,
            attemptNumber: 1,
            maxRetries: 5,
            signature: "sha256=abc",
            timestamp: DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        );
        log.NextAttemptAtUtc = DateTime.UtcNow.AddMinutes(-5);
        db.WebhookDeliveryAttemptLogs.Add(log);
        await db.SaveChangesAsync();

        var processed = await OutwardWebhookDeliveryDaemon.ProcessPendingWebhookDeliveriesAsync(db);

        processed.Should().Be(1);
        var updated = await db.WebhookDeliveryAttemptLogs.FirstAsync(l => l.Id == log.Id);
        updated.IsDeadLetter.Should().BeTrue();
        updated.DeadLetterReason.Should().Contain("disabled or deleted");
    }

    [Fact]
    public async Task ProcessPendingWebhookDeliveriesAsync_WhenHttpSucceeds_MarksSuccess()
    {
        var (_, db) = SetupDependencies();
        using var scope = db;
        var tenantId = Guid.NewGuid();

        var sub = WebhookSubscriptionRecord.Create(
            tenantId,
            "https://merchant.tn/webhooks/orders",
            new List<string> { "order.delivered" }
        );
        db.WebhookSubscriptionRecords.Add(sub);

        var log = WebhookDeliveryAttemptLog.Create(
            tenantId,
            sub.Id,
            "order.delivered",
            "{\"status\":\"delivered\"}",
            statusCode: 0,
            isSuccess: false,
            durationMs: 0,
            attemptNumber: 1,
            maxRetries: 5,
            signature: "sha256=test",
            timestamp: DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        );
        log.NextAttemptAtUtc = DateTime.UtcNow.AddMinutes(-1);
        db.WebhookDeliveryAttemptLogs.Add(log);
        await db.SaveChangesAsync();

        // Mock HttpClient 200 OK
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"received\":true}")
            });

        using var client = new HttpClient(handlerMock.Object);

        var processed = await OutwardWebhookDeliveryDaemon.ProcessPendingWebhookDeliveriesAsync(db, client);

        processed.Should().Be(1);
        var updated = await db.WebhookDeliveryAttemptLogs.FirstAsync(l => l.Id == log.Id);
        updated.IsSuccess.Should().BeTrue();
        updated.ResponseStatusCode.Should().Be(200);
        updated.NextAttemptAtUtc.Should().BeNull();
        updated.IsDeadLetter.Should().BeFalse();
    }
}
