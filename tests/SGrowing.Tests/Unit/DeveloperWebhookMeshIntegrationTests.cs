using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Integrations;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Unit;

public class DeveloperWebhookMeshIntegrationTests
{
    private readonly SGrowingDbContext _db;

    public DeveloperWebhookMeshIntegrationTests()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new SGrowingDbContext(options);
    }

    [Fact]
    public async Task ApiKeys_GenerateAndValidate_UpdatesLastUsedAtUtc()
    {
        var tenantId = Guid.NewGuid();
        var handler = new TestWebhookHandler(HttpStatusCode.OK);
        var factory = new TestHttpClientFactory(handler);
        var service = new DeveloperWebhookService(_db, factory);

        var (keyEntity, plainKey) = await service.GenerateApiKeyAsync(
            tenantId,
            "Production Webhook Ingestion Key",
            new List<string> { "orders:read", "webhooks:manage" },
            isLive: true,
            rateLimit: 300
        );

        keyEntity.Should().NotBeNull();
        plainKey.Should().StartWith("sg_live_");

        var validatedKey = await service.ValidateApiKeyAsync(plainKey);
        validatedKey.Should().NotBeNull();
        validatedKey!.TenantId.Should().Be(tenantId);
        validatedKey.LastUsedAtUtc.Should().NotBeNull();

        // Revoke key
        var revoked = await service.RevokeApiKeyAsync(tenantId, keyEntity.Id);
        revoked.Should().BeTrue();

        var afterRevocation = await service.ValidateApiKeyAsync(plainKey);
        afterRevocation.Should().BeNull();
    }

    [Fact]
    public async Task WebhookDispatch_WithValidSubscriber_ComputesHmacSha256AndTransmitsHeaders()
    {
        var tenantId = Guid.NewGuid();
        var handler = new TestWebhookHandler(HttpStatusCode.OK);
        var factory = new TestHttpClientFactory(handler);
        var service = new DeveloperWebhookService(_db, factory);

        var sub = await service.RegisterWebhookSubscriptionAsync(
            tenantId,
            "https://api.partner.com/webhooks/sgrowing",
            new List<string> { "order.created", "customer.updated" }
        );

        var payload = new { orderId = "ORD-99128", amount = 450.00, currency = "SAR" };

        var logs = await service.DispatchWebhookEventAsync(tenantId, "order.created", payload);

        logs.Should().HaveCount(1);
        var log = logs[0];
        log.IsSuccess.Should().BeTrue();
        log.ResponseStatusCode.Should().Be(200);

        handler.LastRequest.Should().NotBeNull();
        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest!.RequestUri!.ToString().Should().Be("https://api.partner.com/webhooks/sgrowing");

        // Verify headers
        handler.LastRequest.Headers.Contains("X-SGrowing-Signature").Should().BeTrue();
        handler.LastRequest.Headers.Contains("X-SGrowing-Event").Should().BeTrue();
        handler.LastRequest.Headers.Contains("X-SGrowing-Timestamp").Should().BeTrue();
        handler.LastRequest.Headers.Contains("X-SGrowing-Delivery-Id").Should().BeTrue();

        // Validate HMAC signature verification
        var signature = System.Linq.Enumerable.First(handler.LastRequest.Headers.GetValues("X-SGrowing-Signature"));
        var timestamp = System.Linq.Enumerable.First(handler.LastRequest.Headers.GetValues("X-SGrowing-Timestamp"));
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(sub.SecretKey));
        var expectedHash = "sha256=" + Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes($"{timestamp}.{handler.LastRequestBody}"))).ToLowerInvariant();
        signature.Should().Be(expectedHash);
    }

    [Fact]
    public async Task WebhookDispatch_With5ConsecutiveHttpFailures_CircuitBreakerAutoPausesSubscription()
    {
        var tenantId = Guid.NewGuid();
        var failingHandler = new TestWebhookHandler(HttpStatusCode.InternalServerError);
        var factory = new TestHttpClientFactory(failingHandler);
        var service = new DeveloperWebhookService(_db, factory);

        var sub = await service.RegisterWebhookSubscriptionAsync(
            tenantId,
            "https://down.partner.com/webhooks",
            new List<string> { "*" }
        );

        // Pre-condition
        sub.IsActive.Should().BeTrue();
        sub.FailureCount.Should().Be(0);

        // Dispatch 5 failures
        for (int i = 1; i <= 5; i++)
        {
            var logs = await service.DispatchWebhookEventAsync(tenantId, "lead.captured", new { leadId = 1234 });
            logs.Should().HaveCount(1);
            logs[0].IsSuccess.Should().BeFalse();
        }

        // Fetch updated subscription from DB
        var subscriptions = await service.GetWebhookSubscriptionsAsync(tenantId);
        var updatedSub = subscriptions.Find(s => s.Id == sub.Id);
        updatedSub.Should().NotBeNull();
        updatedSub!.IsActive.Should().BeFalse();
        updatedSub.FailureCount.Should().Be(5);

        // Subsequent dispatch should skip inactive subscription
        var subsequentLogs = await service.DispatchWebhookEventAsync(tenantId, "lead.captured", new { leadId = 1234 });
        subsequentLogs.Should().BeEmpty();
    }

    [Fact]
    public async Task WebhookDeadLetter_RetrievesFailedLogsAndAllowsManualRetry()
    {
        var tenantId = Guid.NewGuid();
        var flakilyFailingHandler = new TestWebhookHandler(HttpStatusCode.BadGateway);
        var factory = new TestHttpClientFactory(flakilyFailingHandler);
        var service = new DeveloperWebhookService(_db, factory);

        var sub = await service.RegisterWebhookSubscriptionAsync(
            tenantId,
            "https://flaky.partner.com/receiver",
            new List<string> { "invoice.paid" }
        );
        sub.FailureCount = 4;
        await _db.SaveChangesAsync();

        await service.DispatchWebhookEventAsync(tenantId, "invoice.paid", new { invoiceId = "INV-100" });

        var dlq = await service.GetDeadLetterLogsAsync(tenantId);
        dlq.Should().HaveCount(1);
        dlq[0].IsSuccess.Should().BeFalse();
        dlq[0].ResponseStatusCode.Should().Be(502);

        // Now target service recovers
        flakilyFailingHandler.ConfiguredStatusCode = HttpStatusCode.OK;

        var retriedLog = await service.RetryDeliveryAsync(tenantId, dlq[0].Id);
        retriedLog.Should().NotBeNull();
        retriedLog!.IsSuccess.Should().BeTrue();
        retriedLog.ResponseStatusCode.Should().Be(200);
        retriedLog.AttemptNumber.Should().Be(1);

        // Verify DLQ is now cleared of that failure
        var clearedDlq = await service.GetDeadLetterLogsAsync(tenantId);
        clearedDlq.Should().BeEmpty();
    }

    private class TestWebhookHandler : HttpMessageHandler
    {
        public HttpStatusCode ConfiguredStatusCode { get; set; }
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastRequestBody { get; private set; }

        public TestWebhookHandler(HttpStatusCode initialStatus)
        {
            ConfiguredStatusCode = initialStatus;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            if (request.Content != null)
            {
                LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            return new HttpResponseMessage(ConfiguredStatusCode)
            {
                Content = new StringContent("{\"status\":\"recorded\"}", Encoding.UTF8, "application/json")
            };
        }
    }

    private class TestHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;

        public TestHttpClientFactory(HttpMessageHandler handler)
        {
            _handler = handler;
        }

        public HttpClient CreateClient(string name)
        {
            return new HttpClient(_handler, disposeHandler: false);
        }
    }
}
