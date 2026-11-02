using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace SGrowing.Api.Modules.AI.Governance;

public interface IAiGovernanceEngine
{
    PromptInspectionResult InspectPrompt(InspectPromptRequest request);
    ModelRouteDecision RouteModel(RouteModelRequest request);
    OutputAuditResult AuditOutput(AuditOutputRequest request);
    List<PromptInspectionResult> GetThreatAuditLog(string tenantId);
}

public sealed partial class AiGovernanceEngine : IAiGovernanceEngine
{
    private readonly ConcurrentDictionary<string, ConcurrentQueue<PromptInspectionResult>> _threatLogs = new();

    // Regex definitions for PII redaction
    private static readonly Regex EmailRegex = new(
        @"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex CardRegex = new(
        @"\b(?:\d{4}[-\s]?){3}\d{4}\b|\b\d{4}[-\s]?\d{6}[-\s]?\d{5}\b",
        RegexOptions.Compiled);

    private static readonly Regex PhoneRegex = new(
        @"(?:\+?\d{1,3}[-.\s]?)?\(?\d{3}\)?[-.\s]?\d{3}[-.\s]?\d{4}\b",
        RegexOptions.Compiled);

    // Prompt injection threat signatures
    private static readonly (string Pattern, SecurityThreatLevel Level, string Description)[] ThreatSignatures =
    [
        (@"\bignore\s+(?:all\s+)?previous\s+instructions\b", SecurityThreatLevel.Critical, "Jailbreak signature: Ignore previous instructions"),
        (@"\bdisregard\s+(?:all\s+)?prior\s+instructions\b", SecurityThreatLevel.Critical, "Jailbreak signature: Disregard prior instructions"),
        (@"\bdan\s+mode\b|\bdo\s+anything\s+now\b", SecurityThreatLevel.Critical, "Jailbreak signature: DAN (Do Anything Now) mode attempt"),
        (@"\bdeveloper\s+mode\s+(?:override|enabled|on)\b", SecurityThreatLevel.Critical, "Jailbreak signature: Developer mode override"),
        (@"\b(?:unrestricted|unfiltered)\s+ai\b", SecurityThreatLevel.Critical, "Jailbreak signature: Unrestricted/unfiltered persona bypass"),
        (@"\bbypass\s+(?:all\s+)?safety\s+filters\b", SecurityThreatLevel.Critical, "Jailbreak signature: Safety filter bypass attempt"),
        (@"(?:show|reveal|print|repeat|output|leak)\s+(?:your\s+)?system\s+prompt\b", SecurityThreatLevel.High, "System prompt extraction attempt"),
        (@"\bwhat\s+(?:are|is)\s+your\s+(?:initial\s+)?instructions\b", SecurityThreatLevel.High, "System instruction probe"),
        (@"---(?:END|START)\s+OF\s+PROMPT---", SecurityThreatLevel.High, "Delimiter injection: Prompt boundary spoofing"),
        (@"```system\b|<\|im_start\|>system|<\|im_end\|>", SecurityThreatLevel.High, "Delimiter injection: Chat template delimiter spoofing"),
        (@"\bpretend\s+you\s+have\s+no\s+(?:rules|ethics|guardrails)\b", SecurityThreatLevel.High, "Guardrail suspension roleplay"),
        (@"\bbase64\s+decode\s+and\s+execute\b", SecurityThreatLevel.Medium, "Obfuscated payload execution attempt")
    ];

    // Toxic / Abusive terms
    private static readonly (string Pattern, double Severity, string Term)[] ToxicPatterns =
    [
        (@"\b(?:kill\s+yourself|go\s+die|commit\s+suicide)\b", 1.0, "Self-harm or death incitement"),
        (@"\b(?:bomb\s+making|create\s+explosives|synthesize\s+poisons)\b", 1.0, "Dangerous weapons or synthesis"),
        (@"\b(?:hate\s+all|exterminate|wipe\s+out)\s+(?:races|groups|people)\b", 0.95, "Hate speech / incitement"),
        (@"\b(?:fuck\s+you|asshole|bitch|bastard)\b", 0.70, "Profanity and direct verbal abuse"),
        (@"\b(?:scam\s+artist|fraudulent\s+scum|idiot)\b", 0.60, "Harassment and derogatory defamation")
    ];

    // Unapproved pricing guarantee phrases
    private static readonly (string Pattern, string Description)[] PricingGuaranteeSignatures =
    [
        (@"\$0\s+forever\b", "Unapproved pricing guarantee: $0 forever"),
        (@"\bfree\s+forever\b", "Unapproved pricing guarantee: Free forever"),
        (@"100%\s+discount\b", "Unapproved pricing guarantee: 100% discount"),
        (@"\bguaranteed\s+0\s+dollars\b", "Unapproved pricing guarantee: Guaranteed 0 dollars"),
        (@"\blifetime\s+free\s+(?:access|tier|membership)\b", "Unapproved pricing guarantee: Lifetime free access"),
        (@"\bunlimited\s+free\s+credits\b", "Unapproved pricing guarantee: Unlimited free credits")
    ];

    // Hallucination indicators
    private static readonly (string Pattern, double Risk, string Term)[] HallucinationPatterns =
    [
        (@"\b(?:i\s+fabricated\s+this|completely\s+made\s+up|i\s+made\s+this\s+up)\b", 0.95, "Admitted synthetic fabrication"),
        (@"\b(?:as\s+an\s+ai\s+i\s+cannot\s+verify\s+this\s+fact|unverified\s+citation)\b", 0.75, "Unverified factual claim"),
        (@"\b(?:hallucinated\s+reference|fictitious\s+entity\s+citation)\b", 0.85, "Hallucinated citation or entity"),
        (@"\b(?:i\s+am\s+pretending\s+this\s+exists|imaginary\s+statistic)\b", 0.90, "Fictitious statistic or claim")
    ];

    private static readonly Regex PriceExtractorRegex = new(
        @"\$(\d+(?:\.\d{1,2})?)|(\d+(?:\.\d{1,2})?)\s*(?:USD|dollars)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public PromptInspectionResult InspectPrompt(InspectPromptRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var prompt = request.RawPrompt ?? string.Empty;
        var detectedThreats = new List<string>();
        var maxThreatLevel = SecurityThreatLevel.Clean;

        // 1. Scan for Prompt Injections & Jailbreaks
        foreach (var (pattern, level, description) in ThreatSignatures)
        {
            if (Regex.IsMatch(prompt, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                detectedThreats.Add(description);
                if (level > maxThreatLevel)
                {
                    maxThreatLevel = level;
                }
            }
        }

        bool isBlocked = maxThreatLevel >= SecurityThreatLevel.High;

        // 2. Perform PII Redaction if requested
        string sanitizedPrompt = prompt;
        int redactedCount = 0;

        if (request.EnablePiiRedaction)
        {
            // Redact Credit Cards first before phone numbers to prevent overlap
            var cardMatches = CardRegex.Matches(sanitizedPrompt);
            redactedCount += cardMatches.Count;
            if (cardMatches.Count > 0)
            {
                sanitizedPrompt = CardRegex.Replace(sanitizedPrompt, "[CARD_REDACTED]");
            }

            // Redact Emails
            var emailMatches = EmailRegex.Matches(sanitizedPrompt);
            redactedCount += emailMatches.Count;
            if (emailMatches.Count > 0)
            {
                sanitizedPrompt = EmailRegex.Replace(sanitizedPrompt, "[EMAIL_REDACTED]");
            }

            // Redact Phones
            var phoneMatches = PhoneRegex.Matches(sanitizedPrompt);
            redactedCount += phoneMatches.Count;
            if (phoneMatches.Count > 0)
            {
                sanitizedPrompt = PhoneRegex.Replace(sanitizedPrompt, "[PHONE_REDACTED]");
            }
        }

        var result = new PromptInspectionResult(
            IsBlocked: isBlocked,
            ThreatLevel: maxThreatLevel,
            DetectedThreats: detectedThreats,
            SanitizedPrompt: sanitizedPrompt,
            RedactedPiiTokensCount: redactedCount,
            EvaluatedAtUtc: DateTime.UtcNow
        );

        // Record to threat audit log if a threat or block occurred
        if (maxThreatLevel != SecurityThreatLevel.Clean || isBlocked || detectedThreats.Count > 0)
        {
            var queue = _threatLogs.GetOrAdd(request.TenantId, _ => new ConcurrentQueue<PromptInspectionResult>());
            queue.Enqueue(result);
        }

        return result;
    }

    public ModelRouteDecision RouteModel(RouteModelRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        switch (request.Complexity)
        {
            case TaskComplexity.SimpleClassification:
                if (request.PreferLocal || request.MaxLatencyMsAllowed <= 150)
                {
                    return new ModelRouteDecision(
                        PrimaryProvider: AiProvider.OllamaLocal,
                        FallbackProvider: AiProvider.GeminiPro,
                        MaxTokens: 1024,
                        EstimatedCostPer1kTokens: 0.000m,
                        EstimatedLatencyMs: 95,
                        Reason: "Local Ollama on-premise execution selected for zero-cost, ultra-low-latency classification."
                    );
                }

                return new ModelRouteDecision(
                    PrimaryProvider: AiProvider.GeminiPro,
                    FallbackProvider: AiProvider.OllamaLocal,
                    MaxTokens: 2048,
                    EstimatedCostPer1kTokens: 0.0015m,
                    EstimatedLatencyMs: 250,
                    Reason: "Gemini Pro cloud endpoint selected for high-throughput, low-cost classification with local Ollama fallback."
                );

            case TaskComplexity.StandardCopywriting:
                return new ModelRouteDecision(
                    PrimaryProvider: AiProvider.GeminiPro,
                    FallbackProvider: AiProvider.ClaudeSonnet,
                    MaxTokens: 4096,
                    EstimatedCostPer1kTokens: 0.0025m,
                    EstimatedLatencyMs: 420,
                    Reason: "Gemini Pro optimal for high-quality marketing and copywriting throughput with Claude Sonnet fallback."
                );

            case TaskComplexity.ComplexReasoning:
                return new ModelRouteDecision(
                    PrimaryProvider: AiProvider.ClaudeSonnet,
                    FallbackProvider: AiProvider.Gpt4o,
                    MaxTokens: 8192,
                    EstimatedCostPer1kTokens: 0.015m,
                    EstimatedLatencyMs: 820,
                    Reason: "Claude Sonnet selected for deep multi-step logical reasoning with GPT-4o high-availability fallback."
                );

            case TaskComplexity.CodeOrSqlGeneration:
                return new ModelRouteDecision(
                    PrimaryProvider: AiProvider.ClaudeSonnet,
                    FallbackProvider: AiProvider.Gpt4o,
                    MaxTokens: 8192,
                    EstimatedCostPer1kTokens: 0.015m,
                    EstimatedLatencyMs: 780,
                    Reason: "Claude Sonnet selected for superior code and SQL syntax verification with GPT-4o fallback."
                );

            default:
                return new ModelRouteDecision(
                    PrimaryProvider: AiProvider.GeminiPro,
                    FallbackProvider: AiProvider.ClaudeSonnet,
                    MaxTokens: 2048,
                    EstimatedCostPer1kTokens: 0.0020m,
                    EstimatedLatencyMs: 350,
                    Reason: "Default balanced route assigned."
                );
        }
    }

    public OutputAuditResult AuditOutput(AuditOutputRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var output = request.GeneratedOutput ?? string.Empty;
        var flaggedTerms = new List<string>();

        // 1. Toxicity & Abuse Scan
        double maxToxicity = 0.0;
        foreach (var (pattern, severity, term) in ToxicPatterns)
        {
            if (Regex.IsMatch(output, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                flaggedTerms.Add($"[TOXICITY] {term}");
                if (severity > maxToxicity)
                {
                    maxToxicity = severity;
                }
            }
        }

        // 2. Unapproved Pricing Guarantees Scan
        bool pricingViolation = false;
        foreach (var (pattern, desc) in PricingGuaranteeSignatures)
        {
            if (Regex.IsMatch(output, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                flaggedTerms.Add(desc);
                pricingViolation = true;
            }
        }

        // 3. Price Limit Check if MaxPriceCommitmentAllowed specified
        if (request.MaxPriceCommitmentAllowed.HasValue)
        {
            var matches = PriceExtractorRegex.Matches(output);
            foreach (Match match in matches)
            {
                var valStr = !string.IsNullOrEmpty(match.Groups[1].Value)
                    ? match.Groups[1].Value
                    : match.Groups[2].Value;

                if (decimal.TryParse(valStr, NumberStyles.Number, CultureInfo.InvariantCulture, out var price))
                {
                    if (price > request.MaxPriceCommitmentAllowed.Value)
                    {
                        flaggedTerms.Add($"Unapproved price commitment: ${price} exceeds limit of ${request.MaxPriceCommitmentAllowed.Value}");
                        pricingViolation = true;
                    }
                }
            }
        }

        // 4. Hallucination Risk Scan
        double maxHallucinationRisk = 0.0;
        foreach (var (pattern, risk, term) in HallucinationPatterns)
        {
            if (Regex.IsMatch(output, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                flaggedTerms.Add($"[HALLUCINATION] {term}");
                if (risk > maxHallucinationRisk)
                {
                    maxHallucinationRisk = risk;
                }
            }
        }

        // Determine overall compliance
        bool passedCompliance = maxToxicity < 0.30
                                && maxHallucinationRisk < 0.40
                                && !pricingViolation;

        return new OutputAuditResult(
            PassedCompliance: passedCompliance,
            ToxicityScore: maxToxicity,
            HallucinationRiskScore: maxHallucinationRisk,
            FlaggedTerms: flaggedTerms,
            AuditId: $"audit-{Guid.NewGuid():N}",
            AuditedAtUtc: DateTime.UtcNow
        );
    }

    public List<PromptInspectionResult> GetThreatAuditLog(string tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return [];
        }

        if (_threatLogs.TryGetValue(tenantId, out var queue))
        {
            return queue.OrderByDescending(x => x.EvaluatedAtUtc).ToList();
        }

        return [];
    }
}
