using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace SGrowing.Api.Modules.Forms;

public enum MicroSurveyType
{
    CSAT,
    NPS,
    ExitIntent,
    ProductFeedback
}

public enum NpsCategory
{
    Promoter,
    Passive,
    Detractor
}

public class MicroSurveyQuestion
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string QuestionText { get; set; } = string.Empty;
    public string QuestionType { get; set; } = "Rating"; // Rating, SingleChoice, MultiChoice, Text
    public int? MinRating { get; set; } = 1;
    public int? MaxRating { get; set; } = 5;
    public List<string> Options { get; set; } = [];
}

public class MicroSurveyDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public MicroSurveyType Type { get; set; } = MicroSurveyType.CSAT;
    public string? PagePathPattern { get; set; } // e.g. "/checkout*", "*pricing*", "/app/*"
    public int? MinVisitCount { get; set; }
    public decimal? MinCartValue { get; set; }
    public int? MinDwellSeconds { get; set; }
    public string? UserSegment { get; set; } // e.g. "enterprise", "smb", or comma-separated
    public bool IsActive { get; set; } = true;
    public List<MicroSurveyQuestion> Questions { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class SurveyTriggerEvaluationRequest
{
    public Guid TenantId { get; set; }
    public Guid? UserId { get; set; }
    public string? RespondentId { get; set; }
    public string CurrentPagePath { get; set; } = string.Empty;
    public int VisitCount { get; set; }
    public decimal CartValue { get; set; }
    public int DwellSeconds { get; set; }
    public string? UserSegment { get; set; }
    public bool IsExitIntent { get; set; }
}

public class SurveyTriggerEvaluationResponse
{
    public bool ShouldTrigger { get; set; }
    public Guid? SurveyId { get; set; }
    public string? SurveyTitle { get; set; }
    public MicroSurveyType? SurveyType { get; set; }
    public string? Reason { get; set; }
    public MicroSurveyDefinition? Survey { get; set; }
}

public class SubmitSurveyResponseRequest
{
    public Guid TenantId { get; set; }
    public Guid SurveyId { get; set; }
    public string? RespondentId { get; set; }
    public int? NumericRating { get; set; }
    public List<string>? SelectedOptions { get; set; }
    public string? TextComment { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
}

public class MicroSurveyResponseRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid SurveyId { get; set; }
    public string? RespondentId { get; set; }
    public int? NumericRating { get; set; }
    public List<string> SelectedOptions { get; set; } = [];
    public string? TextComment { get; set; }
    public NpsCategory? NpsCategory { get; set; }
    public double SentimentScore { get; set; } // -1.0 to +1.0
    public string SentimentLabel { get; set; } = "Neutral"; // "Positive", "Neutral", "Negative"
    public DateTimeOffset SubmittedAt { get; set; } = DateTimeOffset.UtcNow;
    public Dictionary<string, string> Metadata { get; set; } = [];
}

public class SurveyResponseResult
{
    public bool Success { get; set; }
    public Guid ResponseId { get; set; }
    public Guid SurveyId { get; set; }
    public NpsCategory? NpsCategory { get; set; }
    public double SentimentScore { get; set; }
    public string SentimentLabel { get; set; } = "Neutral";
    public string? Message { get; set; }
    public DateTimeOffset SubmittedAt { get; set; } = DateTimeOffset.UtcNow;
}

public interface IInAppMicroSurveyEngine
{
    Task<SurveyTriggerEvaluationResponse> EvaluateTriggerAsync(SurveyTriggerEvaluationRequest request, CancellationToken cancellationToken = default);
    Task<SurveyResponseResult> SubmitResponseAsync(SubmitSurveyResponseRequest request, CancellationToken cancellationToken = default);
    Task<MicroSurveyDefinition> RegisterSurveyAsync(MicroSurveyDefinition survey, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MicroSurveyDefinition>> GetSurveysAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<MicroSurveyDefinition?> GetSurveyByIdAsync(Guid surveyId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MicroSurveyResponseRecord>> GetResponsesAsync(Guid surveyId, CancellationToken cancellationToken = default);

    NpsCategory? ClassifyNps(int? rating);
    (double Score, string Label) AnalyzeSentiment(string? text);
    bool MatchesPathPattern(string currentPath, string? pattern);
}

public class InAppMicroSurveyEngine : IInAppMicroSurveyEngine
{
    private readonly ConcurrentDictionary<Guid, MicroSurveyDefinition> _surveys = new();
    private readonly ConcurrentDictionary<Guid, List<MicroSurveyResponseRecord>> _responses = new();

    private static readonly HashSet<string> PositiveWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "great", "excellent", "love", "loved", "awesome", "amazing", "good", "best",
        "fantastic", "super", "helpful", "fast", "easy", "pleased", "happy", "satisfied",
        "smooth", "intuitive", "perfect", "wonderful", "flawless", "brilliant", "delighted"
    };

    private static readonly HashSet<string> NegativeWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "bad", "terrible", "horrible", "awful", "poor", "hate", "hated", "slow",
        "broken", "buggy", "bug", "crash", "error", "difficult", "worst", "frustrating",
        "frustrated", "confusing", "disappointed", "useless", "fail", "failed", "sucks", "clunky"
    };

    private static readonly HashSet<string> NegationWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "not", "no", "never", "hardly", "barely", "scarcely", "isnt", "isn't", "arent", "aren't",
        "dont", "don't", "doesnt", "doesn't", "wont", "won't"
    };

    public InAppMicroSurveyEngine()
    {
        SeedDefaultSurveys();
    }

    private void SeedDefaultSurveys()
    {
        var defaultTenant = Guid.Parse("00000000-0000-0000-0000-000000000001");

        // 1. NPS Micro-Survey
        var npsSurvey = new MicroSurveyDefinition
        {
            Id = Guid.Parse("11111111-2222-3333-4444-555555555001"),
            TenantId = defaultTenant,
            Title = "How likely are you to recommend SGrowing to a peer?",
            Description = "Standard 0-10 Net Promoter Score with sentiment commentary.",
            Type = MicroSurveyType.NPS,
            PagePathPattern = "/app/*",
            MinVisitCount = 3,
            MinDwellSeconds = 30,
            IsActive = true,
            Questions =
            [
                new()
                {
                    QuestionText = "How likely are you to recommend SGrowing to a friend or colleague?",
                    QuestionType = "Rating",
                    MinRating = 0,
                    MaxRating = 10
                },
                new()
                {
                    QuestionText = "What was the primary reason for your score?",
                    QuestionType = "Text"
                }
            ]
        };

        // 2. CSAT Micro-Survey
        var csatSurvey = new MicroSurveyDefinition
        {
            Id = Guid.Parse("11111111-2222-3333-4444-555555555002"),
            TenantId = defaultTenant,
            Title = "Customer Satisfaction Feedback",
            Description = "Post-interaction 1-5 CSAT rating.",
            Type = MicroSurveyType.CSAT,
            PagePathPattern = "*dashboard*",
            MinVisitCount = 1,
            MinDwellSeconds = 15,
            IsActive = true,
            Questions =
            [
                new()
                {
                    QuestionText = "How satisfied are you with your experience today?",
                    QuestionType = "Rating",
                    MinRating = 1,
                    MaxRating = 5
                }
            ]
        };

        // 3. Exit-Intent Micro-Survey
        var exitSurvey = new MicroSurveyDefinition
        {
            Id = Guid.Parse("11111111-2222-3333-4444-555555555003"),
            TenantId = defaultTenant,
            Title = "Before you leave: Is there anything missing?",
            Description = "High-intent cart abandonment / exit survey.",
            Type = MicroSurveyType.ExitIntent,
            PagePathPattern = "/checkout*",
            MinCartValue = 50.0m,
            MinDwellSeconds = 5,
            IsActive = true,
            Questions =
            [
                new()
                {
                    QuestionText = "What prevented you from completing your purchase today?",
                    QuestionType = "SingleChoice",
                    Options = ["Price too high", "Missing payment method", "Just browsing", "Shipping questions", "Other"]
                }
            ]
        };

        // 4. Product Feedback Micro-Survey
        var productSurvey = new MicroSurveyDefinition
        {
            Id = Guid.Parse("11111111-2222-3333-4444-555555555004"),
            TenantId = defaultTenant,
            Title = "Enterprise Feature Feedback",
            Description = "Targeted feature feedback for enterprise customers.",
            Type = MicroSurveyType.ProductFeedback,
            PagePathPattern = "/features/*",
            MinVisitCount = 5,
            UserSegment = "enterprise",
            IsActive = true,
            Questions =
            [
                new()
                {
                    QuestionText = "Which upcoming capability would add the most value to your team?",
                    QuestionType = "MultiChoice",
                    Options = ["Automated Workflows", "Custom Integrations", "Advanced Analytics", "SOC-2 Compliance Tools"]
                }
            ]
        };

        _surveys[npsSurvey.Id] = npsSurvey;
        _surveys[csatSurvey.Id] = csatSurvey;
        _surveys[exitSurvey.Id] = exitSurvey;
        _surveys[productSurvey.Id] = productSurvey;
    }

    public Task<MicroSurveyDefinition> RegisterSurveyAsync(MicroSurveyDefinition survey, CancellationToken cancellationToken = default)
    {
        if (survey.Id == Guid.Empty)
        {
            survey.Id = Guid.NewGuid();
        }
        _surveys[survey.Id] = survey;
        return Task.FromResult(survey);
    }

    public Task<IReadOnlyList<MicroSurveyDefinition>> GetSurveysAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var list = _surveys.Values
            .Where(s => s.TenantId == tenantId)
            .OrderByDescending(s => s.CreatedAt)
            .ToList();
        return Task.FromResult<IReadOnlyList<MicroSurveyDefinition>>(list);
    }

    public Task<MicroSurveyDefinition?> GetSurveyByIdAsync(Guid surveyId, CancellationToken cancellationToken = default)
    {
        _surveys.TryGetValue(surveyId, out var survey);
        return Task.FromResult(survey);
    }

    public Task<IReadOnlyList<MicroSurveyResponseRecord>> GetResponsesAsync(Guid surveyId, CancellationToken cancellationToken = default)
    {
        if (_responses.TryGetValue(surveyId, out var list))
        {
            lock (list)
            {
                return Task.FromResult<IReadOnlyList<MicroSurveyResponseRecord>>(list.ToList());
            }
        }
        return Task.FromResult<IReadOnlyList<MicroSurveyResponseRecord>>([]);
    }

    public Task<SurveyTriggerEvaluationResponse> EvaluateTriggerAsync(SurveyTriggerEvaluationRequest request, CancellationToken cancellationToken = default)
    {
        var tenantSurveys = _surveys.Values
            .Where(s => s.TenantId == request.TenantId && s.IsActive)
            .ToList();

        // 1. If Exit-Intent is active, prioritize ExitIntent surveys first
        if (request.IsExitIntent)
        {
            var exitSurvey = tenantSurveys.FirstOrDefault(s =>
                s.Type == MicroSurveyType.ExitIntent &&
                MatchesSurveyConditions(s, request));

            if (exitSurvey != null)
            {
                return Task.FromResult(new SurveyTriggerEvaluationResponse
                {
                    ShouldTrigger = true,
                    SurveyId = exitSurvey.Id,
                    SurveyTitle = exitSurvey.Title,
                    SurveyType = exitSurvey.Type,
                    Reason = "Exit-intent detected with matching cart and URL rules.",
                    Survey = exitSurvey
                });
            }
        }

        // 2. General survey evaluation in order of specificity
        foreach (var survey in tenantSurveys)
        {
            if (survey.Type == MicroSurveyType.ExitIntent && !request.IsExitIntent)
            {
                // Only trigger exit-intent surveys if exit intent was signalled
                continue;
            }

            if (MatchesSurveyConditions(survey, request))
            {
                return Task.FromResult(new SurveyTriggerEvaluationResponse
                {
                    ShouldTrigger = true,
                    SurveyId = survey.Id,
                    SurveyTitle = survey.Title,
                    SurveyType = survey.Type,
                    Reason = $"Targeting conditions met for {survey.Type} survey on '{request.CurrentPagePath}'.",
                    Survey = survey
                });
            }
        }

        return Task.FromResult(new SurveyTriggerEvaluationResponse
        {
            ShouldTrigger = false,
            Reason = "No active micro-survey matched the visitor's current context or conditions."
        });
    }

    private bool MatchesSurveyConditions(MicroSurveyDefinition survey, SurveyTriggerEvaluationRequest request)
    {
        // 1. PagePathPattern check
        if (!string.IsNullOrWhiteSpace(survey.PagePathPattern))
        {
            if (!MatchesPathPattern(request.CurrentPagePath, survey.PagePathPattern))
            {
                return false;
            }
        }

        // 2. MinVisitCount check
        if (survey.MinVisitCount.HasValue && request.VisitCount < survey.MinVisitCount.Value)
        {
            return false;
        }

        // 3. MinCartValue check
        if (survey.MinCartValue.HasValue && request.CartValue < survey.MinCartValue.Value)
        {
            return false;
        }

        // 4. MinDwellSeconds check
        if (survey.MinDwellSeconds.HasValue && request.DwellSeconds < survey.MinDwellSeconds.Value)
        {
            return false;
        }

        // 5. UserSegment check
        if (!string.IsNullOrWhiteSpace(survey.UserSegment))
        {
            if (string.IsNullOrWhiteSpace(request.UserSegment))
            {
                return false;
            }

            var allowedSegments = survey.UserSegment.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (!allowedSegments.Any(s => string.Equals(s, request.UserSegment.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }
        }

        return true;
    }

    public bool MatchesPathPattern(string currentPath, string? pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern) || pattern == "*")
        {
            return true;
        }

        currentPath = currentPath?.Trim() ?? string.Empty;
        pattern = pattern.Trim();

        // Exact match
        if (string.Equals(currentPath, pattern, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Convert wildcard glob pattern to regex:
        // Escape regex tokens, replace escaped \* with .*
        var regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
        try
        {
            return Regex.IsMatch(currentPath, regexPattern, RegexOptions.IgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public Task<SurveyResponseResult> SubmitResponseAsync(SubmitSurveyResponseRequest request, CancellationToken cancellationToken = default)
    {
        if (!_surveys.TryGetValue(request.SurveyId, out var survey))
        {
            return Task.FromResult(new SurveyResponseResult
            {
                Success = false,
                SurveyId = request.SurveyId,
                Message = $"Micro-survey with ID '{request.SurveyId}' not found."
            });
        }

        // 1. NPS Classification
        NpsCategory? npsCat = null;
        if (survey.Type == MicroSurveyType.NPS || (request.NumericRating.HasValue && request.NumericRating.Value <= 10))
        {
            npsCat = ClassifyNps(request.NumericRating);
        }

        // 2. Real-time Sentiment Scoring
        var (sentimentScore, sentimentLabel) = AnalyzeSentiment(request.TextComment);

        var record = new MicroSurveyResponseRecord
        {
            Id = Guid.NewGuid(),
            TenantId = request.TenantId,
            SurveyId = request.SurveyId,
            RespondentId = request.RespondentId,
            NumericRating = request.NumericRating,
            SelectedOptions = request.SelectedOptions ?? [],
            TextComment = request.TextComment,
            NpsCategory = npsCat,
            SentimentScore = sentimentScore,
            SentimentLabel = sentimentLabel,
            SubmittedAt = DateTimeOffset.UtcNow,
            Metadata = request.Metadata ?? []
        };

        var list = _responses.GetOrAdd(request.SurveyId, _ => []);
        lock (list)
        {
            list.Add(record);
        }

        return Task.FromResult(new SurveyResponseResult
        {
            Success = true,
            ResponseId = record.Id,
            SurveyId = record.SurveyId,
            NpsCategory = record.NpsCategory,
            SentimentScore = record.SentimentScore,
            SentimentLabel = record.SentimentLabel,
            SubmittedAt = record.SubmittedAt,
            Message = "Survey response successfully ingested and analyzed."
        });
    }

    public NpsCategory? ClassifyNps(int? rating)
    {
        if (!rating.HasValue) return null;

        return rating.Value switch
        {
            >= 9 and <= 10 => NpsCategory.Promoter,
            >= 7 and <= 8 => NpsCategory.Passive,
            >= 0 and <= 6 => NpsCategory.Detractor,
            _ => null
        };
    }

    public (double Score, string Label) AnalyzeSentiment(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return (0.0, "Neutral");
        }

        var words = Regex.Split(text.ToLowerInvariant(), @"\W+")
            .Where(w => !string.IsNullOrWhiteSpace(w))
            .ToList();

        if (words.Count == 0)
        {
            return (0.0, "Neutral");
        }

        double score = 0.0;
        bool negated = false;

        for (int i = 0; i < words.Count; i++)
        {
            var word = words[i];

            if (NegationWords.Contains(word))
            {
                negated = true;
                continue;
            }

            if (PositiveWords.Contains(word))
            {
                score += negated ? -0.8 : 1.0;
                negated = false;
            }
            else if (NegativeWords.Contains(word))
            {
                score += negated ? 0.5 : -1.0;
                negated = false;
            }
            else
            {
                // Reset negation after 3 tokens if no sentiment word found
                if (negated && (i > 0 && NegationWords.Contains(words[i - 1])))
                {
                    // keep negated for next token
                }
                else
                {
                    negated = false;
                }
            }
        }

        // Normalize score between -1.0 and 1.0
        var normalized = Math.Clamp(score / Math.Max(1.0, Math.Sqrt(words.Count)), -1.0, 1.0);
        normalized = Math.Round(normalized, 2);

        string label = normalized switch
        {
            > 0.15 => "Positive",
            < -0.15 => "Negative",
            _ => "Neutral"
        };

        return (normalized, label);
    }
}
