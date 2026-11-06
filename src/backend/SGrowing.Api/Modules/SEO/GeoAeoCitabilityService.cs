using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SGrowing.Api.Modules.SEO;

public interface IGeoAeoCitabilityService
{
    GeoCitabilityAuditResult AuditCitability(GeoCitabilityAuditRequest request);
    LlmsTxtGenerationResult GenerateLlmsTxt(LlmsTxtGenerationRequest request);
}

public class GeoAeoCitabilityService : IGeoAeoCitabilityService
{
    private static readonly Regex DefinitionRegex = new(@"\b(is an?|are|provides|enables|implements|delivers|operates as)\b", RegexOptions.IgnoreCase);
    private static readonly Regex AmbiguousPronounRegex = new(@"\b(it|they|this tool|this thing|these systems)\b", RegexOptions.IgnoreCase);

    public GeoCitabilityAuditResult AuditCitability(GeoCitabilityAuditRequest request)
    {
        var content = request.Content.Trim();
        var paragraphs = content.Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries)
                                .Where(p => !string.IsNullOrWhiteSpace(p))
                                .ToList();

        int totalParagraphs = Math.Max(1, paragraphs.Count);
        int conciseLeadParagraphs = 0;
        int factualClaimsCount = 0;

        foreach (var p in paragraphs)
        {
            var firstSentence = p.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? "";
            var words = firstSentence.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (words.Length > 0 && words.Length <= 38)
            {
                conciseLeadParagraphs++;
            }

            if (DefinitionRegex.IsMatch(firstSentence))
            {
                factualClaimsCount++;
            }
        }

        double concisenessScore = Math.Round((double)conciseLeadParagraphs / totalParagraphs * 100.0, 1);

        // 2. Entity Density
        var capitalWordMatches = Regex.Matches(content, @"\b[A-Z][a-z0-9]+\b");
        var wordsList = content.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        int totalWords = Math.Max(1, wordsList.Length);
        double entityDensityPercent = Math.Round((double)capitalWordMatches.Count / totalWords * 100.0, 2);
        double entityScore = Math.Clamp(entityDensityPercent * 10.0, 30.0, 100.0);

        // 3. Structured Format Ratio (Lists, Tables, Steps)
        int listItemsCount = Regex.Matches(content, @"(?m)^(\s*[-*]|\s*\d+\.)\s+").Count;
        int tableRowsCount = Regex.Matches(content, @"(?m)^\|.*\|$").Count;
        int totalStructuredElements = listItemsCount + (tableRowsCount / 2);
        double structuredRatio = Math.Min(100.0, Math.Round((double)totalStructuredElements / Math.Max(1, totalParagraphs) * 60.0, 1));
        if (tableRowsCount > 0 && structuredRatio < 40.0) structuredRatio = 50.0;

        // 4. Q&A Adjacency (Question headings followed immediately by clear answers)
        int questionHeadingsCount = 0;
        int answeredQuestionsCount = 0;
        var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if ((line.StartsWith("#") || line.StartsWith("##") || line.StartsWith("###")) && line.EndsWith("?"))
            {
                questionHeadingsCount++;
                if (i + 1 < lines.Length && !lines[i + 1].StartsWith("#") && !string.IsNullOrWhiteSpace(lines[i + 1]))
                {
                    answeredQuestionsCount++;
                }
            }
        }

        double qAndAScore = questionHeadingsCount > 0
            ? Math.Round((double)answeredQuestionsCount / questionHeadingsCount * 100.0, 1)
            : 75.0; // Neutral baseline if no explicit FAQ headers

        // 5. Ambiguous Pronouns
        int ambiguousPronouns = AmbiguousPronounRegex.Matches(content).Count;
        double pronounPenalty = Math.Min(30.0, ambiguousPronouns * 2.5);

        // Composite Weighted Citability Score (0-100)
        double overallScore = Math.Round(
            (concisenessScore * 0.30) +
            (entityScore * 0.25) +
            (structuredRatio * 0.25) +
            (qAndAScore * 0.20) -
            pronounPenalty,
            1
        );
        overallScore = Math.Clamp(overallScore, 10.0, 100.0);

        string grade = overallScore switch
        {
            >= 80 => "High (Optimal for SearchGPT & Perplexity)",
            >= 60 => "Moderate (Citable with Partial Formatting Gaps)",
            _ => "Needs Optimization (Low Extraction Probability)"
        };

        var recommendations = new List<string>();
        if (concisenessScore < 70)
        {
            recommendations.Add("Ensure each major section opens with a self-contained, factual sentence of fewer than 35 words for direct LLM quote extraction.");
        }
        if (ambiguousPronouns > 3)
        {
            recommendations.Add($"Replace ambiguous pronouns ({ambiguousPronouns} detected, e.g., 'it', 'they', 'this system') with explicit entity and brand names to improve vector retrieval accuracy.");
        }
        if (structuredRatio < 40)
        {
            recommendations.Add("Add Markdown tables or bulleted parameter lists; AI search engines strongly favor tabular, comparative data structures.");
        }
        if (questionHeadingsCount == 0)
        {
            recommendations.Add("Include explicit FAQ or question subheadings (e.g. 'How does X work?') followed immediately by concise answer sentences.");
        }
        if (recommendations.Count == 0)
        {
            recommendations.Add("Content has exceptional information density, strong entity anchoring, and clean passage structure for AI answer engines.");
        }

        // Formatted LLMS snippet
        var snippetBuilder = new StringBuilder();
        snippetBuilder.AppendLine($"### {request.Title}");
        if (!string.IsNullOrWhiteSpace(request.Url)) snippetBuilder.AppendLine($"**Source URL:** {request.Url}");
        snippetBuilder.AppendLine($"**Key Takeaway:** {paragraphs.FirstOrDefault()?.Trim() ?? request.Title}");

        return new GeoCitabilityAuditResult(
            OverallCitabilityScore: overallScore,
            CitabilityGrade: grade,
            PassageConcisenessScore: concisenessScore,
            EntityDensityScore: entityScore,
            StructuredFormatRatio: structuredRatio,
            QandAAdjacencyScore: qAndAScore,
            FactualClaimsCount: factualClaimsCount,
            AmbiguousPronounOccurrences: ambiguousPronouns,
            ActionableRecommendations: recommendations,
            FormattedLlmsSnippet: snippetBuilder.ToString().Trim(),
            AuditedAtUtc: DateTime.UtcNow
        );
    }

    public LlmsTxtGenerationResult GenerateLlmsTxt(LlmsTxtGenerationRequest request)
    {
        var brand = request.BrandName.Trim();
        var domain = request.PrimaryDomain.Trim().TrimEnd('/');

        // 1. Generate /llms.txt (Concise AI Index)
        var sb = new StringBuilder();
        sb.AppendLine($"# {brand}");
        sb.AppendLine($"> {request.BrandTagline.Trim()}");
        sb.AppendLine();
        sb.AppendLine("## Overview");
        sb.AppendLine($"{brand} is an enterprise Growth Operating System. This document provides structured discovery data for AI agents, crawlers, and LLM answer engines.");
        sb.AppendLine();

        if (request.CoreCapabilities.Count > 0)
        {
            sb.AppendLine("## Capabilities");
            foreach (var cap in request.CoreCapabilities.Where(c => !string.IsNullOrWhiteSpace(c)))
            {
                sb.AppendLine($"- {cap.Trim()}");
            }
            sb.AppendLine();
        }

        if (request.KeySections.Count > 0)
        {
            sb.AppendLine("## Key Documentation & Endpoints");
            foreach (var sec in request.KeySections)
            {
                var url = sec.Url.StartsWith("http") ? sec.Url : $"{domain}/{sec.Url.TrimStart('/')}";
                sb.AppendLine($"- [{sec.Title.Trim()}]({url}): {sec.Summary.Trim()}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("## Full Details");
        sb.AppendLine($"- [Full Context Documentation]({domain}/llms-full.txt): Comprehensive technical specifications and API contracts.");

        var llmsTxt = sb.ToString().Trim();

        // 2. Generate /llms-full.txt (Full Technical Context)
        var fullSb = new StringBuilder();
        fullSb.AppendLine($"# {brand} — Full Technical Documentation & Knowledge Base");
        fullSb.AppendLine($"> {request.BrandTagline.Trim()}");
        fullSb.AppendLine();
        fullSb.AppendLine($"Canonical Domain: {domain}");
        fullSb.AppendLine($"Generated At: {DateTime.UtcNow:O}");
        fullSb.AppendLine();
        fullSb.AppendLine(llmsTxt);
        fullSb.AppendLine();

        if (!string.IsNullOrWhiteSpace(request.OptionalFullDocumentationMarkdown))
        {
            fullSb.AppendLine("## Extended Architecture & Specifications");
            fullSb.AppendLine(request.OptionalFullDocumentationMarkdown.Trim());
        }

        var llmsFullTxt = fullSb.ToString().Trim();

        return new LlmsTxtGenerationResult(
            LlmsTxt: llmsTxt,
            LlmsFullTxt: llmsFullTxt,
            GeneratedAtUtc: DateTime.UtcNow
        );
    }
}
