using SGrowing.Api.Data;
using SGrowing.Core.Common;
using SGrowing.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace SGrowing.Api.Modules.CRM;

public record UnifiedCustomer360Dto(
    Contact Contact,
    List<Conversation> Conversations,
    List<Deal> Deals,
    List<CustomerEvent> Events
);

public class IdentityResolverService
{
    private readonly SGrowingDbContext _db;

    public IdentityResolverService(SGrowingDbContext db)
    {
        _db = db;
    }

    public async Task<Result<Contact>> ResolveOrCreateContactAsync(
        Guid tenantId,
        string? email,
        string? phone,
        string? firstName,
        string? lastName,
        CancellationToken cancellationToken = default)
    {
        var normEmail = email?.Trim().ToLowerInvariant();
        var normPhone = phone?.Trim().Replace(" ", "").Replace("-", "").Replace("+", "");

        Contact? contact = null;

        if (!string.IsNullOrEmpty(normPhone))
        {
            contact = await _db.Contacts
                .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Phone == normPhone, cancellationToken);
        }

        if (contact == null && !string.IsNullOrEmpty(normEmail))
        {
            contact = await _db.Contacts
                .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Email == normEmail, cancellationToken);
        }

        if (contact != null)
        {
            // Enrich missing fields
            bool changed = false;
            if (string.IsNullOrEmpty(contact.Email) && !string.IsNullOrEmpty(normEmail))
            {
                contact.Email = normEmail;
                changed = true;
            }
            if (string.IsNullOrEmpty(contact.Phone) && !string.IsNullOrEmpty(normPhone))
            {
                contact.Phone = normPhone;
                changed = true;
            }
            if (changed)
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            return Result<Contact>.Success(contact);
        }

        // Create new contact
        var newContact = Contact.Create(
            tenantId,
            firstName ?? "Anonymous",
            lastName ?? "User",
            normEmail,
            normPhone
        );
        _db.Contacts.Add(newContact);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<Contact>.Success(newContact);
    }

    public async Task<Result<UnifiedCustomer360Dto>> GetUnifiedCustomerTimelineAsync(
        Guid tenantId,
        Guid contactId,
        CancellationToken cancellationToken = default)
    {
        var contact = await _db.Contacts
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == contactId, cancellationToken);

        if (contact == null)
            return Result<UnifiedCustomer360Dto>.Failure("Contact not found.");

        var conversations = await _db.Conversations
            .Where(c => c.TenantId == tenantId && c.ContactId == contactId)
            .OrderByDescending(c => c.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var deals = await _db.Deals
            .Where(d => d.TenantId == tenantId && d.ContactId == contactId)
            .OrderByDescending(d => d.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var events = await _db.CustomerEvents
            .Where(e => e.TenantId == tenantId && e.ContactId == contactId)
            .OrderByDescending(e => e.OccurredAtUtc)
            .ToListAsync(cancellationToken);

        return Result<UnifiedCustomer360Dto>.Success(new UnifiedCustomer360Dto(contact, conversations, deals, events));
    }
}
