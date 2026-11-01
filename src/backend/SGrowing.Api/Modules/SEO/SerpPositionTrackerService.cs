using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.SEO;

public record SerpCompetitorEntry(
    int Position,
    string Url,
    string Domain,
    int DomainAuthority,
    string Title,
    int TitleLength
);

public record CompetitorBenchmarkResult(
    double AverageDomainAuthority,
    int MinDomainAuthority,
    int MaxDomainAuthority,
    double AverageTitleLength,
    int OptimalTitleCount,
    IReadOnlyList<SerpCompetitorEntry> TopCompetitors,
    IReadOnlyList<string> Insights
);

public record SerpMilestoneTrigger(
    string MilestoneType,
    string Description,
    int PreviousRank,
    int CurrentRank,
    DateTime TriggeredAtUtc
);

public record SerpVolatilityAnalysis(
    double VolatilityIndex,
    double MeanDisplacement,
    double StandardDeviation,
    string StabilityGrade,
    IReadOnlyList<int> RankSequence
);

public record TrackKeywordRankCommand(
    Guid TenantId,
    string Keyword,
    string TargetUrl,
    int Position,
    string DeviceType = "Desktop",
    string CountryCode = "DZ",
    IReadOnlyList<SerpCompetitorEntry>? Competitors = null,
    Guid? KeywordId = null
);

public record SerpRankTrackingResult(
    Guid RecordId,
    Guid TenantId,
    string Keyword,
    string TargetUrl,
    int CurrentPosition,
    int PreviousPosition,
    int DeltaDisplacement,
    SerpVolatilityAnalysis Volatility,
    IReadOnlyList<SerpMilestoneTrigger> TriggeredMilestones,
    CompetitorBenchmarkResult? CompetitorBenchmark,
    IReadOnlyList<string> Recommendations,
    DateTime RecordedAtUtc
);

public record KeywordRankHistoryItem(
    DateTime RecordedDateUtc,
    int Position,
    int DeltaPosition,
    string DeviceType
);

public record KeywordRankHistoryReport(
    Guid TenantId,
    string Keyword,
    string TargetUrl,
    int BestPosition,
    int CurrentPosition,
    double VolatilityIndex,
    IReadOnlyList<KeywordRankHistoryItem> History,
    IReadOnlyList<SerpMilestoneTrigger> Milestones
);

public record KeywordVolatilityOverview(
    string Keyword,
    int CurrentRank,
    int PreviousRank,
    int Delta,
    double VolatilityIndex,
    string Stability
);

public record KeywordMoverItem(
    string Keyword,
    int FromRank,
    int ToRank,
    int Delta
);

public record TenantSerpVolatilitySummary(
    Guid TenantId,
    int TotalKeywordsTracked,
    double AverageVolatility,
    IReadOnlyList<KeywordVolatilityOverview> HighVolatilityKeywords,
    IReadOnlyList<KeywordMoverItem> TopRisers,
    IReadOnlyList<KeywordMoverItem> TopFallers,
    IReadOnlyList<string> ActionableRecommendations,
    DateTime GeneratedAtUtc
);

public interface ISerpPositionTrackerService
{
    Task<SerpRankTrackingResult> TrackKeywordAsync(TrackKeywordRankCommand command, CancellationToken ct = default);
    Task<KeywordRankHistoryReport> GetKeywordHistoryAsync(Guid tenantId, Guid keywordId, int days = 30, CancellationToken ct = default);
    Task<TenantSerpVolatilitySummary> GetVolatilityReportAsync(Guid tenantId, int windowDays = 14, CancellationToken ct = default);

    // Pure calculation and analysis engines
    int ComputeDisplacementDelta(int previousRank, int currentRank);
    SerpVolatilityAnalysis CalculateVolatility(IReadOnlyList<int> ranks);
    IReadOnlyList<SerpMilestoneTrigger> DetectMilestones(int previousRank, int currentRank);
    CompetitorBenchmarkResult BenchmarkCompetitors(IReadOnlyList<SerpCompetitorEntry> competitors);
    IReadOnlyList<string> GenerateRecommendations(string keyword, int currentRank, int previousRank, double volatilityIndex, CompetitorBenchmarkResult? competitorBenchmark);
}

public class SerpPositionTrackerService(SGrowingDbContext db) : ISerpPositionTrackerService
{
    private readonly SGrowingDbContext _db = db;

    public async Task<SerpRankTrackingResult> TrackKeywordAsync(TrackKeywordRankCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.Keyword);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.TargetUrl);

        var cleanKw = command.Keyword.Trim();
        var cleanUrl = command.TargetUrl.Trim();
        var device = string.IsNullOrWhiteSpace(command.DeviceType) ? "Desktop" : command.DeviceType.Trim();
        var country = string.IsNullOrWhiteSpace(command.CountryCode) ? "DZ" : command.CountryCode.Trim().ToUpperInvariant();

        // 1. Fetch previous historical records
        var priorRecords = await _db.SerpRankHistoryRecords
            .Where(r => r.TenantId == command.TenantId && r.Keyword.ToLower() == cleanKw.ToLower() && r.DeviceType == device)
            .OrderByDescending(r => r.RecordedDateUtc)
            .Take(30)
            .ToListAsync(ct);

        var previousRecord = priorRecords.FirstOrDefault();
        var previousRank = previousRecord?.Position ?? 0;
        var currentRank = command.Position;

        // 2. Compute delta displacement: Delta = Rank_(t-1) - Rank_t
        var delta = ComputeDisplacementDelta(previousRank, currentRank);

        // 3. Compute Volatility
        var rankHistorySequence = priorRecords
            .Select(r => r.Position)
            .Reverse()
            .Concat([currentRank])
            .ToList();

        var volatility = CalculateVolatility(rankHistorySequence);

        // 4. Milestone triggers
        var milestones = DetectMilestones(previousRank, currentRank);

        // 5. Competitor benchmark
        CompetitorBenchmarkResult? competitorBenchmark = null;
        if (command.Competitors is { Count: > 0 })
        {
            competitorBenchmark = BenchmarkCompetitors(command.Competitors);
        }

        // 6. Actionable recommendations
        var recommendations = GenerateRecommendations(cleanKw, currentRank, previousRank, volatility.VolatilityIndex, competitorBenchmark);

        // 7. Persist record to DB
        var newRecord = SerpRankHistoryRecord.Create(
            tenantId: command.TenantId,
            keyword: cleanKw,
            targetUrl: cleanUrl,
            position: currentRank,
            previousPosition: previousRank,
            deviceType: device,
            countryCode: country,
            serpFeatures: competitorBenchmark != null ? ["CompetitorBenchmarkIndexed"] : null,
            keywordId: command.KeywordId
        );

        _db.SerpRankHistoryRecords.Add(newRecord);

        // Update or create SerpKeywordTracking if exists
        var keywordTracking = command.KeywordId.HasValue
            ? await _db.SerpKeywordTrackings.FirstOrDefaultAsync(k => k.Id == command.KeywordId.Value && k.TenantId == command.TenantId, ct)
            : await _db.SerpKeywordTrackings.FirstOrDefaultAsync(k => k.TenantId == command.TenantId && k.Keyword.ToLower() == cleanKw.ToLower(), ct);

        if (keywordTracking is not null)
        {
            keywordTracking.PreviousRank = previousRank > 0 ? previousRank : currentRank;
            keywordTracking.CurrentRank = currentRank;
            if (keywordTracking.BestRank == null || currentRank < keywordTracking.BestRank)
            {
                keywordTracking.BestRank = currentRank;
            }
            keywordTracking.TrackedDateUtc = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);

        return new SerpRankTrackingResult(
            RecordId: newRecord.Id,
            TenantId: command.TenantId,
            Keyword: cleanKw,
            TargetUrl: cleanUrl,
            CurrentPosition: currentRank,
            PreviousPosition: previousRank,
            DeltaDisplacement: delta,
            Volatility: volatility,
            TriggeredMilestones: milestones,
            CompetitorBenchmark: competitorBenchmark,
            Recommendations: recommendations,
            RecordedAtUtc: newRecord.RecordedDateUtc
        );
    }

    public async Task<KeywordRankHistoryReport> GetKeywordHistoryAsync(Guid tenantId, Guid keywordId, int days = 30, CancellationToken ct = default)
    {
        var keyword = await _db.SerpKeywordTrackings
            .AsNoTracking()
            .FirstOrDefaultAsync(k => k.TenantId == tenantId && k.Id == keywordId, ct);

        var keywordName = keyword?.Keyword ?? string.Empty;
        var targetUrl = keyword?.TargetUrl ?? string.Empty;

        var since = DateTime.UtcNow.AddDays(-Math.Max(1, days));
        var records = await _db.SerpRankHistoryRecords
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId && (r.KeywordId == keywordId || (keywordName != "" && r.Keyword.ToLower() == keywordName.ToLower())) && r.RecordedDateUtc >= since)
            .OrderBy(r => r.RecordedDateUtc)
            .ToListAsync(ct);

        if (records.Count == 0)
        {
            return new KeywordRankHistoryReport(
                TenantId: tenantId,
                Keyword: keywordName,
                TargetUrl: targetUrl,
                BestPosition: keyword?.BestRank ?? 0,
                CurrentPosition: keyword?.CurrentRank ?? 0,
                VolatilityIndex: 0.0,
                History: [],
                Milestones: []
            );
        }

        var historyItems = records.Select(r => new KeywordRankHistoryItem(
            RecordedDateUtc: r.RecordedDateUtc,
            Position: r.Position,
            DeltaPosition: r.DeltaPosition,
            DeviceType: r.DeviceType
        )).ToList();

        var ranks = records.Select(r => r.Position).ToList();
        var volatility = CalculateVolatility(ranks);

        var milestones = new List<SerpMilestoneTrigger>();
        for (int i = 1; i < records.Count; i++)
        {
            var detected = DetectMilestones(records[i - 1].Position, records[i].Position);
            milestones.AddRange(detected);
        }

        var best = ranks.Min();
        var current = ranks.Last();

        return new KeywordRankHistoryReport(
            TenantId: tenantId,
            Keyword: records[0].Keyword,
            TargetUrl: records[0].TargetUrl,
            BestPosition: best,
            CurrentPosition: current,
            VolatilityIndex: volatility.VolatilityIndex,
            History: historyItems,
            Milestones: milestones
        );
    }

    public async Task<TenantSerpVolatilitySummary> GetVolatilityReportAsync(Guid tenantId, int windowDays = 14, CancellationToken ct = default)
    {
        var since = DateTime.UtcNow.AddDays(-Math.Max(1, windowDays));
        var records = await _db.SerpRankHistoryRecords
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.RecordedDateUtc >= since)
            .OrderBy(r => r.RecordedDateUtc)
            .ToListAsync(ct);

        var groupedByKeyword = records
            .GroupBy(r => r.Keyword.ToLowerInvariant())
            .ToList();

        var overviews = new List<KeywordVolatilityOverview>();
        var risers = new List<KeywordMoverItem>();
        var fallers = new List<KeywordMoverItem>();

        foreach (var group in groupedByKeyword)
        {
            var ordered = group.OrderBy(r => r.RecordedDateUtc).ToList();
            var ranks = ordered.Select(r => r.Position).ToList();
            var vol = CalculateVolatility(ranks);

            var first = ordered.First();
            var last = ordered.Last();
            var delta = ComputeDisplacementDelta(first.Position, last.Position);

            var kwName = last.Keyword;
            overviews.Add(new KeywordVolatilityOverview(
                Keyword: kwName,
                CurrentRank: last.Position,
                PreviousRank: first.Position,
                Delta: delta,
                VolatilityIndex: vol.VolatilityIndex,
                Stability: vol.StabilityGrade
            ));

            if (delta >= 3)
            {
                risers.Add(new KeywordMoverItem(kwName, first.Position, last.Position, delta));
            }
            else if (delta <= -3)
            {
                fallers.Add(new KeywordMoverItem(kwName, first.Position, last.Position, delta));
            }
        }

        var totalTracked = overviews.Count;
        var avgVolatility = totalTracked > 0 ? Math.Round(overviews.Average(o => o.VolatilityIndex), 2) : 0.0;

        var highVol = overviews
            .Where(o => o.VolatilityIndex >= 35.0)
            .OrderByDescending(o => o.VolatilityIndex)
            .ToList();

        var topRisers = risers.OrderByDescending(r => r.Delta).Take(5).ToList();
        var topFallers = fallers.OrderBy(f => f.Delta).Take(5).ToList();

        var recommendations = new List<string>();
        if (topFallers.Count > 0)
        {
            recommendations.Add($"Alert: {topFallers.Count} keywords experienced significant rank drops. Prioritize content audits and canonical tag verification for '{topFallers[0].Keyword}'.");
        }
        if (avgVolatility >= 40.0)
        {
            recommendations.Add($"High portfolio volatility ({avgVolatility:F1}/100) indicates active search engine index fluctuations. Maintain stable URLs and avoid mass structural rewrites.");
        }
        if (topRisers.Count > 0)
        {
            recommendations.Add($"Growth momentum: '{topRisers[0].Keyword}' climbed +{topRisers[0].Delta} positions! Strengthen leadership with Schema.org FAQ and review snippets.");
        }

        return new TenantSerpVolatilitySummary(
            TenantId: tenantId,
            TotalKeywordsTracked: totalTracked,
            AverageVolatility: avgVolatility,
            HighVolatilityKeywords: highVol,
            TopRisers: topRisers,
            TopFallers: topFallers,
            ActionableRecommendations: recommendations,
            GeneratedAtUtc: DateTime.UtcNow
        );
    }

    public int ComputeDisplacementDelta(int previousRank, int currentRank)
    {
        if (previousRank <= 0 || currentRank <= 0) return 0;
        // In SERP ranking, lower number is better rank.
        // e.g. Day 1: #14, Day 2: #11 -> Delta = 14 - 11 = +3 (improvement)
        // e.g. Day 2: #11, Day 3: #4  -> Delta = 11 - 4  = +7 (improvement)
        // e.g. Day 3: #4,  Day 4: #12 -> Delta = 4 - 12  = -8 (drop)
        return previousRank - currentRank;
    }

    public SerpVolatilityAnalysis CalculateVolatility(IReadOnlyList<int> ranks)
    {
        if (ranks is null || ranks.Count <= 1)
        {
            return new SerpVolatilityAnalysis(
                VolatilityIndex: 0.0,
                MeanDisplacement: 0.0,
                StandardDeviation: 0.0,
                StabilityGrade: "Rock-Solid",
                RankSequence: ranks ?? []
            );
        }

        // Calculate daily absolute displacements
        double displacementSum = 0;
        for (int i = 1; i < ranks.Count; i++)
        {
            displacementSum += Math.Abs(ranks[i] - ranks[i - 1]);
        }
        double meanDisplacement = displacementSum / (ranks.Count - 1);

        // Calculate standard deviation
        double meanRank = ranks.Average();
        double sumSquares = ranks.Sum(r => Math.Pow(r - meanRank, 2));
        double stdDev = Math.Sqrt(sumSquares / ranks.Count);

        // Volatility Index formula: weighted combination clamped between 0 and 100
        double rawIndex = (meanDisplacement * 6.0) + (stdDev * 8.0);
        double volatilityIndex = Math.Min(100.0, Math.Round(rawIndex, 2));

        var stability = volatilityIndex switch
        {
            <= 15.0 => "Rock-Solid",
            <= 35.0 => "Normal Variance",
            <= 65.0 => "High Volatility",
            _ => "Turbulent"
        };

        return new SerpVolatilityAnalysis(
            VolatilityIndex: volatilityIndex,
            MeanDisplacement: Math.Round(meanDisplacement, 2),
            StandardDeviation: Math.Round(stdDev, 2),
            StabilityGrade: stability,
            RankSequence: ranks
        );
    }

    public IReadOnlyList<SerpMilestoneTrigger> DetectMilestones(int previousRank, int currentRank)
    {
        var triggers = new List<SerpMilestoneTrigger>();

        if (previousRank <= 0 || currentRank <= 0) return triggers;

        var delta = ComputeDisplacementDelta(previousRank, currentRank);
        var now = DateTime.UtcNow;

        // Number One rank reached
        if (currentRank == 1 && previousRank != 1)
        {
            triggers.Add(new SerpMilestoneTrigger(
                MilestoneType: "ReachedNumberOne",
                Description: "Achieved the #1 top spot on Google Search results!",
                PreviousRank: previousRank,
                CurrentRank: currentRank,
                TriggeredAtUtc: now
            ));
        }

        // Entered Top 3
        if (currentRank <= 3 && previousRank > 3)
        {
            triggers.Add(new SerpMilestoneTrigger(
                MilestoneType: "EnteredTopThree",
                Description: $"Climbed into Google Top 3 (now rank #{currentRank})!",
                PreviousRank: previousRank,
                CurrentRank: currentRank,
                TriggeredAtUtc: now
            ));
        }

        // Entered Top 10 (Page 1)
        if (currentRank <= 10 && previousRank > 10)
        {
            triggers.Add(new SerpMilestoneTrigger(
                MilestoneType: "EnteredTopTen",
                Description: $"Secured Google Page 1 placement (now rank #{currentRank})!",
                PreviousRank: previousRank,
                CurrentRank: currentRank,
                TriggeredAtUtc: now
            ));
        }

        // Dropped out of Top 3
        if (currentRank > 3 && previousRank <= 3)
        {
            triggers.Add(new SerpMilestoneTrigger(
                MilestoneType: "DroppedOutOfTopThree",
                Description: $"Fell off the Google podium to rank #{currentRank}.",
                PreviousRank: previousRank,
                CurrentRank: currentRank,
                TriggeredAtUtc: now
            ));
        }

        // Dropped out of Top 10 (Page 1)
        if (currentRank > 10 && previousRank <= 10)
        {
            triggers.Add(new SerpMilestoneTrigger(
                MilestoneType: "DroppedOutOfTopTen",
                Description: $"Lost Google Page 1 ranking, slipping to rank #{currentRank}.",
                PreviousRank: previousRank,
                CurrentRank: currentRank,
                TriggeredAtUtc: now
            ));
        }

        // Big Jump (>= 5 positions)
        if (delta >= 5)
        {
            triggers.Add(new SerpMilestoneTrigger(
                MilestoneType: "SignificantRankJump",
                Description: $"Surged +{delta} positions in rank displacement!",
                PreviousRank: previousRank,
                CurrentRank: currentRank,
                TriggeredAtUtc: now
            ));
        }
        // Big Drop (<= -5 positions)
        else if (delta <= -5)
        {
            triggers.Add(new SerpMilestoneTrigger(
                MilestoneType: "SignificantRankDrop",
                Description: $"Dropped {Math.Abs(delta)} positions in rank displacement.",
                PreviousRank: previousRank,
                CurrentRank: currentRank,
                TriggeredAtUtc: now
            ));
        }

        return triggers;
    }

    public CompetitorBenchmarkResult BenchmarkCompetitors(IReadOnlyList<SerpCompetitorEntry> competitors)
    {
        if (competitors is null || competitors.Count == 0)
        {
            return new CompetitorBenchmarkResult(
                AverageDomainAuthority: 0.0,
                MinDomainAuthority: 0,
                MaxDomainAuthority: 0,
                AverageTitleLength: 0.0,
                OptimalTitleCount: 0,
                TopCompetitors: [],
                Insights: ["No competitor benchmark data provided."]
            );
        }

        var ordered = competitors.OrderBy(c => c.Position).ToList();
        var avgDa = Math.Round(competitors.Average(c => c.DomainAuthority), 1);
        var minDa = competitors.Min(c => c.DomainAuthority);
        var maxDa = competitors.Max(c => c.DomainAuthority);

        var avgTitleLen = Math.Round(competitors.Average(c => c.TitleLength), 1);
        var optimalTitles = competitors.Count(c => c.TitleLength is >= 50 and <= 60);

        var insights = new List<string>
        {
            $"Top {competitors.Count} competitors have an average Domain Authority of {avgDa:F1} (Range: {minDa}-{maxDa}).",
            $"Average competitor title tag length is {avgTitleLen:F1} characters ({optimalTitles}/{competitors.Count} within ideal 50-60 character range)."
        };

        if (avgDa >= 50)
        {
            insights.Add("High competitive authority barrier detected: Strengthen topical cluster backlinks and local citations.");
        }
        else
        {
            insights.Add("Low-to-moderate competitive authority: High probability of outranking competitors with optimized Schema.org markup and fast Core Web Vitals.");
        }

        return new CompetitorBenchmarkResult(
            AverageDomainAuthority: avgDa,
            MinDomainAuthority: minDa,
            MaxDomainAuthority: maxDa,
            AverageTitleLength: avgTitleLen,
            OptimalTitleCount: optimalTitles,
            TopCompetitors: ordered,
            Insights: insights
        );
    }

    public IReadOnlyList<string> GenerateRecommendations(
        string keyword,
        int currentRank,
        int previousRank,
        double volatilityIndex,
        CompetitorBenchmarkResult? competitorBenchmark)
    {
        var recommendations = new List<string>();
        var delta = ComputeDisplacementDelta(previousRank, currentRank);

        // Rank drop > 5 positions
        if (delta <= -5)
        {
            recommendations.Add($"Keyword '{keyword}' dropped {Math.Abs(delta)} positions (from #{previousRank} to #{currentRank}): Check canonical tags, internal links, robots.txt, and competitor updates.");
        }
        else if (currentRank > 10 && previousRank is > 0 and <= 10)
        {
            recommendations.Add($"Keyword '{keyword}' dropped off Page 1 to position #{currentRank}: Review search intent alignment and enrich page with local FAQ Schema.org.");
        }

        // Rank improvements
        if (currentRank == 1)
        {
            recommendations.Add($"Congratulations on rank #1 for '{keyword}'! Maintain title CTR hooks and monitor SERP featured snippets regularly.");
        }
        else if (currentRank <= 3 && previousRank > 3)
        {
            recommendations.Add($"Keyword '{keyword}' climbed into Top 3 (#{currentRank}): Reinforce click-through rate with high-intent CTA copy and rich star ratings.");
        }

        // High volatility
        if (volatilityIndex >= 45.0)
        {
            recommendations.Add($"Elevated SERP volatility ({volatilityIndex:F1}/100) detected for '{keyword}': Search engines are recalibrating results. Monitor over 48 hours before modifying URL structure or titles.");
        }

        // Competitor insights
        if (competitorBenchmark is not null)
        {
            if (competitorBenchmark.AverageTitleLength < 45 || competitorBenchmark.AverageTitleLength > 65)
            {
                recommendations.Add($"Competitors average {competitorBenchmark.AverageTitleLength:F1} characters in title tags. Ensure your title stays strictly between 50-60 characters for maximum SERP visibility.");
            }
        }

        if (recommendations.Count == 0)
        {
            recommendations.Add($"Keyword '{keyword}' ranking is stable at position #{currentRank}. Continue programmatic content freshness updates.");
        }

        return recommendations;
    }
}
