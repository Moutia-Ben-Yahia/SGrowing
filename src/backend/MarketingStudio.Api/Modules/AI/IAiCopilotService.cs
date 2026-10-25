using MarketingStudio.Core.Entities;

namespace MarketingStudio.Api.Modules.AI;

public interface IAiCopilotService
{
    Task<AiDraft> GenerateDraftAsync(Guid tenantId, Guid conversationId, string incomingMessageContent, CancellationToken cancellationToken = default);
}
