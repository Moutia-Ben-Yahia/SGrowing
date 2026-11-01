using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Analytics.SessionReplay;

/// <summary>
/// Telemetry engine aggregating pointer, click, touch, and scroll data.
/// Detects micro-frustrations (rage clicks, dead clicks, scroll thrashing)
/// and calculates 2D spatial density heatmaps.
/// </summary>
public sealed class UserInteractionHeatmapService(IDomMutationVectorEngine? mutationEngine = null) : IUserInteractionHeatmapService
{
    private sealed class SessionInteractions
    {
        public readonly System.Threading.Lock Lock = new();
        public readonly List<InteractionEvent> Events = [];
        public string? PageUrl;
    }

    private readonly ConcurrentDictionary<string, SessionInteractions> _sessions = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, List<InteractionEvent>> _pageInteractions = new(StringComparer.OrdinalIgnoreCase);
    private readonly System.Threading.Lock _pageLock = new();

    /// <inheritdoc />
    public int IngestInteractions(string sessionId, string pageUrl, IEnumerable<InteractionEvent> events)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        if (events == null)
        {
            return 0;
        }

        var eventList = events as IReadOnlyList<InteractionEvent> ?? events.ToList();
        if (eventList.Count == 0)
        {
            return 0;
        }

        var sessionState = _sessions.GetOrAdd(sessionId, _ => new SessionInteractions());

        lock (sessionState.Lock)
        {
            sessionState.Events.AddRange(eventList);
            if (!string.IsNullOrWhiteSpace(pageUrl))
            {
                sessionState.PageUrl = pageUrl;
            }
        }

        if (!string.IsNullOrWhiteSpace(pageUrl))
        {
            lock (_pageLock)
            {
                var list = _pageInteractions.GetOrAdd(pageUrl, _ => []);
                list.AddRange(eventList);
            }
        }

        return eventList.Count;
    }

    /// <inheritdoc />
    public HeatmapResult GetHeatmap(string pageUrl, HeatmapType type, int gridWidth = 50, int gridHeight = 50)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pageUrl);
        if (gridWidth <= 0) gridWidth = 50;
        if (gridHeight <= 0) gridHeight = 50;

        List<InteractionEvent> matchingEvents = [];
        lock (_pageLock)
        {
            if (_pageInteractions.TryGetValue(pageUrl, out var list))
            {
                matchingEvents = [.. list];
            }
        }

        // Filter by heatmap type
        var filtered = matchingEvents.Where(e => type switch
        {
            HeatmapType.Click => e.Type is InteractionType.Click or InteractionType.TouchEnd,
            HeatmapType.Move => e.Type is InteractionType.PointerMove or InteractionType.TouchStart,
            HeatmapType.Scroll => e.Type is InteractionType.Scroll,
            _ => false
        }).ToList();

        if (filtered.Count == 0)
        {
            return new HeatmapResult(pageUrl, type, gridWidth, gridHeight, 0, 0, []);
        }

        var counts = new Dictionary<(int X, int Y), int>();
        int maxCount = 0;

        foreach (var ev in filtered)
        {
            double vw = ev.ViewportWidth > 0 ? ev.ViewportWidth : 1920.0;
            double vh = ev.ViewportHeight > 0 ? ev.ViewportHeight : 1080.0;

            double u = Math.Clamp(ev.X / vw, 0.0, 0.999999);
            double v = Math.Clamp(ev.Y / vh, 0.0, 0.999999);

            int gx = (int)(u * gridWidth);
            int gy = (int)(v * gridHeight);

            counts.TryGetValue((gx, gy), out int current);
            current++;
            counts[(gx, gy)] = current;

            if (current > maxCount)
            {
                maxCount = current;
            }
        }

        var cells = new List<HeatmapCell>(counts.Count);
        foreach (var (coord, count) in counts)
        {
            double normX = Math.Round((coord.X + 0.5) / gridWidth, 4);
            double normY = Math.Round((coord.Y + 0.5) / gridHeight, 4);
            double intensity = maxCount > 0 ? Math.Round((double)count / maxCount, 4) : 0.0;

            cells.Add(new HeatmapCell(coord.X, coord.Y, normX, normY, count, intensity));
        }

        cells.Sort((a, b) =>
        {
            int cmp = a.GridY.CompareTo(b.GridY);
            return cmp != 0 ? cmp : a.GridX.CompareTo(b.GridX);
        });

        return new HeatmapResult(
            PageUrl: pageUrl,
            Type: type,
            GridWidth: gridWidth,
            GridHeight: gridHeight,
            TotalInteractions: filtered.Count,
            MaxCount: maxCount,
            Cells: cells);
    }

    /// <inheritdoc />
    public IReadOnlyList<FrustrationEvent> DetectFrustrations(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || !_sessions.TryGetValue(sessionId, out var state))
        {
            return [];
        }

        List<InteractionEvent> events;
        lock (state.Lock)
        {
            events = [.. state.Events];
        }

        if (events.Count == 0)
        {
            return [];
        }

        var frustrations = new List<FrustrationEvent>();

        // 1. Detect Rage Clicks: >= 3 clicks within 500ms on same element or radius <= 25px
        DetectRageClicks(events, frustrations);

        // 2. Detect Dead Clicks: Clicks on interactive elements with 0 DOM mutations within 1000ms
        DetectDeadClicks(sessionId, events, frustrations);

        // 3. Detect Scroll Thrashing: >= 4 oscillations in 2 seconds
        DetectScrollThrashing(events, frustrations);

        return frustrations;
    }

    /// <inheritdoc />
    public IReadOnlyList<InteractionEvent> GetSessionInteractions(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || !_sessions.TryGetValue(sessionId, out var state))
        {
            return [];
        }

        lock (state.Lock)
        {
            return [.. state.Events];
        }
    }

    /// <inheritdoc />
    public bool ClearSession(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return false;
        }

        return _sessions.TryRemove(sessionId, out _);
    }

    private static void DetectRageClicks(List<InteractionEvent> events, List<FrustrationEvent> frustrations)
    {
        var clicks = events
            .Where(e => e.Type is InteractionType.Click or InteractionType.TouchEnd)
            .OrderBy(e => e.Timestamp)
            .ToList();

        if (clicks.Count < 3)
        {
            return;
        }

        int i = 0;
        while (i < clicks.Count)
        {
            var seed = clicks[i];
            int clusterCount = 1;
            int lastIndexInCluster = i;

            for (int j = i + 1; j < clicks.Count; j++)
            {
                var candidate = clicks[j];
                double elapsedMs = (candidate.Timestamp - seed.Timestamp).TotalMilliseconds;

                if (elapsedMs > 500.0)
                {
                    break;
                }

                double dx = candidate.X - seed.X;
                double dy = candidate.Y - seed.Y;
                double distSq = (dx * dx) + (dy * dy);

                bool sameSelector = !string.IsNullOrWhiteSpace(seed.ElementSelector) &&
                                    string.Equals(seed.ElementSelector, candidate.ElementSelector, StringComparison.OrdinalIgnoreCase);

                if (distSq <= 25.0 * 25.0 || sameSelector)
                {
                    clusterCount++;
                    lastIndexInCluster = j;
                }
            }

            if (clusterCount >= 3)
            {
                var endClick = clicks[lastIndexInCluster];
                string target = !string.IsNullOrWhiteSpace(seed.ElementSelector) ? seed.ElementSelector : $"({seed.X:F0},{seed.Y:F0})";
                double elapsed = (endClick.Timestamp - seed.Timestamp).TotalMilliseconds;

                frustrations.Add(new FrustrationEvent(
                    Type: FrustrationType.RageClick,
                    ElementSelector: seed.ElementSelector,
                    X: seed.X,
                    Y: seed.Y,
                    StartTime: seed.Timestamp,
                    EndTime: endClick.Timestamp,
                    OccurrenceCount: clusterCount,
                    Description: $"Rage click detected: {clusterCount} rapid clicks within {elapsed:F0}ms on '{target}'.",
                    Severity: Math.Min(1.0, 0.4 + (0.1 * clusterCount))));

                i = lastIndexInCluster + 1;
            }
            else
            {
                i++;
            }
        }
    }

    private void DetectDeadClicks(string sessionId, List<InteractionEvent> events, List<FrustrationEvent> frustrations)
    {
        var clicks = events
            .Where(e => e.Type == InteractionType.Click && IsInteractiveElement(e.ElementSelector))
            .OrderBy(e => e.Timestamp)
            .ToList();

        if (clicks.Count == 0)
        {
            return;
        }

        var mutations = mutationEngine?.GetSessionMutations(sessionId) ?? [];

        foreach (var click in clicks)
        {
            var windowStart = click.Timestamp;
            var windowEnd = click.Timestamp.AddMilliseconds(1000);

            // Check if any DOM mutation responded within 1000ms
            bool hasResponse = mutations.Any(m => m.Timestamp >= windowStart && m.Timestamp <= windowEnd);

            if (!hasResponse)
            {
                frustrations.Add(new FrustrationEvent(
                    Type: FrustrationType.DeadClick,
                    ElementSelector: click.ElementSelector,
                    X: click.X,
                    Y: click.Y,
                    StartTime: click.Timestamp,
                    EndTime: windowEnd,
                    OccurrenceCount: 1,
                    Description: $"Dead click detected on interactive element '{click.ElementSelector}' with zero DOM response within 1000ms.",
                    Severity: 0.75));
            }
        }
    }

    private static void DetectScrollThrashing(List<InteractionEvent> events, List<FrustrationEvent> frustrations)
    {
        var scrolls = events
            .Where(e => e.Type == InteractionType.Scroll)
            .OrderBy(e => e.Timestamp)
            .ToList();

        if (scrolls.Count < 5)
        {
            return;
        }

        // Identify direction changes (reversals)
        var reversals = new List<(DateTimeOffset Timestamp, double ScrollY, int Direction)>();
        int lastDirection = 0; // +1 down, -1 up

        for (int i = 1; i < scrolls.Count; i++)
        {
            double diff = scrolls[i].ScrollY - scrolls[i - 1].ScrollY;
            if (Math.Abs(diff) < 5.0)
            {
                continue;
            }

            int currentDirection = diff > 0 ? 1 : -1;
            if (lastDirection != 0 && currentDirection != lastDirection)
            {
                reversals.Add((scrolls[i].Timestamp, scrolls[i].ScrollY, currentDirection));
            }

            lastDirection = currentDirection;
        }

        if (reversals.Count < 4)
        {
            return;
        }

        // Sliding window of 2.0 seconds with >= 4 reversals
        int idx = 0;
        while (idx <= reversals.Count - 4)
        {
            var start = reversals[idx];
            var fourth = reversals[idx + 3];

            if ((fourth.Timestamp - start.Timestamp).TotalMilliseconds <= 2000.0)
            {
                frustrations.Add(new FrustrationEvent(
                    Type: FrustrationType.ScrollThrashing,
                    ElementSelector: null,
                    X: 0,
                    Y: start.ScrollY,
                    StartTime: start.Timestamp,
                    EndTime: fourth.Timestamp,
                    OccurrenceCount: 4,
                    Description: $"Scroll thrashing detected: 4 direction oscillations within {(fourth.Timestamp - start.Timestamp).TotalMilliseconds:F0}ms.",
                    Severity: 0.8));

                idx += 4;
            }
            else
            {
                idx++;
            }
        }
    }

    private static bool IsInteractiveElement(string? selector)
    {
        if (string.IsNullOrWhiteSpace(selector))
        {
            return false;
        }

        string s = selector.ToLowerInvariant();
        return s.Contains("btn") ||
               s.Contains("button") ||
               s.Contains("clickable") ||
               s.Contains("link") ||
               s.Contains("cta") ||
               s.Contains("submit") ||
               s.Contains("input") ||
               s.Contains("select") ||
               s.Contains("textarea") ||
               s.Contains("tab") ||
               s.Contains("dropdown") ||
               s.StartsWith("a", StringComparison.Ordinal) ||
               s.Contains("role=\"button\"") ||
               s.Contains("role='button'");
    }
}
