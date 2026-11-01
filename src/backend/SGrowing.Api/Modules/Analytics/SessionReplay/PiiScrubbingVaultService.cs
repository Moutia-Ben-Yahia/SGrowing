using System.Text.RegularExpressions;

namespace SGrowing.Api.Modules.Analytics.SessionReplay;

/// <summary>
/// Production-grade privacy scrubber adhering to GDPR Article 25 (Data Protection by Design and by Default)
/// and Algerian Personal Data Protection Law 18-07.
/// </summary>
public sealed partial class PiiScrubbingVaultService : IPiiScrubbingVaultService
{
    private const string MaskedPassword = "••••••••";
    private static readonly string[] DangerousAttributePrefixes = ["on"];
    private static readonly string[] SensitiveFieldNameTokens =
    [
        "pass", "pwd", "cvv", "cvc", "pin", "token", "secret", "apikey",
        "api_key", "auth", "credential", "cardnumber", "cc-number", "creditcard",
        "ssn", "nin", "national_id"
    ];

    [GeneratedRegex(@"\b([a-zA-Z0-9._%+-]+)@([a-zA-Z0-9.-]+\.[a-zA-Z]{2,})\b", RegexOptions.Compiled)]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"(?i)\bNIN[-:\s]*(\d{18})\b", RegexOptions.Compiled)]
    private static partial Regex ExplicitNinRegex();

    [GeneratedRegex(@"(?:(?:\+213|00213|0)[\s.-]?[567](?:[\s.-]?\d){8})\b", RegexOptions.Compiled)]
    private static partial Regex AlgerianPhoneRegex();

    [GeneratedRegex(@"\+(?:[1-9]\d{0,2})[\s.-]?(?:\(?\d{2,4}\)?[\s.-]?){2,4}\d{2}\b", RegexOptions.Compiled)]
    private static partial Regex InternationalPhoneRegex();

    [GeneratedRegex(@"(?i)\b(password|passwd|pwd|secret|token|api_?key|pin|cvv|cvc)\s*[:=]\s*([^\s,;""']+)", RegexOptions.Compiled)]
    private static partial Regex SecretKeyValueRegex();

    [GeneratedRegex(@"<script\b[^<]*(?:(?!<\/script>)<[^<]*)*<\/script>", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex ScriptTagRegex();

    [GeneratedRegex(@"\b(?:\d[ -]*?){13,19}\b", RegexOptions.Compiled)]
    private static partial Regex CardCandidateRegex();

    /// <inheritdoc />
    public bool IsSensitiveField(string fieldName, string? inputType = null)
    {
        if (string.Equals(inputType, "password", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(fieldName))
        {
            return false;
        }

        string normalized = fieldName.Trim().ToLowerInvariant();
        foreach (var token in SensitiveFieldNameTokens)
        {
            if (normalized.Contains(token, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc />
    public string ScrubTextPayload(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        // 1. Strip script tags
        string result = ScriptTagRegex().Replace(text, string.Empty);

        // 2. Mask password / secret key-value patterns
        result = SecretKeyValueRegex().Replace(result, m =>
        {
            string key = m.Groups[1].Value;
            string separator = m.Value.Contains(':') ? ": " : "=";
            return $"{key}{separator}{MaskedPassword}";
        });

        // 3. Mask Explicit Algerian NIN (18 digits)
        result = ExplicitNinRegex().Replace(result, m =>
        {
            string digits = m.Groups[1].Value;
            string last4 = digits.Length >= 4 ? digits[^4..] : digits;
            return $"NIN-****-{last4}";
        });

        // 4. Scan for Credit Card candidates with Luhn validation or raw 18-digit NIN
        result = CardCandidateRegex().Replace(result, m =>
        {
            string digitsOnly = string.Concat(m.Value.Where(char.IsAsciiDigit));

            if (digitsOnly.Length == 18)
            {
                // 18 digits is Algerian National ID (NIN)
                return $"NIN-****-{digitsOnly[^4..]}";
            }

            if (digitsOnly.Length is >= 13 and <= 19 && IsValidLuhn(digitsOnly))
            {
                string last4 = digitsOnly[^4..];
                return $"**** **** **** {last4}";
            }

            return m.Value;
        });

        // 5. Mask Email Addresses
        result = EmailRegex().Replace(result, m =>
        {
            string user = m.Groups[1].Value;
            string domain = m.Groups[2].Value;
            char initial = user.Length > 0 ? user[0] : 'u';
            return $"{initial}***@{domain}";
        });

        // 6. Mask Algerian Phone Numbers
        result = AlgerianPhoneRegex().Replace(result, m =>
        {
            string digitsOnly = string.Concat(m.Value.Where(char.IsAsciiDigit));
            string last2 = digitsOnly.Length >= 2 ? digitsOnly[^2..] : "89";
            return $"+213 *** *** {last2}";
        });

        // 7. Mask Other International Phone Numbers
        result = InternationalPhoneRegex().Replace(result, m =>
        {
            string digitsOnly = string.Concat(m.Value.Where(char.IsAsciiDigit));
            string last2 = digitsOnly.Length >= 2 ? digitsOnly[^2..] : "89";
            return $"+213 *** *** {last2}";
        });

        return result;
    }

    /// <inheritdoc />
    public DomMutationBatch ScrubMutationBatch(DomMutationBatch batch)
    {
        if (batch == null || batch.Mutations == null || batch.Mutations.Count == 0)
        {
            return batch ?? new DomMutationBatch(string.Empty, 0, DateTimeOffset.UtcNow, []);
        }

        var scrubbedMutations = new List<DomMutationOperation>(batch.Mutations.Count);

        foreach (var op in batch.Mutations)
        {
            // Drop or neutralize dangerous script tag mutations
            if (string.Equals(op.TagName, "script", StringComparison.OrdinalIgnoreCase))
            {
                scrubbedMutations.Add(new DomMutationOperation(
                    SequenceNumber: op.SequenceNumber,
                    FrameIndex: op.FrameIndex,
                    Timestamp: op.Timestamp,
                    Type: op.Type,
                    TargetNodePath: op.TargetNodePath,
                    TagName: "script",
                    AttributeDeltas: new Dictionary<string, string?> { ["type"] = "text/plain", ["data-scrubbed"] = "true" },
                    TextContentDelta: "/* [SCRUBBED EXECUTABLE SCRIPT] */",
                    ScrollDelta: op.ScrollDelta,
                    ViewportWidth: op.ViewportWidth,
                    ViewportHeight: op.ViewportHeight));
                continue;
            }

            // Inspect element selector / target node path
            bool isTargetSensitive = IsSensitiveField(op.TargetNodePath ?? string.Empty);

            // Clean attributes
            Dictionary<string, string?>? cleanedAttributes = null;
            if (op.AttributeDeltas != null)
            {
                cleanedAttributes = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

                string? inputType = null;
                string? nameAttr = null;
                op.AttributeDeltas.TryGetValue("type", out inputType);
                op.AttributeDeltas.TryGetValue("name", out nameAttr);

                if (IsSensitiveField(nameAttr ?? string.Empty, inputType))
                {
                    isTargetSensitive = true;
                }

                foreach (var (key, value) in op.AttributeDeltas)
                {
                    // 1. Strip event handler attributes (onload, onerror, onclick, on*)
                    if (IsDangerousAttributeName(key))
                    {
                        continue;
                    }

                    if (value == null)
                    {
                        cleanedAttributes[key] = null;
                        continue;
                    }

                    // 2. Strip javascript: / vbscript: / data: protocols
                    string trimmedVal = value.Trim();
                    if (trimmedVal.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase) ||
                        trimmedVal.StartsWith("vbscript:", StringComparison.OrdinalIgnoreCase) ||
                        trimmedVal.StartsWith("data:text/html", StringComparison.OrdinalIgnoreCase))
                    {
                        cleanedAttributes[key] = "about:blank";
                        continue;
                    }

                    // 3. Mask sensitive input values
                    if (isTargetSensitive && string.Equals(key, "value", StringComparison.OrdinalIgnoreCase))
                    {
                        cleanedAttributes[key] = MaskedPassword;
                        continue;
                    }

                    // 4. General PII scrubbing for attribute text
                    cleanedAttributes[key] = ScrubTextPayload(value);
                }
            }

            // Scrub text content
            string? scrubbedText = op.TextContentDelta;
            if (scrubbedText != null)
            {
                scrubbedText = isTargetSensitive ? MaskedPassword : ScrubTextPayload(scrubbedText);
            }

            scrubbedMutations.Add(new DomMutationOperation(
                SequenceNumber: op.SequenceNumber,
                FrameIndex: op.FrameIndex,
                Timestamp: op.Timestamp,
                Type: op.Type,
                TargetNodePath: op.TargetNodePath,
                TagName: op.TagName,
                AttributeDeltas: cleanedAttributes,
                TextContentDelta: scrubbedText,
                ScrollDelta: op.ScrollDelta,
                ViewportWidth: op.ViewportWidth,
                ViewportHeight: op.ViewportHeight));
        }

        return new DomMutationBatch(
            SessionId: batch.SessionId,
            BatchSequence: batch.BatchSequence,
            CreatedAt: batch.CreatedAt,
            Mutations: scrubbedMutations,
            EstimatedRawBytes: batch.EstimatedRawBytes,
            CompressedBytes: batch.CompressedBytes);
    }

    private static bool IsDangerousAttributeName(string attributeName)
    {
        if (string.IsNullOrWhiteSpace(attributeName))
        {
            return false;
        }

        string lower = attributeName.ToLowerInvariant();
        if (lower.Length > 2 && lower.StartsWith("on", StringComparison.Ordinal) && char.IsLetter(lower[2]))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Validates Luhn formula for credit card checksum verification.
    /// </summary>
    public static bool IsValidLuhn(ReadOnlySpan<char> digits)
    {
        if (digits.Length is < 13 or > 19)
        {
            return false;
        }

        int sum = 0;
        bool alternate = false;

        for (int i = digits.Length - 1; i >= 0; i--)
        {
            char c = digits[i];
            if (!char.IsAsciiDigit(c))
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
}
