using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.KnowledgeBase;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.KnowledgeBase;

public class SemanticVectorRagTests
{
    private (SGrowingDbContext db, SemanticVectorRagService service) CreateContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var db = new SGrowingDbContext(options);
        var service = new SemanticVectorRagService(db);
        return (db, service);
    }

    [Fact]
    public void GenerateEmbedding_ProducesNormalizedVector()
    {
        // Arrange
        var (_, service) = CreateContext();

        // Act
        var vec = service.GenerateEmbedding("How do I return a damaged COD product?");

        // Assert
        vec.Length.Should().Be(64);

        // Vector magnitude should be approximately 1.0 (L2 normalized)
        double magSq = vec.Sum(x => x * x);
        magSq.Should().BeInRange(0.98, 1.02);
    }

    [Fact]
    public void GenerateEmbedding_HandlesNullOrWhitespace_ReturnsZeroVector()
    {
        // Arrange
        var (_, service) = CreateContext();

        // Act
        var nullVec = service.GenerateEmbedding(null!);
        var emptyVec = service.GenerateEmbedding("    ");

        // Assert
        nullVec.Length.Should().Be(64);
        nullVec.All(v => v == 0f).Should().BeTrue();

        emptyVec.Length.Should().Be(64);
        emptyVec.All(v => v == 0f).Should().BeTrue();
    }

    [Fact]
    public void ComputeCosineSimilarity_IdenticalVectorsReturnOne()
    {
        // Arrange
        var (_, service) = CreateContext();
        var vec1 = service.GenerateEmbedding("same text query");
        var vec2 = service.GenerateEmbedding("same text query");

        // Act
        var similarity = service.ComputeCosineSimilarity(vec1, vec2);

        // Assert
        similarity.Should().BeInRange(0.999, 1.001);
    }

    [Fact]
    public void ComputeCosineSimilarity_EmptyOrNullVectors_ReturnsZero()
    {
        // Arrange
        var (_, service) = CreateContext();
        var validVec = service.GenerateEmbedding("some content text");

        // Act & Assert
        service.ComputeCosineSimilarity(null!, validVec).Should().Be(0.0);
        service.ComputeCosineSimilarity(validVec, Array.Empty<float>()).Should().Be(0.0);
        service.ComputeCosineSimilarity(new float[64], validVec).Should().Be(0.0);
    }

    [Fact]
    public async Task IndexAndSearchKnowledge_ReturnsRankedMatches()
    {
        // Arrange
        var (db, service) = CreateContext();
        var tenantId = Guid.NewGuid();

        var cat = HelpCategory.Create(tenantId, "Return Policies", "return-policies");
        db.HelpCategories.Add(cat);

        var art1 = HelpArticle.Create(
            tenantId,
            cat.Id,
            "Return and Refund Policy for Cash on Delivery",
            "return-refund-cod",
            "Customers can return defective items within 14 days of delivery. Doorstep swap is available in major cities."
        );

        var art2 = HelpArticle.Create(
            tenantId,
            cat.Id,
            "Setting up DNS DKIM Records for Custom Domains",
            "setting-up-dkim-dns",
            "To authenticate outgoing email, add a CNAME record with selector s1._domainkey pointing to our relay."
        );

        db.HelpArticles.AddRange(art1, art2);
        await db.SaveChangesAsync();

        // Act: Index articles
        var indexedCount = await service.IndexTenantKnowledgeAsync(tenantId);
        indexedCount.Should().Be(2);

        // Act: Search for return policy
        var searchResults = await service.SearchKnowledgeAsync(tenantId, "How do I return a broken item?");

        // Assert
        searchResults.Should().NotBeEmpty();
        searchResults[0].DocumentId.Should().Be(art1.Id);
        searchResults[0].DocumentTitle.Should().Contain("Return and Refund Policy");
        searchResults[0].SimilarityScore.Should().BeGreaterThan(0.10);
    }

    [Fact]
    public async Task SearchKnowledgeAsync_RespectsTenantIsolation_ExcludesOtherTenantChunks()
    {
        // Arrange
        var (db, service) = CreateContext();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var catA = HelpCategory.Create(tenantA, "Billing", "billing-a");
        var catB = HelpCategory.Create(tenantB, "Billing", "billing-b");
        db.HelpCategories.AddRange(catA, catB);

        var artA = HelpArticle.Create(tenantA, catA.Id, "Enterprise Invoicing", "invoicing-a", "Tenant A enterprise net-30 invoicing instructions.");
        var artB = HelpArticle.Create(tenantB, catB.Id, "Enterprise Invoicing", "invoicing-b", "Tenant B enterprise net-30 invoicing instructions.");
        db.HelpArticles.AddRange(artA, artB);
        await db.SaveChangesAsync();

        await service.IndexTenantKnowledgeAsync(tenantA);
        await service.IndexTenantKnowledgeAsync(tenantB);

        // Act
        var resultsForA = await service.SearchKnowledgeAsync(tenantA, "Enterprise Invoicing");

        // Assert
        resultsForA.Should().NotBeEmpty();
        resultsForA.Should().OnlyContain(r => r.DocumentId == artA.Id);
    }

    [Fact]
    public async Task IndexTenantKnowledgeAsync_ExcludesUnpublishedArticles()
    {
        // Arrange
        var (db, service) = CreateContext();
        var tenantId = Guid.NewGuid();
        var cat = HelpCategory.Create(tenantId, "Drafts", "drafts");
        db.HelpCategories.Add(cat);

        var published = HelpArticle.Create(tenantId, cat.Id, "Live Article", "live-art", "Public knowledge base documentation.", isPublished: true);
        var draft = HelpArticle.Create(tenantId, cat.Id, "Internal Draft", "draft-art", "Unreleased feature instructions.", isPublished: false);
        db.HelpArticles.AddRange(published, draft);
        await db.SaveChangesAsync();

        // Act
        var indexed = await service.IndexTenantKnowledgeAsync(tenantId);
        var chunks = await db.DocumentChunks.Where(c => c.TenantId == tenantId).ToListAsync();

        // Assert
        indexed.Should().Be(1);
        chunks.Should().HaveCount(1);
        chunks[0].DocumentId.Should().Be(published.Id);
    }

    [Fact]
    public async Task IndexTenantKnowledgeAsync_UpdatesExistingChunk_WhenReindexed()
    {
        // Arrange
        var (db, service) = CreateContext();
        var tenantId = Guid.NewGuid();
        var cat = HelpCategory.Create(tenantId, "Guides", "guides");
        db.HelpCategories.Add(cat);

        var article = HelpArticle.Create(tenantId, cat.Id, "Original Title", "orig-guide", "Initial content version.");
        db.HelpArticles.Add(article);
        await db.SaveChangesAsync();

        await service.IndexTenantKnowledgeAsync(tenantId);
        var initialChunk = await db.DocumentChunks.FirstAsync(c => c.DocumentId == article.Id);
        initialChunk.DocumentTitle.Should().Be("Original Title");

        // Act: Update article and re-index
        article.Title = "Updated Title 2026";
        article.ContentMarkdown = "Revised and updated content version.";
        await db.SaveChangesAsync();

        var reindexedCount = await service.IndexTenantKnowledgeAsync(tenantId);

        // Assert
        reindexedCount.Should().Be(1);
        var totalChunks = await db.DocumentChunks.CountAsync(c => c.TenantId == tenantId);
        totalChunks.Should().Be(1, "should update existing chunk rather than creating duplicates");

        var updatedChunk = await db.DocumentChunks.FirstAsync(c => c.DocumentId == article.Id);
        updatedChunk.DocumentTitle.Should().Be("Updated Title 2026");
        updatedChunk.Content.Should().Be("Revised and updated content version.");
    }
}
