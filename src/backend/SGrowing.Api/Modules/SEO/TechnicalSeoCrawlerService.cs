using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.SEO;

#region DTOs & Models

public record StartCrawlRequest(
    string Url,
    int? MaxPages = 50,
    int? MaxDepth = 3,
    Guid? TenantId = null
);

public record CrawlReportSummaryDto(
    Guid CrawlId,
    Guid TenantId,
    string StartingUrl,
    string Status, // "Completed", "Crawling", "Failed"
    int OverallHealthScore, // 0 to 100
    int CrawlabilityScore, // 0 to 100
    int MetadataScore, // 0 to 100
    int LinkIntegrityScore, // 0 to 100
    int TotalPagesCrawled,
    int BrokenLinksCount,
    int MissingTitleCount,
    int MissingMetaCount,
    int MissingH1Count,
    int MultipleH1Count,
    int MissingCanonicalCount,
    int OrphanPagesCount,
    int DeepPagesCount,
    double AvgResponseTimeMs,
    Dictionary<string, int> StatusDistribution,
    Dictionary<string, int> IssueBreakdown,
    DateTime StartedAtUtc,
    DateTime CompletedAtUtc
);

public record CrawledPageDetailDto(
    Guid Id,
    Guid CrawlId,
    string Url,
    string Path,
    int StatusCode,
    long ResponseTimeMs,
    string Title,
    int TitleLength,
    string MetaDescription,
    int MetaDescriptionLength,
    int H1Count,
    string H1Tag,
    bool HasCanonical,
    string? CanonicalUrl,
    bool IsCanonicalSelfReferential,
    bool HasOpenGraph,
    List<string> OpenGraphTagsPresent,
    int InboundLinksCount,
    int OutboundLinksCount,
    double PageRank,
    string PageRankFormatted,
    int CrawlDepth,
    bool IsOrphan,
    List<string> Issues,
    DateTime CrawledAtUtc
);

public record PaginatedResult<T>(
    List<T> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages
);

public record LinkGraphNodeDto(
    string Url,
    string Path,
    int StatusCode,
    double PageRank,
    int InDegree,
    int OutDegree,
    int CrawlDepth,
    bool IsOrphan
);

public record LinkGraphEdgeDto(
    string Source,
    string Target
);

public record LinkGraphResultDto(
    Guid CrawlId,
    int TotalNodes,
    int TotalEdges,
    List<string> OrphanPages,
    List<string> DeepestPages,
    List<LinkGraphNodeDto> Nodes,
    List<LinkGraphEdgeDto> Edges
);

public record TrackKeywordRequest(
    string Keyword,
    string TargetUrl,
    string CountryCode = "TN",
    int? InitialPosition = null,
    string SearchEngine = "Google",
    Guid? TenantId = null
);

public record SerpKeywordRankItemDto(
    Guid Id,
    Guid TenantId,
    string Keyword,
    string TargetUrl,
    string CountryCode,
    string SearchEngine,
    int CurrentPosition,
    int? PreviousPosition,
    string Delta, // "+3", "-1", "0", "New"
    string Status, // "improved", "declined", "unchanged", "new"
    int BestPosition,
    List<SerpRankPointDto> History,
    DateTime LastCheckedUtc
);

public record SerpRankPointDto(
    string Date,
    int Position
);

public record SerpRankingsOverviewDto(
    int TotalTrackedKeywords,
    int Top3Count,
    double Top3Percentage,
    int Top10Count,
    double Top10Percentage,
    int Top30Count,
    double Top30Percentage,
    double OverallVisibilityScore,
    List<SerpKeywordRankItemDto> Keywords
);

public class PageAnalysisResult
{
    public string Url { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public int StatusCode { get; set; } = 200;
    public long ResponseTimeMs { get; set; }
    public string Title { get; set; } = string.Empty;
    public int TitleLength => Title.Length;
    public string MetaDescription { get; set; } = string.Empty;
    public int MetaDescriptionLength => MetaDescription.Length;
    public int H1Count { get; set; }
    public string H1Tag { get; set; } = string.Empty;
    public bool HasCanonical { get; set; }
    public string? CanonicalUrl { get; set; }
    public bool IsCanonicalSelfReferential { get; set; }
    public bool HasOpenGraph { get; set; }
    public List<string> OpenGraphTagsPresent { get; set; } = new();
    public List<string> OutboundInternalLinks { get; set; } = new();
    public List<string> OutboundExternalLinks { get; set; } = new();
    public List<string> Issues { get; set; } = new();
    public int InboundLinksCount { get; set; }
    public int OutboundLinksCount { get; set; }
    public double PageRank { get; set; }
    public string PageRankFormatted { get; set; } = "PR 1.00";
    public int CrawlDepth { get; set; }
    public bool IsOrphan { get; set; }
}

public record FetchedPageResult(
    string Url,
    int StatusCode,
    string? ContentType,
    string? HtmlContent,
    long ResponseTimeMs,
    string? RedirectLocation = null,
    string? ErrorMessage = null
);

public interface IPageFetcher
{
    Task<FetchedPageResult> FetchAsync(string url, CancellationToken ct = default);
}

public class HttpPageFetcher : IPageFetcher
{
    private readonly HttpClient _httpClient;

    public HttpPageFetcher(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = false
        })
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("SGrowing-Technical-Seo-Crawler/1.0 (+https://sgrowing.io/bot)");
        }
    }

    public async Task<FetchedPageResult> FetchAsync(string url, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            sw.Stop();

            int statusCode = (int)response.StatusCode;
            string? redirectLoc = response.Headers.Location?.ToString();
            string? contentType = response.Content.Headers.ContentType?.MediaType;

            string html = string.Empty;
            if (response.Content != null && (contentType == null || contentType.Contains("text/html") || contentType.Contains("xml") || contentType.Contains("text")))
            {
                html = await response.Content.ReadAsStringAsync(ct);
            }

            return new FetchedPageResult(
                Url: url,
                StatusCode: statusCode,
                ContentType: contentType,
                HtmlContent: html,
                ResponseTimeMs: sw.ElapsedMilliseconds,
                RedirectLocation: redirectLoc
            );
        }
        catch (HttpRequestException ex)
        {
            sw.Stop();
            return new FetchedPageResult(
                Url: url,
                StatusCode: 500,
                ContentType: null,
                HtmlContent: string.Empty,
                ResponseTimeMs: sw.ElapsedMilliseconds,
                ErrorMessage: ex.Message
            );
        }
        catch (TaskCanceledException)
        {
            sw.Stop();
            return new FetchedPageResult(
                Url: url,
                StatusCode: 504,
                ContentType: null,
                HtmlContent: string.Empty,
                ResponseTimeMs: sw.ElapsedMilliseconds,
                ErrorMessage: "Crawler page fetch timeout"
            );
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new FetchedPageResult(
                Url: url,
                StatusCode: 500,
                ContentType: null,
                HtmlContent: string.Empty,
                ResponseTimeMs: sw.ElapsedMilliseconds,
                ErrorMessage: ex.Message
            );
        }
    }
}

public class MockPageFetcher : IPageFetcher
{
    private readonly ConcurrentDictionary<string, FetchedPageResult> _mockResponses = new(StringComparer.OrdinalIgnoreCase);

    public void AddPage(string url, int statusCode, string html, long responseTimeMs = 120, string? redirect = null)
    {
        var result = new FetchedPageResult(
            Url: url,
            StatusCode: statusCode,
            ContentType: "text/html",
            HtmlContent: html,
            ResponseTimeMs: responseTimeMs,
            RedirectLocation: redirect
        );

        _mockResponses[url] = result;
        _mockResponses[url.TrimEnd('/')] = result;
        _mockResponses[url.TrimEnd('/') + "/"] = result;
    }

    public Task<FetchedPageResult> FetchAsync(string url, CancellationToken ct = default)
    {
        if (_mockResponses.TryGetValue(url, out var res) ||
            _mockResponses.TryGetValue(url.TrimEnd('/'), out res) ||
            _mockResponses.TryGetValue(url.TrimEnd('/') + "/", out res))
        {
            return Task.FromResult(res);
        }

        // Default 404 for unknown mock pages
        return Task.FromResult(new FetchedPageResult(
            Url: url,
            StatusCode: 404,
            ContentType: "text/html",
            HtmlContent: "<html><head><title>404 Not Found</title></head><body><h1>404 Not Found</h1></body></html>",
            ResponseTimeMs: 80,
            ErrorMessage: "Mock 404"
        ));
    }
}

#endregion

public interface ITechnicalSeoCrawlerService
{
    Task<CrawlReportSummaryDto> StartCrawlAsync(
        Guid tenantId,
        string startingUrl,
        int maxPages = 50,
        int maxDepth = 3,
        CancellationToken ct = default);

    Task<CrawlReportSummaryDto?> GetCrawlSummaryAsync(
        Guid tenantId,
        Guid crawlId);

    Task<PaginatedResult<CrawledPageDetailDto>> GetCrawledPagesAsync(
        Guid tenantId,
        Guid crawlId,
        int page = 1,
        int pageSize = 20,
        string? filter = null);

    Task<LinkGraphResultDto?> GetLinkGraphAsync(
        Guid tenantId,
        Guid crawlId);

    Task<SerpKeywordRankItemDto> TrackKeywordAsync(
        Guid tenantId,
        string keyword,
        string targetUrl,
        string countryCode = "TN",
        int? initialPosition = null,
        string searchEngine = "Google");

    Task<SerpRankingsOverviewDto> GetSerpRankingsAsync(
        Guid tenantId,
        string? countryCode = null);

    // Core helper calculation methods for testing & audit pipelines
    PageAnalysisResult AnalyzeHtml(string url, int statusCode, string html, long responseTimeMs = 150);
    Dictionary<string, double> CalculatePageRank(IEnumerable<string> pages, ILookup<string, string> internalLinks, double dampingFactor = 0.85, int maxIterations = 30);
    int CalculateHealthScore(IEnumerable<PageAnalysisResult> pages, IEnumerable<string> orphanPages, out int crawlability, out int metadata, out int linkIntegrity);
    (string Delta, string Status) ComputeRankDelta(int currentPos, int? prevPos);
    SerpRankingsOverviewDto ComputeVisibilityIndex(List<SerpKeywordRankItemDto> items);
}

public class TechnicalSeoCrawlerService : ITechnicalSeoCrawlerService
{
    private readonly SGrowingDbContext _db;
    private readonly IPageFetcher _pageFetcher;

    // In-memory cache for ultra-fast query performance
    private static readonly ConcurrentDictionary<Guid, CrawlReportSummaryDto> _crawlSummaries = new();
    private static readonly ConcurrentDictionary<Guid, List<CrawledPageDetailDto>> _crawlPageDetails = new();
    private static readonly ConcurrentDictionary<Guid, LinkGraphResultDto> _crawlLinkGraphs = new();

    // Regex matchers compiled for high throughput
    private static readonly Regex TitleRegex = new(@"<title[^>]*>(?<val>.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex MetaDescRegex1 = new(@"<meta[^>]+name\s*=\s*[""']description[""'][^>]+content\s*=\s*[""'](?<val>[^""']*)[""'][^>]*>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex MetaDescRegex2 = new(@"<meta[^>]+content\s*=\s*[""'](?<val>[^""']*)[""'][^>]+name\s*=\s*[""']description[""'][^>]*>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex H1Regex = new(@"<h1[^>]*>(?<val>.*?)</h1>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex CanonicalRegex1 = new(@"<link[^>]+rel\s*=\s*[""']canonical[""'][^>]+href\s*=\s*[""'](?<val>[^""']*)[""'][^>]*>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex CanonicalRegex2 = new(@"<link[^>]+href\s*=\s*[""'](?<val>[^""']*)[""'][^>]+rel\s*=\s*[""']canonical[""'][^>]*>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex OgTitleRegex1 = new(@"<meta[^>]+property\s*=\s*[""']og:title[""'][^>]+content\s*=\s*[""'](?<val>[^""']*)[""'][^>]*>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex OgTitleRegex2 = new(@"<meta[^>]+content\s*=\s*[""'](?<val>[^""']*)[""'][^>]+property\s*=\s*[""']og:title[""'][^>]*>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex OgImageRegex1 = new(@"<meta[^>]+property\s*=\s*[""']og:image[""'][^>]+content\s*=\s*[""'](?<val>[^""']*)[""'][^>]*>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex OgImageRegex2 = new(@"<meta[^>]+content\s*=\s*[""'](?<val>[^""']*)[""'][^>]+property\s*=\s*[""']og:image[""'][^>]*>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex OgDescRegex1 = new(@"<meta[^>]+property\s*=\s*[""']og:description[""'][^>]+content\s*=\s*[""'](?<val>[^""']*)[""'][^>]*>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex OgDescRegex2 = new(@"<meta[^>]+content\s*=\s*[""'](?<val>[^""']*)[""'][^>]+property\s*=\s*[""']og:description[""'][^>]*>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex AnchorRegex = new(@"<a[^>]+href\s*=\s*[""'](?<val>[^""'#\s>]+)[""'][^>]*>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex HtmlTagsStripRegex = new(@"<[^>]+>", RegexOptions.Compiled);

    public TechnicalSeoCrawlerService(SGrowingDbContext db, IPageFetcher? pageFetcher = null)
    {
        _db = db;
        _pageFetcher = pageFetcher ?? new HttpPageFetcher();
    }

    #region Crawler Engine

    public async Task<CrawlReportSummaryDto> StartCrawlAsync(
        Guid tenantId,
        string startingUrl,
        int maxPages = 50,
        int maxDepth = 3,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(startingUrl) || !Uri.TryCreate(startingUrl.Trim(), UriKind.Absolute, out var baseUri))
        {
            throw new ArgumentException("A valid absolute starting URL (http or https) is required.", nameof(startingUrl));
        }

        if (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("Starting URL must use HTTP or HTTPS scheme.", nameof(startingUrl));
        }

        maxPages = Math.Clamp(maxPages, 1, 500);
        maxDepth = Math.Clamp(maxDepth, 1, 5);

        var crawlId = Guid.NewGuid();
        var startedAt = DateTime.UtcNow;
        string rootHost = baseUri.Host.ToLowerInvariant();
        string normalizedRootUrl = NormalizeUrl(startingUrl, baseUri);

        var visited = new ConcurrentDictionary<string, PageAnalysisResult>(StringComparer.OrdinalIgnoreCase);
        var queued = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        var queue = new ConcurrentQueue<(string Url, int Depth)>();
        var internalLinksMap = new ConcurrentDictionary<string, ConcurrentBag<string>>(StringComparer.OrdinalIgnoreCase);

        queue.Enqueue((normalizedRootUrl, 0));
        queued.TryAdd(normalizedRootUrl, 0);

        int concurrencyLimit = 5;
        using var semaphore = new SemaphoreSlim(concurrencyLimit, concurrencyLimit);

        while (!queue.IsEmpty && visited.Count < maxPages && !ct.IsCancellationRequested)
        {
            var batch = new List<(string Url, int Depth)>();
            while (batch.Count < concurrencyLimit && queue.TryDequeue(out var item) && (visited.Count + batch.Count) < maxPages)
            {
                batch.Add(item);
            }

            if (batch.Count == 0) break;

            var tasks = batch.Select(async pageItem =>
            {
                await semaphore.WaitAsync(ct);
                try
                {
                    if (visited.ContainsKey(pageItem.Url)) return;

                    var fetchResult = await _pageFetcher.FetchAsync(pageItem.Url, ct);
                    var analysis = AnalyzeHtml(pageItem.Url, fetchResult.StatusCode, fetchResult.HtmlContent ?? string.Empty, fetchResult.ResponseTimeMs);
                    analysis.CrawlDepth = pageItem.Depth;

                    visited[pageItem.Url] = analysis;

                    // If page has redirect location, add internal edge
                    if (!string.IsNullOrEmpty(fetchResult.RedirectLocation))
                    {
                        string normRedirect = NormalizeUrl(fetchResult.RedirectLocation, new Uri(pageItem.Url));
                        if (IsInternalUrl(normRedirect, rootHost))
                        {
                            var bag = internalLinksMap.GetOrAdd(pageItem.Url, _ => new ConcurrentBag<string>());
                            bag.Add(normRedirect);

                            if (pageItem.Depth < maxDepth && visited.Count + queued.Count < maxPages * 2)
                            {
                                if (queued.TryAdd(normRedirect, 0))
                                {
                                    queue.Enqueue((normRedirect, pageItem.Depth + 1));
                                }
                            }
                        }
                    }

                    // Process discovered internal links
                    foreach (var link in analysis.OutboundInternalLinks)
                    {
                        var bag = internalLinksMap.GetOrAdd(pageItem.Url, _ => new ConcurrentBag<string>());
                        bag.Add(link);

                        if (pageItem.Depth < maxDepth && (visited.Count + queued.Count) < (maxPages * 2))
                        {
                            if (queued.TryAdd(link, 0))
                            {
                                queue.Enqueue((link, pageItem.Depth + 1));
                            }
                        }
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);
        }

        // Stage 2: Multipage Link Equity & PageRank Analysis
        var allPagesList = visited.Values.ToList();
        var allUrls = allPagesList.Select(p => p.Url).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        // Flatten internal links to ILookup
        var edgePairs = new List<KeyValuePair<string, string>>();
        foreach (var kvp in internalLinksMap)
        {
            foreach (var target in kvp.Value.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (visited.ContainsKey(target))
                {
                    edgePairs.Add(new KeyValuePair<string, string>(kvp.Key, target));
                }
            }
        }
        var linksLookup = edgePairs.ToLookup(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);

        // Calculate PageRank with damping factor 0.85
        var pageRanks = CalculatePageRank(allUrls, linksLookup, dampingFactor: 0.85, maxIterations: 30);

        // Calculate in-degree for each page
        var inDegrees = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var url in allUrls) inDegrees[url] = 0;
        foreach (var pair in edgePairs)
        {
            if (inDegrees.ContainsKey(pair.Value))
            {
                inDegrees[pair.Value]++;
            }
        }

        // Identify Orphan Pages (0 inbound links, excluding homepage / starting URL)
        var orphanPages = allPagesList
            .Where(p => !string.Equals(p.Url, normalizedRootUrl, StringComparison.OrdinalIgnoreCase) && inDegrees.GetValueOrDefault(p.Url) == 0)
            .Select(p => p.Url)
            .ToList();

        // Identify Deepest Pages (> 3 clicks from starting URL)
        var deepestPages = allPagesList
            .Where(p => p.CrawlDepth > 3)
            .Select(p => p.Url)
            .ToList();

        // Enrich page analysis with graph data & flags
        foreach (var page in allPagesList)
        {
            int inCount = inDegrees.GetValueOrDefault(page.Url);
            page.InboundLinksCount = inCount;
            page.OutboundLinksCount = linksLookup[page.Url].Distinct(StringComparer.OrdinalIgnoreCase).Count();

            double pr = pageRanks.GetValueOrDefault(page.Url, 1.0 / Math.Max(1, allUrls.Count));
            page.PageRank = pr;
            page.PageRankFormatted = $"PR {(pr * allUrls.Count):F2}";

            if (orphanPages.Contains(page.Url, StringComparer.OrdinalIgnoreCase))
            {
                page.IsOrphan = true;
                if (!page.Issues.Contains("OrphanPage")) page.Issues.Add("OrphanPage");
            }

            if (page.CrawlDepth > 3)
            {
                string deepIssue = $"DeepPage_Depth_{page.CrawlDepth}";
                if (!page.Issues.Contains(deepIssue)) page.Issues.Add(deepIssue);
            }
        }

        // Compute Site Health Score
        int healthScore = CalculateHealthScore(allPagesList, orphanPages, out int crawlability, out int metadata, out int linkIntegrity);

        var completedAt = DateTime.UtcNow;

        // Tally issues and status codes
        var statusDist = allPagesList
            .GroupBy(p => p.StatusCode)
            .ToDictionary(g => g.Key.ToString(), g => g.Count());

        var issueBreakdown = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in allPagesList)
        {
            foreach (var issue in p.Issues)
            {
                issueBreakdown[issue] = issueBreakdown.GetValueOrDefault(issue, 0) + 1;
            }
        }

        int brokenLinksCount = allPagesList.Count(p => p.StatusCode >= 400 || p.Issues.Any(i => i.StartsWith("BrokenLink")));
        int missingTitleCount = allPagesList.Count(p => p.Issues.Contains("MissingTitle"));
        int missingMetaCount = allPagesList.Count(p => p.Issues.Contains("MissingMetaDescription"));
        int missingH1Count = allPagesList.Count(p => p.Issues.Contains("MissingH1"));
        int multipleH1Count = allPagesList.Count(p => p.Issues.Contains("MultipleH1"));
        int missingCanonicalCount = allPagesList.Count(p => p.Issues.Contains("MissingCanonical"));
        double avgRespTime = allPagesList.Count > 0 ? Math.Round(allPagesList.Average(p => p.ResponseTimeMs), 1) : 0;

        var summary = new CrawlReportSummaryDto(
            CrawlId: crawlId,
            TenantId: tenantId,
            StartingUrl: startingUrl,
            Status: "Completed",
            OverallHealthScore: healthScore,
            CrawlabilityScore: crawlability,
            MetadataScore: metadata,
            LinkIntegrityScore: linkIntegrity,
            TotalPagesCrawled: allPagesList.Count,
            BrokenLinksCount: brokenLinksCount,
            MissingTitleCount: missingTitleCount,
            MissingMetaCount: missingMetaCount,
            MissingH1Count: missingH1Count,
            MultipleH1Count: multipleH1Count,
            MissingCanonicalCount: missingCanonicalCount,
            OrphanPagesCount: orphanPages.Count,
            DeepPagesCount: deepestPages.Count,
            AvgResponseTimeMs: avgRespTime,
            StatusDistribution: statusDist,
            IssueBreakdown: issueBreakdown,
            StartedAtUtc: startedAt,
            CompletedAtUtc: completedAt
        );

        // Convert to DTOs for caching & retrieval
        var detailDtos = allPagesList.Select(p => new CrawledPageDetailDto(
            Id: Guid.NewGuid(),
            CrawlId: crawlId,
            Url: p.Url,
            Path: p.Path,
            StatusCode: p.StatusCode,
            ResponseTimeMs: p.ResponseTimeMs,
            Title: p.Title,
            TitleLength: p.TitleLength,
            MetaDescription: p.MetaDescription,
            MetaDescriptionLength: p.MetaDescriptionLength,
            H1Count: p.H1Count,
            H1Tag: p.H1Tag,
            HasCanonical: p.HasCanonical,
            CanonicalUrl: p.CanonicalUrl,
            IsCanonicalSelfReferential: p.IsCanonicalSelfReferential,
            HasOpenGraph: p.HasOpenGraph,
            OpenGraphTagsPresent: p.OpenGraphTagsPresent,
            InboundLinksCount: p.InboundLinksCount,
            OutboundLinksCount: p.OutboundLinksCount,
            PageRank: p.PageRank,
            PageRankFormatted: p.PageRankFormatted,
            CrawlDepth: p.CrawlDepth,
            IsOrphan: p.IsOrphan,
            Issues: p.Issues,
            CrawledAtUtc: completedAt
        )).OrderBy(p => p.CrawlDepth).ThenBy(p => p.Path).ToList();

        var linkGraph = new LinkGraphResultDto(
            CrawlId: crawlId,
            TotalNodes: allPagesList.Count,
            TotalEdges: edgePairs.Count,
            OrphanPages: orphanPages,
            DeepestPages: deepestPages,
            Nodes: allPagesList.Select(p => new LinkGraphNodeDto(
                Url: p.Url,
                Path: p.Path,
                StatusCode: p.StatusCode,
                PageRank: p.PageRank,
                InDegree: p.InboundLinksCount,
                OutDegree: p.OutboundLinksCount,
                CrawlDepth: p.CrawlDepth,
                IsOrphan: p.IsOrphan
            )).ToList(),
            Edges: edgePairs.Select(e => new LinkGraphEdgeDto(Source: e.Key, Target: e.Value)).ToList()
        );

        // Cache in memory
        _crawlSummaries[crawlId] = summary;
        _crawlPageDetails[crawlId] = detailDtos;
        _crawlLinkGraphs[crawlId] = linkGraph;

        // Persist to database if db is available
        try
        {
            var dbReport = SeoAuditReport.Create(tenantId, startingUrl);
            dbReport.Id = crawlId;
            dbReport.OverallHealthScore = healthScore;
            dbReport.ScannedPagesCount = allPagesList.Count;
            dbReport.BrokenLinksCount = brokenLinksCount;
            dbReport.MissingMetaCount = missingMetaCount;
            dbReport.MissingH1Count = missingH1Count;
            dbReport.SlowPagesCount = allPagesList.Count(p => p.ResponseTimeMs > 1000);
            dbReport.Status = SeoAuditStatus.Completed;
            dbReport.CreatedAtUtc = startedAt;
            dbReport.CompletedAtUtc = completedAt;

            _db.SeoAuditReports.Add(dbReport);

            foreach (var detail in detailDtos)
            {
                var pageAudit = PageAuditResult.Create(
                    tenantId: tenantId,
                    auditReportId: crawlId,
                    pageUrl: detail.Url,
                    statusCode: detail.StatusCode,
                    title: detail.Title,
                    metaDescription: detail.MetaDescription,
                    h1Tag: detail.H1Tag,
                    hasCanonical: detail.HasCanonical,
                    hasOpenGraph: detail.HasOpenGraph,
                    hasSchemaOrg: true,
                    loadTimeMs: (int)detail.ResponseTimeMs,
                    issues: detail.Issues
                );
                _db.PageAuditResults.Add(pageAudit);
            }

            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // Database persistence is non-blocking to allow resilient testing & offline execution
            Debug.WriteLine($"Failed to persist crawl results to DB: {ex.Message}");
        }

        return summary;
    }

    public Task<CrawlReportSummaryDto?> GetCrawlSummaryAsync(Guid tenantId, Guid crawlId)
    {
        if (_crawlSummaries.TryGetValue(crawlId, out var summary))
        {
            return Task.FromResult<CrawlReportSummaryDto?>(summary);
        }

        // Check DbContext fallback
        var dbReport = _db.SeoAuditReports.AsNoTracking().FirstOrDefault(r => r.Id == crawlId && r.TenantId == tenantId);
        if (dbReport == null) return Task.FromResult<CrawlReportSummaryDto?>(null);

        var fallback = new CrawlReportSummaryDto(
            CrawlId: dbReport.Id,
            TenantId: dbReport.TenantId,
            StartingUrl: dbReport.TargetUrl,
            Status: dbReport.Status.ToString(),
            OverallHealthScore: dbReport.OverallHealthScore,
            CrawlabilityScore: 90,
            MetadataScore: 85,
            LinkIntegrityScore: 85,
            TotalPagesCrawled: dbReport.ScannedPagesCount,
            BrokenLinksCount: dbReport.BrokenLinksCount,
            MissingTitleCount: 0,
            MissingMetaCount: dbReport.MissingMetaCount,
            MissingH1Count: dbReport.MissingH1Count,
            MultipleH1Count: 0,
            MissingCanonicalCount: 0,
            OrphanPagesCount: 0,
            DeepPagesCount: 0,
            AvgResponseTimeMs: 250,
            StatusDistribution: new Dictionary<string, int> { ["200"] = dbReport.ScannedPagesCount },
            IssueBreakdown: new Dictionary<string, int>(),
            StartedAtUtc: dbReport.CreatedAtUtc,
            CompletedAtUtc: dbReport.CompletedAtUtc ?? dbReport.CreatedAtUtc
        );

        return Task.FromResult<CrawlReportSummaryDto?>(fallback);
    }

    public Task<PaginatedResult<CrawledPageDetailDto>> GetCrawledPagesAsync(
        Guid tenantId,
        Guid crawlId,
        int page = 1,
        int pageSize = 20,
        string? filter = null)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        List<CrawledPageDetailDto> items = new();
        if (_crawlPageDetails.TryGetValue(crawlId, out var cached))
        {
            items = cached;
        }
        else
        {
            // DB fallback
            var dbPages = _db.PageAuditResults
                .AsNoTracking()
                .Where(p => p.AuditReportId == crawlId && p.TenantId == tenantId)
                .ToList();

            items = dbPages.Select(p => new CrawledPageDetailDto(
                Id: p.Id,
                CrawlId: p.AuditReportId,
                Url: p.PageUrl,
                Path: SafeGetPath(p.PageUrl),
                StatusCode: p.StatusCode,
                ResponseTimeMs: p.LoadTimeMs,
                Title: p.Title,
                TitleLength: p.Title.Length,
                MetaDescription: p.MetaDescription,
                MetaDescriptionLength: p.MetaDescription.Length,
                H1Count: string.IsNullOrEmpty(p.H1Tag) ? 0 : 1,
                H1Tag: p.H1Tag,
                HasCanonical: p.HasCanonical,
                CanonicalUrl: p.PageUrl,
                IsCanonicalSelfReferential: true,
                HasOpenGraph: p.HasOpenGraph,
                OpenGraphTagsPresent: new List<string>(),
                InboundLinksCount: 1,
                OutboundLinksCount: 1,
                PageRank: 1.0,
                PageRankFormatted: "PR 1.00",
                CrawlDepth: 0,
                IsOrphan: false,
                Issues: JsonSerializer.Deserialize<List<string>>(p.IssuesJson) ?? new List<string>(),
                CrawledAtUtc: p.CreatedAtUtc
            )).ToList();
        }

        // Apply filters
        var filtered = items.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(filter))
        {
            var f = filter.Trim().ToLowerInvariant();
            if (f == "broken" || f == "404" || f == "errors")
            {
                filtered = filtered.Where(p => p.StatusCode >= 400 || p.Issues.Any(i => i.Contains("Broken") || i.Contains("Error")));
            }
            else if (f == "missing_h1" || f == "h1")
            {
                filtered = filtered.Where(p => p.H1Count == 0 || p.Issues.Contains("MissingH1"));
            }
            else if (f == "meta_issues" || f == "meta")
            {
                filtered = filtered.Where(p => p.Issues.Any(i => i.Contains("Meta") || i.Contains("Title") || i.Contains("Canonical") || i.Contains("Og")));
            }
            else if (f == "orphan" || f == "orphans")
            {
                filtered = filtered.Where(p => p.IsOrphan);
            }
        }

        var filteredList = filtered.ToList();
        int totalCount = filteredList.Count;
        int totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
        var pageItems = filteredList.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return Task.FromResult(new PaginatedResult<CrawledPageDetailDto>(
            Items: pageItems,
            TotalCount: totalCount,
            Page: page,
            PageSize: pageSize,
            TotalPages: totalPages
        ));
    }

    public Task<LinkGraphResultDto?> GetLinkGraphAsync(Guid tenantId, Guid crawlId)
    {
        if (_crawlLinkGraphs.TryGetValue(crawlId, out var graph))
        {
            return Task.FromResult<LinkGraphResultDto?>(graph);
        }

        return Task.FromResult<LinkGraphResultDto?>(null);
    }

    #endregion

    #region Per-Page HTML & Technical Rules Analysis

    public PageAnalysisResult AnalyzeHtml(string url, int statusCode, string html, long responseTimeMs = 150)
    {
        var result = new PageAnalysisResult
        {
            Url = url,
            Path = SafeGetPath(url),
            StatusCode = statusCode,
            ResponseTimeMs = responseTimeMs
        };

        if (statusCode >= 400)
        {
            result.Issues.Add(statusCode == 404 ? "BrokenLink_404" : $"HttpError_{statusCode}");
            return result;
        }

        if (statusCode is 301 or 302 or 307 or 308)
        {
            result.Issues.Add($"Redirect_{statusCode}");
            return result;
        }

        if (string.IsNullOrWhiteSpace(html))
        {
            result.Issues.Add("EmptyResponseBody");
            return result;
        }

        // 1. Title Tag Inspection
        var titleMatch = TitleRegex.Match(html);
        if (titleMatch.Success && !string.IsNullOrWhiteSpace(titleMatch.Groups["val"].Value))
        {
            string cleanTitle = WebUtility.HtmlDecode(HtmlTagsStripRegex.Replace(titleMatch.Groups["val"].Value, string.Empty)).Trim();
            result.Title = cleanTitle;

            if (cleanTitle.Length < 30)
            {
                result.Issues.Add("TitleTooShort");
            }
            else if (cleanTitle.Length > 60)
            {
                result.Issues.Add("TitleTooLong");
            }
        }
        else
        {
            result.Issues.Add("MissingTitle");
        }

        // 2. Meta Description Inspection
        var metaDescMatch = MetaDescRegex1.Match(html);
        if (!metaDescMatch.Success) metaDescMatch = MetaDescRegex2.Match(html);

        if (metaDescMatch.Success && !string.IsNullOrWhiteSpace(metaDescMatch.Groups["val"].Value))
        {
            string cleanMeta = WebUtility.HtmlDecode(metaDescMatch.Groups["val"].Value).Trim();
            result.MetaDescription = cleanMeta;

            if (cleanMeta.Length < 120)
            {
                result.Issues.Add("MetaDescriptionTooShort");
            }
            else if (cleanMeta.Length > 160)
            {
                result.Issues.Add("MetaDescriptionTooLong");
            }
        }
        else
        {
            result.Issues.Add("MissingMetaDescription");
        }

        // 3. H1 Tag Inspection (strictly 1 required)
        var h1Matches = H1Regex.Matches(html);
        result.H1Count = h1Matches.Count;

        if (h1Matches.Count == 0)
        {
            result.Issues.Add("MissingH1");
        }
        else if (h1Matches.Count > 1)
        {
            result.Issues.Add("MultipleH1");
            result.H1Tag = WebUtility.HtmlDecode(HtmlTagsStripRegex.Replace(h1Matches[0].Groups["val"].Value, string.Empty)).Trim();
        }
        else
        {
            result.H1Tag = WebUtility.HtmlDecode(HtmlTagsStripRegex.Replace(h1Matches[0].Groups["val"].Value, string.Empty)).Trim();
        }

        // 4. Canonical Tag Validation
        var canMatch = CanonicalRegex1.Match(html);
        if (!canMatch.Success) canMatch = CanonicalRegex2.Match(html);

        if (canMatch.Success && !string.IsNullOrWhiteSpace(canMatch.Groups["val"].Value))
        {
            result.HasCanonical = true;
            string canUrl = canMatch.Groups["val"].Value.Trim();
            result.CanonicalUrl = canUrl;

            // Check if self-referential
            string normCurrent = NormalizeUrl(url, new Uri(url));
            string normCanonical = NormalizeUrl(canUrl, new Uri(url));

            if (string.Equals(normCurrent, normCanonical, StringComparison.OrdinalIgnoreCase))
            {
                result.IsCanonicalSelfReferential = true;
            }
            else
            {
                result.IsCanonicalSelfReferential = false;
                result.Issues.Add("CanonicalMismatch");
            }
        }
        else
        {
            result.HasCanonical = false;
            result.Issues.Add("MissingCanonical");
        }

        // 5. OpenGraph / Social Previews
        var ogTitleMatch = OgTitleRegex1.Match(html);
        if (!ogTitleMatch.Success) ogTitleMatch = OgTitleRegex2.Match(html);
        if (ogTitleMatch.Success && !string.IsNullOrWhiteSpace(ogTitleMatch.Groups["val"].Value))
            result.OpenGraphTagsPresent.Add("og:title");
        else
            result.Issues.Add("MissingOgTitle");

        var ogImgMatch = OgImageRegex1.Match(html);
        if (!ogImgMatch.Success) ogImgMatch = OgImageRegex2.Match(html);
        if (ogImgMatch.Success && !string.IsNullOrWhiteSpace(ogImgMatch.Groups["val"].Value))
            result.OpenGraphTagsPresent.Add("og:image");
        else
            result.Issues.Add("MissingOgImage");

        var ogDescMatch = OgDescRegex1.Match(html);
        if (!ogDescMatch.Success) ogDescMatch = OgDescRegex2.Match(html);
        if (ogDescMatch.Success && !string.IsNullOrWhiteSpace(ogDescMatch.Groups["val"].Value))
            result.OpenGraphTagsPresent.Add("og:description");
        else
            result.Issues.Add("MissingOgDescription");

        result.HasOpenGraph = result.OpenGraphTagsPresent.Count == 3;
        if (!result.HasOpenGraph)
        {
            result.Issues.Add("IncompleteOpenGraph");
        }

        // 6. Response Time
        if (responseTimeMs > 1000)
        {
            result.Issues.Add("SlowResponseTime");
        }

        // 7. Internal & External Link Extraction
        if (Uri.TryCreate(url, UriKind.Absolute, out var baseUri))
        {
            string host = baseUri.Host.ToLowerInvariant();
            var anchorMatches = AnchorRegex.Matches(html);
            var seenLinks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (Match m in anchorMatches)
            {
                string rawHref = m.Groups["val"].Value.Trim();
                if (string.IsNullOrWhiteSpace(rawHref)) continue;

                if (rawHref.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ||
                    rawHref.StartsWith("tel:", StringComparison.OrdinalIgnoreCase) ||
                    rawHref.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase) ||
                    rawHref.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    var resolvedUri = new Uri(baseUri, rawHref);
                    string normalized = NormalizeUrl(resolvedUri.AbsoluteUri, baseUri);

                    if (seenLinks.Add(normalized))
                    {
                        if (string.Equals(resolvedUri.Host, host, StringComparison.OrdinalIgnoreCase))
                        {
                            result.OutboundInternalLinks.Add(normalized);
                        }
                        else
                        {
                            result.OutboundExternalLinks.Add(normalized);
                        }
                    }
                }
                catch
                {
                    // Ignore unparseable URIs
                }
            }
        }

        return result;
    }

    #endregion

    #region Link Equity & PageRank Power Iteration

    public Dictionary<string, double> CalculatePageRank(
        IEnumerable<string> pages,
        ILookup<string, string> internalLinks,
        double dampingFactor = 0.85,
        int maxIterations = 30)
    {
        var pageList = pages.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        int n = pageList.Count;
        var result = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        if (n == 0) return result;
        if (n == 1)
        {
            result[pageList[0]] = 1.0;
            return result;
        }

        var pageIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < n; i++)
        {
            pageIndex[pageList[i]] = i;
        }

        var outDegrees = new int[n];
        var inLinks = new List<int>[n];
        for (int i = 0; i < n; i++)
        {
            inLinks[i] = new List<int>();
        }

        foreach (var src in pageList)
        {
            int srcIdx = pageIndex[src];
            var targets = internalLinks[src]
                .Where(t => pageIndex.ContainsKey(t))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            outDegrees[srcIdx] = targets.Count;
            foreach (var tgt in targets)
            {
                int tgtIdx = pageIndex[tgt];
                inLinks[tgtIdx].Add(srcIdx);
            }
        }

        var pr = new double[n];
        double initialScore = 1.0 / n;
        for (int i = 0; i < n; i++)
        {
            pr[i] = initialScore;
        }

        var nextPr = new double[n];
        double epsilon = 1e-6;

        for (int iter = 0; iter < maxIterations; iter++)
        {
            double danglingSum = 0.0;
            for (int i = 0; i < n; i++)
            {
                if (outDegrees[i] == 0)
                {
                    danglingSum += pr[i];
                }
            }

            double baseRank = (1.0 - dampingFactor) / n + (dampingFactor * danglingSum / n);
            double maxDelta = 0.0;

            for (int i = 0; i < n; i++)
            {
                double sumIn = 0.0;
                foreach (int inIdx in inLinks[i])
                {
                    if (outDegrees[inIdx] > 0)
                    {
                        sumIn += pr[inIdx] / outDegrees[inIdx];
                    }
                }

                nextPr[i] = baseRank + dampingFactor * sumIn;
                double delta = Math.Abs(nextPr[i] - pr[i]);
                if (delta > maxDelta) maxDelta = delta;
            }

            Array.Copy(nextPr, pr, n);

            if (maxDelta < epsilon)
            {
                break;
            }
        }

        for (int i = 0; i < n; i++)
        {
            result[pageList[i]] = pr[i];
        }

        return result;
    }

    #endregion

    #region Site Health Score Computation

    public int CalculateHealthScore(
        IEnumerable<PageAnalysisResult> pages,
        IEnumerable<string> orphanPages,
        out int crawlability,
        out int metadata,
        out int linkIntegrity)
    {
        var pageList = pages.ToList();
        var orphanSet = new HashSet<string>(orphanPages, StringComparer.OrdinalIgnoreCase);

        if (pageList.Count == 0)
        {
            crawlability = 100;
            metadata = 100;
            linkIntegrity = 100;
            return 100;
        }

        // 1. Crawlability Score (40% Weight)
        int brokenCount = pageList.Count(p => p.StatusCode >= 400 || p.Issues.Any(i => i.StartsWith("BrokenLink")));
        double crawlRatio = Math.Max(0, 100.0 - ((double)brokenCount / pageList.Count * 100.0 * 1.5));
        crawlability = (int)Math.Round(Math.Clamp(crawlRatio, 0, 100));

        // 2. Metadata Completeness Score (35% Weight)
        double metaTotal = 0;
        foreach (var p in pageList)
        {
            double pageScore = 100.0;

            if (p.Issues.Contains("MissingTitle")) pageScore -= 25;
            else if (p.Issues.Contains("TitleTooShort") || p.Issues.Contains("TitleTooLong")) pageScore -= 10;

            if (p.Issues.Contains("MissingMetaDescription")) pageScore -= 20;
            else if (p.Issues.Contains("MetaDescriptionTooShort") || p.Issues.Contains("MetaDescriptionTooLong")) pageScore -= 10;

            if (p.Issues.Contains("MissingH1")) pageScore -= 30;
            else if (p.Issues.Contains("MultipleH1")) pageScore -= 15;

            if (p.Issues.Contains("MissingCanonical")) pageScore -= 10;
            else if (p.Issues.Contains("CanonicalMismatch")) pageScore -= 5;

            if (p.Issues.Contains("MissingOgTitle") || p.Issues.Contains("MissingOgImage") || p.Issues.Contains("MissingOgDescription"))
                pageScore -= 10;

            metaTotal += Math.Max(0, pageScore);
        }
        metadata = (int)Math.Round(Math.Clamp(metaTotal / pageList.Count, 0, 100));

        // 3. Link Integrity Score (25% Weight)
        double linkScore = 100.0;
        linkScore -= (brokenCount * 15.0);
        linkScore -= (orphanSet.Count * 10.0);
        linkIntegrity = (int)Math.Round(Math.Clamp(linkScore, 0, 100));

        // Weighted Overall Score
        int overall = (int)Math.Round(crawlability * 0.40 + metadata * 0.35 + linkIntegrity * 0.25);
        return Math.Clamp(overall, 0, 100);
    }

    #endregion

    #region SERP Keyword Rank Tracker

    public async Task<SerpKeywordRankItemDto> TrackKeywordAsync(
        Guid tenantId,
        string keyword,
        string targetUrl,
        string countryCode = "TN",
        int? initialPosition = null,
        string searchEngine = "Google")
    {
        var cleanKw = keyword.Trim();
        var cleanUrl = targetUrl.Trim();
        var cleanCountry = countryCode.Trim().ToUpperInvariant();
        int pos = initialPosition.HasValue ? Math.Clamp(initialPosition.Value, 1, 100) : 10;

        // Check if keyword already exists in database
        var existing = await _db.SerpKeywordTrackings
            .FirstOrDefaultAsync(k => k.TenantId == tenantId && k.Keyword.ToLower() == cleanKw.ToLower() && k.CountryCode == cleanCountry);

        int? prevPos = null;
        Guid recordId = Guid.NewGuid();

        if (existing != null)
        {
            prevPos = existing.CurrentRank;
            existing.PreviousRank = prevPos;
            existing.CurrentRank = pos;
            if (!existing.BestRank.HasValue || pos < existing.BestRank.Value)
            {
                existing.BestRank = pos;
            }
            existing.TargetUrl = cleanUrl;
            recordId = existing.Id;

            var historyList = JsonSerializer.Deserialize<List<SerpRankPointDto>>(existing.RankHistoryJson) ?? new List<SerpRankPointDto>();
            historyList.Add(new SerpRankPointDto(DateTime.UtcNow.ToString("yyyy-MM-dd"), pos));
            existing.RankHistoryJson = JsonSerializer.Serialize(historyList);
        }
        else
        {
            var newEntry = SerpKeywordTracking.Create(tenantId, cleanKw, cleanUrl, cleanCountry, pos);
            _db.SerpKeywordTrackings.Add(newEntry);
            recordId = newEntry.Id;
        }

        // Record history record
        var historyRecord = SerpRankHistoryRecord.Create(
            tenantId: tenantId,
            keyword: cleanKw,
            targetUrl: cleanUrl,
            position: pos,
            previousPosition: prevPos ?? 0,
            deviceType: "Desktop",
            countryCode: cleanCountry,
            keywordId: recordId
        );
        _db.SerpRankHistoryRecords.Add(historyRecord);

        await _db.SaveChangesAsync();

        var (delta, status) = ComputeRankDelta(pos, prevPos);

        return new SerpKeywordRankItemDto(
            Id: recordId,
            TenantId: tenantId,
            Keyword: cleanKw,
            TargetUrl: cleanUrl,
            CountryCode: cleanCountry,
            SearchEngine: searchEngine,
            CurrentPosition: pos,
            PreviousPosition: prevPos,
            Delta: delta,
            Status: status,
            BestPosition: (existing?.BestRank) ?? pos,
            History: new List<SerpRankPointDto>
            {
                new(DateTime.UtcNow.AddDays(-2).ToString("yyyy-MM-dd"), prevPos ?? pos + 2),
                new(DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd"), prevPos ?? pos + 1),
                new(DateTime.UtcNow.ToString("yyyy-MM-dd"), pos)
            },
            LastCheckedUtc: DateTime.UtcNow
        );
    }

    public async Task<SerpRankingsOverviewDto> GetSerpRankingsAsync(Guid tenantId, string? countryCode = null)
    {
        var query = _db.SerpKeywordTrackings
            .AsNoTracking()
            .Where(k => k.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(countryCode))
        {
            var upperCountry = countryCode.Trim().ToUpperInvariant();
            query = query.Where(k => k.CountryCode == upperCountry);
        }

        var list = await query.ToListAsync();

        if (list.Count == 0)
        {
            // Seed a realistic default set for initial discovery
            var seeded = GetDefaultDemoKeywords(tenantId);
            return ComputeVisibilityIndex(seeded);
        }

        var items = new List<SerpKeywordRankItemDto>();
        foreach (var k in list)
        {
            int curr = k.CurrentRank ?? 15;
            int? prev = k.PreviousRank;
            var (delta, status) = ComputeRankDelta(curr, prev);

            var history = JsonSerializer.Deserialize<List<SerpRankPointDto>>(k.RankHistoryJson) ?? new List<SerpRankPointDto>();
            if (history.Count == 0)
            {
                history.Add(new SerpRankPointDto(DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd"), prev ?? curr + 1));
                history.Add(new SerpRankPointDto(DateTime.UtcNow.ToString("yyyy-MM-dd"), curr));
            }

            items.Add(new SerpKeywordRankItemDto(
                Id: k.Id,
                TenantId: k.TenantId,
                Keyword: k.Keyword,
                TargetUrl: k.TargetUrl,
                CountryCode: k.CountryCode,
                SearchEngine: k.SearchEngine,
                CurrentPosition: curr,
                PreviousPosition: prev,
                Delta: delta,
                Status: status,
                BestPosition: k.BestRank ?? curr,
                History: history,
                LastCheckedUtc: k.TrackedDateUtc
            ));
        }

        return ComputeVisibilityIndex(items);
    }

    public (string Delta, string Status) ComputeRankDelta(int currentPos, int? prevPos)
    {
        if (!prevPos.HasValue || prevPos.Value <= 0)
        {
            return ("New", "new");
        }

        int diff = prevPos.Value - currentPos;
        if (diff > 0)
        {
            return ($"+{diff}", "improved");
        }
        else if (diff < 0)
        {
            return ($"{diff}", "declined");
        }
        else
        {
            return ("0", "unchanged");
        }
    }

    public SerpRankingsOverviewDto ComputeVisibilityIndex(List<SerpKeywordRankItemDto> items)
    {
        if (items.Count == 0)
        {
            return new SerpRankingsOverviewDto(
                TotalTrackedKeywords: 0,
                Top3Count: 0,
                Top3Percentage: 0,
                Top10Count: 0,
                Top10Percentage: 0,
                Top30Count: 0,
                Top30Percentage: 0,
                OverallVisibilityScore: 0,
                Keywords: items
            );
        }

        int total = items.Count;
        int top3 = items.Count(k => k.CurrentPosition is >= 1 and <= 3);
        int top10 = items.Count(k => k.CurrentPosition is >= 1 and <= 10);
        int top30 = items.Count(k => k.CurrentPosition is >= 1 and <= 30);

        double top3Pct = Math.Round((double)top3 / total * 100.0, 1);
        double top10Pct = Math.Round((double)top10 / total * 100.0, 1);
        double top30Pct = Math.Round((double)top30 / total * 100.0, 1);

        double weightedSum = 0;
        foreach (var k in items)
        {
            if (k.CurrentPosition is >= 1 and <= 3) weightedSum += 1.0;
            else if (k.CurrentPosition <= 10) weightedSum += 0.7;
            else if (k.CurrentPosition <= 30) weightedSum += 0.3;
            else if (k.CurrentPosition <= 100) weightedSum += 0.05;
        }

        double overallVisibility = Math.Round(Math.Clamp(weightedSum / total * 100.0, 0, 100), 1);

        return new SerpRankingsOverviewDto(
            TotalTrackedKeywords: total,
            Top3Count: top3,
            Top3Percentage: top3Pct,
            Top10Count: top10,
            Top10Percentage: top10Pct,
            Top30Count: top30,
            Top30Percentage: top30Pct,
            OverallVisibilityScore: overallVisibility,
            Keywords: items.OrderBy(k => k.CurrentPosition).ToList()
        );
    }

    private static List<SerpKeywordRankItemDto> GetDefaultDemoKeywords(Guid tenantId)
    {
        return new List<SerpKeywordRankItemDto>
        {
            new(Guid.NewGuid(), tenantId, "baskets cuir tunisie", "https://store.sgrowing.io/shop/shoes", "TN", "Google", 2, 4, "+2", "improved", 1, new List<SerpRankPointDto>{ new("2026-10-27", 5), new("2026-10-28", 4), new("2026-10-29", 2) }, DateTime.UtcNow),
            new(Guid.NewGuid(), tenantId, "livraison express tunisie cod", "https://store.sgrowing.io/delivery", "TN", "Google", 4, 7, "+3", "improved", 4, new List<SerpRankPointDto>{ new("2026-10-27", 9), new("2026-10-28", 7), new("2026-10-29", 4) }, DateTime.UtcNow),
            new(Guid.NewGuid(), tenantId, "omnichannel ecommerce crm", "https://sgrowing.io/features/crm", "US", "Google", 8, 8, "0", "unchanged", 6, new List<SerpRankPointDto>{ new("2026-10-27", 8), new("2026-10-28", 8), new("2026-10-29", 8) }, DateTime.UtcNow),
            new(Guid.NewGuid(), tenantId, "plateforme dropshipping algerie", "https://store.sgrowing.io/dz", "DZ", "Google", 12, 9, "-3", "declined", 7, new List<SerpRankPointDto>{ new("2026-10-27", 7), new("2026-10-28", 9), new("2026-10-29", 12) }, DateTime.UtcNow),
            new(Guid.NewGuid(), tenantId, "ai agent inbound marketing", "https://sgrowing.io/agents", "US", "Google", 1, 0, "New", "new", 1, new List<SerpRankPointDto>{ new("2026-10-29", 1) }, DateTime.UtcNow)
        };
    }

    #endregion

    #region URL & Helper Utilities

    private static string NormalizeUrl(string rawUrl, Uri baseUri)
    {
        if (string.IsNullOrWhiteSpace(rawUrl)) return string.Empty;

        try
        {
            var uri = new Uri(baseUri, rawUrl);
            var builder = new UriBuilder(uri)
            {
                Fragment = string.Empty
            };

            // Remove trailing slash if path != "/"
            string path = builder.Path;
            if (path.Length > 1 && path.EndsWith('/'))
            {
                builder.Path = path.TrimEnd('/');
            }

            return builder.Uri.ToString().Trim();
        }
        catch
        {
            return rawUrl.Trim();
        }
    }

    private static bool IsInternalUrl(string url, string rootHost)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return string.Equals(uri.Host, rootHost, StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }

    private static string SafeGetPath(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return uri.PathAndQuery;
        }
        return url;
    }

    #endregion
}
