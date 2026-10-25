using SGrowing.Api.Data;
using SGrowing.Core.Common;
using Microsoft.EntityFrameworkCore;

namespace SGrowing.Api.Modules.CRM;

public class GetContactHandler
{
    private readonly SGrowingDbContext _db;

    public GetContactHandler(SGrowingDbContext db)
    {
        _db = db;
    }

    public async Task<Result<ContactDto>> HandleAsync(Guid tenantId, Guid contactId, CancellationToken cancellationToken = default)
    {
        var contact = await _db.Contacts
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == contactId, cancellationToken);

        if (contact == null)
            return Result<ContactDto>.Failure("Contact not found.");

        return Result<ContactDto>.Success(new ContactDto(
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
        ));
    }
}
