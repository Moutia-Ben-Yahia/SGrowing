namespace SGrowing.Api.Modules.AI.VectorRag;

public record VectorChunk(
    string ChunkId,
    string DocumentId,
    string Title,
    string Content,
    float[] VectorEmbedding,
    string Category,
    DateTime IngestedAtUtc
);

public record SemanticSearchResult(
    string ChunkId,
    string DocumentId,
    string Title,
    string Content,
    string Category,
    double SimilarityScore,
    double CosineDistance
);

public record SemanticSearchQuery(
    string QueryText,
    int TopK = 3,
    double MinScore = 0.70,
    string? CategoryFilter = null
);

public record IngestDocumentRequest(
    string Title,
    string Content,
    string Category
);

public record RagGroundedResponse(
    string Query,
    string SynthesizedAnswer,
    List<SemanticSearchResult> SourcedChunks,
    int RetrievedChunksCount,
    int TotalTokensUsed,
    int LatencyMs
);
