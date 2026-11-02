using System.Text.RegularExpressions;

namespace SGrowing.Api.Modules.AI.DocumentGrounding;

/// <summary>
/// Autonomous document knowledge grounding and citation verification engine.
/// Evaluates verbatim token overlap to prevent hallucination in enterprise contract/tender RAG queries.
/// </summary>
public sealed class DocumentGroundingCitationEngine
{
    private const double MinimumSentenceGroundingThreshold = 0.65; // 65% token overlap required
    private const double StrictHallucinationSafetyThreshold = 80.0; // 80% overall score required

    public GroundedAnswerVerification VerifyAnswerGrounding(AuditAnswerGroundingRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var answer = request.CandidateAnswer.Trim();
        var rawSentences = Regex.Split(answer, @"(?<=[.!?])\s+")
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();

        if (rawSentences.Count == 0 && !string.IsNullOrWhiteSpace(answer))
        {
            rawSentences.Add(answer);
        }

        // Pre-tokenize all retrieved corpus chunks
        var preprocessedChunks = request.RetrievedCorpusChunks
            .Select(c => new PreprocessedChunk(c, TokenizeWords(c.VerbatimContent)))
            .ToList();

        var citationSpans = new List<SentenceCitationSpan>();
        var groundedCount = 0;
        var totalTokens = 0;
        var groundedTokens = 0;

        for (var i = 0; i < rawSentences.Count; i++)
        {
            var sentence = rawSentences[i].Trim();
            var tokens = TokenizeWords(sentence);
            totalTokens += tokens.Count;

            if (tokens.Count == 0) continue;

            var bestRatio = 0.0;
            PreprocessedChunk? bestChunk = null;

            foreach (var chunk in preprocessedChunks)
            {
                var overlap = tokens.Count(t => chunk.Words.Contains(t));
                var ratio = (double)overlap / tokens.Count;

                if (ratio > bestRatio)
                {
                    bestRatio = ratio;
                    bestChunk = chunk;
                }
            }

            var isGrounded = bestRatio >= MinimumSentenceGroundingThreshold;
            if (isGrounded)
            {
                groundedCount++;
                groundedTokens += (int)(tokens.Count * bestRatio);
            }

            string? citation = isGrounded && bestChunk != null
                ? $"[{bestChunk.Chunk.DocumentName}, p. {bestChunk.Chunk.PageNumber} § {bestChunk.Chunk.SectionTitle}]"
                : null;

            citationSpans.Add(new SentenceCitationSpan(
                SentenceIndex: i,
                SentenceText: sentence,
                IsGrounded: isGrounded,
                GroundingRatio: Math.Round(bestRatio, 3),
                MatchedChunkId: isGrounded ? bestChunk?.Chunk.ChunkId : null,
                CitationReference: citation
            ));
        }

        var overallScore = totalTokens > 0
            ? ((double)groundedTokens / totalTokens) * 100.0
            : 0.0;

        var hallucinationDetected = overallScore < StrictHallucinationSafetyThreshold;

        return new GroundedAnswerVerification(
            VerificationId: $"GRD-{Guid.NewGuid():N}"[..10].ToUpperInvariant(),
            Question: request.Question,
            GeneratedAnswer: request.CandidateAnswer,
            OverallGroundingScorePercent: Math.Round(overallScore, 1),
            HallucinationRiskDetected: hallucinationDetected,
            SentenceCitations: citationSpans,
            TotalSentences: rawSentences.Count,
            GroundedSentencesCount: groundedCount,
            AuditedAtUtc: DateTime.UtcNow
        );
    }

    private static HashSet<string> TokenizeWords(string text)
    {
        return Regex.Matches(text.ToLowerInvariant(), @"\b[a-z0-9_-]{2,}\b")
            .Select(m => m.Value)
            .ToHashSet();
    }

    private sealed record PreprocessedChunk(DocumentChunk Chunk, HashSet<string> Words);
}
