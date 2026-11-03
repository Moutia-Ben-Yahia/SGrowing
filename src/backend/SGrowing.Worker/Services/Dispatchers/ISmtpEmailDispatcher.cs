namespace SGrowing.Worker.Services.Dispatchers;

public record EmailDispatchResult(
    bool IsSuccess,
    string? MessageId = null,
    string? ErrorMessage = null,
    bool IsSimulated = false);

public interface ISmtpEmailDispatcher
{
    Task<EmailDispatchResult> SendEmailAsync(
        Guid tenantId,
        string recipientEmail,
        string subject,
        string body,
        string? senderEmail = null,
        CancellationToken cancellationToken = default);
}
