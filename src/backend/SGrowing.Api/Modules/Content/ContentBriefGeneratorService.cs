using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace SGrowing.Api.Modules.Content;

public interface IContentBriefGeneratorService
{
    ContentBrief GenerateBrief(ContentBriefRequest request);
    DraftGradeReport GradeDraft(DraftGradeRequest request);
}

public class ContentBriefGeneratorService : IContentBriefGeneratorService
{
    public ContentBrief GenerateBrief(ContentBriefRequest request)
    {
        var primaryKeyword = request.PrimaryKeyword.Trim();
        var intent = request.Intent ?? DetermineSearchIntent(primaryKeyword, request.SecondaryKeywords);

        var (minWords, maxWords) = (request.MinWordCount, request.MaxWordCount) switch
        {
            (int min, int max) when min > 0 && max >= min => (min, max),
            _ => intent switch
            {
                SearchIntent.Commercial => (1800, 2600),
                SearchIntent.Transactional => (1000, 1600),
                SearchIntent.Navigational => (800, 1400),
                _ => (1500, 2400)
            }
        };

        var outline = new List<OutlineSectionDto>
        {
            new("H1", $"The Definitive Guide to {primaryKeyword}: Architecture, Strategy & Results",
                new List<string> { "Primary Hook", "Direct Definition", "Key Takeaways" }),
            new("H2", $"What is {primaryKeyword} and Why Does It Matter?",
                new List<string> { "Clear Industry Definition", "Market Drivers", "Key Challenges Solved" }),
            new("H2", "Core Architectural Pillars & Essential Capabilities",
                new List<string> { "Foundational Components", "Data Flow", "Integration Ecosystem" }),
            new("H2", "Step-by-Step Implementation Framework",
                new List<string> { "Prerequisites", "Configuration Checklist", "Execution Workflow" }),
            new("H2", "Comparative Analysis, Benchmarks & ROI",
                new List<string> { "Industry Metrics", "Before vs After Performance", "Cost Efficiency" }),
            new("H2", "Frequently Asked Questions (FAQ)",
                new List<string> { "Implementation Timeline", "Common Pitfalls", "Best Practices" })
        };

        var semanticKeywords = new List<SemanticKeywordDto>
        {
            new(primaryKeyword, 1.5, true)
        };

        if (request.SecondaryKeywords != null)
        {
            foreach (var kw in request.SecondaryKeywords.Where(k => !string.IsNullOrWhiteSpace(k)))
            {
                semanticKeywords.Add(new SemanticKeywordDto(kw.Trim(), 0.8, false));
            }
        }

        var questions = new List<string>
        {
            $"How does {primaryKeyword} work in practice?",
            $"What are the primary benefits of implementing {primaryKeyword}?",
            $"What common mistakes should teams avoid with {primaryKeyword}?",
            $"How is ROI measured when adopting {primaryKeyword}?"
        };

        var benchmarks = new CompetitorBenchmarkDto(
            AverageWordCount: (minWords + maxWords) / 2,
            RecommendedHeadingsCount: 8,
            RecommendedImagesCount: 4
        );

        var voiceRules = new List<string>
        {
            "Use authoritative, fact-dense language with specific quantifiable metrics.",
            "Avoid generic marketing fluff and unsubstantiated superlatives (e.g., 'revolutionary', 'game-changing').",
            "Ensure the opening paragraph defines the primary topic in fewer than 40 words for AI search engine extraction.",
            request.BrandVoiceGuidelines ?? "Direct, enterprise-grade tone with clear architectural reasoning."
        };

        return new ContentBrief(
            BriefId: Guid.NewGuid(),
            PrimaryKeyword: primaryKeyword,
            SearchIntent: intent.ToString(),
            TargetWordCountMin: minWords,
            TargetWordCountMax: maxWords,
            TargetReadingGrade: "Grade 8-10 (Clear Technical English)",
            OutlineSections: outline,
            SemanticKeywords: semanticKeywords,
            MandatoryQuestionsToAnswer: questions,
            CompetitorBenchmarks: benchmarks,
            BrandVoiceRules: voiceRules,
            CreatedAtUtc: DateTime.UtcNow
        );
    }

    public DraftGradeReport GradeDraft(DraftGradeRequest request)
    {
        var primaryKeyword = request.PrimaryKeyword.Trim();
        var fullText = $"{request.DraftTitle}\n\n{request.DraftBody}".Trim();

        var words = TokenizeWords(fullText);
        int totalWords = words.Count;

        // 1. Word Count Evaluation (Target: 1,200 - 2,500)
        double wordCountScore;
        if (totalWords >= 1200 && totalWords <= 2800)
        {
            wordCountScore = 100.0;
        }
        else if (totalWords < 1200)
        {
            wordCountScore = Math.Max(20.0, Math.Round((double)totalWords / 1200.0 * 100.0, 1));
        }
        else
        {
            wordCountScore = 90.0; // Slightly over target is acceptable
        }

        // 2. Keyword Density Analysis
        int primaryCount = CountOccurrences(fullText, primaryKeyword);
        int primaryKwWordLength = TokenizeWords(primaryKeyword).Count;
        double primaryDensity = totalWords > 0
            ? Math.Round((double)(primaryCount * primaryKwWordLength) / totalWords * 100.0, 2)
            : 0.0;

        double densityScore;
        if (primaryDensity >= 1.0 && primaryDensity <= 2.5)
        {
            densityScore = 100.0;
        }
        else if (primaryDensity > 0 && primaryDensity < 1.0)
        {
            densityScore = Math.Round(primaryDensity / 1.0 * 85.0, 1);
        }
        else if (primaryDensity > 2.5 && primaryDensity <= 3.5)
        {
            densityScore = 80.0;
        }
        else if (primaryDensity > 3.5)
        {
            densityScore = 40.0; // Keyword stuffing penalty
        }
        else
        {
            densityScore = 10.0; // Primary keyword missing
        }

        // 3. Secondary Keywords Check
        var coveredKeywords = new List<string>();
        var missingKeywords = new List<string>();

        if (primaryCount > 0) coveredKeywords.Add(primaryKeyword);
        else missingKeywords.Add(primaryKeyword);

        if (request.SecondaryKeywords != null)
        {
            foreach (var kw in request.SecondaryKeywords)
            {
                if (string.IsNullOrWhiteSpace(kw)) continue;
                if (CountOccurrences(fullText, kw.Trim()) > 0)
                {
                    coveredKeywords.Add(kw.Trim());
                }
                else
                {
                    missingKeywords.Add(kw.Trim());
                }
            }
        }

        double keywordCoverageScore = (coveredKeywords.Count + missingKeywords.Count) > 0
            ? Math.Round((double)coveredKeywords.Count / (coveredKeywords.Count + missingKeywords.Count) * 100.0, 1)
            : 100.0;

        // 4. Flesch Reading Ease Calculation
        var sentences = Regex.Split(fullText, @"[.!?]+").Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        int sentenceCount = Math.Max(1, sentences.Count);
        int syllableCount = words.Sum(CountSyllables);

        double asl = (double)totalWords / sentenceCount;
        double asw = totalWords > 0 ? (double)syllableCount / totalWords : 1.0;
        double flesch = Math.Round(206.835 - (1.015 * asl) - (84.6 * asw), 1);
        flesch = Math.Clamp(flesch, 0.0, 100.0);

        string readabilityLevel = flesch switch
        {
            >= 70 => "Fairly Easy / Conversational",
            >= 55 => "Standard / Accessible Technical",
            >= 40 => "Difficult / Academic",
            _ => "Very Difficult"
        };

        double readabilityScore = flesch >= 50 ? 100.0 : Math.Max(40.0, flesch * 1.8);

        // 5. Outline & Heading Structure Coverage
        var headings = ExtractHeadings(request.DraftBody);
        double outlineScore = headings.Count >= 4 ? 100.0 : Math.Round((double)headings.Count / 4.0 * 100.0, 1);

        // 6. Common Questions Detection
        var targetQuestions = new[]
        {
            "how", "what", "why", "benefits", "steps", "faq"
        };
        var answeredQuestions = new List<string>();
        var missingQuestions = new List<string>();

        foreach (var q in targetQuestions)
        {
            if (fullText.Contains(q, StringComparison.OrdinalIgnoreCase))
            {
                answeredQuestions.Add($"Addresses '{q}' aspects");
            }
            else
            {
                missingQuestions.Add($"Missing explicit '{q}' section");
            }
        }

        // 7. Composite Grade Calculation
        double overallScore = Math.Round(
            (wordCountScore * 0.30) +
            (densityScore * 0.25) +
            (keywordCoverageScore * 0.15) +
            (readabilityScore * 0.15) +
            (outlineScore * 0.15),
            1
        );

        string grade = overallScore switch
        {
            >= 92 => "A+",
            >= 85 => "A",
            >= 75 => "B",
            >= 65 => "C",
            >= 50 => "D",
            _ => "F"
        };

        // 8. Actionable Recommendations
        var recommendations = new List<string>();
        if (totalWords < 1200)
        {
            recommendations.Add($"Expand article body by at least {1200 - totalWords} words to reach comprehensive search depth.");
        }
        if (primaryDensity < 1.0)
        {
            recommendations.Add($"Increase primary keyword '{primaryKeyword}' usage naturally (current: {primaryDensity}%, target: 1.0% - 2.5%).");
        }
        else if (primaryDensity > 3.0)
        {
            recommendations.Add($"Reduce primary keyword '{primaryKeyword}' frequency (current: {primaryDensity}%) to prevent search engine keyword stuffing penalties.");
        }
        if (missingKeywords.Count > 0)
        {
            recommendations.Add($"Incorporate missing secondary keywords: {string.Join(", ", missingKeywords.Take(4))}.");
        }
        if (headings.Count < 4)
        {
            recommendations.Add("Add clear H2 and H3 subheadings to structure content for reader retention and search snippet extraction.");
        }
        if (flesch < 45)
        {
            recommendations.Add("Shorten long, multi-clause sentences to improve readability and AI answer engine extraction.");
        }
        if (recommendations.Count == 0)
        {
            recommendations.Add("Content meets enterprise editorial standards and is well-optimized for organic search and AI citations.");
        }

        return new DraftGradeReport(
            OverallScore: overallScore,
            Grade: grade,
            TotalWordCount: totalWords,
            WordCountScore: wordCountScore,
            PrimaryKeywordDensityPercent: primaryDensity,
            KeywordDensityScore: densityScore,
            FleschReadingEase: flesch,
            ReadabilityLevel: readabilityLevel,
            OutlineCoverageScore: outlineScore,
            CoveredKeywords: coveredKeywords,
            MissingKeywords: missingKeywords,
            QuestionsAnswered: answeredQuestions,
            MissingQuestions: missingQuestions,
            ActionableRecommendations: recommendations,
            GradedAtUtc: DateTime.UtcNow
        );
    }

    private static SearchIntent DetermineSearchIntent(string primaryKeyword, List<string>? secondaryKeywords)
    {
        var allText = $"{primaryKeyword} {string.Join(" ", secondaryKeywords ?? new List<string>())}".ToLowerInvariant();

        if (allText.Contains("buy") || allText.Contains("order") || allText.Contains("purchase") || allText.Contains("checkout") || allText.Contains("coupon") || allText.Contains("discount"))
        {
            return SearchIntent.Transactional;
        }
        if (allText.Contains("vs") || allText.Contains("versus") || allText.Contains("best") || allText.Contains("review") || allText.Contains("alternative") || allText.Contains("comparison") || allText.Contains("pricing") || allText.Contains("cost"))
        {
            return SearchIntent.Commercial;
        }
        if (allText.Contains("login") || allText.Contains("portal") || allText.Contains("support") || allText.Contains("website"))
        {
            return SearchIntent.Navigational;
        }
        return SearchIntent.Informational;
    }

    private static List<string> TokenizeWords(string text)
    {
        return Regex.Matches(text, @"\b[\w'-]+\b")
            .Select(m => m.Value)
            .Where(w => !string.IsNullOrWhiteSpace(w))
            .ToList();
    }

    private static int CountOccurrences(string source, string target)
    {
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target)) return 0;
        return Regex.Matches(source, $@"\b{Regex.Escape(target)}\b", RegexOptions.IgnoreCase).Count;
    }

    private static int CountSyllables(string word)
    {
        var lower = word.ToLowerInvariant();
        if (lower.Length <= 3) return 1;

        lower = Regex.Replace(lower, @"(?:[^laeiouy]|ed|es|e)$", "");
        lower = Regex.Replace(lower, @"^y", "");
        var matches = Regex.Matches(lower, @"[aeiouy]{1,2}");
        return Math.Max(1, matches.Count);
    }

    private static List<string> ExtractHeadings(string body)
    {
        var lines = body.Split('\n');
        return lines
            .Where(l => l.StartsWith("#") || l.StartsWith("##") || l.StartsWith("###"))
            .Select(l => l.TrimStart('#').Trim())
            .Where(h => !string.IsNullOrWhiteSpace(h))
            .ToList();
    }
}
