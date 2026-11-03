namespace SGrowing.Worker.Services.Dispatchers;

public record WhatsAppDispatchResult(
    bool IsSuccess,
    string? MessageId = null,
    string? ErrorMessage = null,
    bool IsSimulated = false);

public interface IMetaWhatsAppDispatcher
{
    Task<WhatsAppDispatchResult> SendTextMessageAsync(
        Guid tenantId,
        string recipientPhone,
        string message,
        CancellationToken cancellationToken = default);

    Task<WhatsAppDispatchResult> SendTemplateMessageAsync(
        Guid tenantId,
        string recipientPhone,
        string templateName,
        string? languageCode = "en_US",
        Dictionary<string, string>? parameters = null,
        CancellationToken cancellationToken = default);
}
