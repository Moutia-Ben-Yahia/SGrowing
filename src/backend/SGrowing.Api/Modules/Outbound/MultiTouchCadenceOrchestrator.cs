using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Voice;
using SGrowing.Core.Entities;
using SGrowing.Core.Outbox;

namespace SGrowing.Api.Modules.Outbound;

public class MultiTouchCadenceOrchestrator
{
    private readonly SGrowingDbContext _db;
    private readonly MailboxService _mailboxService;
    private readonly ILinkedInOutreachAdapter _linkedInOutreachAdapter;
    private readonly VoiceTelephonyService? _voiceTelephonyService;
    private readonly IOutbox _outbox;
    private readonly ILogger<MultiTouchCadenceOrchestrator>? _logger;

    public MultiTouchCadenceOrchestrator(
        SGrowingDbContext db,
        MailboxService mailboxService,
        LinkedInOutreachAdapter linkedInOutreachAdapter,
        VoiceTelephonyService? voiceTelephonyService,
        IOutbox outbox,
        ILogger<MultiTouchCadenceOrchestrator>? logger = null)
        : this(db, mailboxService, (ILinkedInOutreachAdapter)linkedInOutreachAdapter, voiceTelephonyService, outbox, logger)
    {
    }

    public MultiTouchCadenceOrchestrator(
        SGrowingDbContext db,
        MailboxService mailboxService,
        ILinkedInOutreachAdapter linkedInOutreachAdapter,
        VoiceTelephonyService? voiceTelephonyService,
        IOutbox outbox,
        ILogger<MultiTouchCadenceOrchestrator>? logger = null)
    {
        _db = db;
        _mailboxService = mailboxService;
        _linkedInOutreachAdapter = linkedInOutreachAdapter;
        _voiceTelephonyService = voiceTelephonyService ?? (db is VoiceDbContext voiceDb ? new VoiceTelephonyService(voiceDb) : null);
        _outbox = outbox;
        _logger = logger;
    }

    public MultiTouchCadenceOrchestrator(
        SGrowingDbContext db,
        IOutbox outbox)
        : this(db, new MailboxService(db), new LinkedInOutreachAdapter(null, outbox), db is VoiceDbContext voiceDb ? new VoiceTelephonyService(voiceDb) : null, outbox, null)
    {
    }

    public Task<int> ProcessDueMultiTouchStepsAsync(Guid tenantId)
    {
        return ProcessDueMultiTouchStepsAsync(tenantId, CancellationToken.None);
    }

    public async Task<int> ProcessDueMultiTouchStepsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var dueExecutions = await _db.Set<TouchpointExecution>()
            .Where(e => e.TenantId == tenantId && e.Status == "Pending" && e.ScheduledAtUtc <= now)
            .OrderBy(e => e.ScheduledAtUtc)
            .ToListAsync(cancellationToken);

        int processed = 0;

        foreach (var execution in dueExecutions)
        {
            var touchpoint = await _db.Set<CadenceTouchpoint>()
                .FirstOrDefaultAsync(t => t.Id == execution.TouchpointId, cancellationToken);

            var contact = await _db.Contacts
                .FirstOrDefaultAsync(c => c.Id == execution.ContactId, cancellationToken);

            var subject = touchpoint?.SubjectTemplate ?? string.Empty;
            var body = touchpoint?.BodyTemplate ?? string.Empty;

            if (contact != null)
            {
                subject = subject.Replace("{{first_name}}", contact.FirstName)
                                 .Replace("{{last_name}}", contact.LastName);
                body = body.Replace("{{first_name}}", contact.FirstName)
                           .Replace("{{last_name}}", contact.LastName);
            }

            _logger?.LogInformation(
                "Processing touchpoint execution {ExecutionId} on channel {Channel} for contact {ContactId} (Tenant: {TenantId})",
                execution.Id, execution.Channel, execution.ContactId, tenantId);

            switch (execution.Channel)
            {
                case TouchpointChannel.Email:
                    var recipientEmail = contact?.Email ?? string.Empty;
                    if (touchpoint != null && touchpoint.CampaignId != Guid.Empty)
                    {
                        var campaign = await _db.OutboundCampaigns
                            .FirstOrDefaultAsync(c => c.Id == touchpoint.CampaignId, cancellationToken);

                        var senderPoolIds = campaign?.GetSenderPoolIds() ?? Enumerable.Empty<Guid>();
                        var sender = await _mailboxService.GetNextAvailableSenderAsync(tenantId, senderPoolIds, cancellationToken);

                        if (sender != null)
                        {
                            await _mailboxService.RecordSendAsync(sender.Id, cancellationToken);
                        }
                        else
                        {
                            await _mailboxService.SendEmailAsync(tenantId, execution.ContactId, recipientEmail, subject, body, cancellationToken);
                        }
                    }
                    else
                    {
                        await _mailboxService.SendEmailAsync(tenantId, execution.ContactId, recipientEmail, subject, body, cancellationToken);
                    }
                    break;

                case TouchpointChannel.LinkedInConnect:
                    var profileUrlConnect = ResolveLinkedInUrl(contact);
                    await _linkedInOutreachAdapter.SendConnectionRequestAsync(
                        tenantId,
                        execution.ContactId,
                        profileUrlConnect,
                        body,
                        cancellationToken);
                    break;

                case TouchpointChannel.LinkedInMessage:
                    var profileUrlMsg = ResolveLinkedInUrl(contact);
                    await _linkedInOutreachAdapter.SendInMailMessageAsync(
                        tenantId,
                        execution.ContactId,
                        profileUrlMsg,
                        subject,
                        body,
                        cancellationToken);
                    break;

                case TouchpointChannel.WhatsApp:
                    var recipientPhone = contact?.Phone ?? string.Empty;
                    await _outbox.EnqueueAsync(tenantId, "WhatsAppOutboundDispatched", new
                    {
                        ExecutionId = execution.Id,
                        TouchpointId = execution.TouchpointId,
                        ContactId = execution.ContactId,
                        RecipientPhone = recipientPhone,
                        Message = body,
                        ScheduledAtUtc = execution.ScheduledAtUtc,
                        DispatchedAtUtc = DateTime.UtcNow
                    }, cancellationToken);
                    break;

                case TouchpointChannel.VoiceCall:
                    var voicePhone = contact?.Phone ?? "+10000000000";
                    if (_voiceTelephonyService != null)
                    {
                        await _voiceTelephonyService.InitiateOutboundCallAsync(
                            tenantId,
                            execution.ContactId,
                            fromNumber: "+15550199000",
                            toNumber: voicePhone,
                            cancellationToken: cancellationToken);
                    }
                    await _outbox.EnqueueAsync(tenantId, "VoiceCallOutboundDispatched", new
                    {
                        ExecutionId = execution.Id,
                        TouchpointId = execution.TouchpointId,
                        ContactId = execution.ContactId,
                        ToNumber = voicePhone,
                        ScheduledAtUtc = execution.ScheduledAtUtc,
                        DispatchedAtUtc = DateTime.UtcNow
                    }, cancellationToken);
                    break;

                case TouchpointChannel.Sms:
                    var smsPhone = contact?.Phone ?? string.Empty;
                    await _outbox.EnqueueAsync(tenantId, "SmsOutboundDispatched", new
                    {
                        ExecutionId = execution.Id,
                        TouchpointId = execution.TouchpointId,
                        ContactId = execution.ContactId,
                        RecipientPhone = smsPhone,
                        Message = body,
                        ScheduledAtUtc = execution.ScheduledAtUtc,
                        DispatchedAtUtc = DateTime.UtcNow
                    }, cancellationToken);
                    break;
            }

            execution.MarkSent();

            await _outbox.EnqueueAsync(tenantId, "TouchpointExecutionSent", new
            {
                ExecutionId = execution.Id,
                TouchpointId = execution.TouchpointId,
                ContactId = execution.ContactId,
                Channel = execution.Channel.ToString(),
                Status = execution.Status,
                ExecutedAtUtc = execution.ExecutedAtUtc,
                Subject = subject,
                Body = body,
                RecipientEmail = contact?.Email,
                RecipientPhone = contact?.Phone
            }, cancellationToken);

            processed++;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return processed;
    }

    private static string ResolveLinkedInUrl(Contact? contact)
    {
        if (contact == null)
            return "https://linkedin.com/in/prospect";

        var fields = contact.GetCustomFields();
        if (fields.TryGetValue("LinkedInUrl", out var val) && val is not null)
            return val.ToString()!;
        if (fields.TryGetValue("linkedin_url", out var val2) && val2 is not null)
            return val2.ToString()!;
        if (fields.TryGetValue("LinkedIn", out var val3) && val3 is not null)
            return val3.ToString()!;

        var fn = string.IsNullOrWhiteSpace(contact.FirstName) ? "prospect" : contact.FirstName.ToLowerInvariant();
        var ln = string.IsNullOrWhiteSpace(contact.LastName) ? "unknown" : contact.LastName.ToLowerInvariant();
        return $"https://linkedin.com/in/{fn}-{ln}";
    }
}
