using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Integrations;
using SGrowing.Core.Entities;
using SGrowing.Worker.Services;
using Xunit;

namespace SGrowing.Tests.Integrations;

public class OutwardWebhookDeliveryTests
{
    private readonly SGrowingDbContext _db;
    private readonly DeveloperWebhookService _service;

    public OutwardWebhookDeliveryTests()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new SGrowingDbContext(options);
        _service = new DeveloperWebhookService(_db);
    }

    [Fact]
    public void HmacSignature_ComputesAndValidatesSha256Signature()
    {
        var secret = "whsec_test_secret_key_1234567890";
        var timestamp = 1793112800L;
        var payload = JsonSerializer.Serialize(new { eventType = "deal.won", dealId = Guid.NewGuid(), amount = 15000 });

        var signature = OutwardWebhookDeliveryDaemon.ComputeHmacSignature(payload, secret, timestamp);

        signature.Should().StartWith("sha256=");
        signature.Length.Should().Be(71); // "sha256=" (7) + 64 hex chars

        // Valid signature verification
        var isValid = OutwardWebhookDeliveryDaemon.VerifyHmacSignature(payload, signature, secret, timestamp);
        isValid.Should().BeTrue();

        // Tampered payload verification
        var tampered = payload + " ";
        var isTamperedValid = OutwardWebhookDeliveryDaemon.VerifyHmacSignature(tampered, signature, secret, timestamp);
        isTamperedValid.Should().BeFalse();

        // Wrong secret verification
        var isWrongSecretValid = OutwardWebhookDeliveryDaemon.VerifyHmacSignature(payload, signature, "whsec_wrong_key", timestamp);
        isWrongSecretValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(1, 1)] // 1 min
    [InlineData(2, 5)] // 5 min
    [InlineData(3, 15)] // 15 min
    [InlineData(4, 60)] // 1 hour
    [InlineData(5, 360)] // 6 hours
    public void CalculateNextBackoff_ReturnsExactExponentialSchedule(int attempt, int expectedMinutes)
    {
        var backoff = OutwardWebhookDeliveryDaemon.CalculateNextBackoff(attempt);
        backoff.TotalMinutes.Should().Be(expectedMinutes);
    }

    [Fact]
    public async Task RegisterSubscription_AndDispatchWebhook_GeneratesSignedDeliveryLog()
    {
        var tenantId = Guid.NewGuid();

        // 1. Register subscription for deal.won and contract.signed
        var sub = await _service.RegisterWebhookSubscriptionAsync(
            tenantId,
            "https://merchant-platform.io/events",
            ["deal.won", "contract.signed"]);

        sub.Should().NotBeNull();
        sub.IsActive.Should().BeTrue();
        sub.SecretKey.Should().StartWith("whsec_");

        // 2. Dispatch deal.won event
        var payload = new { dealId = Guid.NewGuid(), customer = "Maghreb Retail", value = 18000m };
        var logs = await _service.DispatchWebhookEventAsync(tenantId, "deal.won", payload);

        logs.Should().HaveCount(1);
        var log = logs[0];

        log.EventType.Should().Be("deal.won");
        log.SubscriptionId.Should().Be(sub.Id);
        log.ResponseStatusCode.Should().Be(200);
        log.IsSuccess.Should().BeTrue();
        log.SignatureHeader.Should().StartWith("sha256=");
        log.TimestampHeader.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task DeadLetterQueue_QueriesAndRetriesDeadLetterEvents()
    {
        var tenantId = Guid.NewGuid();

        var sub = await _service.RegisterWebhookSubscriptionAsync(
            tenantId,
            "https://failing-endpoint.com/webhook",
            ["order.delivered"]);

        // Simulate exhausted retries -> DLQ record
        var deadLetterLog = WebhookDeliveryAttemptLog.Create(
            tenantId,
            sub.Id,
            "order.delivered",
            JsonSerializer.Serialize(new { orderId = "ord-99" }),
            statusCode: 504,
            isSuccess: false,
            durationMs: 5000,
            attemptNumber: 5,
            maxRetries: 5
        );
        deadLetterLog.IsDeadLetter = true;
        deadLetterLog.DeadLetterReason = "Exceeded 5 retry attempts (504 Gateway Timeout)";

        _db.WebhookDeliveryAttemptLogs.Add(deadLetterLog);
        await _db.SaveChangesAsync();

        // Query DLQ
        var dlqList = await _service.GetDeadLetterLogsAsync(tenantId);
        dlqList.Should().HaveCount(1);
        dlqList[0].IsDeadLetter.Should().BeTrue();
        dlqList[0].DeadLetterReason.Should().Contain("504 Gateway Timeout");

        // Retry DLQ event
        var retryResult = await _service.RetryDeliveryAsync(tenantId, deadLetterLog.Id);
        retryResult.Should().NotBeNull();
        retryResult!.IsSuccess.Should().BeTrue();
        retryResult.AttemptNumber.Should().Be(1);
        retryResult.SignatureHeader.Should().StartWith("sha256=");

        // DLQ should now be empty of the retried item
        var dlqAfterRetry = await _service.GetDeadLetterLogsAsync(tenantId);
        dlqAfterRetry.Should().BeEmpty();
    }
}
