using SGrowing.Api.Data;
using SGrowing.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace SGrowing.Api.Modules.CRM;

public class CompanyAccountService
{
    private readonly SGrowingDbContext _db;

    public CompanyAccountService(SGrowingDbContext db)
    {
        _db = db;
    }

    public async Task<Company> CreateCompanyAsync(
        Guid tenantId,
        string name,
        string domain,
        string? industry = null,
        decimal revenue = 0,
        Guid? parentCompanyId = null,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId cannot be empty.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name cannot be empty.", nameof(name));

        var cleanDomain = ExtractDomain(domain);
        var company = Company.Create(tenantId, name, cleanDomain, industry, revenue, parentCompanyId);

        _db.Companies.Add(company);
        await _db.SaveChangesAsync(cancellationToken);

        return company;
    }

    public async Task<Company?> AssociateContactByDomainAsync(
        Guid tenantId,
        Guid contactId,
        string contactEmail,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(contactEmail))
            return null;

        var domain = ExtractDomain(contactEmail);
        if (string.IsNullOrEmpty(domain))
            return null;

        var company = await _db.Companies
            .FirstOrDefaultAsync(c => c.TenantId == tenantId &&
                (c.Domain.ToLower() == domain ||
                 c.Domain.ToLower() == "www." + domain ||
                 domain == "www." + c.Domain.ToLower()),
                cancellationToken);

        if (company == null)
            return null;

        var contact = await _db.Contacts
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == contactId, cancellationToken);

        if (contact != null)
        {
            contact.CompanyId = company.Id;
            var fields = contact.GetCustomFields();
            fields["companyId"] = company.Id.ToString();
            contact.SetCustomFields(fields);

            await _db.SaveChangesAsync(cancellationToken);
        }

        return company;
    }

    public async Task<Company?> GetCompanyByIdAsync(
        Guid tenantId,
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        return await _db.Companies
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == companyId, cancellationToken);
    }

    public async Task<List<Company>> GetChildCompaniesAsync(
        Guid tenantId,
        Guid parentCompanyId,
        CancellationToken cancellationToken = default)
    {
        return await _db.Companies
            .Where(c => c.TenantId == tenantId && c.ParentCompanyId == parentCompanyId)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Company?> SetParentCompanyAsync(
        Guid tenantId,
        Guid companyId,
        Guid parentCompanyId,
        CancellationToken cancellationToken = default)
    {
        var company = await _db.Companies
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == companyId, cancellationToken);

        if (company == null)
            return null;

        company.ParentCompanyId = parentCompanyId;
        await _db.SaveChangesAsync(cancellationToken);
        return company;
    }

    public async Task<List<Contact>> GetContactsForCompanyAsync(
        Guid tenantId,
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        return await _db.Contacts
            .Where(c => c.TenantId == tenantId && c.CompanyId == companyId)
            .OrderBy(c => c.LastName)
            .ThenBy(c => c.FirstName)
            .ToListAsync(cancellationToken);
    }

    private static string ExtractDomain(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var trimmed = input.Trim().ToLowerInvariant();
        if (trimmed.Contains('@'))
        {
            var parts = trimmed.Split('@');
            return parts.Length > 1 ? parts[1].Trim() : string.Empty;
        }

        // Handle URL like https://acme.com or acme.com/path
        if (trimmed.StartsWith("http://") || trimmed.StartsWith("https://"))
        {
            if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
            {
                return uri.Host.StartsWith("www.") ? uri.Host[4..] : uri.Host;
            }
        }

        var slashIdx = trimmed.IndexOf('/');
        if (slashIdx >= 0)
        {
            trimmed = trimmed[..slashIdx];
        }

        return trimmed.StartsWith("www.") ? trimmed[4..] : trimmed;
    }
}
