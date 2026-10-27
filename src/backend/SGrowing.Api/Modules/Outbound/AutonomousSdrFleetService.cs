using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Outbound;

public record InboxSlotAllocationDto(
    Guid SenderAccountId,
    string SenderEmail,
    string SendingDomain,
    int DailyCap,
    int AllocatedSlotsRemaining,
    int PacingSecondsDelay
);

public record CallSessionInitiationDto(
    Guid CallSessionId,
    string ProspectPhone,
    string AgentPhone,
    string WebRtcRoomToken,
    string Status, // "Initiated", "Ringing", "Connected"
    DateTime StartedAtUtc
);

public record CallDispositionDto(
    Guid CallSessionId,
    string Disposition, // "Interested", "FollowUp", "NoAnswer", "WrongNumber"
    int DurationSeconds,
    string? RecordingUrl,
    string Notes
);

public enum ReplyIntentCategory
{
    Interested = 1,
    MeetingBooked = 2,
    NotInterested = 3,
    OutOfOffice = 4,
    WrongPerson = 5,
    Unsubscribe = 6
}

public record ClassifiedReplyDto(
    string InboundSubject,
    string InboundBody,
    ReplyIntentCategory Category,
    double ConfidenceScore,
    string RecommendedAction,
    bool AutoCalendarLinkInjected
);

public interface IAutonomousSdrFleetService
{
    Task<List<InboxSlotAllocationDto>> AllocateInboxSlotsAsync(Guid tenantId, int requestedVolume);
    Task<CallSessionInitiationDto> StartWebRtcCallSessionAsync(Guid tenantId, string prospectPhone, string agentPhone);
    Task<bool> SubmitCallDispositionAsync(Guid tenantId, CallDispositionDto disposition);
    Task<ClassifiedReplyDto> ClassifyInboundReplyAsync(Guid tenantId, string subject, string body);
}

public class AutonomousSdrFleetService : IAutonomousSdrFleetService
{
    private readonly SGrowingDbContext _db;
    private static readonly Dictionary<Guid, CallDispositionDto> _callDispositions = new();

    public AutonomousSdrFleetService(SGrowingDbContext db)
    {
        _db = db;
    }

    public async Task<List<InboxSlotAllocationDto>> AllocateInboxSlotsAsync(Guid tenantId, int requestedVolume)
    {
        var accounts = await _db.SenderAccounts
            .Where(a => a.TenantId == tenantId && a.IsActive)
            .ToListAsync();

        var allocations = new List<InboxSlotAllocationDto>();
        int remainingToAllocate = requestedVolume;

        foreach (var acc in accounts)
        {
            if (remainingToAllocate <= 0) break;

            var domain = acc.Email.Contains('@') ? acc.Email.Split('@')[1] : "domain.com";
            var cap = acc.DailyQuota > 0 ? acc.DailyQuota : 50;
            var slots = Math.Min(cap, remainingToAllocate);

            allocations.Add(new InboxSlotAllocationDto(
                acc.Id,
                acc.Email,
                domain,
                cap,
                slots,
                PacingSecondsDelay: 180 + Random.Shared.Next(30, 90) // randomized pacing delay
            ));

            remainingToAllocate -= slots;
        }

        if (allocations.Count == 0)
        {
            allocations.Add(new InboxSlotAllocationDto(
                Guid.NewGuid(),
                "sdr1@growth-engine.com",
                "growth-engine.com",
                50,
                Math.Min(50, requestedVolume),
                210
            ));
        }

        return allocations;
    }

    public Task<CallSessionInitiationDto> StartWebRtcCallSessionAsync(Guid tenantId, string prospectPhone, string agentPhone)
    {
        var sessionId = Guid.NewGuid();
        var token = $"livekit-jwt-{Guid.NewGuid():N}";

        return Task.FromResult(new CallSessionInitiationDto(
            sessionId,
            prospectPhone,
            agentPhone,
            token,
            "Ringing",
            DateTime.UtcNow
        ));
    }

    public Task<bool> SubmitCallDispositionAsync(Guid tenantId, CallDispositionDto disposition)
    {
        _callDispositions[disposition.CallSessionId] = disposition;
        return Task.FromResult(true);
    }

    public Task<ClassifiedReplyDto> ClassifyInboundReplyAsync(Guid tenantId, string subject, string body)
    {
        var content = $"{subject} {body}".ToLowerInvariant();

        ReplyIntentCategory category;
        string action;
        bool autoCalendar = false;
        double confidence = 0.92;

        if (content.Contains("interested") || content.Contains("tell me more") || content.Contains("pricing") || content.Contains("demo") || content.Contains("let's talk"))
        {
            category = ReplyIntentCategory.Interested;
            action = "Immediately send booking calendar link & notify primary sales rep.";
            autoCalendar = true;
        }
        else if (content.Contains("booked") || content.Contains("calendar invite") || content.Contains("scheduled"))
        {
            category = ReplyIntentCategory.MeetingBooked;
            action = "Move deal to 'Meeting Scheduled' and stop outbound sequence.";
        }
        else if (content.Contains("out of office") || content.Contains("on vacation") || content.Contains("back on"))
        {
            category = ReplyIntentCategory.OutOfOffice;
            action = "Pause sequence until return date and log reminder.";
        }
        else if (content.Contains("unsubscribe") || content.Contains("stop emailing") || content.Contains("remove me"))
        {
            category = ReplyIntentCategory.Unsubscribe;
            action = "Add email and domain to global suppression list.";
        }
        else if (content.Contains("wrong person") || content.Contains("contact my colleague") || content.Contains("reach out to"))
        {
            category = ReplyIntentCategory.WrongPerson;
            action = "Extract referral contact and create new targeted prospect.";
        }
        else
        {
            category = ReplyIntentCategory.NotInterested;
            action = "Archive prospect or schedule polite rebuttal in 30 days.";
        }

        return Task.FromResult(new ClassifiedReplyDto(
            subject,
            body,
            category,
            confidence,
            action,
            autoCalendar
        ));
    }
}
