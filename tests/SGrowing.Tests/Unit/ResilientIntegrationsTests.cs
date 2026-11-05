using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using SGrowing.Api.Common.Outbox;
using SGrowing.Api.Common.Resilience;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Attribution;
using SGrowing.Api.Modules.Commerce;
using SGrowing.Api.Modules.Voice.Streaming;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Unit;

public class ResilientIntegrationsTests
{
    private SGrowingDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public void ResilienceExtensions_AddGrowthOsStandardResilience_RegistersResilienceHandler()
    {
        var services = new ServiceCollection();
        services.AddHttpClient("TestClient")
            .AddGrowthOsStandardResilience(
                attemptTimeout: TimeSpan.FromSeconds(2),
                totalRequestTimeout: TimeSpan.FromSeconds(10),
                maxRetries: 2);

        var provider = services.BuildServiceProvider();
        var clientFactory = provider.GetRequiredService<System.Net.Http.IHttpClientFactory>();
        var client = clientFactory.CreateClient("TestClient");

        client.Should().NotBeNull();
    }

    [Fact]
    public async Task ResilientCapiDispatcher_WhenCircuitIsClosedAndSucceeds_DispatchesImmediately()
    {
        var fanoutMock = new Mock<IServerSideCapiFanoutService>();
        var outboxMock = new Mock<IOutboxService>();

        fanoutMock.Setup(f => f.GetCircuitBreakerState(It.IsAny<CapiPlatform>()))
            .Returns(CircuitState.Closed);

        fanoutMock.Setup(f => f.DispatchAsync(It.IsAny<CapiDispatchEventRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CapiFanoutResult(
                EventId: "evt_123",
                TenantId: Guid.NewGuid(),
                EventName: "Purchase",
                AllSucceeded: true,
                PlatformOutcomes: new List<PlatformDispatchOutcome>(),
                DispatchedAtUtc: DateTime.UtcNow));

        var dispatcher = new ResilientCapiDispatcher(fanoutMock.Object, outboxMock.Object);

        var request = new CapiDispatchEventRequest(
            TenantId: Guid.NewGuid(),
            EventName: "Purchase",
            Value: 150m,
            Currency: "USD");

        var result = await dispatcher.EnqueueOrDispatchAsync(request);

        result.DispatchedImmediately.Should().BeTrue();
        result.EnqueuedToOutbox.Should().BeFalse();
        outboxMock.Verify(o => o.EnqueueAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResilientCapiDispatcher_WhenCircuitIsOpen_EnqueuesToOutboxDirectly()
    {
        var fanoutMock = new Mock<IServerSideCapiFanoutService>();
        var outboxMock = new Mock<IOutboxService>();

        fanoutMock.Setup(f => f.GetCircuitBreakerState(CapiPlatform.Meta))
            .Returns(CircuitState.Open);

        outboxMock.Setup(o => o.EnqueueAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OutboxMessageEntity { Id = Guid.NewGuid() });

        var dispatcher = new ResilientCapiDispatcher(fanoutMock.Object, outboxMock.Object);

        var request = new CapiDispatchEventRequest(
            TenantId: Guid.NewGuid(),
            EventName: "Purchase",
            TargetPlatforms: new List<CapiPlatform> { CapiPlatform.Meta });

        var result = await dispatcher.EnqueueOrDispatchAsync(request);

        result.DispatchedImmediately.Should().BeFalse();
        result.EnqueuedToOutbox.Should().BeTrue();
        fanoutMock.Verify(f => f.DispatchAsync(It.IsAny<CapiDispatchEventRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        outboxMock.Verify(o => o.EnqueueAsync(It.IsAny<Guid>(), "Advertising.CapiConversion.Queued", It.IsAny<object>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResilientStripeWebhookProcessor_ValidSignature_EnqueuesToOutbox()
    {
        using var db = CreateInMemoryDb();
        var outboxService = new OutboxService(db);
        var webhookSecret = "whsec_test_secret_key_1234567890";
        var tenantId = Guid.NewGuid();

        var payload = JsonSerializer.Serialize(new
        {
            id = "evt_stripe_test_001",
            type = "checkout.session.completed",
            data = new
            {
                @object = new
                {
                    amount_total = 25000,
                    metadata = new { order_id = "ORD-999" }
                }
            }
        });

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signedPayload = $"{timestamp}.{payload}";
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(webhookSecret), Encoding.UTF8.GetBytes(signedPayload));
        var sigHeader = $"t={timestamp},v1={Convert.ToHexString(hash).ToLowerInvariant()}";

        var inMemoryConfig = new Dictionary<string, string?>
        {
            ["Commerce:Stripe:WebhookSecret"] = webhookSecret
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemoryConfig).Build();

        var processor = new ResilientStripeWebhookProcessor(db, outboxService, config);

        var result = await processor.ProcessWebhookAsync(tenantId, payload, sigHeader);

        result.Success.Should().BeTrue();
        result.EventType.Should().Be("checkout.session.completed");
        result.OrderId.Should().Be("ORD-999");
        result.AmountReceived.Should().Be(250.00m);
        result.OutboxMessageId.Should().NotBeNull();

        var outboxMsg = await db.TransactionalOutboxMessages.FirstOrDefaultAsync(m => m.Id == result.OutboxMessageId);
        outboxMsg.Should().NotBeNull();
        outboxMsg!.EventType.Should().Be("Stripe.checkout.session.completed");
    }

    [Fact]
    public async Task ResilientStripeWebhookProcessor_InvalidSignature_RejectsWithFailure()
    {
        using var db = CreateInMemoryDb();
        var outboxService = new OutboxService(db);
        var webhookSecret = "whsec_correct_secret";
        var tenantId = Guid.NewGuid();

        var payload = "{\"id\":\"evt_123\",\"type\":\"checkout.session.completed\"}";
        var sigHeader = "t=1600000000,v1=bad_signature_hash";

        var inMemoryConfig = new Dictionary<string, string?>
        {
            ["Commerce:Stripe:WebhookSecret"] = webhookSecret
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemoryConfig).Build();

        var processor = new ResilientStripeWebhookProcessor(db, outboxService, config);

        var result = await processor.ProcessWebhookAsync(tenantId, payload, sigHeader);

        result.Success.Should().BeFalse();
        result.FailureReason.Should().Contain("signature mismatch");
    }

    [Fact]
    public void WebRtcAudioBridgeService_ReordersPacketsAndCalculatesJitter()
    {
        var bridge = new WebRtcAudioBridgeService();
        var sessionId = Guid.NewGuid();

        var now = DateTime.UtcNow;

        // Ingest packets out of order: 2, 0, 1
        bridge.IngestPacket(sessionId, new RtpAudioPacket(SequenceNumber: 2, Timestamp: 320, Payload: new byte[] { 3 }, ReceivedAtUtc: now.AddMilliseconds(20)));
        bridge.IngestPacket(sessionId, new RtpAudioPacket(SequenceNumber: 0, Timestamp: 0, Payload: new byte[] { 1 }, ReceivedAtUtc: now));
        bridge.IngestPacket(sessionId, new RtpAudioPacket(SequenceNumber: 1, Timestamp: 160, Payload: new byte[] { 2 }, ReceivedAtUtc: now.AddMilliseconds(10)));

        var playable = bridge.DrainPlayablePackets(sessionId, maxPackets: 5);

        playable.Should().HaveCount(3);
        playable[0].SequenceNumber.Should().Be(0);
        playable[1].SequenceNumber.Should().Be(1);
        playable[2].SequenceNumber.Should().Be(2);

        var stats = bridge.GetStats(sessionId);
        stats.PacketsReceived.Should().Be(3);
        stats.PacketsDelivered.Should().Be(3);
        stats.PacketsDroppedLate.Should().Be(0);
    }

    [Fact]
    public void WebRtcAudioBridgeService_TriggersSipFallbackOnExcessiveLoss()
    {
        var bridge = new WebRtcAudioBridgeService();
        var sessionId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        // Establish sequence 100
        bridge.IngestPacket(sessionId, new RtpAudioPacket(100, 0, new byte[] { 1 }, now));
        bridge.DrainPlayablePackets(sessionId);

        // Ingest 25 late packets (sequence < 100), simulating severe out-of-order/retransmission drop
        for (uint i = 1; i <= 25; i++)
        {
            bridge.IngestPacket(sessionId, new RtpAudioPacket(i, 0, new byte[] { 1 }, now));
        }

        var shouldFallback = bridge.ShouldFallbackToSipTrunk(sessionId, lossThreshold: 0.15);
        shouldFallback.Should().BeTrue();

        var stats = bridge.GetStats(sessionId);
        stats.FallbackToSipTriggered.Should().BeTrue();
        stats.PacketLossRatio.Should().BeGreaterThan(0.5);
    }
}
