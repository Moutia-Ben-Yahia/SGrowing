using System.Text.RegularExpressions;
using SGrowing.Core.Entities;
using Microsoft.Extensions.Logging;

namespace SGrowing.Api.Modules.AI;

public class PromptFirewallService
{
    private readonly ILogger<PromptFirewallService>? _logger;

    private static readonly (string Pattern, string Category, double Weight)[] ThreatRules = new[]
    {
        (@"ignore\s+(all\s+)?(previous|prior)\s+instructions", "JailbreakAttempt", 0.95),
        (@"disregard\s+(all\s+)?(previous|prior)\s+directives", "JailbreakAttempt", 0.95),
        (@"(reveal|print|show|leak)\s+(your\s+)?(system\s+prompt|instructions|initial\s+prompt)", "SystemPromptExfiltration", 0.90),
        (@"you\s+are\s+now\s+(an?\s+)?(unfiltered|unrestricted|DAN|developer\s+mode)", "PersonaJailbreak", 0.95),
        (@"<\|im_start\|>|<\|im_end\|>|<\|endoftext\|>", "ChatTemplateExploit", 0.99),
        (@"###\s*(SYSTEM|ADMIN|INTERNAL)\s*:", "DelimiterExploit", 0.85),
        (@"\[SYSTEM\s+PROMPT\]", "DelimiterExploit", 0.85)
    };

    public PromptFirewallService(ILogger<PromptFirewallService>? logger = null)
    {
        _logger = logger;
    }

    public PromptFirewallInspectionResult InspectAndSanitize(string userPrompt)
    {
        if (string.IsNullOrWhiteSpace(userPrompt))
        {
            return new PromptFirewallInspectionResult
            {
                IsSafe = true,
                RiskScore = 0.0,
                SanitizedPrompt = string.Empty
            };
        }

        double maxScore = 0.0;
        string? topCategory = null;
        string sanitized = userPrompt;

        foreach (var (pattern, category, weight) in ThreatRules)
        {
            if (Regex.IsMatch(userPrompt, pattern, RegexOptions.IgnoreCase))
            {
                if (weight > maxScore)
                {
                    maxScore = weight;
                    topCategory = category;
                }

                // Neutralize malicious phrases
                sanitized = Regex.Replace(sanitized, pattern, "[FILTERED_SECURITY_VIOLATION]", RegexOptions.IgnoreCase);
            }
        }

        bool isSafe = maxScore < 0.80;

        if (!isSafe)
        {
            _logger?.LogWarning("Prompt Firewall BLOCKED potential threat '{Category}' (Risk: {Score:F2}). Original length: {Len}",
                topCategory, maxScore, userPrompt.Length);
        }

        return new PromptFirewallInspectionResult
        {
            IsSafe = isSafe,
            RiskScore = maxScore,
            ThreatCategory = topCategory,
            SanitizedPrompt = sanitized.Trim()
        };
    }
}
