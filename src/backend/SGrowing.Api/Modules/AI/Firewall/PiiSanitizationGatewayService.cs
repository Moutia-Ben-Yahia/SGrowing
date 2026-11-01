using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace SGrowing.Api.Modules.AI.Firewall;

public record PiiRedactedEntity(
    string EntityType,
    string OriginalSnippet,
    string MaskedValue,
    int StartIndex,
    int Length
);

public record PiiSanitizationResult(
    string OriginalText,
    string SanitizedText,
    bool ContainsPii,
    IReadOnlyList<PiiRedactedEntity> RedactedEntities,
    IReadOnlyList<string> RedactedTypes
);

public interface IPiiSanitizationGatewayService
{
    PiiSanitizationResult Sanitize(string input);
    bool ContainsPii(string input);
}

public class PiiSanitizationGatewayService : IPiiSanitizationGatewayService
{
    private readonly ILogger<PiiSanitizationGatewayService>? _logger;

    // Credit Card candidate pattern (13-19 digits with optional spaces or dashes)
    private static readonly Regex CreditCardCandidateRegex = new(
        @"\b(?:\d[ -]*?){13,19}\b",
        RegexOptions.Compiled);

    // US SSN: 9 digits in 3-2-4 format
    private static readonly Regex UsSsnRegex = new(
        @"\b\d{3}-\d{2}-\d{4}\b",
        RegexOptions.Compiled);

    // Algerian NIN (Numéro d'Identification Nationale): exactly 18 digits
    private static readonly Regex AlgerianNinRegex = new(
        @"\b\d{18}\b",
        RegexOptions.Compiled);

    // French NIR (Numéro de Sécurité Sociale): 13 or 15 digits starting with 1 or 2
    private static readonly Regex FrenchNirRegex = new(
        @"\b[12]\s?\d{2}\s?(?:0[1-9]|1[0-2]|20|[2-9]\d)\s?\d{2}\s?\d{3}\s?\d{3}(?:\s?\d{2})?\b",
        RegexOptions.Compiled);

    // Well-known API key patterns
    private static readonly Regex KnownApiKeyRegex = new(
        @"\b(?:sk-(?:proj-)?[a-zA-Z0-9_\-]{20,}|(?:sk|pk)_(?:live|test)_[0-9a-zA-Z]{24,}|AKIA[0-9A-Z]{16}|gh[pousr]_[A-Za-z0-9_]{36,})\b",
        RegexOptions.Compiled);

    // Generic API Key / Token in key-value context
    private static readonly Regex KeyValueApiKeyRegex = new(
        @"(?i)\b(?:api[_-]?key|bearer|access_token|auth_token)\s*[:=]\s*[""']?([a-zA-Z0-9_\-\.]{20,})[""']?",
        RegexOptions.Compiled);

    // Plaintext password in key-value context
    private static readonly Regex KeyValuePasswordRegex = new(
        @"(?i)\b(?:password|passwd|pwd|client_secret)\s*[:=]\s*[""']?([^""' \r\n\t]{6,})[""']?",
        RegexOptions.Compiled);

    public PiiSanitizationGatewayService(ILogger<PiiSanitizationGatewayService>? logger = null)
    {
        _logger = logger;
    }

    public bool ContainsPii(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return false;
        var res = Sanitize(input);
        return res.ContainsPii;
    }

    public PiiSanitizationResult Sanitize(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return new PiiSanitizationResult(
                OriginalText: input ?? string.Empty,
                SanitizedText: input ?? string.Empty,
                ContainsPii: false,
                RedactedEntities: [],
                RedactedTypes: []
            );
        }

        var detectedList = new List<PiiRedactedEntity>();

        // 1. Credit Cards with Luhn validation
        foreach (Match m in CreditCardCandidateRegex.Matches(input))
        {
            var rawDigits = ExtractDigits(m.Value);
            if (rawDigits.Length is >= 13 and <= 19 && IsValidLuhn(rawDigits))
            {
                detectedList.Add(new PiiRedactedEntity(
                    EntityType: "CreditCard",
                    OriginalSnippet: m.Value,
                    MaskedValue: "[REDACTED_CREDIT_CARD]",
                    StartIndex: m.Index,
                    Length: m.Length
                ));
            }
        }

        // 2. US SSN
        foreach (Match m in UsSsnRegex.Matches(input))
        {
            detectedList.Add(new PiiRedactedEntity(
                EntityType: "SSN",
                OriginalSnippet: m.Value,
                MaskedValue: "[REDACTED_SSN]",
                StartIndex: m.Index,
                Length: m.Length
            ));
        }

        // 3. Algerian NIN (18 digits)
        foreach (Match m in AlgerianNinRegex.Matches(input))
        {
            // Avoid overlapping if already caught by credit card
            if (!detectedList.Any(d => d.StartIndex <= m.Index && d.StartIndex + d.Length >= m.Index + m.Length))
            {
                detectedList.Add(new PiiRedactedEntity(
                    EntityType: "AlgerianNIN",
                    OriginalSnippet: m.Value,
                    MaskedValue: "[REDACTED_ALGERIAN_NIN]",
                    StartIndex: m.Index,
                    Length: m.Length
                ));
            }
        }

        // 4. French NIR
        foreach (Match m in FrenchNirRegex.Matches(input))
        {
            var digits = ExtractDigits(m.Value);
            if (digits.Length is 13 or 15)
            {
                if (!detectedList.Any(d => d.StartIndex <= m.Index && d.StartIndex + d.Length >= m.Index + m.Length))
                {
                    detectedList.Add(new PiiRedactedEntity(
                        EntityType: "FrenchNIR",
                        OriginalSnippet: m.Value,
                        MaskedValue: "[REDACTED_FRENCH_NIR]",
                        StartIndex: m.Index,
                        Length: m.Length
                    ));
                }
            }
        }

        // 5. Known API Keys (OpenAI, Stripe, AWS, GitHub)
        foreach (Match m in KnownApiKeyRegex.Matches(input))
        {
            if (!detectedList.Any(d => d.StartIndex <= m.Index && d.StartIndex + d.Length >= m.Index + m.Length))
            {
                detectedList.Add(new PiiRedactedEntity(
                    EntityType: "ApiKey",
                    OriginalSnippet: m.Value,
                    MaskedValue: "[REDACTED_API_KEY]",
                    StartIndex: m.Index,
                    Length: m.Length
                ));
            }
        }

        // 6. Key-Value API Keys
        foreach (Match m in KeyValueApiKeyRegex.Matches(input))
        {
            var valGroup = m.Groups[1];
            if (valGroup.Success && !detectedList.Any(d => d.StartIndex <= valGroup.Index && d.StartIndex + d.Length >= valGroup.Index + valGroup.Length))
            {
                detectedList.Add(new PiiRedactedEntity(
                    EntityType: "ApiKey",
                    OriginalSnippet: valGroup.Value,
                    MaskedValue: "[REDACTED_API_KEY]",
                    StartIndex: valGroup.Index,
                    Length: valGroup.Length
                ));
            }
        }

        // 7. Plaintext Passwords in Key-Value format
        foreach (Match m in KeyValuePasswordRegex.Matches(input))
        {
            var valGroup = m.Groups[1];
            if (valGroup.Success && !detectedList.Any(d => d.StartIndex <= valGroup.Index && d.StartIndex + d.Length >= valGroup.Index + valGroup.Length))
            {
                detectedList.Add(new PiiRedactedEntity(
                    EntityType: "Password",
                    OriginalSnippet: valGroup.Value,
                    MaskedValue: "[REDACTED_PASSWORD]",
                    StartIndex: valGroup.Index,
                    Length: valGroup.Length
                ));
            }
        }

        if (detectedList.Count == 0)
        {
            return new PiiSanitizationResult(
                OriginalText: input,
                SanitizedText: input,
                ContainsPii: false,
                RedactedEntities: [],
                RedactedTypes: []
            );
        }

        // Sort descending by start index to replace cleanly without offsetting indices
        var sortedEntities = detectedList
            .OrderByDescending(e => e.StartIndex)
            .ThenByDescending(e => e.Length)
            .ToList();

        // Filter out overlapping entities
        var nonOverlapping = new List<PiiRedactedEntity>();
        int lastEnd = int.MaxValue;

        foreach (var entity in sortedEntities)
        {
            int entityEnd = entity.StartIndex + entity.Length;
            if (entityEnd <= lastEnd)
            {
                nonOverlapping.Add(entity);
                lastEnd = entity.StartIndex;
            }
        }

        // Build sanitized text
        var sb = new StringBuilder(input);
        foreach (var entity in nonOverlapping)
        {
            sb.Remove(entity.StartIndex, entity.Length);
            sb.Insert(entity.StartIndex, entity.MaskedValue);
        }

        var resultEntities = nonOverlapping.OrderBy(e => e.StartIndex).ToList();
        var redactedTypes = resultEntities.Select(e => e.EntityType).Distinct().OrderBy(t => t).ToList();

        _logger?.LogInformation("PII Sanitization Gateway redacted {Count} entities of types: {Types}",
            resultEntities.Count, string.Join(", ", redactedTypes));

        return new PiiSanitizationResult(
            OriginalText: input,
            SanitizedText: sb.ToString(),
            ContainsPii: true,
            RedactedEntities: resultEntities,
            RedactedTypes: redactedTypes
        );
    }

    /// <summary>
    /// Validates numeric sequence against Luhn algorithm (Mod 10).
    /// </summary>
    public static bool IsValidLuhn(string number)
    {
        if (string.IsNullOrWhiteSpace(number) || number.Length < 13 || number.Length > 19)
        {
            return false;
        }

        int sum = 0;
        bool alternate = false;

        for (int i = number.Length - 1; i >= 0; i--)
        {
            char c = number[i];
            if (!char.IsDigit(c))
            {
                return false;
            }

            int n = c - '0';
            if (alternate)
            {
                n *= 2;
                if (n > 9)
                {
                    n -= 9;
                }
            }

            sum += n;
            alternate = !alternate;
        }

        return sum % 10 == 0;
    }

    private static string ExtractDigits(string value)
    {
        var sb = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            if (char.IsDigit(c))
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }
}
