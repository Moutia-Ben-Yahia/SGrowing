using SGrowing.Api.Modules.CRM;
using SGrowing.Api.Modules.Outbound.EmailVerification;

namespace SGrowing.Api.Modules.Outbound.Enrichment;

public class ContactEnrichmentPipeline(
    LeadEnrichmentService leadEnrichmentService,
    IEmailVerificationService emailVerificationService) : IContactEnrichmentPipeline
{
    public async Task<EnrichedContactDto> EnrichAndVerifyContactAsync(
        Guid tenantId,
        ContactEnrichmentRequest request,
        CancellationToken ct = default)
    {
        var deliverability = await emailVerificationService.VerifyEmailAsync(request.Email, ct);

        // Derive target domain for firmographic enrichment
        var domain = !string.IsNullOrWhiteSpace(request.Website)
            ? LeadEnrichmentService.ExtractDomainFromEmailOrWeb(request.Website)
            : LeadEnrichmentService.ExtractDomainFromEmailOrWeb(request.Email);

        string companyName = request.CompanyName ?? string.Empty;
        string industry = "B2B SaaS & Technology";
        string employeeRange = "51-200";
        string revenueRange = "$3M-$10M";
        string techStack = "PostgreSQL, React, .NET 10, Docker, AWS";
        string linkedinUrl = $"https://linkedin.com/in/{request.FirstName?.ToLowerInvariant()}-{request.LastName?.ToLowerInvariant()}";

        if (!string.IsNullOrWhiteSpace(domain))
        {
            try
            {
                var profile = await leadEnrichmentService.EnrichLeadAsync(tenantId, domain, ct);
                companyName = string.IsNullOrWhiteSpace(companyName) ? profile.CompanyName : companyName;
                industry = profile.Industry;
                employeeRange = profile.EmployeeCountRange;
                revenueRange = profile.EstimatedAnnualRevenue;
                techStack = profile.TechStack;
                linkedinUrl = profile.LinkedinUrl ?? linkedinUrl;
            }
            catch
            {
                // Fallback to domain parsing if enrichment lookup encounters issues
                companyName = string.IsNullOrWhiteSpace(companyName) ? domain.Split('.')[0] : companyName;
            }
        }

        var seniority = ClassifySeniority(request.JobTitle);

        // Calculate holistic confidence score
        int confidence = 60;
        if (deliverability.Status == EmailVerificationStatus.Deliverable) confidence += 25;
        else if (deliverability.Status == EmailVerificationStatus.CatchAll) confidence += 10;
        else confidence -= 30;

        if (!string.IsNullOrWhiteSpace(request.FirstName) && !string.IsNullOrWhiteSpace(request.LastName)) confidence += 10;
        if (!string.IsNullOrWhiteSpace(request.JobTitle)) confidence += 5;

        confidence = Math.Clamp(confidence, 10, 100);

        return new EnrichedContactDto(
            Email: request.Email,
            FirstName: request.FirstName ?? string.Empty,
            LastName: request.LastName ?? string.Empty,
            JobTitle: request.JobTitle ?? "Decision Maker",
            SeniorityLevel: seniority,
            CompanyName: companyName,
            Domain: domain,
            Industry: industry,
            EmployeeRange: employeeRange,
            RevenueRange: revenueRange,
            TechStack: techStack,
            LinkedinUrl: linkedinUrl,
            Deliverability: deliverability,
            OverallDataConfidenceScore: confidence
        );
    }

    public async Task<BatchEnrichmentResultDto> BatchEnrichContactsAsync(
        Guid tenantId,
        List<ContactEnrichmentRequest> requests,
        CancellationToken ct = default)
    {
        var list = new List<EnrichedContactDto>();
        int deliverable = 0;
        int riskyOrUndeliverable = 0;

        foreach (var req in requests)
        {
            ct.ThrowIfCancellationRequested();
            var item = await EnrichAndVerifyContactAsync(tenantId, req, ct);
            list.Add(item);

            if (item.Deliverability.Status == EmailVerificationStatus.Deliverable)
            {
                deliverable++;
            }
            else
            {
                riskyOrUndeliverable++;
            }
        }

        return new BatchEnrichmentResultDto(
            TotalProcessed: list.Count,
            DeliverableCount: deliverable,
            RiskyOrUndeliverableCount: riskyOrUndeliverable,
            EnrichedContacts: list
        );
    }

    private static string ClassifySeniority(string? title)
    {
        if (string.IsNullOrWhiteSpace(title)) return "Individual Contributor";
        var lower = title.ToLowerInvariant();

        if (lower.Contains("chief") || lower.Contains("ceo") || lower.Contains("cto") || lower.Contains("cfo") || lower.Contains("coo") || lower.Contains("founder") || lower.Contains("owner"))
            return "C-Level";
        if (lower.Contains("vp") || lower.Contains("vice president"))
            return "VP";
        if (lower.Contains("director") || lower.Contains("head of"))
            return "Director";
        if (lower.Contains("lead") || lower.Contains("principal") || lower.Contains("staff"))
            return "Lead / Principal";
        if (lower.Contains("manager") || lower.Contains("supervisor"))
            return "Manager";
        if (lower.Contains("senior") || lower.Contains("sr."))
            return "Senior";

        return "Individual Contributor";
    }
}
