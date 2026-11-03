namespace SGrowing.Worker.Services.Dispatchers;

public record SmsDispatchResult(
    bool IsSuccess,
    string? MessageSid = null,
    string? ErrorMessage = null,
    bool IsSimulated = false);

public interface ITwilioSmsDispatcher
{
    Task<SmsDispatchResult> SendSmsAsync(
        Guid tenantId,
        string recipientPhone,
        string message,
        string? fromNumber = null,
        CancellationToken cancellationToken = default);
}
