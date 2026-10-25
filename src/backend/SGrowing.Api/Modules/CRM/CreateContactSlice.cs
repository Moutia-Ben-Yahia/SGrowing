using SGrowing.Api.Data;
using SGrowing.Core.Common;
using SGrowing.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace SGrowing.Api.Modules.CRM;

public record CreateContactRequest(
    Guid TenantId,
    string FirstName,
    string LastName,
    string? Email,
    string? Phone,
    LifecycleStage LifecycleStage,
    Dictionary<string, object>? CustomFields = null,
    List<string>? Tags = null
);

public record ContactDto(
    Guid Id,
    Guid TenantId,
    string FirstName,
    string LastName,
    string? Email,
    string? Phone,
    LifecycleStage LifecycleStage,
    Dictionary<string, object> CustomFields,
    List<string> Tags,
    DateTime CreatedAtUtc
);

public class CreateContactHandler
{
    private readonly SGrowingDbContext _db;

    public CreateContactHandler(SGrowingDbContext db)
    {
        _db = db;
    }

    public async Task<Result<ContactDto>> HandleAsync(CreateContactRequest request, CancellationToken cancellationToken = default)
    {
        if (request.TenantId == Guid.Empty)
            return Result<ContactDto>.Failure("TenantId is required.");

        if (string.IsNullOrWhiteSpace(request.Email) && string.IsNullOrWhiteSpace(request.Phone))
            return Result<ContactDto>.Failure("At least one contact method (Email or Phone) must be provided.");

        var contact = Contact.Create(
            tenantId: request.TenantId,
            firstName: request.FirstName,
            lastName: request.LastName,
            email: request.Email,
            phone: request.Phone,
            stage: request.LifecycleStage,
            customFields: request.CustomFields,
            tags: request.Tags
        );

        _db.Contacts.Add(contact);
        await _db.SaveChangesAsync(cancellationToken);

        var dto = new ContactDto(
            contact.Id,
            contact.TenantId,
            contact.FirstName,
            contact.LastName,
            contact.Email,
            contact.Phone,
            contact.LifecycleStage,
            contact.GetCustomFields(),
            contact.Tags,
            contact.CreatedAtUtc
        );

        return Result<ContactDto>.Success(dto);
    }
}
