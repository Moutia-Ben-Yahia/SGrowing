using SGrowing.Api.Modules.Conversations;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.AI;

public interface IAiCopilotService
{
    Task<AiDraft> GenerateDraftAsync(Guid tenantId, Guid conversationId, string incomingMessageContent, CancellationToken cancellationToken = default);
    Task<MessageDto?> AcceptDraftAsync(Guid tenantId, Guid draftId, string? editedReply = null, CancellationToken cancellationToken = default);
    Task<bool> RejectDraftAsync(Guid tenantId, Guid draftId, string? reason = null, CancellationToken cancellationToken = default);
    Task<IEnumerable<AiDraft>> GetDraftsAsync(Guid tenantId, Guid conversationId, CancellationToken cancellationToken = default);
}
