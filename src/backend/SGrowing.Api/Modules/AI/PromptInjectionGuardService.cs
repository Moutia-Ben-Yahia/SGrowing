using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace SGrowing.Api.Modules.AI;

public enum PromptThreatCategory
{
    None = 0,
    DirectJailbreak = 1,
    SystemPromptOverride = 2,
    IndirectInjection = 3,
    DataExfiltration = 4,
    DelimiterHijacking = 5,
    EncodingObfuscation = 6
}

public record PromptSecurityEvaluationResult(
    bool IsSafe,
    double RiskScore,
    IReadOnlyList<PromptThreatCategory> DetectedThreats,
    IReadOnlyList<string> ThreatSignatures,
    string SanitizedPrompt,
    DateTime EvaluatedAtUtc
);

public interface IPromptInjectionGuardService
{
    PromptSecurityEvaluationResult EvaluatePrompt(string inputPrompt, double riskThreshold = 0.65);
    string SanitizePrompt(string inputPrompt);
    bool IsSafe(string inputPrompt);
}

/// <summary>
/// Autonomous LLM prompt injection and jailbreak defense guard (OWASP LLM01 & SOC 2 Type II).
/// Defends AI agent swarms and copilots against direct jailbreaks, indirect document injections,
/// system delimiter escapes, and markdown exfiltration channels.
/// </summary>
public class PromptInjectionGuardService : IPromptInjectionGuardService
{
    private readonly ILogger<PromptInjectionGuardService>? _logger;

    private static readonly (Regex Pattern, PromptThreatCategory Category, double Weight, string Description)[] Rules = new[]
    {
        // Direct Jailbreak Rules
        (new Regex(@"(?i)\bignore\s+(all\s+)?(previous|prior|above)\s+instructions\b", RegexOptions.Compiled), PromptThreatCategory.DirectJailbreak, 0.90, "Ignore previous instructions attempt"),
        (new Regex(@"(?i)\bdisregard\s+(all\s+)?(previous|prior|above)\s+(rules|guidelines|instructions)\b", RegexOptions.Compiled), PromptThreatCategory.DirectJailbreak, 0.90, "Disregard prior instructions attempt"),
        (new Regex(@"(?i)\b(DAN\s+mode|do\s+anything\s+now|developer\s+mode\s+enabled)\b", RegexOptions.Compiled), PromptThreatCategory.DirectJailbreak, 0.95, "Classic DAN/Developer mode jailbreak"),
        (new Regex(@"(?i)\b(unfiltered|unrestricted|uncensored)\s+(mode|ai|assistant)\b", RegexOptions.Compiled), PromptThreatCategory.DirectJailbreak, 0.85, "Unfiltered mode request"),
        (new Regex(@"(?i)\bbypass\s+(all\s+)?(ethical|safety|security)\s+(guidelines|filters|boundaries)\b", RegexOptions.Compiled), PromptThreatCategory.DirectJailbreak, 0.85, "Safety filter bypass attempt"),

        // System Prompt Overrides
        (new Regex(@"(?i)<\/?system>", RegexOptions.Compiled), PromptThreatCategory.SystemPromptOverride, 0.90, "System tag injection"),
        (new Regex(@"(?i)\[\/?(system|inst|sys)\]", RegexOptions.Compiled), PromptThreatCategory.SystemPromptOverride, 0.85, "Instruction tag spoofing"),
        (new Regex(@"(?i)###\s*(system|instruction|admin)\s*:", RegexOptions.Compiled), PromptThreatCategory.SystemPromptOverride, 0.80, "Markdown header system spoofing"),
        (new Regex(@"(?i)---BEGIN\s+(SYSTEM|ADMIN)\s+PROMPT---", RegexOptions.Compiled), PromptThreatCategory.SystemPromptOverride, 0.90, "System prompt boundary delimiter"),

        // Indirect Injections
        (new Regex(@"(?i)\b(when\s+you\s+read\s+this|secret\s+instruction\s+for\s+ai|hidden\s+directive)\s*:\b", RegexOptions.Compiled), PromptThreatCategory.IndirectInjection, 0.75, "Indirect document instruction injection"),
        (new Regex(@"(?i)\bimportant\s+update\s*:\s*forget\s+everything\b", RegexOptions.Compiled), PromptThreatCategory.IndirectInjection, 0.85, "Indirect context memory wipe"),

        // Data Exfiltration Channels
        (new Regex(@"!\[.*?\]\(https?:\/\/[^\s\)]+[\?&](?:token|key|secret|password|data|exfil)=.*?[\)]", RegexOptions.Compiled), PromptThreatCategory.DataExfiltration, 0.95, "Markdown image leak URL exfiltration"),
        (new Regex(@"(?i)(webhook\.site|requestbin\.com|ngrok\.io|burpcollaborator\.net)\b", RegexOptions.Compiled), PromptThreatCategory.DataExfiltration, 0.70, "Known out-of-band exfiltration domain"),
        (new Regex(@"(?i)\b(send|transmit|post)\s+(all|the)\s+(passwords?|keys?|secrets?|tokens?)\s+to\b", RegexOptions.Compiled), PromptThreatCategory.DataExfiltration, 0.90, "Secret transmission instruction"),

        // Delimiter and Control Hijackings
        (new Regex(@"`{3,}\s*(system|instruction|override)", RegexOptions.Compiled), PromptThreatCategory.DelimiterHijacking, 0.75, "Fenced code block system escape"),
        (new Regex(@"[\u0000-\u0008\u000B\u000C\u000E-\u001F]", RegexOptions.Compiled), PromptThreatCategory.DelimiterHijacking, 0.60, "Null or invisible control character injection")
    };

    public PromptInjectionGuardService(ILogger<PromptInjectionGuardService>? logger = null)
    {
        _logger = logger;
    }

    public PromptSecurityEvaluationResult EvaluatePrompt(string inputPrompt, double riskThreshold = 0.65)
    {
        if (string.IsNullOrWhiteSpace(inputPrompt))
        {
            return new PromptSecurityEvaluationResult(
                IsSafe: true,
                RiskScore: 0.0,
                DetectedThreats: Array.Empty<PromptThreatCategory>(),
                ThreatSignatures: Array.Empty<string>(),
                SanitizedPrompt: string.Empty,
                EvaluatedAtUtc: DateTime.UtcNow
            );
        }

        var detectedThreats = new HashSet<PromptThreatCategory>();
        var signatures = new List<string>();
        double maxWeight = 0.0;
        double accumulatedRisk = 0.0;

        foreach (var rule in Rules)
        {
            if (rule.Pattern.IsMatch(inputPrompt))
            {
                detectedThreats.Add(rule.Category);
                signatures.Add(rule.Description);
                maxWeight = Math.Max(maxWeight, rule.Weight);
                accumulatedRisk += rule.Weight * 0.25;
            }
        }

        // Composite risk calculation bounded between 0.0 and 1.0
        double finalScore = Math.Min(1.0, Math.Round(maxWeight + accumulatedRisk * (1.0 - maxWeight), 4));
        bool isSafe = finalScore < riskThreshold;

        string sanitized = isSafe ? inputPrompt : SanitizePrompt(inputPrompt);

        if (!isSafe)
        {
            _logger?.LogWarning(
                "Prompt injection detected! Score: {Score}, Categories: {Categories}, Signatures: {Signatures}",
                finalScore,
                string.Join(", ", detectedThreats),
                string.Join("; ", signatures));
        }

        return new PromptSecurityEvaluationResult(
            IsSafe: isSafe,
            RiskScore: finalScore,
            DetectedThreats: detectedThreats.ToList(),
            ThreatSignatures: signatures,
            SanitizedPrompt: sanitized,
            EvaluatedAtUtc: DateTime.UtcNow
        );
    }

    public string SanitizePrompt(string inputPrompt)
    {
        if (string.IsNullOrWhiteSpace(inputPrompt)) return string.Empty;

        string cleaned = inputPrompt;
        foreach (var rule in Rules)
        {
            cleaned = rule.Pattern.Replace(cleaned, "[REDACTED_HOSTILE_INJECTION]");
        }

        return cleaned;
    }

    public bool IsSafe(string inputPrompt)
    {
        return EvaluatePrompt(inputPrompt).IsSafe;
    }
}
