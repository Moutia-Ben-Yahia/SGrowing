using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.KnowledgeBase;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.KnowledgeBase;

public class VectorRagTests
{
    private readonly SGrowingDbContext _db;
    private readonly DocumentChunkingService _chunkingService;
    private readonly VectorRagRetrievalService _ragService;

    public VectorRagTests()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new SGrowingDbContext(options);
        _chunkingService = new DocumentChunkingService();
        _ragService = new VectorRagRetrievalService(_db, _chunkingService);
    }

    [Fact]
    public void DocumentChunking_SplitsWithTargetWordsAndSlidingOverlap()
    {
        // 120 words of text
        var text = string.Join(" ", Enumerable.Range(1, 120).Select(i => $"word{i}"));

        var chunks = _chunkingService.ChunkText(text, targetChunkWords: 50, overlapWords: 10);

        Assert.True(chunks.Count >= 3);
        Assert.Equal(0, chunks[0].Index);
        Assert.Equal(1, chunks[1].Index);
        Assert.Equal(50, chunks[0].WordCount);
        Assert.True(chunks[0].EstimatedTokens > 0);

        // Check sliding overlap: words from end of chunk 0 should be at start of chunk 1
        Assert.Contains("word41", chunks[0].Content);
        Assert.Contains("word41", chunks[1].Content);
    }

    [Fact]
    public void CosineSimilarity_ComputesNormalizedSimilarityAccurately()
    {
        var v1 = new float[] { 1f, 0f, 0f, 0f };
        var v2 = new float[] { 1f, 0f, 0f, 0f };
        var v3 = new float[] { 0f, 1f, 0f, 0f };
        var v4 = new float[] { 0.7071f, 0.7071f, 0f, 0f };

        // Identical vectors = 1.0
        var simIdentical = DocumentChunkingService.CalculateCosineSimilarity(v1, v2);
        Assert.Equal(1.0f, simIdentical, precision: 3);

        // Orthogonal vectors = 0.0
        var simOrthogonal = DocumentChunkingService.CalculateCosineSimilarity(v1, v3);
        Assert.Equal(0.0f, simOrthogonal, precision: 3);

        // 45 degree angle vector = ~0.707
        var simDiagonal = DocumentChunkingService.CalculateCosineSimilarity(v1, v4);
        Assert.InRange(simDiagonal, 0.70f, 0.72f);
    }

    [Fact]
    public async Task IndexAndHybridSearch_RetrievesRelevantChunkWithRrfScore()
    {
        var tenantId = Guid.NewGuid();
        var docId1 = Guid.NewGuid();
        var docId2 = Guid.NewGuid();

        // Index document 1: WhatsApp API Configuration
        var content1 = "WhatsApp Cloud API allows businesses to connect with customers globally. Configure your webhook URL and verify token in Meta Developer Portal to receive incoming messages.";
        await _ragService.IndexDocumentAsync(tenantId, docId1, "WhatsApp Setup Guide", content1, "https://docs.sgrowing.com/whatsapp");

        // Index document 2: Telephony SIP Setup
        var content2 = "To route voice calls with Twilio SIP Trunking, register your Twilio Phone Number with our WebRTC softphone media streams pipeline for real-time speech translation.";
        await _ragService.IndexDocumentAsync(tenantId, docId2, "Voice Telephony Configuration", content2, "https://docs.sgrowing.com/telephony");

        // Search for WhatsApp query
        var results = await _ragService.HybridSearchAsync(tenantId, "How do I setup WhatsApp Cloud API webhook?", topK: 1);

        Assert.Single(results);
        Assert.Equal(docId1, results[0].DocumentId);
        Assert.Equal("WhatsApp Setup Guide", results[0].DocumentTitle);
        Assert.True(results[0].CombinedRrfScore > 0);
        Assert.True(results[0].CosineSimilarity > 0);
    }

    [Fact]
    public async Task HybridSearch_EnforcesStrictTenantIsolation()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var docId = Guid.NewGuid();
        var secretPlan = "Project Falcon: Confidential expansion into EMEA regional markets with exclusive banking partnerships.";
        await _ragService.IndexDocumentAsync(tenantA, docId, "Confidential Strategy", secretPlan);

        // Tenant B searching for exact query should find 0 results
        var resultsTenantB = await _ragService.HybridSearchAsync(tenantB, "Project Falcon EMEA banking", topK: 5);
        Assert.Empty(resultsTenantB);

        // Tenant A searching gets the result
        var resultsTenantA = await _ragService.HybridSearchAsync(tenantA, "Project Falcon EMEA banking", topK: 5);
        Assert.Single(resultsTenantA);
        Assert.Equal("Confidential Strategy", resultsTenantA[0].DocumentTitle);
    }

    [Fact]
    public async Task GenerateGroundedContext_BuildsStructuredPromptWithCitations()
    {
        var tenantId = Guid.NewGuid();
        var docId = Guid.NewGuid();
        var content = "SGrowing CRM automatically enriches leads with company headcount, annual recurring revenue, and tech stack using Clearbit and Apollo integrations.";
        await _ragService.IndexDocumentAsync(tenantId, docId, "Lead Enrichment Manual", content, "https://docs.sgrowing.com/enrichment");

        var promptContext = await _ragService.GenerateGroundedContextAsync(tenantId, "How does lead enrichment work?", topK: 2);

        Assert.NotEmpty(promptContext.SystemInstructions);
        Assert.Contains("Lead Enrichment Manual", promptContext.GroundedContext);
        Assert.Contains("SGrowing CRM automatically enriches leads", promptContext.GroundedContext);
        Assert.Contains("https://docs.sgrowing.com/enrichment", promptContext.GroundedContext);
        Assert.Single(promptContext.RetrievedSources);
    }
}
