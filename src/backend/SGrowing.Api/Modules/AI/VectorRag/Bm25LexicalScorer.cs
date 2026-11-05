using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace SGrowing.Api.Modules.AI.VectorRag;

public record Bm25Document(
    string Id,
    string Title,
    string Content,
    string Category
);

public record Bm25ScoredResult(
    string DocumentId,
    string Title,
    string Content,
    string Category,
    double Bm25Score
);

public class Bm25LexicalScorer
{
    private readonly double _k1;
    private readonly double _b;
    private static readonly Regex TokenRegex = new(@"\b[a-zA-Z0-9_\-\$]+\b", RegexOptions.Compiled);

    public Bm25LexicalScorer(double k1 = 1.2, double b = 0.75)
    {
        _k1 = k1;
        _b = b;
    }

    public List<string> Tokenize(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return new List<string>();
        return TokenRegex.Matches(text.ToLowerInvariant())
            .Select(m => m.Value)
            .Where(w => w.Length > 1)
            .ToList();
    }

    public IReadOnlyList<Bm25ScoredResult> ScoreCorpus(
        IReadOnlyList<Bm25Document> corpus,
        string query,
        int topK = 10)
    {
        if (corpus.Count == 0 || string.IsNullOrWhiteSpace(query))
            return Array.Empty<Bm25ScoredResult>();

        var queryTokens = Tokenize(query);
        if (queryTokens.Count == 0)
            return Array.Empty<Bm25ScoredResult>();

        int totalDocs = corpus.Count;
        var tokenizedDocs = corpus.Select(d => (Doc: d, Tokens: Tokenize(d.Content + " " + d.Title))).ToList();
        double avgDocLength = tokenizedDocs.Average(d => d.Tokens.Count);
        if (avgDocLength == 0) avgDocLength = 1.0;

        // Calculate Document Frequency for each query token
        var docFreq = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var qToken in queryTokens.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            docFreq[qToken] = tokenizedDocs.Count(d => d.Tokens.Contains(qToken, StringComparer.OrdinalIgnoreCase));
        }

        var results = new List<Bm25ScoredResult>();

        foreach (var (doc, tokens) in tokenizedDocs)
        {
            double docScore = 0.0;
            int docLength = tokens.Count;

            var termFreqs = tokens
                .GroupBy(t => t, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

            foreach (var qToken in queryTokens)
            {
                if (!termFreqs.TryGetValue(qToken, out var tf)) continue;
                int df = docFreq.GetValueOrDefault(qToken, 0);

                // Probabilistic Robertson-Spärck Jones IDF
                double idf = Math.Log(1.0 + (totalDocs - df + 0.5) / (df + 0.5));
                if (idf < 0) idf = 0.01;

                double numerator = tf * (_k1 + 1.0);
                double denominator = tf + _k1 * (1.0 - _b + _b * (docLength / avgDocLength));

                docScore += idf * (numerator / denominator);
            }

            if (docScore > 0.0)
            {
                results.Add(new Bm25ScoredResult(
                    DocumentId: doc.Id,
                    Title: doc.Title,
                    Content: doc.Content,
                    Category: doc.Category,
                    Bm25Score: Math.Round(docScore, 4)));
            }
        }

        return results
            .OrderByDescending(r => r.Bm25Score)
            .Take(topK)
            .ToList();
    }
}
