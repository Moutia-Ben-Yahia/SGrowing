using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Conversations;

public record OperatorPresenceDto(
    Guid ConversationId,
    string OperatorId,
    string OperatorName,
    bool IsTyping,
    DateTime LastSeenUtc
);

public record SlaBreachAlertDto(
    Guid ConversationId,
    string CustomerName,
    ConversationPriority Priority,
    string PolicyName,
    int ElapsedMinutes,
    int TargetMinutes,
    bool IsBreached,
    int MinutesRemaining
);

public record CsatFeedbackDto(
    Guid FeedbackId,
    Guid ConversationId,
    int RatingScore, // 1 to 5
    string? FeedbackComment,
    DateTime SubmittedAtUtc
);

public interface ICollisionDetectionAndSlaService
{
    Task<List<OperatorPresenceDto>> UpdatePresenceAsync(Guid tenantId, Guid conversationId, string operatorId, string operatorName, bool isTyping);
    Task<List<OperatorPresenceDto>> GetActiveViewersAsync(Guid tenantId, Guid conversationId);
    Task<List<SlaBreachAlertDto>> EvaluateSlaBreachesAsync(Guid tenantId);
    Task<CsatFeedbackDto> RecordCsatFeedbackAsync(Guid tenantId, Guid conversationId, int ratingScore, string? comment);
}

public class CollisionDetectionAndSlaService : ICollisionDetectionAndSlaService
{
    private readonly SGrowingDbContext _db;
    private static readonly Dictionary<string, OperatorPresenceDto> _presenceRegistry = new();
    private static readonly List<CsatFeedbackDto> _feedbackStore = new();

    public CollisionDetectionAndSlaService(SGrowingDbContext db)
    {
        _db = db;
    }

    public Task<List<OperatorPresenceDto>> UpdatePresenceAsync(Guid tenantId, Guid conversationId, string operatorId, string operatorName, bool isTyping)
    {
        var key = $"{tenantId}:{conversationId}:{operatorId}";
        var dto = new OperatorPresenceDto(conversationId, operatorId, operatorName, isTyping, DateTime.UtcNow);
        _presenceRegistry[key] = dto;

        return GetActiveViewersAsync(tenantId, conversationId);
    }

    public Task<List<OperatorPresenceDto>> GetActiveViewersAsync(Guid tenantId, Guid conversationId)
    {
        var prefix = $"{tenantId}:{conversationId}:";
        var cutoff = DateTime.UtcNow.AddSeconds(-30); // 30s presence window

        var active = _presenceRegistry
            .Where(kvp => kvp.Key.StartsWith(prefix) && kvp.Value.LastSeenUtc >= cutoff)
            .Select(kvp => kvp.Value)
            .ToList();

        return Task.FromResult(active);
    }

    public async Task<List<SlaBreachAlertDto>> EvaluateSlaBreachesAsync(Guid tenantId)
    {
        var policies = await _db.SlaPolicies
            .Where(p => p.TenantId == tenantId && p.IsActive)
            .ToListAsync();

        var openConversations = await _db.Conversations
            .Where(c => c.TenantId == tenantId && c.Status == ConversationStatus.Open)
            .OrderBy(c => c.CreatedAtUtc)
            .Take(50)
            .ToListAsync();

        var alerts = new List<SlaBreachAlertDto>();
        var now = DateTime.UtcNow;

        foreach (var conv in openConversations)
        {
            var policy = policies.FirstOrDefault(p => p.Priority == conv.Priority)
                         ?? policies.FirstOrDefault()
                         ?? SlaPolicy.Create(tenantId, "Standard SLA", conv.Priority, 30, 240);

            var elapsed = (int)(now - conv.CreatedAtUtc).TotalMinutes;
            var isBreached = elapsed > policy.TargetFirstResponseMinutes;
            var remaining = Math.Max(0, policy.TargetFirstResponseMinutes - elapsed);

            alerts.Add(new SlaBreachAlertDto(
                conv.Id,
                conv.ContactId.ToString(),
                conv.Priority,
                policy.Name,
                elapsed,
                policy.TargetFirstResponseMinutes,
                isBreached,
                remaining
            ));
        }

        return alerts;
    }

    public Task<CsatFeedbackDto> RecordCsatFeedbackAsync(Guid tenantId, Guid conversationId, int ratingScore, string? comment)
    {
        if (ratingScore < 1 || ratingScore > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(ratingScore), "CSAT rating must be between 1 and 5.");
        }

        var feedback = new CsatFeedbackDto(Guid.NewGuid(), conversationId, ratingScore, comment, DateTime.UtcNow);
        _feedbackStore.Add(feedback);
        return Task.FromResult(feedback);
    }
}
