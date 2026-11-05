using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.AI.VectorRag;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Unit;

public class HybridRagAndVectorSearchTests
{
    private SGrowingDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public void Bm25LexicalScorer_TokenizesAndScoresRelevantDocumentsHigher()
    {
        var scorer = new Bm25LexicalScorer(k1: 1.2, b: 0.75);

        var corpus = new List<Bm25Document>
        {
            new("doc1", "PostgreSQL Schemas", "PostgreSQL provides partitioned database schemas for multi-tenant data isolation.", "Database"),
            new("doc2", "Stripe Checkout", "Stripe payment intents support credit cards, Apple Pay, and webhook confirmations.", "Payments"),
            new("doc3", "Omnichannel Voice", "WebRTC streaming voice SDR allows inbound call center automation.", "Voice")
        };

        var query = "multi-tenant database schemas PostgreSQL";
        var results = scorer.ScoreCorpus(corpus, query, topK: 3);

        results.Should().NotBeEmpty();
        results[0].DocumentId.Should().Be("doc1");
        results[0].Bm25Score.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ReciprocalRankFusion_FusesDenseAndSparseRankingsCorrectly()
    {
        var denseResults = new List<SemanticSearchResult>
        {
            new("c1", "docA", "Title A", "Content A", "Category", 0.95, 0.05),
            new("c2", "docB", "Title B", "Content B", "Category", 0.85, 0.15)
        };

        var sparseResults = new List<Bm25ScoredResult>
        {
            new("docB", "Title B", "Content B", "Category", 4.2),
            new("docC", "Title C", "Content C", "Category", 3.1)
        };

        var fused = ReciprocalRankFusion.Fuse(denseResults, sparseResults, k: 60, topK: 5);

        fused.Should().HaveCount(3);

        // docB is ranked #2 in dense and #1 in sparse:
        // score = 1/(60 + 2) + 1/(60 + 1) = 1/62 + 1/61 = 0.016129 + 0.016393 = 0.032522
        var itemB = fused.First(f => f.DocumentId == "docB");
        itemB.DenseRank.Should().Be(2);
        itemB.SparseRank.Should().Be(1);
        itemB.RrfScore.Should().BeApproximately(0.032522, 0.0001);

        // docA is ranked #1 in dense, unranked in sparse:
        // score = 1/(60 + 1) = 0.016393
        var itemA = fused.First(f => f.DocumentId == "docA");
        itemA.DenseRank.Should().Be(1);
        itemA.SparseRank.Should().Be(-1);
        itemA.RrfScore.Should().BeApproximately(0.016393, 0.0001);
    }

    [Fact]
    public async Task HybridRagEngine_PerformsGroundedSearchWithCitations()
    {
        using var db = CreateInMemoryDb();
        var engine = new HybridRagEngine(db);
        var tenantId = Guid.NewGuid();

        var query = new HybridSearchQuery(
            QueryText: "What is the pricing for SGrowing Enterprise tier?",
            TopK: 3);

        var response = await engine.QueryGroundedAsync(tenantId, query);

        response.QueryText.Should().Be(query.QueryText);
        response.RetrievedContext.Should().NotBeEmpty();
        response.Citations.Should().NotBeEmpty();
        response.GroundedSynthesis.Should().Contain("Enterprise Pricing");
        response.LatencyMs.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task HybridRagEngine_WithDatabaseChunks_RetrievesAndFusesTenantKnowledge()
    {
        using var db = CreateInMemoryDb();
        var tenantId = Guid.NewGuid();
        var docId = Guid.NewGuid();

        // Seed a custom document chunk in db
        var chunk = DocumentChunk.Create(
            tenantId: tenantId,
            documentId: docId,
            documentTitle: "Enterprise Compliance SLA Handbook",
            chunkIndex: 0,
            content: "SGrowing guarantees a sub-150ms WebRTC voice SDR latency with 99.99% sovereign cluster uptime SLA.",
            embedding: new float[] { 0.5f, 0.5f, 0.5f, 0.5f },
            sourceUrl: "https://sgrowing.internal/docs/sla");

        await db.DocumentChunks.AddAsync(chunk);
        await db.SaveChangesAsync();

        var engine = new HybridRagEngine(db);

        var query = new HybridSearchQuery(
            QueryText: "WebRTC voice latency SLA compliance",
            TopK: 2);

        var results = await engine.HybridSearchAsync(tenantId, query);

        results.Should().NotBeEmpty();
        results[0].Title.Should().Be("Enterprise Compliance SLA Handbook");
        results[0].Content.Should().Contain("sub-150ms");
    }
}
