using FluentAssertions;
using SGrowing.Api.Modules.KnowledgeBase;
using SGrowing.Api.Modules.KnowledgeBase.Autopilot;
using Xunit;

namespace SGrowing.Tests.KnowledgeBase;

public class HybridRagAutopilotServiceTests
{
    private readonly HybridRagAutopilotService _service = new();

    [Fact]
    public void GenerateDenseEmbedding_ProducesDeterministicOutput()
    {
        // Arrange
        const string text = "Cash on delivery courier remittance SLA guidelines";

        // Act
        var vec1 = _service.GenerateDenseEmbedding(text);
        var vec2 = _service.GenerateDenseEmbedding(text);

        // Assert
        vec1.Should().NotBeNull();
        vec1.Length.Should().Be(64);
        vec1.Should().Equal(vec2);
    }

    [Fact]
    public void GenerateDenseEmbedding_ProducesL2NormalizedVector()
    {
        // Arrange
        const string text = "30-day return policy and defective unit drop and swap replacement protocol";

        // Act
        var vec = _service.GenerateDenseEmbedding(text);

        // Assert
        double magSq = vec.Sum(x => (double)x * x);
        magSq.Should().BeInRange(0.98, 1.02);
    }

    [Fact]
    public void GenerateDenseEmbedding_HandlesEmptyOrWhitespace()
    {
        // Act
        var vecEmpty = _service.GenerateDenseEmbedding("");
        var vecWhitespace = _service.GenerateDenseEmbedding("   ");

        // Assert
        vecEmpty.Should().AllBeEquivalentTo(0f);
        vecWhitespace.Should().AllBeEquivalentTo(0f);
    }

    [Fact]
    public void ComputeCosineSimilarity_IdenticalVectors_ReturnsOne()
    {
        // Arrange
        var v1 = new float[] { 0.6f, 0.8f, 0.0f };
        var v2 = new float[] { 0.6f, 0.8f, 0.0f };

        // Act
        var sim = _service.ComputeCosineSimilarity(v1, v2);

        // Assert
        sim.Should().BeApproximately(1.0, 0.0001);
    }

    [Fact]
    public void ComputeCosineSimilarity_OrthogonalVectors_ReturnsZero()
    {
        // Arrange
        var v1 = new float[] { 1.0f, 0.0f, 0.0f };
        var v2 = new float[] { 0.0f, 1.0f, 0.0f };

        // Act
        var sim = _service.ComputeCosineSimilarity(v1, v2);

        // Assert
        sim.Should().BeApproximately(0.0, 0.0001);
    }

    [Fact]
    public void ComputeCosineSimilarity_OppositeVectors_ReturnsNegativeOne()
    {
        // Arrange
        var v1 = new float[] { 0.0f, 1.0f, 0.0f };
        var v2 = new float[] { 0.0f, -1.0f, 0.0f };

        // Act
        var sim = _service.ComputeCosineSimilarity(v1, v2);

        // Assert
        sim.Should().BeApproximately(-1.0, 0.0001);
    }

    [Fact]
    public void ComputeCosineSimilarity_HandlesEmptyOrNullGracefully()
    {
        // Arrange
        var valid = new float[] { 1.0f, 0.0f };

        // Act & Assert
        _service.ComputeCosineSimilarity(null!, valid).Should().Be(0.0);
        _service.ComputeCosineSimilarity(valid, null!).Should().Be(0.0);
        _service.ComputeCosineSimilarity(Array.Empty<float>(), valid).Should().Be(0.0);
        _service.ComputeCosineSimilarity(new float[] { 0f, 0f }, valid).Should().Be(0.0);
    }

    [Fact]
    public void ComputeBm25Score_ExactKeywordMatch_ReturnsPositiveScore()
    {
        // Arrange
        var chunk1 = new DocumentChunk(Guid.NewGuid(), Guid.NewGuid(), 0, "damaged unit courier swap replacement", new float[64], new[] { "damaged", "unit", "courier", "swap", "replacement" });
        var chunk2 = new DocumentChunk(Guid.NewGuid(), Guid.NewGuid(), 1, "customs vat tariff import documents", new float[64], new[] { "customs", "vat", "tariff", "import", "documents" });
        var allChunks = new List<DocumentChunk> { chunk1, chunk2 };

        // Act
        var score1 = _service.ComputeBm25Score(new[] { "damaged", "swap" }, chunk1, allChunks);
        var score2 = _service.ComputeBm25Score(new[] { "damaged", "swap" }, chunk2, allChunks);

        // Assert
        score1.Should().BeGreaterThan(0.0);
        score2.Should().Be(0.0);
    }

    [Fact]
    public void ComputeBm25Score_UnrelatedQuery_ReturnsZero()
    {
        // Arrange
        var chunk = new DocumentChunk(Guid.NewGuid(), Guid.NewGuid(), 0, "annual subscription cancellation policy", new float[64], new[] { "annual", "subscription", "cancellation", "policy" });
        var allChunks = new List<DocumentChunk> { chunk };

        // Act
        var score = _service.ComputeBm25Score(new[] { "unrelated", "extraneous", "keywords" }, chunk, allChunks);

        // Assert
        score.Should().Be(0.0);
    }

    [Fact]
    public void ComputeBm25Score_ExhibitsTermFrequencySaturation()
    {
        // Arrange: Test that doubling term frequency does not double the score (concave sublinear saturation)
        // With k1 = 1.2:
        // f = 1 -> tf = (1 * 2.2) / (1 + 1.2) = 2.2 / 2.2 = 1.0
        // f = 10 -> tf = (10 * 2.2) / (10 + 1.2) = 22 / 11.2 ≈ 1.964 (less than 2x the f=1 score despite 10x occurrences)
        var chunkLowTf = new DocumentChunk(Guid.NewGuid(), Guid.NewGuid(), 0, "refund refund refund", new float[64], new[] { "refund", "standard", "policy", "window" });
        var chunkHighTf = new DocumentChunk(Guid.NewGuid(), Guid.NewGuid(), 1, "refund refund refund refund refund refund", new float[64], new[] { "refund", "refund", "refund", "refund", "refund", "refund", "standard", "policy" });
        var allChunks = new List<DocumentChunk> { chunkLowTf, chunkHighTf };

        // Act
        var scoreLow = _service.ComputeBm25Score(new[] { "refund" }, chunkLowTf, allChunks);
        var scoreHigh = _service.ComputeBm25Score(new[] { "refund" }, chunkHighTf, allChunks);

        // Assert
        scoreHigh.Should().BeGreaterThan(scoreLow);
        // Diminishing returns: 6x occurrences should NOT produce 6x score
        (scoreHigh / scoreLow).Should().BeLessThan(3.0);
    }

    [Fact]
    public void PerformHybridRetrieval_CombinesDenseAndSparseWithRrf()
    {
        // Arrange
        var chunks = new List<DocumentChunk>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), 0, "COD courier payout remittance SLA occurs within T+2 banking days.", _service.GenerateDenseEmbedding("COD courier payout remittance SLA occurs within T+2 banking days."), new[] { "cod", "courier", "payout", "remittance", "sla", "banking", "days" }),
            new(Guid.NewGuid(), Guid.NewGuid(), 1, "Customs duties and import clearance DDP delivery handling.", _service.GenerateDenseEmbedding("Customs duties and import clearance DDP delivery handling."), new[] { "customs", "duties", "import", "clearance", "ddp", "delivery" }),
            new(Guid.NewGuid(), Guid.NewGuid(), 2, "Instant drop-and-swap courier replacement for defective products.", _service.GenerateDenseEmbedding("Instant drop-and-swap courier replacement for defective products."), new[] { "instant", "drop", "swap", "courier", "replacement", "defective" })
        };

        // Act
        var results = _service.PerformHybridRetrieval("When will my COD remittance payout arrive?", chunks, topK: 2);

        // Assert
        results.Should().NotBeEmpty();
        results.Count.Should().Be(2);
        results[0].Chunk.Text.Should().Contain("COD courier payout remittance");
        results[0].RrfScore.Should().BeGreaterThan(0.0);
        results[0].DenseScore.Should().BeGreaterThan(0.0);
    }

    [Fact]
    public void PerformHybridRetrieval_HonorsTopKParameter()
    {
        // Arrange
        var chunks = Enumerable.Range(0, 10).Select(i =>
            new DocumentChunk(Guid.NewGuid(), Guid.NewGuid(), i, $"Knowledge chunk sample {i} for logistics", _service.GenerateDenseEmbedding($"Knowledge chunk sample {i} for logistics"), new[] { "knowledge", "chunk", "sample", "logistics" })
        ).ToList();

        // Act
        var resultsTop3 = _service.PerformHybridRetrieval("logistics chunk", chunks, topK: 3);
        var resultsTop5 = _service.PerformHybridRetrieval("logistics chunk", chunks, topK: 5);

        // Assert
        resultsTop3.Count.Should().Be(3);
        resultsTop5.Count.Should().Be(5);
    }

    [Fact]
    public void ChunkDocument_SlidingWindow_CreatesExpectedChunksAndOverlap()
    {
        // Arrange: 10 words, windowWords 4, overlapWords 2 -> step = 2
        // Chunk 0: words 0..3
        // Chunk 1: words 2..5 (overlap words 2..3)
        // Chunk 2: words 4..7 (overlap words 4..5)
        // Chunk 3: words 6..9 (overlap words 6..7)
        // Chunk 4: words 8..9
        const string text = "word0 word1 word2 word3 word4 word5 word6 word7 word8 word9";
        var docId = Guid.NewGuid();

        // Act
        var chunks = _service.ChunkDocument(docId, text, windowWords: 4, overlapWords: 2);

        // Assert
        chunks.Should().NotBeEmpty();
        chunks.Count.Should().BeGreaterThanOrEqualTo(4);
        chunks[0].ChunkIndex.Should().Be(0);
        chunks[1].ChunkIndex.Should().Be(1);

        // Check overlap between Chunk 0 and Chunk 1
        chunks[0].Text.Should().Contain("word2 word3");
        chunks[1].Text.Should().Contain("word2 word3");
    }

    [Fact]
    public void ChunkDocument_EmptyOrWhitespace_ReturnsEmptyList()
    {
        // Act
        var c1 = _service.ChunkDocument(Guid.NewGuid(), "");
        var c2 = _service.ChunkDocument(Guid.NewGuid(), "    ");

        // Assert
        c1.Should().BeEmpty();
        c2.Should().BeEmpty();
    }

    [Fact]
    public async Task EvaluateTicket_HighConfidence_ReturnsAutoResolved()
    {
        // Arrange: Question matching pre-seeded "Damaged Item Drop-and-Swap Replacement"
        const string question = "I received a damaged defective item, how does drop and swap work?";

        // Act
        var resolution = await _service.EvaluateTicketAsync("TCK-TEST-001", question, threshold: 0.88);

        // Assert
        resolution.Should().NotBeNull();
        resolution.TicketId.Should().Be("TCK-TEST-001");
        resolution.Confidence.Should().BeGreaterThanOrEqualTo(0.88);
        resolution.ActionTaken.Should().Be("AutoResolved");
        resolution.ResolutionText.Should().Contain("[Damaged Item Drop-and-Swap Replacement:¶");
        resolution.AppliedCitations.Should().NotBeEmpty();
        resolution.AppliedCitations[0].DocumentTitle.Should().Be("Damaged Item Drop-and-Swap Replacement");
    }

    [Fact]
    public async Task EvaluateTicket_LowConfidence_ReturnsRoutedToAgentReview()
    {
        // Arrange: Unrelated query that does not exist in the pre-seeded KB
        const string question = "What is the orbital velocity of Jupiter's moon Ganymede?";

        // Act
        var resolution = await _service.EvaluateTicketAsync("TCK-TEST-002", question, threshold: 0.88);

        // Assert
        resolution.Should().NotBeNull();
        resolution.Confidence.Should().BeLessThan(0.88);
        resolution.ActionTaken.Should().Be("RoutedToAgentReview");
        resolution.ResolutionText.Should().Contain("[DRAFT FOR AGENT REVIEW - Missing verified policy details]");
    }

    [Fact]
    public async Task EvaluateTicket_GroundedCitations_IncludeExactDocumentTitleAndChunkSnippet()
    {
        // Arrange: Query about 30-day return policy
        const string question = "What is your 30-day return and exchange policy?";

        // Act
        var resolution = await _service.EvaluateTicketAsync("TCK-TEST-003", question, threshold: 0.85);

        // Assert
        resolution.AppliedCitations.Should().NotBeEmpty();
        var primaryCitation = resolution.AppliedCitations[0];
        primaryCitation.DocumentTitle.Should().Be("30-Day Return & Exchange Protocol");
        primaryCitation.ChunkIndex.Should().BeGreaterThanOrEqualTo(0);
        primaryCitation.ExcerptSnippet.Should().NotBeNullOrWhiteSpace();
        primaryCitation.ExcerptSnippet.ToLowerInvariant().Should().Contain("return");
        primaryCitation.RelevanceScore.Should().BeGreaterThan(0.0);
    }

    [Fact]
    public async Task EvaluateTicket_AppendsToAutopilotLogsAuditTrail()
    {
        // Arrange
        const string uniqueTicketId = "TCK-AUDIT-VERIFY-999";
        const string question = "How does cash on delivery remittance settlement work?";

        // Act
        await _service.EvaluateTicketAsync(uniqueTicketId, question);
        var logs = await _service.GetAutopilotLogsAsync(limit: 50);

        // Assert
        logs.Should().Contain(l => l.TicketId == uniqueTicketId);
    }

    [Fact]
    public async Task QueryAsync_IncludesLatencyAndScoreBreakdown()
    {
        // Arrange
        var request = new RagQueryRequest(
            TenantId: HybridRagAutopilotService.DefaultTenantId,
            CustomerQuestion: "What are the UN 38.3 battery shipping regulations?",
            MinimumConfidenceThreshold: 0.88,
            TopK: 3
        );

        // Act
        var result = await _service.QueryAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.DenseScore.Should().BeGreaterThan(0.0);
        result.SparseScore.Should().BeGreaterThan(0.0);
        result.RrfScore.Should().BeGreaterThan(0.0);
        result.ConfidenceScore.Should().BeInRange(0.0, 1.0);
        result.LatencyMs.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task IngestDocumentAsync_AddsDocumentAndIndexableChunks()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        const string title = "Wholesale Volume Discount Tiering";
        const string category = "ProductSpecs";
        const string content = "Wholesale buyers ordering more than 500 units receive a 25% discount tier. Orders exceeding 2,000 units require dedicated container booking and 30-day advance notice.";

        // Act
        var doc = await _service.IngestDocumentAsync(tenantId, title, category, content, new List<string> { "wholesale", "bulk" });
        var docs = await _service.GetDocumentsAsync(tenantId);

        // Assert
        doc.Should().NotBeNull();
        doc.Title.Should().Be(title);
        doc.ChunkCount.Should().BeGreaterThan(0);
        docs.Should().Contain(d => d.Id == doc.Id);

        // Search against the newly ingested document
        var searchResult = await _service.QueryAsync(new RagQueryRequest(
            TenantId: tenantId,
            CustomerQuestion: "What is the wholesale volume discount for 500 units?",
            MinimumConfidenceThreshold: 0.80
        ));

        searchResult.Citations.Should().Contain(c => c.DocumentTitle == title);
    }

    [Fact]
    public async Task GetMetricsAsync_ComputesAccurateResolutionRateAndAverages()
    {
        // Act
        var metrics = await _service.GetMetricsAsync();

        // Assert
        metrics.Should().NotBeNull();
        metrics.TotalQueriesEvaluated.Should().BeGreaterThan(0);
        metrics.AutoResolvedCount.Should().BeGreaterThan(0);
        metrics.TotalDocuments.Should().BeGreaterThanOrEqualTo(6);
        metrics.TotalChunks.Should().BeGreaterThan(0);
        metrics.ResolutionRatePercent.Should().BeInRange(0.0, 100.0);
        metrics.AverageConfidence.Should().BeInRange(0.0, 1.0);
        metrics.AverageLatencyMs.Should().BeGreaterThan(0.0);
    }
}
