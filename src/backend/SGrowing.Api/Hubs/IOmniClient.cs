using SGrowing.Api.Modules.Conversations;

namespace SGrowing.Api.Hubs;

public record AiDraftDto(
    Guid Id,
    Guid ConversationId,
    string SuggestedReply,
    float ConfidenceScore,
    string Status,
    DateTime CreatedAtUtc
);

public interface IOmniClient
{
    Task OnMessageReceived(MessageDto message);
    Task OnConversationUpdated(ConversationDto conversation);
    Task OnAiDraftGenerated(AiDraftDto draft);
    Task OnTypingIndicator(Guid conversationId, string senderName, bool isTyping);
}
