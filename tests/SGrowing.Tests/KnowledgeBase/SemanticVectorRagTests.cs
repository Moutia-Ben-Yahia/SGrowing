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
        Assert.Equal(64, vec.Length);

        // Vector magnitude should be approximately 1.0 (L2 normalized)
        double magSq = vec.Sum(x => x * x);
        Assert.InRange(magSq, 0.98, 1.02);
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
        Assert.InRange(similarity, 0.999, 1.001);
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
        Assert.Equal(2, indexedCount);

        // Act: Search for return policy
        var searchResults = await service.SearchKnowledgeAsync(tenantId, "How do I return a broken item?");

        // Assert
        Assert.NotEmpty(searchResults);
        Assert.Equal(art1.Id, searchResults[0].DocumentId);
        Assert.Contains("Return and Refund Policy", searchResults[0].DocumentTitle);
    }
}
