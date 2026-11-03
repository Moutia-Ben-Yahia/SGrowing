using SGrowing.Api.Modules.Ai.KnowledgeGraph;
using Xunit;

namespace SGrowing.Tests.Ai;

public sealed class KnowledgeGraphTests
{
    private readonly KnowledgeGraphEngine _engine = new();

    [Fact]
    public void GetGraphSnapshot_ReturnsSeededEntitiesAndTriples()
    {
        var snapshot = _engine.GetGraphSnapshot();

        Assert.NotNull(snapshot);
        Assert.True(snapshot.TotalEntities >= 5);
        Assert.True(snapshot.TotalTriples >= 4);
        Assert.Contains(snapshot.Entities, e => e.Id == "ent_sgrowing_core");
    }

    [Fact]
    public void HybridSearch_FindsRelevantEntitiesAndConstructsSummary()
    {
        var query = new HybridSearchQuery("Voice SDR", MaxTriples: 5, MinConfidence: 0.80);
        var result = _engine.HybridSearch(query);

        Assert.NotNull(result);
        Assert.NotEmpty(result.MatchedEntities);
        Assert.Contains(result.MatchedEntities, e => e.Name.Contains("Voice SDR"));
        Assert.Contains("Grounded by", result.GroundedContextSummary);
    }

    [Fact]
    public void IngestKnowledge_AppendsEntityAndTripleCorrectly()
    {
        var req = new IngestKnowledgeRequest("PostgreSQL 17 Logical Replication provides sub-10ms disaster recovery cross-region.", "Doc_Tech_Stack");
        var res = _engine.IngestKnowledge(req);

        Assert.Equal("Success", res.Status);
        Assert.Equal(1, res.IngestedEntitiesCount);
        Assert.Equal(1, res.IngestedTriplesCount);

        var snapshot = _engine.GetGraphSnapshot();
        Assert.Contains(snapshot.Entities, e => e.Id == res.CreatedNodeIds[0]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void IngestKnowledge_EmptyOrWhitespace_ReturnsSkippedResponse(string? emptyText)
    {
        var req = new IngestKnowledgeRequest(emptyText!, "Doc_Empty");
        var res = _engine.IngestKnowledge(req);

        Assert.Equal("Skipped: Empty input", res.Status);
        Assert.Equal(0, res.IngestedEntitiesCount);
        Assert.Equal(0, res.IngestedTriplesCount);
        Assert.Empty(res.CreatedNodeIds);
    }

    [Fact]
    public void HybridSearch_UnmatchedQueryHighConfidence_ReturnsNoTriplesGroundedSummary()
    {
        var query = new HybridSearchQuery("QuantumEntanglementSuperconductivity", MaxTriples: 5, MinConfidence: 0.999);
        var result = _engine.HybridSearch(query);

        Assert.NotNull(result);
        Assert.Contains("No high-confidence graph triples matched the target query", result.GroundedContextSummary);
    }

    [Fact]
    public void HybridSearch_EmptyQuery_ReturnsTopCentralityEntities()
    {
        var query = new HybridSearchQuery("", MaxTriples: 3, MinConfidence: 0.5);
        var result = _engine.HybridSearch(query);

        Assert.NotNull(result);
        Assert.NotEmpty(result.MatchedEntities);
        Assert.True(result.MatchedEntities.Count <= 10);
    }
}
