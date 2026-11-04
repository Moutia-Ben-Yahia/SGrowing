using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SGrowing.Api.Data;
using SGrowing.Core.Messaging;
using SGrowing.Worker.Services;
using SGrowing.Worker.Services.Dispatchers;
using Xunit;

namespace SGrowing.Tests.Worker;

public class OutboundDispatcherTests
{
    private readonly IConfiguration _emptyConfig;

    public OutboundDispatcherTests()
    {
        _emptyConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();
    }

    [Fact]
    public async Task SmtpEmailDispatcher_WithoutCredentials_ReturnsSimulatedSuccess()
    {
        using var httpClient = new HttpClient();
        var dispatcher = new SmtpEmailDispatcher(httpClient, _emptyConfig, NullLogger<SmtpEmailDispatcher>.Instance);

        var result = await dispatcher.SendEmailAsync(
            tenantId: Guid.NewGuid(),
            recipientEmail: "test@example.com",
            subject: "Welcome",
            body: "<p>Hello World</p>");

        result.IsSuccess.Should().BeTrue();
        result.IsSimulated.Should().BeTrue();
        result.MessageId.Should().StartWith("sim_email_");
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task SmtpEmailDispatcher_InvalidEmail_ReturnsFailure()
    {
        using var httpClient = new HttpClient();
        var dispatcher = new SmtpEmailDispatcher(httpClient, _emptyConfig, NullLogger<SmtpEmailDispatcher>.Instance);

        var result = await dispatcher.SendEmailAsync(
            tenantId: Guid.NewGuid(),
            recipientEmail: "not-a-valid-email",
            subject: "Welcome",
            body: "<p>Hello</p>");

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Invalid or missing");
    }

    [Fact]
    public async Task TwilioSmsDispatcher_WithoutCredentials_ReturnsSimulatedSuccess()
    {
        using var httpClient = new HttpClient();
        var dispatcher = new TwilioSmsDispatcher(httpClient, _emptyConfig, NullLogger<TwilioSmsDispatcher>.Instance);

        var result = await dispatcher.SendSmsAsync(
            tenantId: Guid.NewGuid(),
            recipientPhone: "+15551234567",
            message: "Your verification code is 123456");

        result.IsSuccess.Should().BeTrue();
        result.IsSimulated.Should().BeTrue();
        result.MessageSid.Should().StartWith("SM");
    }

    [Fact]
    public async Task TwilioSmsDispatcher_MissingPhone_ReturnsFailure()
    {
        using var httpClient = new HttpClient();
        var dispatcher = new TwilioSmsDispatcher(httpClient, _emptyConfig, NullLogger<TwilioSmsDispatcher>.Instance);

        var result = await dispatcher.SendSmsAsync(
            tenantId: Guid.NewGuid(),
            recipientPhone: "",
            message: "Hello");

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Missing recipient phone");
    }

    [Fact]
    public async Task MetaWhatsAppDispatcher_WithoutCredentials_ReturnsSimulatedSuccess()
    {
        using var httpClient = new HttpClient();
        var dispatcher = new MetaWhatsAppDispatcher(httpClient, _emptyConfig, NullLogger<MetaWhatsAppDispatcher>.Instance);

        var textResult = await dispatcher.SendTextMessageAsync(
            tenantId: Guid.NewGuid(),
            recipientPhone: "+15559876543",
            message: "Hello from SGrowing WhatsApp");

        textResult.IsSuccess.Should().BeTrue();
        textResult.IsSimulated.Should().BeTrue();
        textResult.MessageId.Should().StartWith("wamid.");

        var templateResult = await dispatcher.SendTemplateMessageAsync(
            tenantId: Guid.NewGuid(),
            recipientPhone: "+15559876543",
            templateName: "order_confirmation",
            languageCode: "en_US",
            parameters: new Dictionary<string, string> { { "1", "John" } });

        templateResult.IsSuccess.Should().BeTrue();
        templateResult.IsSimulated.Should().BeTrue();
        templateResult.MessageId.Should().StartWith("wamid.");
    }

    [Fact]
    public async Task MetaWhatsAppDispatcher_MissingPhone_ReturnsFailure()
    {
        using var httpClient = new HttpClient();
        var dispatcher = new MetaWhatsAppDispatcher(httpClient, _emptyConfig, NullLogger<MetaWhatsAppDispatcher>.Instance);

        var result = await dispatcher.SendTextMessageAsync(
            tenantId: Guid.NewGuid(),
            recipientPhone: "",
            message: "Hello WhatsApp");

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Missing recipient phone number");
    }

    [Fact]
    public async Task RedisStreamConsumerDaemon_ProcessesOutboundMessageDispatched_AndRoutesCorrectly()
    {
        var services = new ServiceCollection();
        var dbName = Guid.NewGuid().ToString();
        services.AddDbContext<SGrowingDbContext>(options => options.UseInMemoryDatabase(dbName));
        services.AddSingleton<IConfiguration>(_emptyConfig);
        services.AddHttpClient();
        services.AddHttpClient<ISmtpEmailDispatcher, SmtpEmailDispatcher>();
        services.AddHttpClient<ITwilioSmsDispatcher, TwilioSmsDispatcher>();
        services.AddHttpClient<IMetaWhatsAppDispatcher, MetaWhatsAppDispatcher>();

        var sp = services.BuildServiceProvider();
        var mockBroker = new TestRedisStreamBroker();

        var tenantId = Guid.NewGuid();
        var emailPayload = JsonSerializer.Serialize(new
        {
            RecipientEmail = "prospect@example.com",
            Channel = "Email",
            Subject = "Demo Request",
            Body = "Thanks for reaching out"
        });

        var smsPayload = JsonSerializer.Serialize(new
        {
            RecipientPhone = "+15551234567",
            Channel = "Sms",
            Body = "Your meeting is scheduled"
        });

        var waPayload = JsonSerializer.Serialize(new
        {
            RecipientPhone = "+15559998877",
            Channel = "WhatsApp",
            Body = "Welcome to WhatsApp support"
        });

        await mockBroker.PublishEventAsync("sgrowing:stream:outbox", "OutboundMessageDispatched", emailPayload, tenantId);
        await mockBroker.PublishEventAsync("sgrowing:stream:outbox", "OutboundMessageDispatched", smsPayload, tenantId);
        await mockBroker.PublishEventAsync("sgrowing:stream:outbox", "OutboundMessageDispatched", waPayload, tenantId);

        var daemon = new RedisStreamConsumerDaemon(
            sp,
            NullLogger<RedisStreamConsumerDaemon>.Instance,
            streamKey: "sgrowing:stream:outbox",
            groupName: "test-group");

        var processed = await daemon.ProcessStreamBatchAsync(mockBroker);

        processed.Should().Be(3);
        mockBroker.AcknowledgedIds.Should().HaveCount(3);
    }

    [Fact]
    public async Task RedisStreamConsumerDaemon_OnFailure_LogsDeadLetterAndAcknowledges()
    {
        var services = new ServiceCollection();
        var dbName = Guid.NewGuid().ToString();
        services.AddDbContext<SGrowingDbContext>(options => options.UseInMemoryDatabase(dbName));
        services.AddSingleton<IConfiguration>(_emptyConfig);
        services.AddHttpClient();
        // Register dispatchers
        services.AddHttpClient<ISmtpEmailDispatcher, SmtpEmailDispatcher>();
        services.AddHttpClient<ITwilioSmsDispatcher, TwilioSmsDispatcher>();
        services.AddHttpClient<IMetaWhatsAppDispatcher, MetaWhatsAppDispatcher>();

        var sp = services.BuildServiceProvider();
        var mockBroker = new TestRedisStreamBroker();

        var tenantId = Guid.NewGuid();
        // Invalid email payload (missing valid recipient address)
        var invalidPayload = JsonSerializer.Serialize(new
        {
            RecipientEmail = "invalid-address",
            Channel = "Email",
            Subject = "Failure test",
            Body = "This should fail"
        });

        await mockBroker.PublishEventAsync("sgrowing:stream:outbox", "OutboundMessageDispatched", invalidPayload, tenantId);

        var daemon = new RedisStreamConsumerDaemon(
            sp,
            NullLogger<RedisStreamConsumerDaemon>.Instance,
            streamKey: "sgrowing:stream:outbox",
            groupName: "test-group");

        var processed = await daemon.ProcessStreamBatchAsync(mockBroker);

        processed.Should().Be(1);
        mockBroker.AcknowledgedIds.Should().HaveCount(1);

        using var verifyScope = sp.CreateScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<SGrowingDbContext>();
        var dlqLogs = await db.WorkflowDeadLetterLogs.Where(d => d.TenantId == tenantId).ToListAsync();
        dlqLogs.Should().HaveCount(1);
        dlqLogs[0].ErrorMessage.Should().Contain("Invalid or missing recipient email address");
    }

    private class TestRedisStreamBroker : IRedisStreamBroker
    {
        private readonly List<StreamMessageEnvelope> _messages = new();
        public List<string> AcknowledgedIds { get; } = new();

        public bool IsConnected => true;

        public Task<string> PublishEventAsync(string streamKey, string eventType, string payloadJson, Guid tenantId)
        {
            var id = $"msg_{Guid.NewGuid():N}";
            _messages.Add(new StreamMessageEnvelope(id, streamKey, eventType, tenantId, payloadJson, DateTime.UtcNow));
            return Task.FromResult(id);
        }

        public Task EnsureConsumerGroupAsync(string streamKey, string groupName) => Task.CompletedTask;

        public Task<List<StreamMessageEnvelope>> ReadGroupMessagesAsync(string streamKey, string groupName, string consumerName, int count = 10)
        {
            var unacked = _messages.Where(m => !AcknowledgedIds.Contains(m.MessageId)).Take(count).ToList();
            return Task.FromResult(unacked);
        }

        public Task AcknowledgeAsync(string streamKey, string groupName, string messageId)
        {
            AcknowledgedIds.Add(messageId);
            return Task.CompletedTask;
        }
    }
}
