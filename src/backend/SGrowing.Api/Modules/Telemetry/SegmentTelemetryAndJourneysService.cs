using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Telemetry;

public record SegmentIdentifyDto(
    string UserId,
    string? AnonymousId,
    Dictionary<string, object> Traits,
    DateTime TimestampUtc
);

public record SegmentTrackDto(
    string UserId,
    string? AnonymousId,
    string EventName,
    Dictionary<string, object> Properties,
    DateTime TimestampUtc
);

public record DurableJourneyEvaluationDto(
    Guid JourneyId,
    string UserId,
    string CurrentStepName,
    bool IsConditionMet,
    string NextAction, // "DispatchEmail", "WaitUntilEvent", "ExitJourney"
    int RemainingDelayHours
);

public interface ISegmentTelemetryAndJourneysService
{
    Task<bool> RecordIdentifyEventAsync(Guid tenantId, SegmentIdentifyDto payload);
    Task<bool> RecordTrackEventAsync(Guid tenantId, SegmentTrackDto payload);
    Task<DurableJourneyEvaluationDto> EvaluateDurableJourneyStepAsync(Guid tenantId, Guid journeyId, string userId, string eventTriggerName);
}

public class SegmentTelemetryAndJourneysService : ISegmentTelemetryAndJourneysService
{
    private readonly SGrowingDbContext _db;
    private static readonly List<SegmentTrackDto> _trackedEvents = new();

    public SegmentTelemetryAndJourneysService(SGrowingDbContext db)
    {
        _db = db;
    }

    public async Task<bool> RecordIdentifyEventAsync(Guid tenantId, SegmentIdentifyDto payload)
    {
        // Link to CustomerEvent / Customer profile
        var contactId = Guid.TryParse(payload.UserId, out var cId) ? cId : Guid.NewGuid();
        var ev = CustomerEvent.Create(
            tenantId,
            contactId,
            "identify",
            System.Text.Json.JsonSerializer.Serialize(payload.Traits)
        );
        _db.CustomerEvents.Add(ev);
        await _db.SaveChangesAsync();

        return true;
    }

    public async Task<bool> RecordTrackEventAsync(Guid tenantId, SegmentTrackDto payload)
    {
        _trackedEvents.Add(payload);

        var contactId = Guid.TryParse(payload.UserId, out var cId) ? cId : Guid.NewGuid();
        var ev = CustomerEvent.Create(
            tenantId,
            contactId,
            payload.EventName,
            System.Text.Json.JsonSerializer.Serialize(payload.Properties)
        );
        _db.CustomerEvents.Add(ev);
        await _db.SaveChangesAsync();

        return true;
    }

    public Task<DurableJourneyEvaluationDto> EvaluateDurableJourneyStepAsync(Guid tenantId, Guid journeyId, string userId, string eventTriggerName)
    {
        // Simulate Temporal.io stateful wait-until condition evaluation
        bool conditionMet = eventTriggerName.Equals("project_created", StringComparison.OrdinalIgnoreCase) ||
                           eventTriggerName.Equals("checkout_completed", StringComparison.OrdinalIgnoreCase);

        return Task.FromResult(new DurableJourneyEvaluationDto(
            journeyId,
            userId,
            conditionMet ? "Send Milestone Congratulatory Email" : "Wait 3 Days For Activation",
            conditionMet,
            conditionMet ? "DispatchEmail" : "WaitUntilEvent",
            conditionMet ? 0 : 72
        ));
    }
}
