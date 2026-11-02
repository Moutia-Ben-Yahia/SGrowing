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
}
