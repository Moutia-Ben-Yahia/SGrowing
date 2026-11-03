using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;
using SGrowing.Core.Messaging;
using SGrowing.Worker.Services.Dispatchers;

namespace SGrowing.Worker.Services;

public class RedisStreamConsumerDaemon : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RedisStreamConsumerDaemon> _logger;
    private readonly string _streamKey;
    private readonly string _groupName;
    private readonly string _consumerName;
    private readonly TimeSpan _pollInterval;

    public RedisStreamConsumerDaemon(
        IServiceProvider serviceProvider,
        ILogger<RedisStreamConsumerDaemon> logger,
        string streamKey = "sgrowing:stream:outbox",
        string groupName = "sgrowing-worker-group",
        string? consumerName = null,
        TimeSpan? pollInterval = null)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _streamKey = streamKey;
        _groupName = groupName;
        _consumerName = consumerName ?? $"worker-{Guid.NewGuid().ToString()[..6]}";
        _pollInterval = pollInterval ?? TimeSpan.FromSeconds(2);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RedisStreamConsumerDaemon starting. Consumer '{Consumer}' on stream '{Stream}', group '{Group}'.",
            _consumerName, _streamKey, _groupName);

        using var scope = _serviceProvider.CreateScope();
        var broker = scope.ServiceProvider.GetService<IRedisStreamBroker>();
        if (broker == null)
        {
            _logger.LogWarning("No IRedisStreamBroker registered. Daemon will exit.");
            return;
        }

        await broker.EnsureConsumerGroupAsync(_streamKey, _groupName);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessStreamBatchAsync(broker, stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Error occurred while reading messages from Redis stream {StreamKey}", _streamKey);
            }

            try
            {
                await Task.Delay(_pollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("RedisStreamConsumerDaemon stopping.");
    }

    public async Task<int> ProcessStreamBatchAsync(IRedisStreamBroker broker, CancellationToken cancellationToken = default)
    {
        var messages = await broker.ReadGroupMessagesAsync(_streamKey, _groupName, _consumerName, count: 20);
        if (messages.Count == 0) return 0;

        _logger.LogInformation("Consumer '{Consumer}' read {Count} messages from Redis stream '{Stream}'.",
            _consumerName, messages.Count, _streamKey);

        foreach (var msg in messages)
        {
            using var scope = _serviceProvider.CreateScope();
            var emailDispatcher = scope.ServiceProvider.GetService<ISmtpEmailDispatcher>();
            var smsDispatcher = scope.ServiceProvider.GetService<ITwilioSmsDispatcher>();
            var whatsAppDispatcher = scope.ServiceProvider.GetService<IMetaWhatsAppDispatcher>();
            var db = scope.ServiceProvider.GetService<SGrowingDbContext>();

            try
            {
                _logger.LogInformation("Executing event handler for {EventType} (StreamId: {MsgId}, Tenant: {Tenant})",
                    msg.EventType, msg.MessageId, msg.TenantId);

                var dispatchResult = await DispatchEventAsync(
                    msg, emailDispatcher, smsDispatcher, whatsAppDispatcher, cancellationToken);

                if (!dispatchResult.IsSuccess)
                {
                    await RecordDeadLetterAsync(db, msg, dispatchResult.ErrorMessage ?? "Dispatch failed.", cancellationToken);
                }
                else
                {
                    _logger.LogInformation("Successfully dispatched event {EventType} (StreamId: {MsgId}, Detail: {Detail})",
                        msg.EventType, msg.MessageId, dispatchResult.Detail);
                }

                // Acknowledge processed message in Redis
                await broker.AcknowledgeAsync(_streamKey, _groupName, msg.MessageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process stream message {MessageId} of type {EventType}",
                    msg.MessageId, msg.EventType);

                await RecordDeadLetterAsync(db, msg, ex.Message, cancellationToken);

                // Acknowledge on dead letter so consumer group is not permanently blocked
                await broker.AcknowledgeAsync(_streamKey, _groupName, msg.MessageId);
            }
        }

        return messages.Count;
    }

    private async Task<(bool IsSuccess, string? Detail, string? ErrorMessage)> DispatchEventAsync(
        StreamMessageEnvelope msg,
        ISmtpEmailDispatcher? emailDispatcher,
        ITwilioSmsDispatcher? smsDispatcher,
        IMetaWhatsAppDispatcher? whatsAppDispatcher,
        CancellationToken cancellationToken)
    {
        var eventType = msg.EventType;

        // 1. Cadence & Omnichannel outbound message dispatch
        if (eventType.Equals("OutboundMessageDispatched", StringComparison.OrdinalIgnoreCase))
        {
            return await HandleOutboundMessageDispatchedAsync(msg, emailDispatcher, smsDispatcher, whatsAppDispatcher, cancellationToken);
        }

        // 2. WhatsApp event dispatches
        if (eventType.Equals("WhatsAppMessageSendRequested", StringComparison.OrdinalIgnoreCase) ||
            eventType.Equals("WhatsApp.Template.Send", StringComparison.OrdinalIgnoreCase) ||
            eventType.Equals("WhatsAppOutboundDispatched", StringComparison.OrdinalIgnoreCase))
        {
            return await HandleWhatsAppEventAsync(msg, whatsAppDispatcher, cancellationToken);
        }

        // 3. SMS event dispatches
        if (eventType.Equals("SmsMessageSendRequested", StringComparison.OrdinalIgnoreCase) ||
            eventType.Equals("SmsOutboundDispatched", StringComparison.OrdinalIgnoreCase))
        {
            return await HandleSmsEventAsync(msg, smsDispatcher, cancellationToken);
        }

        // 4. Email event dispatches
        if (eventType.Equals("EmailMessageSendRequested", StringComparison.OrdinalIgnoreCase) ||
            eventType.Equals("EmailOutboundDispatched", StringComparison.OrdinalIgnoreCase))
        {
            return await HandleEmailEventAsync(msg, emailDispatcher, cancellationToken);
        }

        // Generic / unsupported event: acknowledge without error
        return (true, $"Acknowledged non-dispatch event type '{eventType}'", null);
    }

    private async Task<(bool IsSuccess, string? Detail, string? ErrorMessage)> HandleOutboundMessageDispatchedAsync(
        StreamMessageEnvelope msg,
        ISmtpEmailDispatcher? emailDispatcher,
        ITwilioSmsDispatcher? smsDispatcher,
        IMetaWhatsAppDispatcher? whatsAppDispatcher,
        CancellationToken cancellationToken)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(msg.PayloadJson) ? "{}" : msg.PayloadJson);
        var root = doc.RootElement;

        var channel = GetStringProperty(root, "Channel") ?? GetStringProperty(root, "channel") ?? "Email";
        var recipientEmail = GetStringProperty(root, "RecipientEmail") ?? GetStringProperty(root, "recipientEmail");
        var recipientPhone = GetStringProperty(root, "RecipientPhone") ?? GetStringProperty(root, "recipientPhone");
        var senderEmail = GetStringProperty(root, "SenderEmail") ?? GetStringProperty(root, "senderEmail");
        var subject = GetStringProperty(root, "Subject") ?? GetStringProperty(root, "subject") ?? "(No Subject)";
        var body = GetStringProperty(root, "Body") ?? GetStringProperty(root, "body") ?? "";

        if (channel.Equals("Sms", StringComparison.OrdinalIgnoreCase))
        {
            if (smsDispatcher == null)
                return (false, null, "ITwilioSmsDispatcher is not registered.");

            var res = await smsDispatcher.SendSmsAsync(msg.TenantId, recipientPhone ?? "", body, null, cancellationToken);
            return res.IsSuccess
                ? (true, $"SMS sent (Sid: {res.MessageSid})", null)
                : (false, null, res.ErrorMessage ?? "SMS dispatch failed.");
        }

        if (channel.Equals("WhatsApp", StringComparison.OrdinalIgnoreCase))
        {
            if (whatsAppDispatcher == null)
                return (false, null, "IMetaWhatsAppDispatcher is not registered.");

            var res = await whatsAppDispatcher.SendTextMessageAsync(msg.TenantId, recipientPhone ?? "", body, cancellationToken);
            return res.IsSuccess
                ? (true, $"WhatsApp message sent (Id: {res.MessageId})", null)
                : (false, null, res.ErrorMessage ?? "WhatsApp dispatch failed.");
        }

        // Default: Email channel
        if (emailDispatcher == null)
            return (false, null, "ISmtpEmailDispatcher is not registered.");

        var emailRes = await emailDispatcher.SendEmailAsync(msg.TenantId, recipientEmail ?? "", subject, body, senderEmail, cancellationToken);
        return emailRes.IsSuccess
            ? (true, $"Email sent (Id: {emailRes.MessageId})", null)
            : (false, null, emailRes.ErrorMessage ?? "Email dispatch failed.");
    }

    private async Task<(bool IsSuccess, string? Detail, string? ErrorMessage)> HandleWhatsAppEventAsync(
        StreamMessageEnvelope msg,
        IMetaWhatsAppDispatcher? whatsAppDispatcher,
        CancellationToken cancellationToken)
    {
        if (whatsAppDispatcher == null)
            return (false, null, "IMetaWhatsAppDispatcher is not registered.");

        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(msg.PayloadJson) ? "{}" : msg.PayloadJson);
        var root = doc.RootElement;

        var phone = GetStringProperty(root, "recipientPhone") ??
                    GetStringProperty(root, "RecipientPhone") ??
                    GetStringProperty(root, "CustomerPhone") ??
                    GetStringProperty(root, "to") ?? "";

        var templateName = GetStringProperty(root, "templateName") ?? GetStringProperty(root, "TemplateName");
        var body = GetStringProperty(root, "renderedBody") ??
                   GetStringProperty(root, "body") ??
                   GetStringProperty(root, "Body") ??
                   GetStringProperty(root, "message") ??
                   GetStringProperty(root, "Message") ?? "";
        var languageCode = GetStringProperty(root, "languageCode") ?? GetStringProperty(root, "LanguageCode") ?? "en_US";

        Dictionary<string, string>? parameters = null;
        if (root.TryGetProperty("parameters", out var paramsProp) && paramsProp.ValueKind == JsonValueKind.Object)
        {
            parameters = JsonSerializer.Deserialize<Dictionary<string, string>>(paramsProp.GetRawText());
        }

        if (!string.IsNullOrWhiteSpace(templateName))
        {
            var templateRes = await whatsAppDispatcher.SendTemplateMessageAsync(
                msg.TenantId, phone, templateName, languageCode, parameters, cancellationToken);
            return templateRes.IsSuccess
                ? (true, $"WhatsApp template '{templateName}' sent (Id: {templateRes.MessageId})", null)
                : (false, null, templateRes.ErrorMessage ?? "WhatsApp template dispatch failed.");
        }

        var textRes = await whatsAppDispatcher.SendTextMessageAsync(msg.TenantId, phone, body, cancellationToken);
        return textRes.IsSuccess
            ? (true, $"WhatsApp text sent (Id: {textRes.MessageId})", null)
            : (false, null, textRes.ErrorMessage ?? "WhatsApp text dispatch failed.");
    }

    private async Task<(bool IsSuccess, string? Detail, string? ErrorMessage)> HandleSmsEventAsync(
        StreamMessageEnvelope msg,
        ITwilioSmsDispatcher? smsDispatcher,
        CancellationToken cancellationToken)
    {
        if (smsDispatcher == null)
            return (false, null, "ITwilioSmsDispatcher is not registered.");

        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(msg.PayloadJson) ? "{}" : msg.PayloadJson);
        var root = doc.RootElement;

        var phone = GetStringProperty(root, "recipientPhone") ??
                    GetStringProperty(root, "RecipientPhone") ??
                    GetStringProperty(root, "phone") ??
                    GetStringProperty(root, "to") ?? "";

        var body = GetStringProperty(root, "body") ??
                   GetStringProperty(root, "Body") ??
                   GetStringProperty(root, "message") ??
                   GetStringProperty(root, "Message") ?? "";

        var fromNumber = GetStringProperty(root, "fromNumber") ?? GetStringProperty(root, "FromNumber");

        var res = await smsDispatcher.SendSmsAsync(msg.TenantId, phone, body, fromNumber, cancellationToken);
        return res.IsSuccess
            ? (true, $"SMS sent (Sid: {res.MessageSid})", null)
            : (false, null, res.ErrorMessage ?? "SMS dispatch failed.");
    }

    private async Task<(bool IsSuccess, string? Detail, string? ErrorMessage)> HandleEmailEventAsync(
        StreamMessageEnvelope msg,
        ISmtpEmailDispatcher? emailDispatcher,
        CancellationToken cancellationToken)
    {
        if (emailDispatcher == null)
            return (false, null, "ISmtpEmailDispatcher is not registered.");

        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(msg.PayloadJson) ? "{}" : msg.PayloadJson);
        var root = doc.RootElement;

        var email = GetStringProperty(root, "recipientEmail") ??
                    GetStringProperty(root, "RecipientEmail") ??
                    GetStringProperty(root, "email") ??
                    GetStringProperty(root, "to") ?? "";

        var subject = GetStringProperty(root, "subject") ?? GetStringProperty(root, "Subject") ?? "(No Subject)";
        var body = GetStringProperty(root, "body") ??
                   GetStringProperty(root, "Body") ??
                   GetStringProperty(root, "htmlBody") ?? "";
        var sender = GetStringProperty(root, "senderEmail") ?? GetStringProperty(root, "SenderEmail");

        var res = await emailDispatcher.SendEmailAsync(msg.TenantId, email, subject, body, sender, cancellationToken);
        return res.IsSuccess
            ? (true, $"Email sent (Id: {res.MessageId})", null)
            : (false, null, res.ErrorMessage ?? "Email dispatch failed.");
    }

    private async Task RecordDeadLetterAsync(
        SGrowingDbContext? db,
        StreamMessageEnvelope msg,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        _logger.LogError("[DEAD-LETTER] Dispatch failure for stream message {MessageId} of type {EventType} (Tenant: {Tenant}): {Error}",
            msg.MessageId, msg.EventType, msg.TenantId, errorMessage);

        if (db != null)
        {
            try
            {
                var dlq = WorkflowDeadLetterLog.Create(
                    tenantId: msg.TenantId,
                    workflowId: Guid.Empty,
                    executionId: Guid.Empty,
                    failedNodeId: msg.MessageId,
                    failedNodeType: msg.EventType,
                    inputPayload: msg.PayloadJson,
                    errorMessage: errorMessage);

                await db.WorkflowDeadLetterLogs.AddAsync(dlq, cancellationToken);
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to persist WorkflowDeadLetterLog for stream message {MessageId}", msg.MessageId);
            }
        }
    }

    private static string? GetStringProperty(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop))
        {
            return prop.ValueKind switch
            {
                JsonValueKind.String => prop.GetString(),
                JsonValueKind.Number => prop.GetRawText(),
                _ => null
            };
        }
        return null;
    }
}
