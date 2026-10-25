using SGrowing.Api.Data;
using SGrowing.Core.Common;
using Microsoft.EntityFrameworkCore;

namespace SGrowing.Api.Modules.CRM;

public record SearchContactsQuery(Guid TenantId, string SearchTerm);

public class SearchContactsHandler
{
    private readonly SGrowingDbContext _db;

    public SearchContactsHandler(SGrowingDbContext db)
    {
        _db = db;
    }

    public async Task<Result<List<ContactDto>>> HandleAsync(SearchContactsQuery query, CancellationToken cancellationToken = default)
    {
        if (query.TenantId == Guid.Empty)
            return Result<List<ContactDto>>.Failure("TenantId is required.");

        var term = (query.SearchTerm ?? "").Trim().ToLower();

        var contacts = await _db.Contacts
            .Where(c => c.TenantId == query.TenantId)
            .Where(c => string.IsNullOrEmpty(term) ||
                        (c.FirstName != null && c.FirstName.ToLower().Contains(term)) ||
                        (c.LastName != null && c.LastName.ToLower().Contains(term)) ||
                        (c.Email != null && c.Email.ToLower().Contains(term)) ||
                        (c.Phone != null && c.Phone.Contains(term)))
            .OrderByDescending(c => c.CreatedAtUtc)
            .Take(50)
            .ToListAsync(cancellationToken);

        var dtos = contacts.Select(c => new ContactDto(
            c.Id,
            c.TenantId,
            c.FirstName,
            c.LastName,
            c.Email,
            c.Phone,
            c.LifecycleStage,
            c.GetCustomFields(),
            c.Tags,
            c.CreatedAtUtc
        )).ToList();

        return Result<List<ContactDto>>.Success(dtos);
    }
}
