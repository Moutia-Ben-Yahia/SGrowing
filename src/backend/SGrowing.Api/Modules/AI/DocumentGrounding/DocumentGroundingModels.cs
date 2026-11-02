namespace SGrowing.Api.Modules.AI.DocumentGrounding;

public sealed record DocumentChunk(
    string ChunkId,
    string DocumentName,
    int PageNumber,
    string SectionTitle,
    string VerbatimContent
);

public sealed record SentenceCitationSpan(
    int SentenceIndex,
    string SentenceText,
    bool IsGrounded,
    double GroundingRatio,
    string? MatchedChunkId,
    string? CitationReference
);

public sealed record GroundedAnswerVerification(
    string VerificationId,
    string Question,
    string GeneratedAnswer,
    double OverallGroundingScorePercent,
    bool HallucinationRiskDetected,
    IReadOnlyList<SentenceCitationSpan> SentenceCitations,
    int TotalSentences,
    int GroundedSentencesCount,
    DateTime AuditedAtUtc
);

public sealed record AuditAnswerGroundingRequest(
    string Question,
    string CandidateAnswer,
    IReadOnlyList<DocumentChunk> RetrievedCorpusChunks
);
