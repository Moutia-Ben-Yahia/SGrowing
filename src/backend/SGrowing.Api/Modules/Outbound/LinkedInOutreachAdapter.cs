using Microsoft.Extensions.Logging;
using SGrowing.Core.Outbox;

namespace SGrowing.Api.Modules.Outbound;

public record LinkedInConnectionRequest(
    Guid TenantId,
    Guid ContactId,
    string ProfileUrl,
    string NoteText,
    DateTime SentAtUtc);

public record LinkedInInMail(
    Guid TenantId,
    Guid ContactId,
    string ProfileUrl,
    string Subject,
    string MessageText,
    DateTime SentAtUtc);

public interface ILinkedInOutreachAdapter
{
    Task<bool> SendConnectionRequestAsync(
        Guid tenantId,
        Guid contactId,
        string linkedinProfileUrl,
        string noteText,
        CancellationToken cancellationToken = default);

    Task<bool> SendInMailMessageAsync(
        Guid tenantId,
        Guid contactId,
        string linkedinProfileUrl,
        string subject,
        string messageText,
        CancellationToken cancellationToken = default);
}

public class LinkedInOutreachAdapter : ILinkedInOutreachAdapter
{
    private readonly ILogger<LinkedInOutreachAdapter>? _logger;
    private readonly IOutbox? _outbox;
    private readonly List<LinkedInConnectionRequest> _connectionRequests = new();
    private readonly List<LinkedInInMail> _inMails = new();

    public IReadOnlyList<LinkedInConnectionRequest> SentConnectionRequests => _connectionRequests;
    public IReadOnlyList<LinkedInInMail> SentInMails => _inMails;

    public LinkedInOutreachAdapter(ILogger<LinkedInOutreachAdapter>? logger = null, IOutbox? outbox = null)
    {
        _logger = logger;
        _outbox = outbox;
    }

    public async Task<bool> SendConnectionRequestAsync(
        Guid tenantId,
        Guid contactId,
        string linkedinProfileUrl,
        string noteText,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation(
            "Sending LinkedIn connection request to {ProfileUrl} for contact {ContactId} (Tenant: {TenantId})",
            linkedinProfileUrl, contactId, tenantId);

        _connectionRequests.Add(new LinkedInConnectionRequest(
            tenantId,
            contactId,
            linkedinProfileUrl,
            noteText,
            DateTime.UtcNow));

        if (_outbox != null)
        {
            await _outbox.EnqueueAsync(tenantId, "LinkedInConnectionRequestSent", new
            {
                TenantId = tenantId,
                ContactId = contactId,
                ProfileUrl = linkedinProfileUrl,
                NoteText = noteText,
                DispatchedAtUtc = DateTime.UtcNow
            }, cancellationToken);
        }

        return true;
    }

    public async Task<bool> SendInMailMessageAsync(
        Guid tenantId,
        Guid contactId,
        string linkedinProfileUrl,
        string subject,
        string messageText,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation(
            "Sending LinkedIn InMail to {ProfileUrl} for contact {ContactId} (Tenant: {TenantId})",
            linkedinProfileUrl, contactId, tenantId);

        _inMails.Add(new LinkedInInMail(
            tenantId,
            contactId,
            linkedinProfileUrl,
            subject,
            messageText,
            DateTime.UtcNow));

        if (_outbox != null)
        {
            await _outbox.EnqueueAsync(tenantId, "LinkedInInMailSent", new
            {
                TenantId = tenantId,
                ContactId = contactId,
                ProfileUrl = linkedinProfileUrl,
                Subject = subject,
                MessageText = messageText,
                DispatchedAtUtc = DateTime.UtcNow
            }, cancellationToken);
        }

        return true;
    }
}
