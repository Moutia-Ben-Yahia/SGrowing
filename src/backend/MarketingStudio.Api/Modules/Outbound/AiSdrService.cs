using MarketingStudio.Core.Entities;

namespace MarketingStudio.Api.Modules.Outbound;

public class AiSdrService : IAiSdrService
{
    public Task<SdrPersonalizationOutput> GeneratePersonalizationAsync(SdrPersonalizationInput input, CancellationToken cancellationToken = default)
    {
        // Dynamic SDR personalization synthesis based on company and industry
        var company = string.IsNullOrWhiteSpace(input.Company) ? "your organization" : input.Company;
        var industry = string.IsNullOrWhiteSpace(input.Industry) ? "the sector" : input.Industry;

        var icebreaker = $"I noticed the impressive work {company} is doing in {industry}.";
        var problemStatement = $"Many {industry} leaders struggle with fragmented cold outreach and customer messaging across WhatsApp and Email.";
        var cta = "Would you be open to a 10-minute demo to see how autonomous AI agents streamline this?";

        return Task.FromResult(new SdrPersonalizationOutput(icebreaker, problemStatement, cta));
    }

    public string RenderTemplate(string template, Contact contact, Company? company, SdrPersonalizationOutput? sdrOutput)
    {
        if (string.IsNullOrEmpty(template)) return "";

        var companyName = company?.Name ?? "your company";
        var icebreaker = sdrOutput?.Icebreaker ?? "";
        var problemStatement = sdrOutput?.ProblemStatement ?? "";
        var cta = sdrOutput?.CallToAction ?? "";

        return template
            .Replace("{{first_name}}", contact.FirstName)
            .Replace("{{last_name}}", contact.LastName)
            .Replace("{{company}}", companyName)
            .Replace("{{icebreaker}}", icebreaker)
            .Replace("{{problem_statement}}", problemStatement)
            .Replace("{{cta}}", cta)
            .Trim();
    }
}
