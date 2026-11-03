using SGrowing.Api.Modules.AI.VectorRag;

namespace SGrowing.Tests.AI;

public class VectorRagTests
{
    private readonly VectorRagEngine _engine = new();

    [Fact]
    public void GetTotalIndexedChunks_ReturnsDefaultCorpusCount()
    {
        var count = _engine.GetTotalIndexedChunks();
        Assert.True(count >= 3);
    }

    [Fact]
    public void IngestDocumentChunk_IncreasesIndexCount()
    {
        var initial = _engine.GetTotalIndexedChunks();

        var chunk = _engine.IngestDocumentChunk(
            "doc_new_sla",
            "Disaster Recovery SLA",
            "Recovery Time Objective (RTO) is guaranteed under 15 minutes with cross-region active standby replica.",
            "SLA"
        );

        Assert.NotNull(chunk);
        Assert.StartsWith("chunk_", chunk.ChunkId);
        Assert.Equal(initial + 1, _engine.GetTotalIndexedChunks());
    }

    [Fact]
    public void SearchSimilar_FindsRelevantChunks()
    {
        var query = new SemanticSearchQuery(
            QueryText: "What is the enterprise pricing for autonomous AI SDR?",
            TopK: 3,
            MinScore: 0.10
        );

        var results = _engine.SearchSimilar(query);

        Assert.NotEmpty(results);
        Assert.Contains(results, r => r.Title.Contains("Pricing"));
        Assert.True(results[0].SimilarityScore > 0);
    }

    [Fact]
    public void GroundPrompt_ReturnsSynthesizedAnswerWithChunks()
    {
        var response = _engine.GroundPrompt("SOC2 compliance and air-gapped licensing");

        Assert.NotNull(response);
        Assert.Contains("Security", response.SynthesizedAnswer);
        Assert.True(response.LatencyMs < 100);
        Assert.NotEmpty(response.SourcedChunks);
    }

    [Fact]
    public void SearchSimilar_WithCategoryFilter_FiltersOutNonMatchingCategories()
    {
        var query = new SemanticSearchQuery(
            QueryText: "HubSpot and Salesforce sync connectors",
            TopK: 5,
            MinScore: 0.10,
            CategoryFilter: "Integrations"
        );

        var results = _engine.SearchSimilar(query);

        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.Equal("Integrations", r.Category));
    }

    [Fact]
    public void SearchSimilar_HighMinScoreThreshold_ReturnsEmptyWhenScoresAreBelow()
    {
        var query = new SemanticSearchQuery(
            QueryText: "Quantum teleportation physics",
            TopK: 5,
            MinScore: 0.999
        );

        var results = _engine.SearchSimilar(query);
        Assert.Empty(results);
    }

    [Fact]
    public void GroundPrompt_NoMatchingChunks_ReturnsFallbackNotice()
    {
        var response = _engine.GroundPrompt("unrelated text", category: "NonExistentCategory");

        Assert.NotNull(response);
        Assert.Equal(0, response.RetrievedChunksCount);
        Assert.Empty(response.SourcedChunks);
        Assert.Contains("No verified knowledge base chunks matched the query", response.SynthesizedAnswer);
    }

    [Fact]
    public void IngestDocumentChunk_PopulatesVectorAndMetadataCorrectly()
    {
        var chunk = _engine.IngestDocumentChunk(
            "doc_test_101",
            "Multi-Tenancy Architecture",
            "Schema-per-tenant isolation with PostgreSQL row-level security.",
            "Architecture"
        );

        Assert.Equal("doc_test_101", chunk.DocumentId);
        Assert.Equal("Multi-Tenancy Architecture", chunk.Title);
        Assert.Equal("Architecture", chunk.Category);
        Assert.NotNull(chunk.VectorEmbedding);
        Assert.Equal(64, chunk.VectorEmbedding.Length);
    }
}
