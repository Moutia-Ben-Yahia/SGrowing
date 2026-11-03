using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using SGrowing.Api.Data;
using SGrowing.Core.Messaging;
using SGrowing.Worker.Services;
using SGrowing.Worker.Services.Dispatchers;
using Xunit;

namespace SGrowing.Tests.Worker;

public class RedisStreamConsumerDaemonTests
{
    private (IServiceProvider Provider, string DbName) CreateServiceProvider(
        ISmtpEmailDispatcher? emailDispatcher = null,
        ITwilioSmsDispatcher? smsDispatcher = null,
        IMetaWhatsAppDispatcher? whatsAppDispatcher = null)
    {
        var services = new ServiceCollection();
        var dbName = Guid.NewGuid().ToString();

        services.AddDbContext<SGrowingDbContext>(options =>
            options.UseInMemoryDatabase(dbName));

        if (emailDispatcher != null) services.AddSingleton(emailDispatcher);
        if (smsDispatcher != null) services.AddSingleton(smsDispatcher);
        if (whatsAppDispatcher != null) services.AddSingleton(whatsAppDispatcher);

        services.AddLogging();
        var provider = services.BuildServiceProvider();

        return (provider, dbName);
    }

    private SGrowingDbContext GetDbContext(IServiceProvider provider)
    {
        var scope = provider.CreateScope();
        return scope.ServiceProvider.GetRequiredService<SGrowingDbContext>();
    }

    [Fact]
    public async Task ProcessStreamBatchAsync_WhenNoMessages_ReturnsZero()
    {
        var (provider, _) = CreateServiceProvider();
        var mockBroker = new Mock<IRedisStreamBroker>();
        mockBroker
            .Setup(b => b.ReadGroupMessagesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(new List<StreamMessageEnvelope>());

        var mockLogger = new Mock<ILogger<RedisStreamConsumerDaemon>>();
        var daemon = new RedisStreamConsumerDaemon(provider, mockLogger.Object);

        var result = await daemon.ProcessStreamBatchAsync(mockBroker.Object);

        result.Should().Be(0);
        mockBroker.Verify(b => b.AcknowledgeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ProcessStreamBatchAsync_WhenEmailEvent_CallsEmailDispatcherAndAcknowledges()
    {
        var mockEmail = new Mock<ISmtpEmailDispatcher>();
        mockEmail
            .Setup(e => e.SendEmailAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmailDispatchResult(true, "email-101", null));

        var (provider, _) = CreateServiceProvider(emailDispatcher: mockEmail.Object);

        var tenantId = Guid.NewGuid();
        var payload = JsonSerializer.Serialize(new
        {
            recipientEmail = "prospect@acme.com",
            subject = "Follow-up",
            body = "Thank you for connecting."
        });

        var envelope = new StreamMessageEnvelope(
            "msg-001",
            "sgrowing:stream:outbox",
            "EmailMessageSendRequested",
            tenantId,
            payload,
            DateTime.UtcNow);

        var mockBroker = new Mock<IRedisStreamBroker>();
        mockBroker
            .Setup(b => b.ReadGroupMessagesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(new List<StreamMessageEnvelope> { envelope });

        var mockLogger = new Mock<ILogger<RedisStreamConsumerDaemon>>();
        var daemon = new RedisStreamConsumerDaemon(provider, mockLogger.Object);

        var count = await daemon.ProcessStreamBatchAsync(mockBroker.Object);

        count.Should().Be(1);

        mockEmail.Verify(e => e.SendEmailAsync(
            tenantId,
            "prospect@acme.com",
            "Follow-up",
            "Thank you for connecting.",
            null,
            It.IsAny<CancellationToken>()), Times.Once);

        mockBroker.Verify(b => b.AcknowledgeAsync("sgrowing:stream:outbox", "sgrowing-worker-group", "msg-001"), Times.Once);
    }

    [Fact]
    public async Task ProcessStreamBatchAsync_WhenSmsEvent_CallsSmsDispatcherAndAcknowledges()
    {
        var mockSms = new Mock<ITwilioSmsDispatcher>();
        mockSms
            .Setup(s => s.SendSmsAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SmsDispatchResult(true, "sms-sid-555", null));

        var (provider, _) = CreateServiceProvider(smsDispatcher: mockSms.Object);

        var tenantId = Guid.NewGuid();
        var payload = JsonSerializer.Serialize(new
        {
            recipientPhone = "+15551234567",
            body = "Your verification code is 4492."
        });

        var envelope = new StreamMessageEnvelope(
            "msg-002",
            "sgrowing:stream:outbox",
            "SmsMessageSendRequested",
            tenantId,
            payload,
            DateTime.UtcNow);

        var mockBroker = new Mock<IRedisStreamBroker>();
        mockBroker
            .Setup(b => b.ReadGroupMessagesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(new List<StreamMessageEnvelope> { envelope });

        var mockLogger = new Mock<ILogger<RedisStreamConsumerDaemon>>();
        var daemon = new RedisStreamConsumerDaemon(provider, mockLogger.Object);

        var count = await daemon.ProcessStreamBatchAsync(mockBroker.Object);

        count.Should().Be(1);

        mockSms.Verify(s => s.SendSmsAsync(
            tenantId,
            "+15551234567",
            "Your verification code is 4492.",
            null,
            It.IsAny<CancellationToken>()), Times.Once);

        mockBroker.Verify(b => b.AcknowledgeAsync("sgrowing:stream:outbox", "sgrowing-worker-group", "msg-002"), Times.Once);
    }

    [Fact]
    public async Task ProcessStreamBatchAsync_WhenWhatsAppTemplateEvent_CallsWhatsAppDispatcherAndAcknowledges()
    {
        var mockWhatsApp = new Mock<IMetaWhatsAppDispatcher>();
        mockWhatsApp
            .Setup(w => w.SendTemplateMessageAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WhatsAppDispatchResult(true, "wa-msg-888", null));

        var (provider, _) = CreateServiceProvider(whatsAppDispatcher: mockWhatsApp.Object);

        var tenantId = Guid.NewGuid();
        var payload = JsonSerializer.Serialize(new
        {
            recipientPhone = "+213555987654",
            templateName = "order_confirmed",
            languageCode = "ar_DZ"
        });

        var envelope = new StreamMessageEnvelope(
            "msg-003",
            "sgrowing:stream:outbox",
            "WhatsApp.Template.Send",
            tenantId,
            payload,
            DateTime.UtcNow);

        var mockBroker = new Mock<IRedisStreamBroker>();
        mockBroker
            .Setup(b => b.ReadGroupMessagesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(new List<StreamMessageEnvelope> { envelope });

        var mockLogger = new Mock<ILogger<RedisStreamConsumerDaemon>>();
        var daemon = new RedisStreamConsumerDaemon(provider, mockLogger.Object);

        var count = await daemon.ProcessStreamBatchAsync(mockBroker.Object);

        count.Should().Be(1);

        mockWhatsApp.Verify(w => w.SendTemplateMessageAsync(
            tenantId,
            "+213555987654",
            "order_confirmed",
            "ar_DZ",
            null,
            It.IsAny<CancellationToken>()), Times.Once);

        mockBroker.Verify(b => b.AcknowledgeAsync("sgrowing:stream:outbox", "sgrowing-worker-group", "msg-003"), Times.Once);
    }

    [Fact]
    public async Task ProcessStreamBatchAsync_WhenDispatcherFails_RecordsDeadLetterAndStillAcknowledges()
    {
        var mockEmail = new Mock<ISmtpEmailDispatcher>();
        mockEmail
            .Setup(e => e.SendEmailAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmailDispatchResult(false, null, "SMTP server connection refused"));

        var (provider, _) = CreateServiceProvider(emailDispatcher: mockEmail.Object);

        var tenantId = Guid.NewGuid();
        var payload = JsonSerializer.Serialize(new
        {
            recipientEmail = "bounce@invalid.com",
            subject = "Alert",
            body = "Important notice"
        });

        var envelope = new StreamMessageEnvelope(
            "msg-004",
            "sgrowing:stream:outbox",
            "EmailMessageSendRequested",
            tenantId,
            payload,
            DateTime.UtcNow);

        var mockBroker = new Mock<IRedisStreamBroker>();
        mockBroker
            .Setup(b => b.ReadGroupMessagesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(new List<StreamMessageEnvelope> { envelope });

        var mockLogger = new Mock<ILogger<RedisStreamConsumerDaemon>>();
        var daemon = new RedisStreamConsumerDaemon(provider, mockLogger.Object);

        var count = await daemon.ProcessStreamBatchAsync(mockBroker.Object);

        count.Should().Be(1);

        // Verify DLQ log saved to database
        using (var db = GetDbContext(provider))
        {
            var dlq = await db.WorkflowDeadLetterLogs.FirstOrDefaultAsync(l => l.FailedNodeId == "msg-004");
            dlq.Should().NotBeNull();
            dlq!.TenantId.Should().Be(tenantId);
            dlq.ErrorMessage.Should().Contain("SMTP server connection refused");
            dlq.FailedNodeType.Should().Be("EmailMessageSendRequested");
        }

        // Verify still acknowledged so Redis group doesn't jam
        mockBroker.Verify(b => b.AcknowledgeAsync("sgrowing:stream:outbox", "sgrowing-worker-group", "msg-004"), Times.Once);
    }

    [Fact]
    public async Task ProcessStreamBatchAsync_WhenGenericEventType_AcknowledgesWithoutError()
    {
        var (provider, _) = CreateServiceProvider();
        var tenantId = Guid.NewGuid();
        var envelope = new StreamMessageEnvelope(
            "msg-005",
            "sgrowing:stream:outbox",
            "CustomerProfileUpdated",
            tenantId,
            "{}",
            DateTime.UtcNow);

        var mockBroker = new Mock<IRedisStreamBroker>();
        mockBroker
            .Setup(b => b.ReadGroupMessagesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(new List<StreamMessageEnvelope> { envelope });

        var mockLogger = new Mock<ILogger<RedisStreamConsumerDaemon>>();
        var daemon = new RedisStreamConsumerDaemon(provider, mockLogger.Object);

        var count = await daemon.ProcessStreamBatchAsync(mockBroker.Object);

        count.Should().Be(1);
        mockBroker.Verify(b => b.AcknowledgeAsync("sgrowing:stream:outbox", "sgrowing-worker-group", "msg-005"), Times.Once);
    }
}
