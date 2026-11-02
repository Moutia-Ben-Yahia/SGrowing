using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Journeys.OmnichannelSequence;

public interface IOmnichannelSequenceEngine
{
    void RegisterCadence(string tenantId, OmnichannelCadenceDefinition cadence);
    OmnichannelCadenceDefinition? GetCadence(string tenantId, string cadenceId);
    void RegisterRecipient(string tenantId, CadenceRecipient recipient);
    CadenceRecipient? GetRecipient(string tenantId, string recipientId);
    IReadOnlyList<CadenceDispatchHistory> GetDispatchHistory(string tenantId, string? recipientId = null);
    ExecuteCadenceStepResult ExecuteStep(ExecuteCadenceStepRequest request);
}

public class OmnichannelSequenceEngine : IOmnichannelSequenceEngine
{
    private readonly ConcurrentDictionary<string, OmnichannelCadenceDefinition> _cadences = new();
    private readonly ConcurrentDictionary<string, CadenceRecipient> _recipients = new();
    private readonly ConcurrentQueue<CadenceDispatchHistory> _dispatchHistories = new();
    private readonly TimeProvider _timeProvider;

    public OmnichannelSequenceEngine(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public void RegisterCadence(string tenantId, OmnichannelCadenceDefinition cadence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentNullException.ThrowIfNull(cadence);

        var key = BuildTenantKey(tenantId, cadence.CadenceId);
        _cadences[key] = cadence;
    }

    public OmnichannelCadenceDefinition? GetCadence(string tenantId, string cadenceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(cadenceId);

        var key = BuildTenantKey(tenantId, cadenceId);
        return _cadences.TryGetValue(key, out var cadence) ? cadence : null;
    }

    public void RegisterRecipient(string tenantId, CadenceRecipient recipient)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentNullException.ThrowIfNull(recipient);

        var key = BuildTenantKey(tenantId, recipient.RecipientId);
        _recipients[key] = recipient;
    }

    public CadenceRecipient? GetRecipient(string tenantId, string recipientId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(recipientId);

        var key = BuildTenantKey(tenantId, recipientId);
        return _recipients.TryGetValue(key, out var recipient) ? recipient : null;
    }

    public IReadOnlyList<CadenceDispatchHistory> GetDispatchHistory(string tenantId, string? recipientId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        return _dispatchHistories
            .Where(h => h.TenantId == tenantId && (recipientId == null || h.RecipientId == recipientId))
            .OrderByDescending(h => h.DispatchedAtUtc)
            .ToList();
    }

    public ExecuteCadenceStepResult ExecuteStep(ExecuteCadenceStepRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;

        var cadence = GetCadence(request.TenantId, request.CadenceId);
        if (cadence is null)
        {
            return new ExecuteCadenceStepResult(
                ActionDispatched: false,
                ChannelUsed: ChannelType.Email,
                RecipientId: request.RecipientId,
                EscalatedToFallback: false,
                SuppressedDueToFatigue: false,
                StatusMessage: $"Cadence '{request.CadenceId}' not found for tenant '{request.TenantId}'.",
                DispatchedAtUtc: nowUtc
            );
        }

        var step = cadence.Steps.FirstOrDefault(s => s.StepOrder == request.StepOrder);
        if (step is null)
        {
            return new ExecuteCadenceStepResult(
                ActionDispatched: false,
                ChannelUsed: ChannelType.Email,
                RecipientId: request.RecipientId,
                EscalatedToFallback: false,
                SuppressedDueToFatigue: false,
                StatusMessage: $"Step order {request.StepOrder} not found in cadence '{request.CadenceId}'.",
                DispatchedAtUtc: nowUtc
            );
        }

        var recipientKey = BuildTenantKey(request.TenantId, request.RecipientId);
        var recipient = _recipients.GetOrAdd(recipientKey, _ => new CadenceRecipient(
            RecipientId: request.RecipientId,
            Name: $"Recipient {request.RecipientId}",
            Email: $"{request.RecipientId}@example.com",
            PhoneNumber: "+15550000000",
            TimeZoneId: "UTC",
            LeadScore: 50,
            WeeklyTouchpointsCount: 0
        ));

        // 1. Buyer Fatigue Protection
        if (recipient.WeeklyTouchpointsCount >= cadence.MaxWeeklyTouchpoints)
        {
            return new ExecuteCadenceStepResult(
                ActionDispatched: false,
                ChannelUsed: step.Channel,
                RecipientId: request.RecipientId,
                EscalatedToFallback: false,
                SuppressedDueToFatigue: true,
                StatusMessage: $"Suppressed due to buyer fatigue protection: Weekly touchpoints cap of {cadence.MaxWeeklyTouchpoints} reached ({recipient.WeeklyTouchpointsCount} existing).",
                DispatchedAtUtc: nowUtc
            );
        }

        // 2. Fallback Ladder
        var channelToUse = step.Channel;
        var escalatedToFallback = false;
        if (!request.PriorStepEngaged && request.ElapsedHoursSincePriorStep >= step.FallbackTimeout.TotalHours)
        {
            channelToUse = EscalateChannel(step.Channel);
            escalatedToFallback = true;
        }

        // 3. Business Hour Enforcement (09:00 - 18:00 local time)
        var tz = ResolveTimeZone(recipient.TimeZoneId);
        var localTime = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, tz);
        var timeOfDay = localTime.TimeOfDay;
        var startBusinessHours = new TimeSpan(9, 0, 0);
        var endBusinessHours = new TimeSpan(18, 0, 0);

        if (timeOfDay < startBusinessHours || timeOfDay > endBusinessHours)
        {
            return new ExecuteCadenceStepResult(
                ActionDispatched: false,
                ChannelUsed: channelToUse,
                RecipientId: request.RecipientId,
                EscalatedToFallback: escalatedToFallback,
                SuppressedDueToFatigue: false,
                StatusMessage: $"Dispatch deferred: Recipient local time ({localTime:HH:mm} in {tz.Id}) is outside business hours (09:00 - 18:00).",
                DispatchedAtUtc: nowUtc
            );
        }

        // 4. Dispatch action & increment touchpoints
        _recipients.AddOrUpdate(
            recipientKey,
            recipient with { WeeklyTouchpointsCount = recipient.WeeklyTouchpointsCount + 1 },
            (_, current) => current with { WeeklyTouchpointsCount = current.WeeklyTouchpointsCount + 1 }
        );

        var statusMessage = escalatedToFallback
            ? $"Step {request.StepOrder} escalated to fallback channel {channelToUse} after {request.ElapsedHoursSincePriorStep}h unengaged timeout."
            : $"Step {request.StepOrder} dispatched successfully via {channelToUse}.";

        var history = new CadenceDispatchHistory(
            TenantId: request.TenantId,
            CadenceId: request.CadenceId,
            RecipientId: request.RecipientId,
            StepOrder: request.StepOrder,
            ChannelUsed: channelToUse,
            EscalatedToFallback: escalatedToFallback,
            DispatchedAtUtc: nowUtc,
            StatusMessage: statusMessage
        );
        _dispatchHistories.Enqueue(history);

        return new ExecuteCadenceStepResult(
            ActionDispatched: true,
            ChannelUsed: channelToUse,
            RecipientId: request.RecipientId,
            EscalatedToFallback: escalatedToFallback,
            SuppressedDueToFatigue: false,
            StatusMessage: statusMessage,
            DispatchedAtUtc: nowUtc
        );
    }

    public static ChannelType EscalateChannel(ChannelType channel) => channel switch
    {
        ChannelType.WhatsApp => ChannelType.Email,
        ChannelType.Email => ChannelType.Sms,
        ChannelType.Sms => ChannelType.VoiceSdr,
        ChannelType.VoiceSdr => ChannelType.VoiceSdr,
        _ => channel
    };

    private static string BuildTenantKey(string tenantId, string entityId) => $"{tenantId}:{entityId}";

    private static TimeZoneInfo ResolveTimeZone(string timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
            return TimeZoneInfo.Utc;

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch
        {
            if (TimeSpan.TryParse(timeZoneId, out var offset))
            {
                return TimeZoneInfo.CreateCustomTimeZone(timeZoneId, offset, timeZoneId, timeZoneId);
            }
            return TimeZoneInfo.Utc;
        }
    }
}
