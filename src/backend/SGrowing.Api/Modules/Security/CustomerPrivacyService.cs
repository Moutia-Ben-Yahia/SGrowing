using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;
using Microsoft.Extensions.Logging;

namespace SGrowing.Api.Modules.Security;

public class PrivacyErasureResult
{
    public bool Success { get; set; }
    public Guid RequestId { get; set; }
    public int ErasedContactsCount { get; set; }
    public int RedactedMessagesCount { get; set; }
    public int RemovedRagChunksCount { get; set; }
    public DateTime CompletedAtUtc { get; set; }
}

public class CustomerPrivacyService
{
    private readonly SGrowingDbContext _db;
    private readonly ILogger<CustomerPrivacyService>? _logger;

    public CustomerPrivacyService(SGrowingDbContext db, ILogger<CustomerPrivacyService>? logger = null)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<PrivacyErasureResult> ExecuteRightToBeForgottenAsync(
        Guid tenantId,
        string contactEmail,
        CancellationToken cancellationToken = default)
    {
        var emailLower = contactEmail.Trim().ToLowerInvariant();

        var request = PrivacyErasureRequest.Create(tenantId, emailLower);
        await _db.PrivacyErasureRequests.AddAsync(request, cancellationToken);

        // 1. Anonymize Contacts
        var contacts = await _db.Contacts
            .Where(c => c.TenantId == tenantId && c.Email != null && c.Email.ToLower() == emailLower)
            .ToListAsync(cancellationToken);

        int erasedContacts = contacts.Count;
        var contactIds = contacts.Select(c => c.Id).ToHashSet();

        foreach (var contact in contacts)
        {
            contact.FirstName = "Anonymized";
            contact.LastName = "Contact";
            contact.Email = $"anonymized_{contact.Id:N}@redacted.internal";
            contact.Phone = null;
        }

        // 2. Redact Messages
        var conversations = await _db.Conversations
            .Where(c => c.TenantId == tenantId && contactIds.Contains(c.ContactId))
            .ToListAsync(cancellationToken);

        var convIds = conversations.Select(c => c.Id).ToHashSet();

        var messages = await _db.Messages
            .Where(m => m.TenantId == tenantId && convIds.Contains(m.ConversationId))
            .ToListAsync(cancellationToken);

        int redactedMessages = messages.Count;
        foreach (var msg in messages)
        {
            msg.Content = "[Personal Data Anonymized via GDPR Right to be Forgotten Request]";
        }

        // 3. Clean up Vector RAG Chunks citing customer email
        var ragChunks = await _db.DocumentChunks
            .Where(ch => ch.TenantId == tenantId && ch.Content.Contains(emailLower))
            .ToListAsync(cancellationToken);

        int removedChunks = ragChunks.Count;
        if (ragChunks.Count > 0)
        {
            _db.DocumentChunks.RemoveRange(ragChunks);
        }

        request.Status = "Completed";
        request.CompletedAtUtc = DateTime.UtcNow;
        request.ErasedRecordsCount = erasedContacts + redactedMessages + removedChunks;

        await _db.SaveChangesAsync(cancellationToken);

        _logger?.LogInformation("GDPR Erasure completed for {Email}. Erased {Contacts} contacts, redacted {Messages} messages, removed {Chunks} chunks.",
            emailLower, erasedContacts, redactedMessages, removedChunks);

        return new PrivacyErasureResult
        {
            Success = true,
            RequestId = request.Id,
            ErasedContactsCount = erasedContacts,
            RedactedMessagesCount = redactedMessages,
            RemovedRagChunksCount = removedChunks,
            CompletedAtUtc = request.CompletedAtUtc.Value
        };
    }
}
