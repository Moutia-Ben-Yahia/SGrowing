using SGrowing.Api.Data;
using SGrowing.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace SGrowing.Api.Modules.CRM;

public class LeadEnrichmentService
{
    private readonly SGrowingDbContext _db;
    private readonly ILogger<LeadEnrichmentService>? _logger;

    public LeadEnrichmentService(SGrowingDbContext db, ILogger<LeadEnrichmentService>? logger = null)
    {
        _db = db;
        _logger = logger;
    }

    public static string ExtractDomainFromEmailOrWeb(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var trimmed = input.Trim().ToLowerInvariant();

        if (trimmed.Contains('@'))
        {
            var parts = trimmed.Split('@');
            return parts.Length > 1 ? parts[1] : string.Empty;
        }

        if (trimmed.StartsWith("http://") || trimmed.StartsWith("https://"))
        {
            if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
            {
                return uri.Host.StartsWith("www.") ? uri.Host.Substring(4) : uri.Host;
            }
        }

        return trimmed.StartsWith("www.") ? trimmed.Substring(4) : trimmed;
    }

    public async Task<LeadEnrichmentProfile> EnrichLeadAsync(
        Guid tenantId,
        string domainOrEmail,
        CancellationToken cancellationToken = default)
    {
        var domain = ExtractDomainFromEmailOrWeb(domainOrEmail);
        if (string.IsNullOrWhiteSpace(domain))
        {
            throw new ArgumentException("A valid email or domain must be provided for enrichment.", nameof(domainOrEmail));
        }

        // Check if cached enrichment profile exists
        var existingProfile = await _db.LeadEnrichmentProfiles
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Domain == domain, cancellationToken);

        if (existingProfile != null)
        {
            return existingProfile;
        }

        // Firmographic heuristic resolver (simulating Clearbit / Apollo / Harmonic API)
        string companyName = CapitalizeDomain(domain);
        string industry = DetectIndustry(domain);
        string employeeRange = DetectEmployeeRange(domain);
        string revenueRange = DetectRevenueRange(employeeRange);
        string techStack = DetectTechStack(domain);
        string linkedin = $"https://linkedin.com/company/{domain.Split('.')[0]}";
        string twitter = $"@{domain.Split('.')[0]}";

        var profile = LeadEnrichmentProfile.Create(
            tenantId,
            domain,
            companyName,
            industry,
            employeeRange,
            revenueRange,
            techStack,
            linkedin,
            twitter);

        await _db.LeadEnrichmentProfiles.AddAsync(profile, cancellationToken);

        // Auto-update or associate company in CRM if exists
        var company = await _db.Companies
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Domain == domain, cancellationToken);

        if (company != null)
        {
            company.Industry = industry;
            company.EmployeeCount = ParseEmployeeMidpoint(employeeRange);
        }

        await _db.SaveChangesAsync(cancellationToken);
        _logger?.LogInformation("Enriched domain {Domain} for tenant {TenantId}. Industry: {Industry}, Headcount: {Range}",
            domain, tenantId, industry, employeeRange);

        return profile;
    }

    private static string CapitalizeDomain(string domain)
    {
        var namePart = domain.Split('.')[0];
        if (namePart.Length <= 1) return namePart.ToUpperInvariant();
        return char.ToUpperInvariant(namePart[0]) + namePart.Substring(1);
    }

    private static string DetectIndustry(string domain)
    {
        if (domain.Contains("bank") || domain.Contains("pay") || domain.Contains("stripe") || domain.Contains("fin")) return "Fintech & Payments";
        if (domain.Contains("health") || domain.Contains("care") || domain.Contains("med")) return "Healthcare & Biotech";
        if (domain.Contains("shop") || domain.Contains("store") || domain.Contains("cart")) return "E-Commerce & Retail";
        if (domain.Contains("edu") || domain.Contains("learn") || domain.Contains("school")) return "Education Technology";
        return "B2B Enterprise SaaS";
    }

    private static string DetectEmployeeRange(string domain)
    {
        int length = domain.Length;
        if (length <= 9) return "1000+";
        if (length <= 13) return "201-500";
        if (length <= 16) return "51-200";
        return "11-50";
    }

    private static string DetectRevenueRange(string employeeRange)
    {
        return employeeRange switch
        {
            "1000+" => "$50M+",
            "201-500" => "$10M-$50M",
            "51-200" => "$3M-$10M",
            _ => "$500K-$2M"
        };
    }

    private static string DetectTechStack(string domain)
    {
        return "PostgreSQL, React, Next.js, AWS, Redis, Twilio, OpenAI";
    }

    private static int ParseEmployeeMidpoint(string range)
    {
        return range switch
        {
            "1000+" => 1500,
            "201-500" => 350,
            "51-200" => 125,
            _ => 30
        };
    }
}
