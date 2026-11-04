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

    [Fact]
    public void GetGraphSnapshot_EntitiesOrderedByCentralityScoreDescending()
    {
        var snapshot = _engine.GetGraphSnapshot();

        Assert.NotEmpty(snapshot.Entities);
        for (var i = 0; i < snapshot.Entities.Count - 1; i++)
        {
            Assert.True(snapshot.Entities[i].CentralityScore >= snapshot.Entities[i + 1].CentralityScore);
        }
    }

    [Fact]
    public void GetGraphSnapshot_TriplesOrderedByConfidenceDescending()
    {
        var snapshot = _engine.GetGraphSnapshot();

        Assert.NotEmpty(snapshot.Triples);
        for (var i = 0; i < snapshot.Triples.Count - 1; i++)
        {
            Assert.True(snapshot.Triples[i].Confidence >= snapshot.Triples[i + 1].Confidence);
        }
    }

    [Fact]
    public void IngestKnowledge_LongTextOverFortyCharacters_TruncatesTitleWithEllipsis()
    {
        const string longText = "Enterprise multi-tenant database partitioning ensures cryptographic isolation across tenants.";
        var req = new IngestKnowledgeRequest(longText, "Doc_Partitioning");
        var res = _engine.IngestKnowledge(req);

        Assert.Equal("Success", res.Status);
        Assert.Single(res.CreatedNodeIds);

        var snapshot = _engine.GetGraphSnapshot();
        var entity = snapshot.Entities.First(e => e.Id == res.CreatedNodeIds[0]);

        Assert.EndsWith("...", entity.Name);
        Assert.Equal(43, entity.Name.Length); // 40 chars + "..."
        Assert.Equal(longText, entity.Description);
        Assert.Equal("Doc_Partitioning", entity.Properties["source"]);
    }

    [Fact]
    public void HybridSearch_NullQueryText_HandledSafelyWithoutNullReferenceException()
    {
        var query = new HybridSearchQuery(QueryText: null!, MaxTriples: 5, MinConfidence: 0.5);
        var result = _engine.HybridSearch(query);

        Assert.NotNull(result);
        Assert.Equal(string.Empty, result.QueryText);
        Assert.NotEmpty(result.MatchedEntities);
    }

    [Fact]
    public void HybridSearch_MinConfidenceFilter_ExcludesLowerConfidenceTriples()
    {
        // Require extremely high confidence (0.999) which seeded triples (max 0.95) do not satisfy
        var query = new HybridSearchQuery("Autonomous", MaxTriples: 10, MinConfidence: 0.999);
        var result = _engine.HybridSearch(query);

        Assert.NotNull(result);
        Assert.Empty(result.MatchedTriples);
    }
}
