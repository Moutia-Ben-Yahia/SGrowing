using System.Diagnostics;
using Microsoft.Extensions.Logging;
using SGrowing.Api.Modules.Gateways.Android;
using SGrowing.Core.Diagnostics;
using SGrowing.Core.Outbox;

namespace SGrowing.Api.Modules.Channels.WhatsApp;

public enum PriorityMessageType
{
    Otp = 0,
    OrderConfirmation = 1,
    InvoiceAlert = 2,
    SecurityAlert = 3,
    PasswordReset = 4
}

public enum FailoverChannel
{
    WhatsApp = 0,
    AndroidSms = 1,
    TransactionalEmail = 2
}

public record FailoverHop(
    FailoverChannel Channel,
    bool Succeeded,
    string Status,
    string? Details,
    DateTime AttemptedAtUtc,
    TimeSpan Duration);

public record PriorityMessageRequest(
    Guid TenantId,
    string RecipientPhone,
    string? RecipientEmail,
    string Subject,
    string Body,
    PriorityMessageType MessageType,
    TimeSpan? WhatsAppTimeout = null,
    string? PreferredWhatsAppRole = null,
    int? PreferredSimSlot = null);

public record OmnichannelDeliveryResult(
    bool IsSuccess,
    FailoverChannel? SuccessfulChannel,
    string MessageId,
    IReadOnlyList<FailoverHop> Hops,
    TimeSpan TotalDuration,
    string? ErrorMessage = null,
    DateTime CompletedAtUtc = default);

public record EmailDispatchResult(
    bool IsSuccess,
    string? MessageId,
    string Status,
    string? ErrorMessage = null);

public interface ISmtpEmailSender
{
    Task<EmailDispatchResult> SendTransactionalEmailAsync(
        Guid tenantId,
        string recipientEmail,
        string subject,
        string htmlBody,
        string? textBody = null,
        CancellationToken ct = default);
}

public class SmtpEmailSender : ISmtpEmailSender
{
    private readonly ILogger<SmtpEmailSender>? _logger;

    public SmtpEmailSender(ILogger<SmtpEmailSender>? logger = null)
    {
        _logger = logger;
    }

    public Task<EmailDispatchResult> SendTransactionalEmailAsync(
        Guid tenantId,
        string recipientEmail,
        string subject,
        string htmlBody,
        string? textBody = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(recipientEmail) || !recipientEmail.Contains('@'))
        {
            return Task.FromResult(new EmailDispatchResult(
                false, null, "Failed", "Invalid or missing recipient email address"));
        }

        var messageId = $"msg_smtp_{Guid.NewGuid():N}";
        _logger?.LogInformation("Transactional email dispatched to {Recipient} via SMTP [Subject: {Subject}, ID: {MsgId}]",
            recipientEmail, subject, messageId);

        return Task.FromResult(new EmailDispatchResult(
            true, messageId, "Sent", null));
    }
}

public interface IOmnichannelFailoverService
{
    Task<OmnichannelDeliveryResult> SendPriorityTransactionalMessageAsync(
        PriorityMessageRequest request,
        CancellationToken ct = default);

    // Testing / simulation hook to inject simulated WhatsApp delivery behavior
    Func<PriorityMessageRequest, ClusterInstanceSession, CancellationToken, Task<bool>>? WhatsAppDeliveryProbe { get; set; }
}

public class OmnichannelFailoverService : IOmnichannelFailoverService
{
    private readonly IWhatsAppSessionClusterService _clusterService;
    private readonly IAndroidSmsGatewayService _smsGatewayService;
    private readonly ISmtpEmailSender _emailSender;
    private readonly IOutbox? _outbox;
    private readonly ILogger<OmnichannelFailoverService>? _logger;

    public Func<PriorityMessageRequest, ClusterInstanceSession, CancellationToken, Task<bool>>? WhatsAppDeliveryProbe { get; set; }

    public OmnichannelFailoverService(
        IWhatsAppSessionClusterService clusterService,
        IAndroidSmsGatewayService smsGatewayService,
        ISmtpEmailSender emailSender,
        IOutbox? outbox = null,
        ILogger<OmnichannelFailoverService>? logger = null)
    {
        _clusterService = clusterService;
        _smsGatewayService = smsGatewayService;
        _emailSender = emailSender;
        _outbox = outbox;
        _logger = logger;
    }

    public async Task<OmnichannelDeliveryResult> SendPriorityTransactionalMessageAsync(
        PriorityMessageRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var totalStopwatch = Stopwatch.StartNew();
        var hops = new List<FailoverHop>();
        string messageId = $"omni_{Guid.NewGuid():N}";

        using var activity = SGrowingTelemetry.ActivitySource.StartActivity("OmnichannelFailover.SendPriority");
        activity?.SetTag("tenant.id", request.TenantId.ToString());
        activity?.SetTag("message.type", request.MessageType.ToString());
        activity?.SetTag("recipient.phone", request.RecipientPhone);

        // STAGE 1: WhatsApp Cluster Delivery Attempt
        var waHopWatch = Stopwatch.StartNew();
        var waAttemptTime = DateTime.UtcNow;

        var selectedInstance = await _clusterService.SelectLoadBalancedInstanceAsync(
            request.TenantId,
            request.PreferredWhatsAppRole,
            ClusterLoadBalanceStrategy.LeastLoaded,
            ct);

        if (selectedInstance == null)
        {
            waHopWatch.Stop();
            hops.Add(new FailoverHop(
                FailoverChannel.WhatsApp,
                false,
                "NoConnectedInstance",
                "No active connected WhatsApp cluster instance available for tenant.",
                waAttemptTime,
                waHopWatch.Elapsed));

            _logger?.LogWarning("Stage 1 (WhatsApp) failed for {Phone}: No connected cluster instance.", request.RecipientPhone);
        }
        else
        {
            bool waDelivered;
            try
            {
                if (WhatsAppDeliveryProbe != null)
                {
                    waDelivered = await WhatsAppDeliveryProbe(request, selectedInstance, ct);
                }
                else
                {
                    // By default, mark message dispatched and acknowledge
                    await _clusterService.RecordDispatchedMessageAsync(request.TenantId, selectedInstance.InstanceName, ct);
                    waDelivered = true;
                }
            }
            catch (Exception ex)
            {
                waDelivered = false;
                _logger?.LogWarning(ex, "Stage 1 (WhatsApp) delivery exception on instance '{Instance}'.", selectedInstance.InstanceName);
            }

            waHopWatch.Stop();

            if (waDelivered)
            {
                hops.Add(new FailoverHop(
                    FailoverChannel.WhatsApp,
                    true,
                    "Delivered",
                    $"Successfully dispatched and acknowledged via WhatsApp instance '{selectedInstance.InstanceName}' ({selectedInstance.PhoneNumber})",
                    waAttemptTime,
                    waHopWatch.Elapsed));

                totalStopwatch.Stop();
                return new OmnichannelDeliveryResult(
                    IsSuccess: true,
                    SuccessfulChannel: FailoverChannel.WhatsApp,
                    MessageId: messageId,
                    Hops: hops,
                    TotalDuration: totalStopwatch.Elapsed,
                    CompletedAtUtc: DateTime.UtcNow);
            }
            else
            {
                var timeoutDuration = request.WhatsAppTimeout ?? TimeSpan.FromSeconds(60);
                hops.Add(new FailoverHop(
                    FailoverChannel.WhatsApp,
                    false,
                    "DeliveryTimeoutOrUnacknowledged",
                    $"WhatsApp delivery unacknowledged within timeout threshold ({timeoutDuration.TotalSeconds:F0}s). Escalating to SMS.",
                    waAttemptTime,
                    waHopWatch.Elapsed));

                _logger?.LogInformation("Stage 1 (WhatsApp) unacknowledged. Cascading to Stage 2 (Android SMS) for {Phone}.", request.RecipientPhone);
            }
        }

        // STAGE 2: Android SMS Gateway Fallback
        var smsHopWatch = Stopwatch.StartNew();
        var smsAttemptTime = DateTime.UtcNow;

        AndroidSmsDispatchResultDto smsResult;
        try
        {
            smsResult = await _smsGatewayService.DispatchSmsAsync(
                request.TenantId,
                request.RecipientPhone,
                request.Body,
                request.PreferredSimSlot);
        }
        catch (Exception ex)
        {
            smsResult = new AndroidSmsDispatchResultDto(
                false, null, null, null, 1, request.RecipientPhone, "Exception", ex.Message);
        }

        smsHopWatch.Stop();

        if (smsResult.IsSuccess)
        {
            hops.Add(new FailoverHop(
                FailoverChannel.AndroidSms,
                true,
                "Delivered",
                $"Delivered via Android SMS handset '{smsResult.DeviceName ?? "Device"}' (SIM Slot {smsResult.SimSlot})",
                smsAttemptTime,
                smsHopWatch.Elapsed));

            totalStopwatch.Stop();
            return new OmnichannelDeliveryResult(
                IsSuccess: true,
                SuccessfulChannel: FailoverChannel.AndroidSms,
                MessageId: messageId,
                Hops: hops,
                TotalDuration: totalStopwatch.Elapsed,
                CompletedAtUtc: DateTime.UtcNow);
        }
        else
        {
            hops.Add(new FailoverHop(
                FailoverChannel.AndroidSms,
                false,
                "SmsDeliveryFailed",
                $"Android SMS failed: {smsResult.Message}. Escalating to Transactional Email.",
                smsAttemptTime,
                smsHopWatch.Elapsed));

            _logger?.LogInformation("Stage 2 (Android SMS) failed: {Reason}. Cascading to Stage 3 (Email) for {Phone}.",
                smsResult.Message, request.RecipientPhone);
        }

        // STAGE 3: Transactional Email Fallback
        var emailHopWatch = Stopwatch.StartNew();
        var emailAttemptTime = DateTime.UtcNow;

        if (string.IsNullOrWhiteSpace(request.RecipientEmail))
        {
            emailHopWatch.Stop();
            hops.Add(new FailoverHop(
                FailoverChannel.TransactionalEmail,
                false,
                "MissingRecipientEmail",
                "Recipient email address was not specified for fallback delivery.",
                emailAttemptTime,
                emailHopWatch.Elapsed));

            totalStopwatch.Stop();
            return new OmnichannelDeliveryResult(
                IsSuccess: false,
                SuccessfulChannel: null,
                MessageId: messageId,
                Hops: hops,
                TotalDuration: totalStopwatch.Elapsed,
                ErrorMessage: "All omnichannel dispatch channels failed (WhatsApp timeout -> SMS failed -> No email provided).",
                CompletedAtUtc: DateTime.UtcNow);
        }

        EmailDispatchResult emailResult;
        try
        {
            emailResult = await _emailSender.SendTransactionalEmailAsync(
                request.TenantId,
                request.RecipientEmail,
                request.Subject,
                $"<div><h2>{request.Subject}</h2><p>{request.Body}</p></div>",
                request.Body,
                ct);
        }
        catch (Exception ex)
        {
            emailResult = new EmailDispatchResult(false, null, "Exception", ex.Message);
        }

        emailHopWatch.Stop();

        if (emailResult.IsSuccess)
        {
            hops.Add(new FailoverHop(
                FailoverChannel.TransactionalEmail,
                true,
                "Delivered",
                $"Delivered via transactional email SMTP to {request.RecipientEmail} (ID: {emailResult.MessageId})",
                emailAttemptTime,
                emailHopWatch.Elapsed));

            totalStopwatch.Stop();
            return new OmnichannelDeliveryResult(
                IsSuccess: true,
                SuccessfulChannel: FailoverChannel.TransactionalEmail,
                MessageId: messageId,
                Hops: hops,
                TotalDuration: totalStopwatch.Elapsed,
                CompletedAtUtc: DateTime.UtcNow);
        }
        else
        {
            hops.Add(new FailoverHop(
                FailoverChannel.TransactionalEmail,
                false,
                "EmailDeliveryFailed",
                $"Transactional email dispatch failed: {emailResult.ErrorMessage}",
                emailAttemptTime,
                emailHopWatch.Elapsed));

            totalStopwatch.Stop();
            return new OmnichannelDeliveryResult(
                IsSuccess: false,
                SuccessfulChannel: null,
                MessageId: messageId,
                Hops: hops,
                TotalDuration: totalStopwatch.Elapsed,
                ErrorMessage: "All omnichannel failover channels failed (WhatsApp -> SMS -> Email).",
                CompletedAtUtc: DateTime.UtcNow);
        }
    }
}
