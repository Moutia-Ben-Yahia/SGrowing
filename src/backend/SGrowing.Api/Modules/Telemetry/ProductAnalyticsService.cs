using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace SGrowing.Api.Modules.Telemetry;

#region Models & DTOs

public record TelemetryIngestEvent(
    string? EventId,
    Guid TenantId,
    string UserId,
    string SessionId,
    string EventType,
    string EventName,
    DateTime TimestampUtc,
    string Url,
    string? Referrer = null,
    string? DeviceType = "Desktop",
    string? Os = "Windows",
    string? Browser = "Chrome",
    string? AppVersion = "1.0.0",
    Dictionary<string, object?>? Properties = null,
    CrashReportPayload? CrashData = null
);

public record CrashReportPayload(
    string ExceptionType,
    string Message,
    string StackTrace,
    List<string>? Breadcrumbs = null,
    string? Severity = "Error"
);

public record IngestResponseDto(
    bool Success,
    string EventId,
    int BufferedQueueCount,
    DateTime IngestedAtUtc
);

public record BatchIngestResponseDto(
    bool Success,
    int AcceptedCount,
    int TotalQueueCount,
    DateTime IngestedAtUtc
);

public record DailyActiveUsersPointDto(
    DateTime Date,
    string DateLabel,
    int Dau,
    int EventsCount,
    int SessionsCount,
    int Rolling7DayActive
);

public record ActiveUsersMetricsDto(
    Guid? TenantId,
    int Dau,
    int Wau,
    int Mau,
    double StickinessRatioPercent,
    string StickinessBenchmarkHealth,
    int TodayEventsCount,
    int TotalEventsCount,
    double DauDayOverDayGrowthPercent,
    double WauWeekOverWeekGrowthPercent,
    List<DailyActiveUsersPointDto> TimeSeries
);

public record CohortRetentionRowDto(
    string CohortId,
    string CohortLabel,
    DateTime CohortStartDate,
    int TotalAcquiredUsers,
    Dictionary<int, double?> RetentionPercentages,
    Dictionary<int, int?> RetainedUserCounts
);

public record CohortRetentionMatrixDto(
    Guid? TenantId,
    string Granularity,
    List<int> RetentionIntervals,
    List<CohortRetentionRowDto> Cohorts,
    Dictionary<int, double> AverageRetentionCurve
);

public record CrashClusterDto(
    string Signature,
    string ExceptionType,
    string NormalizedErrorMessage,
    string StackTraceSample,
    int TotalOccurrences,
    int UniqueUsersAffected,
    List<string> AffectedVersions,
    Dictionary<string, int> PlatformBreakdown,
    List<string> RecentBreadcrumbs,
    DateTime FirstSeenUtc,
    DateTime LastSeenUtc,
    string Severity,
    string Status
);

public record CrashClustersResponseDto(
    Guid? TenantId,
    int TotalCrashOccurrences,
    int TotalUniqueAffectedUsers,
    int TotalClustersCount,
    List<CrashClusterDto> Clusters
);

public record EvaluateFunnelRequestDto(
    Guid? TenantId,
    string FunnelName,
    List<string> Steps,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null
);

public record FunnelStepMetricDto(
    int StepIndex,
    string StepName,
    int ReachedCount,
    int DroppedCount,
    double StepConversionPercent,
    double OverallConversionPercent,
    double DropoffPercent,
    double AverageDurationSecondsFromPrevious
);

public record FunnelEvaluationResultDto(
    string FunnelName,
    DateTime EvaluatedAtUtc,
    int TotalEnteredUsers,
    int TotalCompletedUsers,
    double OverallConversionPercent,
    string BiggestDropoffStep,
    double BiggestDropoffPercent,
    List<FunnelStepMetricDto> Steps
);

#endregion

public interface IProductAnalyticsService
{
    // Fast Ingestion
    Task<IngestResponseDto> IngestEventAsync(TelemetryIngestEvent evt);
    Task<BatchIngestResponseDto> IngestBatchAsync(IEnumerable<TelemetryIngestEvent> events);
    Task<int> FlushBufferAsync(int maxBatchSize = 1000);
    int GetPendingBufferCount();
    int GetTotalStoredEventsCount();

    // Time-Series Rollup Aggregator
    Task<ActiveUsersMetricsDto> GetActiveUsersMetricsAsync(Guid? tenantId = null, int days = 30);

    // Cohort Retention Matrix Engine
    Task<CohortRetentionMatrixDto> GetCohortRetentionMatrixAsync(Guid? tenantId = null, string granularity = "weekly", int cohortCount = 8);

    // Crash Diagnostics & Stack Trace Clustering
    Task<CrashClustersResponseDto> GetCrashClustersAsync(Guid? tenantId = null, string? severity = null, string? status = null);
    Task<CrashClusterDto> RecordCrashAsync(Guid? tenantId, string userId, CrashReportPayload payload, string? appVersion = null, string? os = null, string? browser = null);
    Task<bool> UpdateCrashStatusAsync(string signature, string newStatus);

    // Multi-Step Funnel Conversion Calculator
    Task<FunnelEvaluationResultDto> EvaluateFunnelAsync(EvaluateFunnelRequestDto request);

    // Recent Events Inspection
    Task<IReadOnlyList<TelemetryIngestEvent>> GetRecentEventsAsync(Guid? tenantId = null, int limit = 50, string? eventType = null, string? userId = null);
}

public class ProductAnalyticsService : IProductAnalyticsService
{
    private readonly ConcurrentQueue<TelemetryIngestEvent> _ingestBuffer = new();
    private readonly ConcurrentBag<TelemetryIngestEvent> _storedEvents = new();
    private readonly ConcurrentDictionary<string, InternalCrashCluster> _crashClusters = new();
    private readonly object _flushLock = new();

    public ProductAnalyticsService()
    {
        SeedHistoricalTelemetry();
    }

    #region Fast Telemetry Ingestion

    public Task<IngestResponseDto> IngestEventAsync(TelemetryIngestEvent evt)
    {
        var normalizedEvent = NormalizeEvent(evt);
        _ingestBuffer.Enqueue(normalizedEvent);

        if (normalizedEvent.EventType.Equals("crash", StringComparison.OrdinalIgnoreCase) || normalizedEvent.CrashData != null)
        {
            var crashPayload = normalizedEvent.CrashData ?? new CrashReportPayload(
                "ClientReportedCrash",
                normalizedEvent.EventName,
                normalizedEvent.Properties?.ContainsKey("stack") == true ? normalizedEvent.Properties["stack"]?.ToString() ?? "" : "",
                new List<string> { $"action:{normalizedEvent.EventName}", $"url:{normalizedEvent.Url}" }
            );

            _ = RecordCrashAsync(
                normalizedEvent.TenantId,
                normalizedEvent.UserId,
                crashPayload,
                normalizedEvent.AppVersion,
                normalizedEvent.Os,
                normalizedEvent.Browser
            );
        }

        return Task.FromResult(new IngestResponseDto(
            Success: true,
            EventId: normalizedEvent.EventId!,
            BufferedQueueCount: _ingestBuffer.Count,
            IngestedAtUtc: DateTime.UtcNow
        ));
    }

    public Task<BatchIngestResponseDto> IngestBatchAsync(IEnumerable<TelemetryIngestEvent> events)
    {
        int count = 0;
        foreach (var evt in events)
        {
            var normalizedEvent = NormalizeEvent(evt);
            _ingestBuffer.Enqueue(normalizedEvent);
            count++;

            if (normalizedEvent.EventType.Equals("crash", StringComparison.OrdinalIgnoreCase) || normalizedEvent.CrashData != null)
            {
                var crashPayload = normalizedEvent.CrashData ?? new CrashReportPayload(
                    "ClientReportedCrash",
                    normalizedEvent.EventName,
                    ""
                );

                _ = RecordCrashAsync(
                    normalizedEvent.TenantId,
                    normalizedEvent.UserId,
                    crashPayload,
                    normalizedEvent.AppVersion,
                    normalizedEvent.Os,
                    normalizedEvent.Browser
                );
            }
        }

        return Task.FromResult(new BatchIngestResponseDto(
            Success: true,
            AcceptedCount: count,
            TotalQueueCount: _ingestBuffer.Count,
            IngestedAtUtc: DateTime.UtcNow
        ));
    }

    public Task<int> FlushBufferAsync(int maxBatchSize = 1000)
    {
        lock (_flushLock)
        {
            int drained = 0;
            while (drained < maxBatchSize && _ingestBuffer.TryDequeue(out var evt))
            {
                _storedEvents.Add(evt);
                drained++;
            }
            return Task.FromResult(drained);
        }
    }

    public int GetPendingBufferCount() => _ingestBuffer.Count;

    public int GetTotalStoredEventsCount() => _storedEvents.Count;

    private static TelemetryIngestEvent NormalizeEvent(TelemetryIngestEvent evt)
    {
        var eventId = string.IsNullOrWhiteSpace(evt.EventId) ? Guid.NewGuid().ToString("N") : evt.EventId;
        var timestamp = evt.TimestampUtc == default ? DateTime.UtcNow : evt.TimestampUtc;
        var deviceType = string.IsNullOrWhiteSpace(evt.DeviceType) ? "Desktop" : evt.DeviceType;
        var os = string.IsNullOrWhiteSpace(evt.Os) ? "Web" : evt.Os;
        var browser = string.IsNullOrWhiteSpace(evt.Browser) ? "Chrome" : evt.Browser;
        var appVersion = string.IsNullOrWhiteSpace(evt.AppVersion) ? "1.0.0" : evt.AppVersion;

        return evt with
        {
            EventId = eventId,
            TimestampUtc = timestamp,
            DeviceType = deviceType,
            Os = os,
            Browser = browser,
            AppVersion = appVersion
        };
    }

    #endregion

    #region Time-Series Rollup Aggregator

    public Task<ActiveUsersMetricsDto> GetActiveUsersMetricsAsync(Guid? tenantId = null, int days = 30)
    {
        var allEvents = GetFilteredEvents(tenantId).ToList();
        var now = DateTime.UtcNow;
        var todayStart = now.Date;
        var yesterdayStart = todayStart.AddDays(-1);
        var sevenDaysAgo = now.AddDays(-7);
        var fourteenDaysAgo = now.AddDays(-14);
        var thirtyDaysAgo = now.AddDays(-30);

        // DAU: active users today (or in last 24h)
        var dauUsersToday = allEvents
            .Where(e => e.TimestampUtc >= todayStart && e.TimestampUtc <= now)
            .Select(e => e.UserId)
            .Distinct()
            .Count();

        var dauUsersYesterday = allEvents
            .Where(e => e.TimestampUtc >= yesterdayStart && e.TimestampUtc < todayStart)
            .Select(e => e.UserId)
            .Distinct()
            .Count();

        // WAU: active users in last 7 days
        var wauUsers = allEvents
            .Where(e => e.TimestampUtc >= sevenDaysAgo && e.TimestampUtc <= now)
            .Select(e => e.UserId)
            .Distinct()
            .Count();

        var prevWauUsers = allEvents
            .Where(e => e.TimestampUtc >= fourteenDaysAgo && e.TimestampUtc < sevenDaysAgo)
            .Select(e => e.UserId)
            .Distinct()
            .Count();

        // MAU: active users in last 30 days
        var mauUsers = allEvents
            .Where(e => e.TimestampUtc >= thirtyDaysAgo && e.TimestampUtc <= now)
            .Select(e => e.UserId)
            .Distinct()
            .Count();

        // Fallback for DAU if today has low traffic in test environments
        if (dauUsersToday == 0 && mauUsers > 0)
        {
            var latestDate = allEvents.Max(e => e.TimestampUtc).Date;
            dauUsersToday = allEvents
                .Where(e => e.TimestampUtc.Date == latestDate)
                .Select(e => e.UserId)
                .Distinct()
                .Count();
        }

        // Stickiness ratio: (DAU / MAU) * 100
        double stickiness = mauUsers > 0
            ? Math.Round((double)dauUsersToday / mauUsers * 100.0, 2)
            : 0.0;

        string health = stickiness switch
        {
            >= 25.0 => "Elite (>25%)",
            >= 18.0 => "Good (18-25%)",
            >= 10.0 => "Average (10-18%)",
            _ => "At Risk (<10%)"
        };

        // Day-over-day growth
        double dauDod = dauUsersYesterday > 0
            ? Math.Round(((double)dauUsersToday - dauUsersYesterday) / dauUsersYesterday * 100.0, 1)
            : 0.0;

        // Week-over-week growth
        double wauWow = prevWauUsers > 0
            ? Math.Round(((double)wauUsers - prevWauUsers) / prevWauUsers * 100.0, 1)
            : 0.0;

        // Generate daily time-series points
        var timeSeries = new List<DailyActiveUsersPointDto>();
        int daysToGenerate = Math.Max(1, Math.Min(days, 90));
        var startDate = todayStart.AddDays(-(daysToGenerate - 1));

        for (int i = 0; i < daysToGenerate; i++)
        {
            var date = startDate.AddDays(i);
            var nextDate = date.AddDays(1);
            var dayEvents = allEvents.Where(e => e.TimestampUtc >= date && e.TimestampUtc < nextDate).ToList();

            var rolling7Start = date.AddDays(-6);
            var rolling7Users = allEvents
                .Where(e => e.TimestampUtc >= rolling7Start && e.TimestampUtc < nextDate)
                .Select(e => e.UserId)
                .Distinct()
                .Count();

            timeSeries.Add(new DailyActiveUsersPointDto(
                Date: date,
                DateLabel: date.ToString("MMM dd"),
                Dau: dayEvents.Select(e => e.UserId).Distinct().Count(),
                EventsCount: dayEvents.Count,
                SessionsCount: dayEvents.Select(e => e.SessionId).Distinct().Count(),
                Rolling7DayActive: rolling7Users
            ));
        }

        int todayEventsCount = allEvents.Count(e => e.TimestampUtc >= todayStart);

        return Task.FromResult(new ActiveUsersMetricsDto(
            TenantId: tenantId,
            Dau: dauUsersToday,
            Wau: wauUsers,
            Mau: mauUsers,
            StickinessRatioPercent: stickiness,
            StickinessBenchmarkHealth: health,
            TodayEventsCount: todayEventsCount,
            TotalEventsCount: allEvents.Count,
            DauDayOverDayGrowthPercent: dauDod,
            WauWeekOverWeekGrowthPercent: wauWow,
            TimeSeries: timeSeries
        ));
    }

    #endregion

    #region Cohort Retention Matrix Engine

    public Task<CohortRetentionMatrixDto> GetCohortRetentionMatrixAsync(Guid? tenantId = null, string granularity = "weekly", int cohortCount = 8)
    {
        var allEvents = GetFilteredEvents(tenantId).ToList();
        var intervals = new List<int> { 0, 1, 3, 7, 14, 21, 30 };
        var now = DateTime.UtcNow;

        if (allEvents.Count == 0)
        {
            return Task.FromResult(new CohortRetentionMatrixDto(
                TenantId: tenantId,
                Granularity: granularity,
                RetentionIntervals: intervals,
                Cohorts: new List<CohortRetentionRowDto>(),
                AverageRetentionCurve: intervals.ToDictionary(k => k, _ => 0.0)
            ));
        }

        // Determine each user's acquisition date (first recorded event)
        var userAcquisitions = allEvents
            .GroupBy(e => e.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                FirstEventTime = g.Min(e => e.TimestampUtc),
                Events = g.ToList()
            })
            .ToList();

        // Group users into cohorts based on granularity
        var cohortGroups = new Dictionary<string, (DateTime StartDate, string Label, List<(string UserId, DateTime FirstTime, List<TelemetryIngestEvent> Events)> Users)>();

        foreach (var user in userAcquisitions)
        {
            DateTime cohortStart;
            string cohortId;
            string cohortLabel;

            if (granularity.Equals("monthly", StringComparison.OrdinalIgnoreCase))
            {
                cohortStart = new DateTime(user.FirstEventTime.Year, user.FirstEventTime.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                cohortId = cohortStart.ToString("yyyy-MM");
                cohortLabel = cohortStart.ToString("MMM yyyy");
            }
            else if (granularity.Equals("daily", StringComparison.OrdinalIgnoreCase))
            {
                cohortStart = user.FirstEventTime.Date;
                cohortId = cohortStart.ToString("yyyy-MM-dd");
                cohortLabel = cohortStart.ToString("MMM dd");
            }
            else // "weekly" default
            {
                // Align to Monday of that week
                int diff = (7 + (user.FirstEventTime.Date.DayOfWeek - DayOfWeek.Monday)) % 7;
                cohortStart = user.FirstEventTime.Date.AddDays(-diff);
                var cohortEnd = cohortStart.AddDays(6);
                cohortId = $"{cohortStart:yyyy}-W{(cohortStart.DayOfYear / 7) + 1:D2}";
                cohortLabel = $"{cohortStart:MMM dd} - {cohortEnd:MMM dd}";
            }

            if (!cohortGroups.TryGetValue(cohortId, out var group))
            {
                group = (cohortStart, cohortLabel, new List<(string, DateTime, List<TelemetryIngestEvent>)>());
                cohortGroups[cohortId] = group;
            }

            group.Users.Add((user.UserId, user.FirstEventTime, user.Events));
        }

        // Sort cohorts by start date descending and take top N
        var sortedCohorts = cohortGroups
            .OrderByDescending(kv => kv.Value.StartDate)
            .Take(cohortCount)
            .OrderBy(kv => kv.Value.StartDate)
            .ToList();

        var rows = new List<CohortRetentionRowDto>();

        foreach (var (cId, group) in sortedCohorts)
        {
            int totalAcquired = group.Users.Count;
            var retentionPcts = new Dictionary<int, double?>();
            var retainedCounts = new Dictionary<int, int?>();

            foreach (var interval in intervals)
            {
                // If the interval date is in the future, cell is pending/null
                var intervalEvaluationDate = group.StartDate.AddDays(interval);
                if (intervalEvaluationDate > now.Date.AddDays(1))
                {
                    retentionPcts[interval] = null;
                    retainedCounts[interval] = null;
                    continue;
                }

                if (interval == 0)
                {
                    retentionPcts[0] = 100.0;
                    retainedCounts[0] = totalAcquired;
                    continue;
                }

                // Check how many users in this cohort had activity on day [interval]
                int retainedUsers = 0;
                foreach (var user in group.Users)
                {
                    // Target active window: user's first event + interval days (with 1 day tolerance)
                    var targetDay = user.FirstTime.Date.AddDays(interval);
                    bool wasActive = user.Events.Any(e => e.TimestampUtc.Date == targetDay);
                    if (wasActive)
                    {
                        retainedUsers++;
                    }
                }

                double pct = totalAcquired > 0
                    ? Math.Round((double)retainedUsers / totalAcquired * 100.0, 1)
                    : 0.0;

                retentionPcts[interval] = pct;
                retainedCounts[interval] = retainedUsers;
            }

            rows.Add(new CohortRetentionRowDto(
                CohortId: cId,
                CohortLabel: group.Label,
                CohortStartDate: group.StartDate,
                TotalAcquiredUsers: totalAcquired,
                RetentionPercentages: retentionPcts,
                RetainedUserCounts: retainedCounts
            ));
        }

        // Calculate average retention curve across non-null cohort entries
        var averageCurve = new Dictionary<int, double>();
        foreach (var interval in intervals)
        {
            var validRows = rows.Where(r => r.RetentionPercentages.ContainsKey(interval) && r.RetentionPercentages[interval].HasValue).ToList();
            if (validRows.Count > 0)
            {
                double totalWeighted = validRows.Sum(r => r.RetentionPercentages[interval]!.Value * r.TotalAcquiredUsers);
                int totalUsers = validRows.Sum(r => r.TotalAcquiredUsers);
                averageCurve[interval] = totalUsers > 0 ? Math.Round(totalWeighted / totalUsers, 1) : 0.0;
            }
            else
            {
                averageCurve[interval] = 0.0;
            }
        }

        return Task.FromResult(new CohortRetentionMatrixDto(
            TenantId: tenantId,
            Granularity: granularity,
            RetentionIntervals: intervals,
            Cohorts: rows,
            AverageRetentionCurve: averageCurve
        ));
    }

    #endregion

    #region Crash Diagnostics & Stack Trace Clustering

    public static string ComputeDeterministicSignature(string exceptionType, string stackTrace, string message)
    {
        var normalized = NormalizeStackTrace(exceptionType, stackTrace, message);
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private static string NormalizeStackTrace(string exceptionType, string stackTrace, string message)
    {
        if (string.IsNullOrWhiteSpace(stackTrace))
        {
            var cleanMsg = Regex.Replace(message, @"\d+", "N").Trim();
            return $"{exceptionType.Trim()}::{cleanMsg}";
        }

        // 1. Strip dynamic line numbers (e.g. ":line 123" -> ":line X")
        var normalized = Regex.Replace(stackTrace, @":line\s+\d+", ":line X", RegexOptions.IgnoreCase);

        // 2. Strip hexadecimal memory pointers (e.g. "0x7ffee032" -> "0xADDR")
        normalized = Regex.Replace(normalized, @"0x[0-9a-fA-F]+", "0xADDR");

        // 3. Strip absolute file system paths, keeping just file names
        normalized = Regex.Replace(normalized, @"[A-Za-z]:\\[^:\n\r]+?\\([A-Za-z0-9_.-]+\.cs)", "$1");
        normalized = Regex.Replace(normalized, @"/[^:\n\r]+?/([A-Za-z0-9_.-]+\.(?:cs|ts|js))", "$1");

        // 4. Extract top 4 meaningful stack frames
        var lines = normalized.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => l.StartsWith("at ", StringComparison.OrdinalIgnoreCase) || l.Contains("("))
            .Take(4);

        return $"{exceptionType.Trim()}::{string.Join(" | ", lines)}";
    }

    public Task<CrashClusterDto> RecordCrashAsync(Guid? tenantId, string userId, CrashReportPayload payload, string? appVersion = null, string? os = null, string? browser = null)
    {
        var sig = ComputeDeterministicSignature(payload.ExceptionType, payload.StackTrace, payload.Message);
        var now = DateTime.UtcNow;
        var version = string.IsNullOrWhiteSpace(appVersion) ? "1.0.0" : appVersion;
        var platform = $"{os ?? "Web"} / {browser ?? "Chrome"}";
        var severity = string.IsNullOrWhiteSpace(payload.Severity) ? "Error" : payload.Severity;

        var cluster = _crashClusters.AddOrUpdate(
            sig,
            _ =>
            {
                var newCluster = new InternalCrashCluster
                {
                    Signature = sig,
                    ExceptionType = payload.ExceptionType,
                    NormalizedErrorMessage = payload.Message,
                    StackTraceSample = payload.StackTrace,
                    TotalOccurrences = 1,
                    FirstSeenUtc = now,
                    LastSeenUtc = now,
                    Severity = severity,
                    Status = "Open"
                };
                newCluster.AffectedUserIds.Add(userId);
                newCluster.Versions.Add(version);
                newCluster.Platforms.AddOrUpdate(platform, 1, (_, c) => c + 1);
                if (payload.Breadcrumbs != null)
                {
                    foreach (var bc in payload.Breadcrumbs) newCluster.Breadcrumbs.Add(bc);
                }
                return newCluster;
            },
            (_, existing) =>
            {
                Interlocked.Increment(ref existing.TotalOccurrences);
                existing.LastSeenUtc = now;
                existing.AffectedUserIds.Add(userId);
                lock (existing.Versions)
                {
                    if (!existing.Versions.Contains(version)) existing.Versions.Add(version);
                }
                existing.Platforms.AddOrUpdate(platform, 1, (_, c) => c + 1);
                if (payload.Breadcrumbs != null)
                {
                    lock (existing.Breadcrumbs)
                    {
                        foreach (var bc in payload.Breadcrumbs)
                        {
                            if (existing.Breadcrumbs.Count < 25) existing.Breadcrumbs.Add(bc);
                        }
                    }
                }
                return existing;
            }
        );

        return Task.FromResult(cluster.ToDto());
    }

    public Task<CrashClustersResponseDto> GetCrashClustersAsync(Guid? tenantId = null, string? severity = null, string? status = null)
    {
        var query = _crashClusters.Values.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(severity))
        {
            query = query.Where(c => c.Severity.Equals(severity, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(c => c.Status.Equals(status, StringComparison.OrdinalIgnoreCase));
        }

        var list = query
            .OrderByDescending(c => c.TotalOccurrences)
            .Select(c => c.ToDto())
            .ToList();

        int totalOccurrences = list.Sum(c => c.TotalOccurrences);
        int totalUsers = _crashClusters.Values.SelectMany(c => c.AffectedUserIds).Distinct().Count();

        return Task.FromResult(new CrashClustersResponseDto(
            TenantId: tenantId,
            TotalCrashOccurrences: totalOccurrences,
            TotalUniqueAffectedUsers: totalUsers,
            TotalClustersCount: list.Count,
            Clusters: list
        ));
    }

    public Task<bool> UpdateCrashStatusAsync(string signature, string newStatus)
    {
        if (_crashClusters.TryGetValue(signature, out var cluster))
        {
            cluster.Status = newStatus;
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    #endregion

    #region Multi-Step Funnel Conversion Calculator

    public Task<FunnelEvaluationResultDto> EvaluateFunnelAsync(EvaluateFunnelRequestDto request)
    {
        var steps = request.Steps?.Where(s => !string.IsNullOrWhiteSpace(s)).ToList() ?? new List<string>();
        if (steps.Count == 0)
        {
            return Task.FromResult(new FunnelEvaluationResultDto(
                FunnelName: request.FunnelName ?? "Custom Funnel",
                EvaluatedAtUtc: DateTime.UtcNow,
                TotalEnteredUsers: 0,
                TotalCompletedUsers: 0,
                OverallConversionPercent: 0.0,
                BiggestDropoffStep: "None",
                BiggestDropoffPercent: 0.0,
                Steps: new List<FunnelStepMetricDto>()
            ));
        }

        var allEvents = GetFilteredEvents(request.TenantId);
        if (request.FromUtc.HasValue)
        {
            allEvents = allEvents.Where(e => e.TimestampUtc >= request.FromUtc.Value);
        }
        if (request.ToUtc.HasValue)
        {
            allEvents = allEvents.Where(e => e.TimestampUtc <= request.ToUtc.Value);
        }

        var userEventGroups = allEvents
            .GroupBy(e => e.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                Events = g.OrderBy(e => e.TimestampUtc).ToList()
            })
            .ToList();

        // Evaluate chronological step progression for each user
        // A user completes step K if they have an event matching steps[K] on or after completing steps[K-1]
        var userProgression = new Dictionary<string, List<(int StepIndex, DateTime Timestamp)>>();

        foreach (var user in userEventGroups)
        {
            var userCompletedSteps = new List<(int, DateTime)>();
            DateTime lastStepTime = DateTime.MinValue;

            for (int stepIdx = 0; stepIdx < steps.Count; stepIdx++)
            {
                var stepName = steps[stepIdx];
                var matchingEvent = user.Events.FirstOrDefault(e =>
                    e.EventName.Equals(stepName, StringComparison.OrdinalIgnoreCase) &&
                    e.TimestampUtc >= lastStepTime
                );

                if (matchingEvent != null)
                {
                    userCompletedSteps.Add((stepIdx, matchingEvent.TimestampUtc));
                    lastStepTime = matchingEvent.TimestampUtc;
                }
                else
                {
                    // Strict funnel: cannot skip step
                    break;
                }
            }

            if (userCompletedSteps.Count > 0)
            {
                userProgression[user.UserId] = userCompletedSteps;
            }
        }

        int totalEntered = userProgression.Count(kv => kv.Value.Any(s => s.StepIndex == 0));
        var stepMetrics = new List<FunnelStepMetricDto>();
        string biggestDropoffStep = "None";
        double maxDropoffPct = -1.0;

        for (int i = 0; i < steps.Count; i++)
        {
            int currentStepIdx = i;
            var usersAtThisStep = userProgression.Values.Where(prog => prog.Any(s => s.StepIndex == currentStepIdx)).ToList();
            int reachedCount = usersAtThisStep.Count;

            int nextReachedCount = (i + 1 < steps.Count)
                ? userProgression.Values.Count(prog => prog.Any(s => s.StepIndex == currentStepIdx + 1))
                : reachedCount;

            int droppedCount = reachedCount - nextReachedCount;

            int prevReachedCount = (i == 0)
                ? reachedCount
                : userProgression.Values.Count(prog => prog.Any(s => s.StepIndex == currentStepIdx - 1));

            double stepConversion = (prevReachedCount > 0)
                ? Math.Round((double)reachedCount / prevReachedCount * 100.0, 1)
                : 0.0;

            double overallConversion = (totalEntered > 0)
                ? Math.Round((double)reachedCount / totalEntered * 100.0, 1)
                : 0.0;

            double dropoffPct = (reachedCount > 0)
                ? Math.Round((double)droppedCount / reachedCount * 100.0, 1)
                : 0.0;

            // Compute average transition time from previous step
            double avgDurationSeconds = 0.0;
            if (i > 0 && reachedCount > 0)
            {
                var durations = usersAtThisStep.Select(prog =>
                {
                    var prev = prog.First(s => s.StepIndex == currentStepIdx - 1);
                    var curr = prog.First(s => s.StepIndex == currentStepIdx);
                    return (curr.Timestamp - prev.Timestamp).TotalSeconds;
                }).Where(d => d >= 0).ToList();

                if (durations.Count > 0)
                {
                    avgDurationSeconds = Math.Round(durations.Average(), 1);
                }
            }

            if (i < steps.Count - 1 && dropoffPct > maxDropoffPct)
            {
                maxDropoffPct = dropoffPct;
                biggestDropoffStep = steps[i];
            }

            stepMetrics.Add(new FunnelStepMetricDto(
                StepIndex: i,
                StepName: steps[i],
                ReachedCount: reachedCount,
                DroppedCount: i == steps.Count - 1 ? 0 : droppedCount,
                StepConversionPercent: stepConversion,
                OverallConversionPercent: overallConversion,
                DropoffPercent: i == steps.Count - 1 ? 0.0 : dropoffPct,
                AverageDurationSecondsFromPrevious: avgDurationSeconds
            ));
        }

        int totalCompleted = (steps.Count > 0)
            ? userProgression.Values.Count(prog => prog.Any(s => s.StepIndex == steps.Count - 1))
            : 0;

        double finalOverallConversion = (totalEntered > 0)
            ? Math.Round((double)totalCompleted / totalEntered * 100.0, 1)
            : 0.0;

        return Task.FromResult(new FunnelEvaluationResultDto(
            FunnelName: request.FunnelName ?? "Custom Funnel",
            EvaluatedAtUtc: DateTime.UtcNow,
            TotalEnteredUsers: totalEntered,
            TotalCompletedUsers: totalCompleted,
            OverallConversionPercent: finalOverallConversion,
            BiggestDropoffStep: biggestDropoffStep,
            BiggestDropoffPercent: maxDropoffPct >= 0 ? maxDropoffPct : 0.0,
            Steps: stepMetrics
        ));
    }

    #endregion

    #region Recent Events Inspection

    public Task<IReadOnlyList<TelemetryIngestEvent>> GetRecentEventsAsync(Guid? tenantId = null, int limit = 50, string? eventType = null, string? userId = null)
    {
        var query = GetFilteredEvents(tenantId);

        if (!string.IsNullOrWhiteSpace(eventType))
        {
            query = query.Where(e => e.EventType.Equals(eventType, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(userId))
        {
            query = query.Where(e => e.UserId.Equals(userId, StringComparison.OrdinalIgnoreCase));
        }

        var results = query
            .OrderByDescending(e => e.TimestampUtc)
            .Take(Math.Max(1, Math.Min(limit, 200)))
            .ToList();

        return Task.FromResult<IReadOnlyList<TelemetryIngestEvent>>(results);
    }

    private IEnumerable<TelemetryIngestEvent> GetFilteredEvents(Guid? tenantId)
    {
        if (tenantId == null || tenantId == Guid.Empty)
        {
            return _storedEvents;
        }

        return _storedEvents.Where(e => e.TenantId == tenantId.Value || e.TenantId == Guid.Empty);
    }

    #endregion

    #region Historical Stream Pre-Seeding

    private void SeedHistoricalTelemetry()
    {
        var defaultTenant = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var now = DateTime.UtcNow;
        var random = new Random(42);

        var devices = new[] { "Desktop", "Mobile", "Tablet" };
        var osList = new[] { "macOS", "Windows 11", "iOS", "Android", "Linux" };
        var browsers = new[] { "Chrome", "Safari", "Edge", "Firefox" };

        var funnelStepNames = new[] { "landing_page", "signup_click", "workspace_setup", "team_invite", "plan_subscription" };

        // 1. Seed Cohorts and Event Sequences across 60 users over 42 days
        for (int u = 1; u <= 60; u++)
        {
            var userId = $"USR-10{u:D2}";
            var userDevice = devices[u % devices.Length];
            var userOs = osList[u % osList.Length];
            var userBrowser = browsers[u % browsers.Length];

            // Assign user to an acquisition week (0 to 6 weeks ago)
            int weeksAgo = (u - 1) % 7;
            var acquisitionDate = now.Date.AddDays(-(weeksAgo * 7 + (u % 5))).AddHours(9 + (u % 10));

            // Acquisition Day 0 events (funnel start)
            var session0 = $"SES-{userId}-D0";
            _storedEvents.Add(new TelemetryIngestEvent(
                $"EVT-{userId}-001", defaultTenant, userId, session0, "pageview", "landing_page",
                acquisitionDate, "/home", null, userDevice, userOs, userBrowser, "2.4.0"
            ));

            // Funnel progression for Day 0
            bool signedUp = (u % 10) < 8; // 80% sign up
            if (signedUp)
            {
                _storedEvents.Add(new TelemetryIngestEvent(
                    $"EVT-{userId}-002", defaultTenant, userId, session0, "click", "signup_click",
                    acquisitionDate.AddMinutes(3), "/auth/signup", "/home", userDevice, userOs, userBrowser, "2.4.0"
                ));

                bool setupDone = (u % 10) < 6; // 60% setup workspace
                if (setupDone)
                {
                    _storedEvents.Add(new TelemetryIngestEvent(
                        $"EVT-{userId}-003", defaultTenant, userId, session0, "custom", "workspace_setup",
                        acquisitionDate.AddMinutes(7), "/onboarding/setup", "/auth/signup", userDevice, userOs, userBrowser, "2.4.0"
                    ));

                    bool invited = (u % 10) < 4; // 40% invite team
                    if (invited)
                    {
                        _storedEvents.Add(new TelemetryIngestEvent(
                            $"EVT-{userId}-004", defaultTenant, userId, session0, "custom", "team_invite",
                            acquisitionDate.AddMinutes(12), "/settings/team", "/onboarding/setup", userDevice, userOs, userBrowser, "2.4.0"
                        ));

                        bool subscribed = (u % 10) < 2; // 20% subscribe
                        if (subscribed)
                        {
                            _storedEvents.Add(new TelemetryIngestEvent(
                                $"EVT-{userId}-005", defaultTenant, userId, session0, "conversion", "plan_subscription",
                                acquisitionDate.AddMinutes(20), "/checkout/success", "/settings/team", userDevice, userOs, userBrowser, "2.4.0"
                            ));
                        }
                    }
                }
            }

            // Retention events based on realistic SaaS retention curve
            var retentionDays = new[] { 1, 3, 7, 14, 21, 30 };
            var retentionProbabilities = new[] { 0.65, 0.48, 0.38, 0.28, 0.24, 0.20 };

            for (int r = 0; r < retentionDays.Length; r++)
            {
                int rDay = retentionDays[r];
                var returnDate = acquisitionDate.Date.AddDays(rDay).AddHours(10 + (u % 8));

                if (returnDate <= now)
                {
                    // Check if user returns based on probability curve
                    double userScore = ((u * 17 + r * 31) % 100) / 100.0;
                    if (userScore < retentionProbabilities[r])
                    {
                        var returnSession = $"SES-{userId}-D{rDay}";
                        _storedEvents.Add(new TelemetryIngestEvent(
                            $"EVT-{userId}-R{rDay}", defaultTenant, userId, returnSession, "session_start", "dashboard_visit",
                            returnDate, "/dashboard", "/app", userDevice, userOs, userBrowser, "2.4.0"
                        ));

                        _storedEvents.Add(new TelemetryIngestEvent(
                            $"EVT-{userId}-R{rDay}-2", defaultTenant, userId, returnSession, "pageview", "telemetry_view",
                            returnDate.AddMinutes(5), "/telemetry/analytics", "/dashboard", userDevice, userOs, userBrowser, "2.4.0"
                        ));
                    }
                }
            }
        }

        // 2. Add high-velocity events for Today & Yesterday to populate live DAU / MAU metrics
        for (int i = 1; i <= 28; i++)
        {
            var activeUserId = $"USR-LIVE-{i:D2}";
            var session = $"SES-LIVE-{i}";
            var timeToday = now.Date.AddHours(8 + (i % 12)).AddMinutes(i * 2);

            _storedEvents.Add(new TelemetryIngestEvent(
                $"EVT-LIVE-TODAY-{i}", defaultTenant, activeUserId, session, "pageview", "dashboard_view",
                timeToday, "/dashboard", "/login", "Desktop", "Windows 11", "Chrome", "2.5.0"
            ));

            _storedEvents.Add(new TelemetryIngestEvent(
                $"EVT-LIVE-TODAY-{i}-B", defaultTenant, activeUserId, session, "click", "analytics_export",
                timeToday.AddMinutes(4), "/dashboard/export", "/dashboard", "Desktop", "Windows 11", "Chrome", "2.5.0"
            ));

            // Yesterday events
            var timeYest = now.Date.AddDays(-1).AddHours(9 + (i % 10));
            _storedEvents.Add(new TelemetryIngestEvent(
                $"EVT-LIVE-YEST-{i}", defaultTenant, activeUserId, $"SES-YEST-{i}", "pageview", "campaigns_view",
                timeYest, "/outbound/campaigns", "/dashboard", "Desktop", "macOS", "Safari", "2.5.0"
            ));
        }

        // 3. Pre-seed 5 Distinct Crash Clusters with Deterministic Stack Traces
        var crashSeeds = new[]
        {
            new
            {
                Type = "NullReferenceException",
                Msg = "Object reference not set to an instance of an object in PaymentModal.OnSubmit()",
                Trace = "   at SGrowing.Web.Components.PaymentModal.OnSubmit() in PaymentModal.cs:line 142\n   at Microsoft.AspNetCore.Components.ComponentBase.CallStateHasChanged() in ComponentBase.cs:line 89\n   at SGrowing.Web.Services.CheckoutGateway.ProcessAsync() in CheckoutGateway.cs:line 55",
                Severity = "Critical",
                Occurrences = 142,
                Users = 89,
                Breadcrumbs = new List<string> { "click:upgrade_btn", "select:annual_pro_plan", "input:stripe_token", "submit:payment_modal" },
                Platforms = new Dictionary<string, int> { ["Windows 11 / Chrome"] = 92, ["macOS / Safari"] = 38, ["Android / Chrome"] = 12 }
            },
            new
            {
                Type = "NpgsqlException",
                Msg = "Connection pool timeout (15000ms) exhausted during high query load in AnalyticsRepository.QueryAggregatesAsync()",
                Trace = "   at SGrowing.Data.AnalyticsRepository.QueryAggregatesAsync(Guid tenantId) in AnalyticsRepository.cs:line 218\n   at SGrowing.Api.Modules.Telemetry.ProductTelemetryService.GetLiveSummaryAsync() in ProductTelemetryService.cs:line 82\n   at System.Threading.Tasks.Task.Wait()",
                Severity = "Critical",
                Occurrences = 87,
                Users = 64,
                Breadcrumbs = new List<string> { "nav:dashboard", "filter:date_range_90d", "trigger:export_csv_report" },
                Platforms = new Dictionary<string, int> { ["Linux / Docker"] = 55, ["Windows 11 / Edge"] = 22, ["macOS / Chrome"] = 10 }
            },
            new
            {
                Type = "InvalidOperationException",
                Msg = "Sequence contains no matching element in UserWorkspaceResolver.ResolveDefaultAsync()",
                Trace = "   at System.Linq.ThrowHelper.ThrowNoElementsException()\n   at SGrowing.Services.UserWorkspaceResolver.ResolveDefaultAsync(Guid userId) in UserWorkspaceResolver.cs:line 64\n   at SGrowing.Api.Controllers.AccountController.GetContext() in AccountController.cs:line 31",
                Severity = "Error",
                Occurrences = 53,
                Users = 38,
                Breadcrumbs = new List<string> { "invite:accept_team_invite", "auth:login_oidc_google", "redirect:workspace_dispatch" },
                Platforms = new Dictionary<string, int> { ["macOS / Safari"] = 28, ["iOS / Safari"] = 15, ["Windows 11 / Chrome"] = 10 }
            },
            new
            {
                Type = "HttpRequestException",
                Msg = "Gateway 504 Timeout from upstream WhatsApp Graph API in WhatsAppClient.SendTemplateAsync()",
                Trace = "   at SGrowing.Channels.WhatsAppClient.SendTemplateAsync(String recipientPhone) in WhatsAppClient.cs:line 104\n   at SGrowing.Api.Modules.Channels.WhatsApp.CampaignDispatcher.ExecuteBatchAsync() in CampaignDispatcher.cs:line 78",
                Severity = "Warning",
                Occurrences = 31,
                Users = 19,
                Breadcrumbs = new List<string> { "campaign:open_editor", "preview:template_ar", "btn:dispatch_test_blast" },
                Platforms = new Dictionary<string, int> { ["Windows 11 / Chrome"] = 20, ["Android / Chrome"] = 11 }
            },
            new
            {
                Type = "TypeError",
                Msg = "Cannot read properties of undefined (reading 'jwtToken') in auth-interceptor.ts",
                Trace = "   at AuthInterceptor.intercept (auth-interceptor.ts:42)\n   at HttpHandler.handle (http.mjs:1892)\n   at SessionManager.refreshAuthSession (session.service.ts:114)",
                Severity = "Error",
                Occurrences = 78,
                Users = 52,
                Breadcrumbs = new List<string> { "dialog:session_timeout_warning", "click:extend_session_btn", "api:refresh_token_call" },
                Platforms = new Dictionary<string, int> { ["Windows 11 / Chrome"] = 45, ["macOS / Chrome"] = 22, ["iOS / Safari"] = 11 }
            }
        };

        foreach (var c in crashSeeds)
        {
            var sig = ComputeDeterministicSignature(c.Type, c.Trace, c.Msg);
            var cluster = new InternalCrashCluster
            {
                Signature = sig,
                ExceptionType = c.Type,
                NormalizedErrorMessage = c.Msg,
                StackTraceSample = c.Trace,
                TotalOccurrences = c.Occurrences,
                FirstSeenUtc = now.AddDays(-28),
                LastSeenUtc = now.AddHours(-1),
                Severity = c.Severity,
                Status = "Open"
            };

            for (int u = 1; u <= c.Users; u++)
            {
                cluster.AffectedUserIds.Add($"USR-CRASH-{u:D2}");
            }
            cluster.Versions.Add("2.4.0");
            cluster.Versions.Add("2.4.1");
            foreach (var kvp in c.Platforms) cluster.Platforms[kvp.Key] = kvp.Value;
            foreach (var bc in c.Breadcrumbs) cluster.Breadcrumbs.Add(bc);

            _crashClusters[sig] = cluster;
        }
    }

    #endregion

    private class InternalCrashCluster
    {
        public required string Signature { get; init; }
        public required string ExceptionType { get; init; }
        public required string NormalizedErrorMessage { get; init; }
        public required string StackTraceSample { get; init; }
        public int TotalOccurrences;
        public ConcurrentBag<string> AffectedUserIds { get; } = new();
        public List<string> Versions { get; } = new();
        public ConcurrentDictionary<string, int> Platforms { get; } = new();
        public List<string> Breadcrumbs { get; } = new();
        public DateTime FirstSeenUtc { get; init; }
        public DateTime LastSeenUtc { get; set; }
        public required string Severity { get; init; }
        public string Status { get; set; } = "Open";

        public CrashClusterDto ToDto()
        {
            return new CrashClusterDto(
                Signature: Signature,
                ExceptionType: ExceptionType,
                NormalizedErrorMessage: NormalizedErrorMessage,
                StackTraceSample: StackTraceSample,
                TotalOccurrences: TotalOccurrences,
                UniqueUsersAffected: AffectedUserIds.Distinct().Count(),
                AffectedVersions: Versions.Distinct().ToList(),
                PlatformBreakdown: new Dictionary<string, int>(Platforms),
                RecentBreadcrumbs: Breadcrumbs.Take(15).ToList(),
                FirstSeenUtc: FirstSeenUtc,
                LastSeenUtc: LastSeenUtc,
                Severity: Severity,
                Status: Status
            );
        }
    }
}
