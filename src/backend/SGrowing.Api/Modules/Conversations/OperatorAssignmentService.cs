using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Conversations;

public record OperatorProfile(
    Guid Id,
    Guid TenantId,
    string FullName,
    string Email,
    string Role,
    List<string> Languages,
    int ActiveConversationsCount,
    bool IsOnline
);

public record AssignmentResult(
    Guid ConversationId,
    Guid OperatorId,
    string OperatorName,
    string Reason
);

public interface IOperatorAssignmentService
{
    Task<AssignmentResult?> AssignConversationAsync(Guid tenantId, Guid conversationId, string? preferredLanguage = null, CancellationToken ct = default);
    Task<List<OperatorProfile>> GetAvailableOperatorsAsync(Guid tenantId, CancellationToken ct = default);
}

public class OperatorAssignmentService : IOperatorAssignmentService
{
    private readonly SGrowingDbContext _db;

    public OperatorAssignmentService(SGrowingDbContext db)
    {
        _db = db;
    }

    public Task<List<OperatorProfile>> GetAvailableOperatorsAsync(Guid tenantId, CancellationToken ct = default)
    {
        // Default roster of specialized omnichannel support & SDR agents
        var operators = new List<OperatorProfile>
        {
            new(
                Id: Guid.Parse("11111111-1111-1111-1111-111111111101"),
                TenantId: tenantId,
                FullName: "Youssef Ben Salem",
                Email: "youssef.b@sgrowing.app",
                Role: "Senior Enterprise SDR",
                Languages: new List<string> { "ar-TN", "fr-FR", "en-US" },
                ActiveConversationsCount: 3,
                IsOnline: true
            ),
            new(
                Id: Guid.Parse("11111111-1111-1111-1111-111111111102"),
                TenantId: tenantId,
                FullName: "Amira Gharbi",
                Email: "amira.g@sgrowing.app",
                Role: "Commercial Account Specialist",
                Languages: new List<string> { "fr-FR", "en-US" },
                ActiveConversationsCount: 1,
                IsOnline: true
            ),
            new(
                Id: Guid.Parse("11111111-1111-1111-1111-111111111103"),
                TenantId: tenantId,
                FullName: "Karim Trabelsi",
                Email: "karim.t@sgrowing.app",
                Role: "Logistics & COD Dispatcher",
                Languages: new List<string> { "ar-TN", "en-US" },
                ActiveConversationsCount: 2,
                IsOnline: true
            )
        };

        return Task.FromResult(operators);
    }

    public async Task<AssignmentResult?> AssignConversationAsync(Guid tenantId, Guid conversationId, string? preferredLanguage = null, CancellationToken ct = default)
    {
        var conversation = await _db.Conversations
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == conversationId, ct)
            ?? _db.Conversations.Local.FirstOrDefault(c => c.TenantId == tenantId && c.Id == conversationId);

        if (conversation == null) return null;

        var availableOps = await GetAvailableOperatorsAsync(tenantId, ct);
        if (!availableOps.Any()) return null;

        // 1. Language matching priority
        OperatorProfile targetOperator;
        if (!string.IsNullOrWhiteSpace(preferredLanguage))
        {
            var languageMatch = availableOps
                .Where(o => o.IsOnline && o.Languages.Any(l => l.Equals(preferredLanguage, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(o => o.ActiveConversationsCount)
                .FirstOrDefault();

            targetOperator = languageMatch ?? availableOps.OrderBy(o => o.ActiveConversationsCount).First();
        }
        else
        {
            // 2. Lowest workload round-robin fallback
            targetOperator = availableOps.OrderBy(o => o.ActiveConversationsCount).First();
        }

        conversation.AssigneeId = targetOperator.Id;
        conversation.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return new AssignmentResult(
            ConversationId: conversation.Id,
            OperatorId: targetOperator.Id,
            OperatorName: targetOperator.FullName,
            Reason: !string.IsNullOrWhiteSpace(preferredLanguage)
                ? $"Matched language skill '{preferredLanguage}' with lowest workload ({targetOperator.ActiveConversationsCount} active)"
                : $"Round-robin workload distribution ({targetOperator.ActiveConversationsCount} active)"
        );
    }
}
