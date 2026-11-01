using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SGrowing.Api.Modules.SEO;

public record PSeoFaqTemplate(
    string QuestionTemplate,
    string AnswerTemplate
);

public record PSeoFaqItem(
    string Question,
    string Answer
);

public record PSeoPricingTierTemplate(
    string TierNameTemplate,
    decimal BasePrice,
    string Currency,
    string DescriptionTemplate,
    IReadOnlyList<string> FeaturesTemplates
);

public record PSeoPricingTier(
    string TierName,
    decimal Price,
    string Currency,
    string FormattedPrice,
    string Description,
    IReadOnlyList<string> Features
);

public record PSeoBatchRequest(
    IReadOnlyList<string> Services,
    IReadOnlyList<string> Locations,
    string BaseUrl = "https://example.com",
    string? TitleTemplate = null,
    string? MetaDescriptionTemplate = null,
    string? H1Template = null,
    string? SlugTemplate = null,
    string? DefaultPhone = null,
    int? Year = null,
    IReadOnlyList<PSeoFaqTemplate>? CustomFaqTemplates = null,
    IReadOnlyList<PSeoPricingTierTemplate>? CustomPricingTemplates = null,
    IReadOnlyDictionary<string, string>? CustomTokens = null,
    IReadOnlyDictionary<string, string>? FallbackTokens = null
);

public record PSeoLandingPageBlueprint(
    string Slug,
    string Title,
    string MetaDescription,
    string H1Heading,
    string CanonicalUrl,
    string Service,
    string City,
    int Year,
    string Phone,
    IReadOnlyList<PSeoFaqItem> Faqs,
    IReadOnlyList<PSeoPricingTier> PricingTiers,
    IReadOnlyDictionary<string, string> ResolvedTokens,
    DateTime GeneratedAtUtc
);

public record PSeoBatchResult(
    int TotalGenerated,
    int ServicesCount,
    int LocationsCount,
    IReadOnlyList<PSeoLandingPageBlueprint> Blueprints,
    DateTime GeneratedAtUtc
);

public interface IProgrammaticSeoGeneratorService
{
    PSeoBatchResult GenerateBatch(PSeoBatchRequest request);
    PSeoLandingPageBlueprint GenerateSingle(string service, string city, PSeoBatchRequest baseRequest);
    string ReplaceTokens(string template, IReadOnlyDictionary<string, string> tokens, IReadOnlyDictionary<string, string>? fallbackTokens = null);
    string GenerateSlug(string service, string city, string? slugTemplate = null);
}

public partial class ProgrammaticSeoGeneratorService : IProgrammaticSeoGeneratorService
{
    private const string DefaultTitleTemplate = "{{service}} in {{city}} | 24/7 Fast Response & Certified Experts";
    private const string DefaultMetaDescTemplate = "Need certified {{service}} in {{city}}? Top-rated local experts, instant dispatch, and upfront pricing in {{year}}. Call {{phone}} now!";
    private const string DefaultH1Template = "Professional {{service}} in {{city}}";
    private const string DefaultSlugTemplate = "{{service}}-{{city}}";
    private const string DefaultPhonePlaceholder = "+213 555 0199";

    [GeneratedRegex(@"\{\{\s*([a-zA-Z0-9_\-]+)\s*\}\}", RegexOptions.Compiled)]
    private static partial Regex TokenPatternRegex();

    [GeneratedRegex(@"[^a-z0-9\-]", RegexOptions.Compiled)]
    private static partial Regex SlugSanitizeRegex();

    [GeneratedRegex(@"-{2,}", RegexOptions.Compiled)]
    private static partial Regex MultipleDashesRegex();

    public PSeoBatchResult GenerateBatch(PSeoBatchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var services = request.Services ?? [];
        var locations = request.Locations ?? [];

        var blueprints = new List<PSeoLandingPageBlueprint>(services.Count * locations.Count);

        foreach (var service in services)
        {
            if (string.IsNullOrWhiteSpace(service)) continue;

            foreach (var location in locations)
            {
                if (string.IsNullOrWhiteSpace(location)) continue;

                var blueprint = GenerateSingle(service.Trim(), location.Trim(), request);
                blueprints.Add(blueprint);
            }
        }

        return new PSeoBatchResult(
            TotalGenerated: blueprints.Count,
            ServicesCount: services.Count,
            LocationsCount: locations.Count,
            Blueprints: blueprints,
            GeneratedAtUtc: DateTime.UtcNow
        );
    }

    public PSeoLandingPageBlueprint GenerateSingle(string service, string city, PSeoBatchRequest baseRequest)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(service);
        ArgumentException.ThrowIfNullOrWhiteSpace(city);
        ArgumentNullException.ThrowIfNull(baseRequest);

        var year = baseRequest.Year ?? DateTime.UtcNow.Year;
        var phone = baseRequest.DefaultPhone ?? DefaultPhonePlaceholder;

        // Build token dictionary
        var tokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["service"] = service,
            ["service_lower"] = service.ToLowerInvariant(),
            ["city"] = city,
            ["city_lower"] = city.ToLowerInvariant(),
            ["year"] = year.ToString(CultureInfo.InvariantCulture),
            ["phone"] = phone
        };

        if (baseRequest.CustomTokens is not null)
        {
            foreach (var (k, v) in baseRequest.CustomTokens)
            {
                tokens[k] = v;
            }
        }

        var fallbackTokens = baseRequest.FallbackTokens;

        // Resolve slug
        var slugTemplate = string.IsNullOrWhiteSpace(baseRequest.SlugTemplate) ? DefaultSlugTemplate : baseRequest.SlugTemplate;
        var slugRaw = ReplaceTokens(slugTemplate, tokens, fallbackTokens);
        var slug = SanitizeSlug(slugRaw);

        // Resolve title tag
        var titleTemplate = string.IsNullOrWhiteSpace(baseRequest.TitleTemplate) ? DefaultTitleTemplate : baseRequest.TitleTemplate;
        var title = ReplaceTokens(titleTemplate, tokens, fallbackTokens);

        // Resolve meta description
        var metaTemplate = string.IsNullOrWhiteSpace(baseRequest.MetaDescriptionTemplate) ? DefaultMetaDescTemplate : baseRequest.MetaDescriptionTemplate;
        var metaDescription = ReplaceTokens(metaTemplate, tokens, fallbackTokens);

        // Resolve H1
        var h1Template = string.IsNullOrWhiteSpace(baseRequest.H1Template) ? DefaultH1Template : baseRequest.H1Template;
        var h1Heading = ReplaceTokens(h1Template, tokens, fallbackTokens);

        // Construct canonical URL
        var baseUrl = (baseRequest.BaseUrl ?? "https://example.com").TrimEnd('/');
        var canonicalUrl = $"{baseUrl}/services/{slug}";

        // Generate Localized FAQs
        var faqs = GenerateLocalizedFaqs(service, city, year, phone, tokens, fallbackTokens, baseRequest.CustomFaqTemplates);

        // Generate Localized Pricing breakdown
        var pricingTiers = GenerateLocalizedPricing(service, city, tokens, fallbackTokens, baseRequest.CustomPricingTemplates);

        return new PSeoLandingPageBlueprint(
            Slug: slug,
            Title: title,
            MetaDescription: metaDescription,
            H1Heading: h1Heading,
            CanonicalUrl: canonicalUrl,
            Service: service,
            City: city,
            Year: year,
            Phone: phone,
            Faqs: faqs,
            PricingTiers: pricingTiers,
            ResolvedTokens: tokens,
            GeneratedAtUtc: DateTime.UtcNow
        );
    }

    public string ReplaceTokens(
        string template,
        IReadOnlyDictionary<string, string> tokens,
        IReadOnlyDictionary<string, string>? fallbackTokens = null)
    {
        if (string.IsNullOrEmpty(template)) return string.Empty;

        return TokenPatternRegex().Replace(template, match =>
        {
            var tokenKey = match.Groups[1].Value.Trim();

            if (tokens.TryGetValue(tokenKey, out var value) && !string.IsNullOrEmpty(value))
            {
                return value;
            }

            if (fallbackTokens is not null && fallbackTokens.TryGetValue(tokenKey, out var fallbackValue) && !string.IsNullOrEmpty(fallbackValue))
            {
                return fallbackValue;
            }

            // If token is missing, strip it cleanly
            return string.Empty;
        }).Trim();
    }

    public string GenerateSlug(string service, string city, string? slugTemplate = null)
    {
        var template = string.IsNullOrWhiteSpace(slugTemplate) ? DefaultSlugTemplate : slugTemplate;
        var tokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["service"] = service,
            ["city"] = city
        };

        var resolved = ReplaceTokens(template, tokens);
        return SanitizeSlug(resolved);
    }

    private static string SanitizeSlug(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        var normalized = input.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);

        foreach (var c in normalized)
        {
            var uc = CharUnicodeInfo.GetUnicodeCategory(c);
            if (uc != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        var asciiString = sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
        asciiString = asciiString.Replace(' ', '-').Replace('_', '-');

        var sanitized = SlugSanitizeRegex().Replace(asciiString, "");
        sanitized = MultipleDashesRegex().Replace(sanitized, "-");

        return sanitized.Trim('-');
    }

    private List<PSeoFaqItem> GenerateLocalizedFaqs(
        string service,
        string city,
        int year,
        string phone,
        IReadOnlyDictionary<string, string> tokens,
        IReadOnlyDictionary<string, string>? fallbackTokens,
        IReadOnlyList<PSeoFaqTemplate>? customTemplates)
    {
        if (customTemplates is { Count: > 0 })
        {
            var customList = new List<PSeoFaqItem>(customTemplates.Count);
            foreach (var t in customTemplates)
            {
                var q = ReplaceTokens(t.QuestionTemplate, tokens, fallbackTokens);
                var a = ReplaceTokens(t.AnswerTemplate, tokens, fallbackTokens);
                customList.Add(new PSeoFaqItem(q, a));
            }
            return customList;
        }

        return
        [
            new PSeoFaqItem(
                $"How quickly can you provide {service} in {city}?",
                $"Our certified {service} technicians in {city} typically respond within 30 to 60 minutes for emergency dispatches."
            ),
            new PSeoFaqItem(
                $"How much does professional {service} cost in {city} in {year}?",
                $"Standard pricing for {service} in {city} starts with a transparent flat-rate diagnostic inspection. All estimates are provided upfront with no hidden fees."
            ),
            new PSeoFaqItem(
                $"Are your {service} specialists licensed and insured in {city}?",
                $"Yes, all our local technicians serving the {city} metropolitan area are fully licensed, vetted, and carry complete liability insurance."
            ),
            new PSeoFaqItem(
                $"Can I book weekend or after-hours {service} in {city}?",
                $"Absolutely! We offer 24/7 on-call emergency {service} across {city}. Call {phone} anytime for priority dispatch."
            )
        ];
    }

    private List<PSeoPricingTier> GenerateLocalizedPricing(
        string service,
        string city,
        IReadOnlyDictionary<string, string> tokens,
        IReadOnlyDictionary<string, string>? fallbackTokens,
        IReadOnlyList<PSeoPricingTierTemplate>? customTemplates)
    {
        if (customTemplates is { Count: > 0 })
        {
            var customTiers = new List<PSeoPricingTier>(customTemplates.Count);
            foreach (var ct in customTemplates)
            {
                var tierName = ReplaceTokens(ct.TierNameTemplate, tokens, fallbackTokens);
                var desc = ReplaceTokens(ct.DescriptionTemplate, tokens, fallbackTokens);
                var features = ct.FeaturesTemplates.Select(f => ReplaceTokens(f, tokens, fallbackTokens)).ToList();

                customTiers.Add(new PSeoPricingTier(
                    TierName: tierName,
                    Price: ct.BasePrice,
                    Currency: ct.Currency,
                    FormattedPrice: $"{ct.BasePrice:N0} {ct.Currency}",
                    Description: desc,
                    Features: features
                ));
            }
            return customTiers;
        }

        return
        [
            new PSeoPricingTier(
                TierName: "Basic Diagnostic & Inspection",
                Price: 49.00m,
                Currency: "USD",
                FormattedPrice: "$49 USD",
                Description: $"Initial on-site assessment for {service} in {city} with comprehensive diagnostics.",
                Features: ["Complete system inspection", "Written transparent estimate", "Applied to repair bill"]
            ),
            new PSeoPricingTier(
                TierName: $"Standard {service}",
                Price: 149.00m,
                Currency: "USD",
                FormattedPrice: "$149 USD",
                Description: $"Complete resolution and scheduled maintenance for {service} anywhere in {city}.",
                Features: ["Certified specialist labor", "OEM replacement parts", "90-day satisfaction guarantee"]
            ),
            new PSeoPricingTier(
                TierName: $"24/7 Emergency {service}",
                Price: 249.00m,
                Currency: "USD",
                FormattedPrice: "$249 USD",
                Description: $"Immediate priority dispatch for urgent {service} in {city} within 60 minutes.",
                Features: ["Immediate technician dispatch", "After-hours & holiday coverage", "1-year warranty on repairs"]
            )
        ];
    }
}
