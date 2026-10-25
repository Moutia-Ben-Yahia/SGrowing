using MarketingStudio.Core.Entities;

namespace MarketingStudio.Api.Modules.Outbound;

public record SdrPersonalizationInput(
    string FirstName,
    string LastName,
    string Company,
    string? Industry = null,
    string? Role = null
);

public record SdrPersonalizationOutput(
    string Icebreaker,
    string ProblemStatement,
    string CallToAction
);

public interface IAiSdrService
{
    Task<SdrPersonalizationOutput> GeneratePersonalizationAsync(SdrPersonalizationInput input, CancellationToken cancellationToken = default);
    string RenderTemplate(string template, Contact contact, Company? company, SdrPersonalizationOutput? sdrOutput);
}
