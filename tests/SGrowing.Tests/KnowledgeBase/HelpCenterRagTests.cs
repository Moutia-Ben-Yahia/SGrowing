using FluentAssertions;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.KnowledgeBase;
using SGrowing.Core.Entities;
using SGrowing.Core.Tenancy;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace SGrowing.Tests.KnowledgeBase;

public class HelpCenterRagTests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var tenantContext = new TenantContext();
        return new SGrowingDbContext(options, tenantContext);
    }

    [Fact]
    public async Task GenerateAiDeflection_WithRelevantQuestion_ReturnsDirectAnswerAndCitesArticle()
    {
        var db = CreateDbContext();
        var service = new HelpCenterRagService(db);
        var tenantId = Guid.NewGuid();

        var category = HelpCategory.Create(tenantId, "Integrations", "integrations", 1, "🔌");
        db.HelpCategories.Add(category);

        var article = HelpArticle.Create(
            tenantId,
            category.Id,
            "Setting up WhatsApp Meta Cloud API",
            "whatsapp-meta-setup",
            "To connect WhatsApp Cloud API, navigate to Settings > Channels > WhatsApp, input your Meta System User Access Token and Phone Number ID, then paste your webhook URL."
        );
        db.HelpArticles.Add(article);
        await db.SaveChangesAsync();

        var result = await service.GenerateAiDeflectionReplyAsync(tenantId, "How do I connect WhatsApp and configure the Meta Cloud API webhook?");

        result.Deflected.Should().BeTrue();
        result.ConfidenceScore.Should().BeGreaterThanOrEqualTo(80);
        result.ArticleTitle.Should().Be("Setting up WhatsApp Meta Cloud API");
        result.ArticleSlug.Should().Be("whatsapp-meta-setup");
        result.Answer.Should().Contain("Meta System User Access Token");

        var updatedArticle = await db.HelpArticles.FindAsync(article.Id);
        updatedArticle!.ViewCount.Should().Be(1);
    }

    [Fact]
    public async Task GenerateAiDeflection_WithUnrelatedQuestion_GracefullyEscalatesToHuman()
    {
        var db = CreateDbContext();
        var service = new HelpCenterRagService(db);
        var tenantId = Guid.NewGuid();

        var category = HelpCategory.Create(tenantId, "Billing", "billing");
        db.HelpCategories.Add(category);
        var article = HelpArticle.Create(tenantId, category.Id, "Invoicing and Receipts", "invoicing", "You can download PDF invoices from the Billing settings tab.");
        db.HelpArticles.Add(article);
        await db.SaveChangesAsync();

        var result = await service.GenerateAiDeflectionReplyAsync(tenantId, "What is the capital of Australia?");

        result.Deflected.Should().BeFalse();
        result.ConfidenceScore.Should().BeLessThan(50);
        result.ArticleTitle.Should().BeNull();
        result.Answer.Should().Contain("notified our support team");
    }

    [Fact]
    public async Task GenerateAiDeflection_WithNullOrWhitespaceQuestion_ShouldReturnPromptToProvideQuestion()
    {
        var db = CreateDbContext();
        var service = new HelpCenterRagService(db);
        var tenantId = Guid.NewGuid();

        var resultNull = await service.GenerateAiDeflectionReplyAsync(tenantId, "");
        resultNull.Deflected.Should().BeFalse();
        resultNull.Answer.Should().Contain("Please provide a question");
        resultNull.ConfidenceScore.Should().Be(0);

        var resultWhitespace = await service.GenerateAiDeflectionReplyAsync(tenantId, "    ");
        resultWhitespace.Deflected.Should().BeFalse();
        resultWhitespace.ConfidenceScore.Should().Be(0);
    }

    [Fact]
    public async Task GenerateAiDeflection_WithOnlyStopWords_ShouldReturnLowConfidenceHumanEscalation()
    {
        var db = CreateDbContext();
        var service = new HelpCenterRagService(db);
        var tenantId = Guid.NewGuid();

        // Question composed only of stop words or very short words: "what is how can i"
        var result = await service.GenerateAiDeflectionReplyAsync(tenantId, "what is how can i");

        result.Deflected.Should().BeFalse();
        result.ConfidenceScore.Should().Be(20);
        result.Answer.Should().Contain("human support specialist");
    }

    [Fact]
    public async Task GenerateAiDeflection_WhenArticlesUnpublished_ShouldIgnoreUnpublishedArticles()
    {
        var db = CreateDbContext();
        var service = new HelpCenterRagService(db);
        var tenantId = Guid.NewGuid();

        var category = HelpCategory.Create(tenantId, "Beta", "beta");
        db.HelpCategories.Add(category);

        var draftArticle = HelpArticle.Create(
            tenantId,
            category.Id,
            "Secret Beta Feature Documentation",
            "secret-beta",
            "This secret beta feature enables automated crypto payouts."
        );
        draftArticle.IsPublished = false; // Draft / Unpublished!
        db.HelpArticles.Add(draftArticle);
        await db.SaveChangesAsync();

        var result = await service.GenerateAiDeflectionReplyAsync(tenantId, "How do automated crypto payouts work?");

        result.Deflected.Should().BeFalse();
        result.ArticleTitle.Should().BeNull();
        draftArticle.ViewCount.Should().Be(0); // ViewCount not incremented!
    }

    [Fact]
    public async Task GenerateAiDeflection_ShouldIsolateArticlesByTenant()
    {
        var db = CreateDbContext();
        var service = new HelpCenterRagService(db);
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var catA = HelpCategory.Create(tenantA, "Tenant A Docs", "docs-a");
        var catB = HelpCategory.Create(tenantB, "Tenant B Docs", "docs-b");
        db.HelpCategories.AddRange(catA, catB);

        var articleB = HelpArticle.Create(
            tenantB,
            catB.Id,
            "Tenant B Private Policy",
            "private-policy",
            "Confidential internal guidelines exclusively for Tenant B staff members."
        );
        db.HelpArticles.Add(articleB);
        await db.SaveChangesAsync();

        // Tenant A asks question about Tenant B's article
        var resultA = await service.GenerateAiDeflectionReplyAsync(tenantA, "Where can I read the private policy confidential guidelines?");

        resultA.Deflected.Should().BeFalse();
        resultA.ArticleTitle.Should().BeNull();
        articleB.ViewCount.Should().Be(0);
    }

    [Fact]
    public async Task GenerateAiDeflection_ShouldTruncateLongArticleContentInSummary()
    {
        var db = CreateDbContext();
        var service = new HelpCenterRagService(db);
        var tenantId = Guid.NewGuid();

        var cat = HelpCategory.Create(tenantId, "Guides", "guides");
        db.HelpCategories.Add(cat);

        string longContent = new string('A', 300);
        var article = HelpArticle.Create(tenantId, cat.Id, "Long Technical Reference Manual", "long-manual", longContent);
        db.HelpArticles.Add(article);
        await db.SaveChangesAsync();

        var result = await service.GenerateAiDeflectionReplyAsync(tenantId, "Can I see the technical reference manual?");

        result.Deflected.Should().BeTrue();
        result.Answer.Should().Contain("...");
        result.Answer.Should().Contain("/help/long-manual");
    }

    [Fact]
    public async Task GenerateAiDeflection_WhenNoArticlesExist_ReturnsFallbackEscalation()
    {
        var db = CreateDbContext();
        var service = new HelpCenterRagService(db);
        var tenantId = Guid.NewGuid();

        var result = await service.GenerateAiDeflectionReplyAsync(tenantId, "How do I configure domain email deliverability?");

        result.Deflected.Should().BeFalse();
        result.ConfidenceScore.Should().BeLessThan(50);
        result.ArticleTitle.Should().BeNull();
        result.Answer.Should().Contain("notified our support team");
    }
}
