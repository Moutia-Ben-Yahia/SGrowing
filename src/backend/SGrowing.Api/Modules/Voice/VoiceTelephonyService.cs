using SGrowing.Api.Data;
using SGrowing.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace SGrowing.Api.Modules.Voice;

public class VoiceTelephonyService
{
    private readonly SGrowingDbContext _db;
    private readonly ILogger<VoiceTelephonyService>? _logger;

    public VoiceTelephonyService(SGrowingDbContext db, ILogger<VoiceTelephonyService>? logger = null)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<string> HandleInboundCallWebhookAsync(
        Guid tenantId,
        string callSid,
        string fromNumber,
        string toNumber,
        CancellationToken cancellationToken = default)
    {
        // 1. Resolve or create Contact for caller
        var contact = await _db.Contacts.FirstOrDefaultAsync(
            c => c.TenantId == tenantId && c.Phone == fromNumber, cancellationToken);

        if (contact == null)
        {
            contact = Contact.Create(
                tenantId: tenantId,
                firstName: "Inbound",
                lastName: string.IsNullOrWhiteSpace(fromNumber) ? "Caller" : fromNumber,
                phone: fromNumber
            );
            _db.Contacts.Add(contact);
            await _db.SaveChangesAsync(cancellationToken);
        }

        // 2. Create or find VoiceCallSession
        var session = await _db.VoiceCallSessions.FirstOrDefaultAsync(
            s => s.TenantId == tenantId && s.CallSid == callSid, cancellationToken);

        if (session == null)
        {
            session = VoiceCallSession.Create(
                tenantId: tenantId,
                contactId: contact.Id,
                callSid: callSid,
                direction: CallDirection.Inbound,
                fromNumber: fromNumber,
                toNumber: toNumber
            );
            session.Status = CallStatus.InProgress;
            _db.VoiceCallSessions.Add(session);
            await _db.SaveChangesAsync(cancellationToken);
        }

        _logger?.LogInformation("Inbound call session {SessionId} registered (CallSid: {CallSid}) for tenant {TenantId}.",
            session.Id, callSid, tenantId);

        // 3. Generate and return TwiML/SIP XML response
        var twiml = """<Response><Say voice="alice">Hello! Thank you for calling SGrowing.</Say><Gather input="speech" action="/api/voice/turns" method="POST" speechTimeout="auto" timeout="3"><Say voice="alice">Please describe how we can help your business today.</Say></Gather></Response>""";

        return twiml;
    }

    public async Task<VoiceCallSession?> HandleCallStatusCallbackAsync(
        Guid tenantId,
        string callSid,
        string callStatus,
        int durationSeconds,
        CancellationToken cancellationToken = default)
    {
        var session = await _db.VoiceCallSessions.FirstOrDefaultAsync(
            s => s.TenantId == tenantId && s.CallSid == callSid, cancellationToken);

        if (session == null)
        {
            _logger?.LogWarning("Call session with CallSid {CallSid} not found for tenant {TenantId}.", callSid, tenantId);
            return null;
        }

        var normalized = (callStatus ?? string.Empty).Trim().ToLowerInvariant();
        switch (normalized)
        {
            case "completed":
            case "complete":
                session.CompleteCall(durationSeconds);
                break;
            case "failed":
            case "busy":
            case "no-answer":
            case "canceled":
                session.FailCall();
                break;
            case "in-progress":
            case "in_progress":
                session.Status = CallStatus.InProgress;
                session.DurationSeconds = durationSeconds;
                break;
            case "ringing":
                session.Status = CallStatus.Ringing;
                break;
            default:
                session.DurationSeconds = durationSeconds;
                break;
        }

        await _db.SaveChangesAsync(cancellationToken);

        _logger?.LogInformation("Updated call session {SessionId} (CallSid: {CallSid}) status to {Status} (duration: {Duration}s).",
            session.Id, callSid, session.Status, session.DurationSeconds);

        return session;
    }

    public async Task<VoiceCallSession> InitiateOutboundCallAsync(
        Guid tenantId,
        Guid contactId,
        string fromNumber,
        string toNumber,
        CancellationToken cancellationToken = default)
    {
        var callSid = $"CA{Guid.NewGuid():N}";
        var session = VoiceCallSession.Create(
            tenantId: tenantId,
            contactId: contactId,
            callSid: callSid,
            direction: CallDirection.Outbound,
            fromNumber: fromNumber ?? string.Empty,
            toNumber: toNumber ?? string.Empty
        );

        session.Status = CallStatus.Ringing;
        _db.VoiceCallSessions.Add(session);
        await _db.SaveChangesAsync(cancellationToken);

        _logger?.LogInformation("Initiated outbound call session {SessionId} (CallSid: {CallSid}) from {FromNumber} to {ToNumber} for tenant {TenantId}.",
            session.Id, callSid, fromNumber, toNumber, tenantId);

        return session;
    }
}
