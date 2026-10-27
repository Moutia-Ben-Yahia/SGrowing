using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Conversations.Sla;

public class SlaEscalationEngineService(SGrowingDbContext db) : ISlaEscalationEngineService
{
    public async Task<CsatSurveyDispatchResult> TriggerCsatSurveyAsync(
        Guid tenantId,
        Guid conversationId,
        string channel = "email",
        string baseUrl = "https://feedback.sgrowing.io",
        CancellationToken ct = default)
    {
        var conv = await db.Conversations
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == conversationId, ct);

        var surveyId = Guid.NewGuid();
        var token = Guid.NewGuid().ToString("N");
        var surveyUrl = $"{baseUrl.TrimEnd('/')}/csat/{token}?conversationId={conversationId}";

        var message = $"How would you rate your recent experience with our support team? Please share your feedback in 1 click: {surveyUrl}";

        // Persist outbox event for telemetry / notifications
        var outbox = OutboxMessage.Create(
            tenantId,
            "CsatSurveyTriggeredEvent",
            $"{{\"surveyId\":\"{surveyId}\",\"conversationId\":\"{conversationId}\",\"channel\":\"{channel}\",\"surveyUrl\":\"{surveyUrl}\"}}"
        );
        db.OutboxMessages.Add(outbox);
        await db.SaveChangesAsync(ct);

        return new CsatSurveyDispatchResult(
            SurveyId: surveyId,
            ConversationId: conversationId,
            Channel: channel,
            SurveyUrl: surveyUrl,
            FormattedMessage: message,
            DispatchedAtUtc: DateTime.UtcNow
        );
    }

    public async Task<SlaEscalationEvaluationResult> EvaluateEscalationPoliciesAsync(
        Guid tenantId,
        CancellationToken ct = default)
    {
        var policies = await db.SlaPolicies
            .Where(p => p.TenantId == tenantId && p.IsActive)
            .ToListAsync(ct);

        var openConversations = await db.Conversations
            .Where(c => c.TenantId == tenantId && c.Status == ConversationStatus.Open)
            .OrderBy(c => c.CreatedAtUtc)
            .Take(100)
            .ToListAsync(ct);

        var escalations = new List<SlaEscalationItemDto>();
        int warningCount = 0;
        int breachedCount = 0;
        int criticalCount = 0;
        bool hasChanges = false;
        var now = DateTime.UtcNow;

        foreach (var conv in openConversations)
        {
            var policy = policies.FirstOrDefault(p => p.Priority == conv.Priority)
                         ?? policies.FirstOrDefault()
                         ?? SlaPolicy.Create(tenantId, "Standard SLA", conv.Priority, 30, 240);

            var elapsedMinutes = (int)(now - conv.CreatedAtUtc).TotalMinutes;
            var targetMinutes = policy.TargetFirstResponseMinutes > 0 ? policy.TargetFirstResponseMinutes : 30;

            var ratio = (double)elapsedMinutes / targetMinutes;
            SlaEscalationTier tier = SlaEscalationTier.Normal;
            bool priorityElevated = false;
            string action = "Within expected SLA bounds.";

            if (ratio >= 1.5)
            {
                tier = SlaEscalationTier.Critical;
                criticalCount++;
                action = "Critical SLA violation (150%+ elapsed): Immediate supervisor takeover and urgent routing required.";
                if (conv.Priority != ConversationPriority.Urgent)
                {
                    conv.Priority = ConversationPriority.Urgent;
                    conv.IsSlaBreached = true;
                    priorityElevated = true;
                    hasChanges = true;
                }
            }
            else if (ratio >= 1.0)
            {
                tier = SlaEscalationTier.Breached;
                breachedCount++;
                action = "SLA Target breached (100% elapsed): Auto-elevate conversation priority to High/Urgent.";
                if (!conv.IsSlaBreached)
                {
                    conv.IsSlaBreached = true;
                    if (conv.Priority == ConversationPriority.Low)
                    {
                        conv.Priority = ConversationPriority.Normal;
                    }
                    else if (conv.Priority == ConversationPriority.Normal)
                    {
                        conv.Priority = ConversationPriority.High;
                    }
                    priorityElevated = true;
                    hasChanges = true;
                }
            }
            else if (ratio >= 0.75)
            {
                tier = SlaEscalationTier.Warning;
                warningCount++;
                action = "SLA Warning threshold (75% elapsed): Priority alert flagged to online assigned operators.";
            }

            if (tier != SlaEscalationTier.Normal)
            {
                escalations.Add(new SlaEscalationItemDto(
                    ConversationId: conv.Id,
                    Priority: conv.Priority,
                    PolicyName: policy.Name,
                    ElapsedMinutes: elapsedMinutes,
                    TargetMinutes: targetMinutes,
                    EscalationTier: tier,
                    PriorityElevated: priorityElevated,
                    RecommendedAction: action,
                    DetectedAtUtc: now
                ));
            }
        }

        if (hasChanges)
        {
            await db.SaveChangesAsync(ct);
        }

        return new SlaEscalationEvaluationResult(
            TenantId: tenantId,
            EvaluatedAtUtc: now,
            TotalActiveEvaluated: openConversations.Count,
            TotalWarning: warningCount,
            TotalBreached: breachedCount,
            TotalCritical: criticalCount,
            Escalations: escalations
        );
    }
}
