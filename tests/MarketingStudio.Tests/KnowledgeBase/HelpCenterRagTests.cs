using FluentAssertions;
using MarketingStudio.Api.Data;
using MarketingStudio.Api.Modules.KnowledgeBase;
using MarketingStudio.Core.Entities;
using MarketingStudio.Core.Tenancy;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MarketingStudio.Tests.KnowledgeBase;

public class HelpCenterRagTests
{
    private MarketingStudioDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MarketingStudioDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(Guid.NewGuid());
        return new MarketingStudioDbContext(options, tenantContext);
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
}
