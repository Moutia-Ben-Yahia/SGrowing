using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace SGrowing.Api.Modules.Search.Universal;

#region Domain Enums & Models

public enum SearchableEntityType
{
    Customer,
    TicketMessage,
    OrderFulfillment,
    ContractDocument,
    CrmDeal,
    KnowledgeArticle
}

public record SearchDocument(
    Guid DocumentId,
    Guid TenantId,
    SearchableEntityType EntityType,
    string Title,
    string Body,
    List<string>? Tags = null,
    string? UrlRoute = null,
    string? AuthorOrOwner = null,
    DateTime? CreatedAtUtc = null
);

public record SearchResultItem(
    Guid DocumentId,
    SearchableEntityType EntityType,
    string Title,
    string SnippetHighlight,
    string? UrlRoute,
    double RelevanceScore,
    string? AuthorOrOwner,
    DateTime CreatedAtUtc
);

public record UniversalSearchQuery(
    Guid TenantId,
    string QueryText,
    List<SearchableEntityType>? EntityFilter = null,
    int Limit = 20,
    bool EnableFuzzyMatching = true
);

public record UniversalSearchResponse(
    string QueryText,
    int TotalMatches,
    long ExecutionTimeMs,
    List<SearchResultItem> Results,
    Dictionary<SearchableEntityType, int> Facets
);

#endregion

public interface IUniversalSearchEngineService
{
    Task IndexDocumentAsync(SearchDocument document, CancellationToken ct = default);
    Task IndexDocumentsBatchAsync(List<SearchDocument> documents, CancellationToken ct = default);
    Task<UniversalSearchResponse> SearchAsync(UniversalSearchQuery query, CancellationToken ct = default);
    Task<int> GetDocumentCountAsync(Guid tenantId, CancellationToken ct = default);
}

public class UniversalSearchEngineService : IUniversalSearchEngineService
{
    // TenantId -> (DocId -> SearchDocument)
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, SearchDocument>> _documents = new();

    // BM25 parameters
    private const double K1 = 1.2;
    private const double B = 0.75;

    public Task IndexDocumentAsync(SearchDocument document, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        var tenantDocs = _documents.GetOrAdd(document.TenantId, _ => new ConcurrentDictionary<Guid, SearchDocument>());
        tenantDocs[document.DocumentId] = document;

        return Task.CompletedTask;
    }

    public Task IndexDocumentsBatchAsync(List<SearchDocument> documents, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(documents);

        foreach (var doc in documents)
        {
            var tenantDocs = _documents.GetOrAdd(doc.TenantId, _ => new ConcurrentDictionary<Guid, SearchDocument>());
            tenantDocs[doc.DocumentId] = doc;
        }

        return Task.CompletedTask;
    }

    public Task<UniversalSearchResponse> SearchAsync(UniversalSearchQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var stopwatch = Stopwatch.StartNew();
        var rawTerms = Tokenize(query.QueryText);

        if (rawTerms.Count == 0 || !_documents.TryGetValue(query.TenantId, out var tenantDocs) || tenantDocs.IsEmpty)
        {
            stopwatch.Stop();
            return Task.FromResult(new UniversalSearchResponse(
                query.QueryText,
                0,
                stopwatch.ElapsedMilliseconds,
                new List<SearchResultItem>(),
                new Dictionary<SearchableEntityType, int>()
            ));
        }

        // Apply Entity Filtering
        var filteredDocs = tenantDocs.Values
            .Where(d => query.EntityFilter == null || query.EntityFilter.Count == 0 || query.EntityFilter.Contains(d.EntityType))
            .ToList();

        if (filteredDocs.Count == 0)
        {
            stopwatch.Stop();
            return Task.FromResult(new UniversalSearchResponse(
                query.QueryText,
                0,
                stopwatch.ElapsedMilliseconds,
                new List<SearchResultItem>(),
                new Dictionary<SearchableEntityType, int>()
            ));
        }

        int N = filteredDocs.Count;
        double avgDocLength = filteredDocs.Average(d => GetDocumentLength(d));

        // Score each document with BM25
        var scoredList = new List<(SearchDocument Doc, double Score, string Highlight)>();

        foreach (var doc in filteredDocs)
        {
            double docScore = 0.0;
            string bestSnippet = string.Empty;
            int matchedTerms = 0;

            var titleTokens = Tokenize(doc.Title);
            var bodyTokens = Tokenize(doc.Body);
            var tagTokens = doc.Tags?.SelectMany(Tokenize).ToList() ?? new List<string>();

            int docLength = titleTokens.Count + bodyTokens.Count + tagTokens.Count;

            foreach (var term in rawTerms)
            {
                // Term frequency with field weights
                int tfTitle = CountMatches(titleTokens, term, query.EnableFuzzyMatching);
                int tfBody = CountMatches(bodyTokens, term, query.EnableFuzzyMatching);
                int tfTags = CountMatches(tagTokens, term, query.EnableFuzzyMatching);

                double weightedTf = (tfTitle * 3.0) + (tfTags * 2.0) + (tfBody * 1.0);

                if (weightedTf > 0)
                {
                    matchedTerms++;

                    // Document frequency: how many docs in N contain this term
                    int df = filteredDocs.Count(d =>
                        Tokenize(d.Title).Contains(term) ||
                        Tokenize(d.Body).Contains(term) ||
                        (d.Tags != null && d.Tags.Any(t => Tokenize(t).Contains(term))));

                    double idf = Math.Log(((N - df + 0.5) / (df + 0.5)) + 1.0);
                    idf = Math.Max(0.1, idf);

                    double numerator = weightedTf * (K1 + 1.0);
                    double denominator = weightedTf + (K1 * (1.0 - B + (B * (docLength / Math.Max(1.0, avgDocLength)))));

                    docScore += idf * (numerator / denominator);

                    if (string.IsNullOrEmpty(bestSnippet))
                    {
                        bestSnippet = GenerateSnippet(doc.Body, term);
                    }
                }
            }

            if (matchedTerms > 0 && docScore > 0)
            {
                if (string.IsNullOrEmpty(bestSnippet))
                {
                    bestSnippet = doc.Body.Length > 120 ? doc.Body[..120] + "..." : doc.Body;
                }
                scoredList.Add((doc, docScore, bestSnippet));
            }
        }

        // Facets calculation
        var facets = scoredList
            .GroupBy(s => s.Doc.EntityType)
            .ToDictionary(g => g.Key, g => g.Count());

        var topResults = scoredList
            .OrderByDescending(s => s.Score)
            .Take(query.Limit)
            .Select(s => new SearchResultItem(
                s.Doc.DocumentId,
                s.Doc.EntityType,
                s.Doc.Title,
                s.Highlight,
                s.Doc.UrlRoute,
                Math.Round(s.Score, 3),
                s.Doc.AuthorOrOwner,
                s.Doc.CreatedAtUtc ?? DateTime.UtcNow
            ))
            .ToList();

        stopwatch.Stop();

        return Task.FromResult(new UniversalSearchResponse(
            query.QueryText,
            scoredList.Count,
            stopwatch.ElapsedMilliseconds,
            topResults,
            facets
        ));
    }

    public Task<int> GetDocumentCountAsync(Guid tenantId, CancellationToken ct = default)
    {
        if (_documents.TryGetValue(tenantId, out var tenantDocs))
        {
            return Task.FromResult(tenantDocs.Count);
        }
        return Task.FromResult(0);
    }

    private static List<string> Tokenize(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return new List<string>();
        return Regex.Matches(text.ToLowerInvariant(), @"\b[a-z0-9_\-]{2,}\b")
            .Select(m => m.Value)
            .ToList();
    }

    private static int GetDocumentLength(SearchDocument doc)
    {
        return Tokenize(doc.Title).Count + Tokenize(doc.Body).Count + (doc.Tags?.SelectMany(Tokenize).Count() ?? 0);
    }

    private static int CountMatches(List<string> tokens, string term, bool fuzzy)
    {
        if (!fuzzy)
        {
            return tokens.Count(t => t == term);
        }

        return tokens.Count(t => t == term || t.StartsWith(term) || (term.Length >= 4 && LevenshteinDistance(t, term) <= 1));
    }

    private static int LevenshteinDistance(string s, string t)
    {
        if (string.IsNullOrEmpty(s)) return t?.Length ?? 0;
        if (string.IsNullOrEmpty(t)) return s.Length;

        int[,] d = new int[s.Length + 1, t.Length + 1];

        for (int i = 0; i <= s.Length; i++) d[i, 0] = i;
        for (int j = 0; j <= t.Length; j++) d[0, j] = j;

        for (int j = 1; j <= t.Length; j++)
        {
            for (int i = 1; i <= s.Length; i++)
            {
                int cost = s[i - 1] == t[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }

        return d[s.Length, t.Length];
    }

    private static string GenerateSnippet(string body, string term)
    {
        if (string.IsNullOrEmpty(body)) return string.Empty;

        int index = body.IndexOf(term, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return body.Length > 120 ? body[..120] + "..." : body;
        }

        int start = Math.Max(0, index - 40);
        int length = Math.Min(body.Length - start, 120);
        string snippet = body.Substring(start, length);

        if (start > 0) snippet = "..." + snippet;
        if (start + length < body.Length) snippet += "...";

        // Inject highlight
        return Regex.Replace(snippet, Regex.Escape(term), $"<mark>$0</mark>", RegexOptions.IgnoreCase);
    }
}
