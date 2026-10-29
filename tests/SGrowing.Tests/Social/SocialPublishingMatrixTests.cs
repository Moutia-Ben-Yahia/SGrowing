using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Marketing;
using SGrowing.Core.Entities;
using SGrowing.Worker.Services;
using Xunit;

namespace SGrowing.Tests.Social;

public class SocialPublishingMatrixTests
{
    private static SGrowingDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public async Task RewriteForPlatformsAsync_AdaptsHashtagRules_AndAspectRatios_PerPlatform()
    {
        // Arrange
        using var db = CreateInMemoryDb();
        var service = new SocialPublishingEngineService(db);
        var tenantId = Guid.NewGuid();
        var masterDraft = "Supercharge your B2B sales pipeline with autonomous AI agents.";
        var platforms = new List<SocialPlatform>
        {
            SocialPlatform.X,
            SocialPlatform.LinkedIn,
            SocialPlatform.Instagram,
            SocialPlatform.TikTok
        };

        // Act
        var results = await service.RewriteForPlatformsAsync(tenantId, masterDraft, platforms, preferredAspectRatio: "9:16 Vertical Reel/Story");

        // Assert
        results.Should().HaveCount(4);

        // 1. X: 1-2 hashtags, 1:1 Square, <= 280 chars
        var xPost = results.First(r => r.Platform == SocialPlatform.X);
        xPost.RecommendedHashtags.Count.Should().BeInRange(1, 2);
        xPost.RecommendedHashtags.Should().OnlyContain(h => h.StartsWith("#"));
        xPost.RecommendedAspectRatio.Should().Be("1:1 Square");
        xPost.MaxCharacterLimit.Should().Be(280);
        xPost.IsWithinLimit.Should().BeTrue();
        xPost.CharacterCount.Should().BeLessThanOrEqualTo(280);

        // 2. LinkedIn: 3-5 hashtags, 1:1 Square, Executive takeaway format
        var liPost = results.First(r => r.Platform == SocialPlatform.LinkedIn);
        liPost.RecommendedHashtags.Count.Should().BeInRange(3, 5);
        liPost.RecommendedHashtags.Should().OnlyContain(h => h.StartsWith("#"));
        liPost.FormattedContent.Should().Contain("Executive Takeaway");
        liPost.RecommendedAspectRatio.Should().Be("1:1 Square");
        liPost.MaxCharacterLimit.Should().Be(3000);
        liPost.IsWithinLimit.Should().BeTrue();

        // 3. Instagram: 3-5 hashtags, 9:16 Vertical Reel/Story when preferred
        var igPost = results.First(r => r.Platform == SocialPlatform.Instagram);
        igPost.RecommendedHashtags.Count.Should().BeInRange(3, 5);
        igPost.RecommendedHashtags.Should().OnlyContain(h => h.StartsWith("#"));
        igPost.RecommendedAspectRatio.Should().Be("9:16 Vertical Reel/Story");
        igPost.FormattedContent.Should().Contain("REEL HOOK");
        igPost.MaxCharacterLimit.Should().Be(2200);
        igPost.IsWithinLimit.Should().BeTrue();

        // 4. TikTok: 3-5 hashtags, 9:16 Vertical Reel/Story, POV format
        var ttPost = results.First(r => r.Platform == SocialPlatform.TikTok);
        ttPost.RecommendedHashtags.Count.Should().BeInRange(3, 5);
        ttPost.RecommendedHashtags.Should().OnlyContain(h => h.StartsWith("#"));
        ttPost.RecommendedAspectRatio.Should().Be("9:16 Vertical Reel/Story");
        ttPost.FormattedContent.Should().Contain("POV:");
        ttPost.MaxCharacterLimit.Should().Be(2200);
        ttPost.IsWithinLimit.Should().BeTrue();
    }

    [Theory]
    [InlineData(SocialPlatform.X, 280, 200, true, 80)]
    [InlineData(SocialPlatform.X, 280, 281, false, -1)]
    [InlineData(SocialPlatform.LinkedIn, 3000, 1500, true, 1500)]
    [InlineData(SocialPlatform.LinkedIn, 3000, 3001, false, -1)]
    [InlineData(SocialPlatform.Instagram, 2200, 1000, true, 1200)]
    [InlineData(SocialPlatform.Instagram, 2200, 2205, false, -5)]
    [InlineData(SocialPlatform.TikTok, 2200, 1000, true, 1200)]
    [InlineData(SocialPlatform.TikTok, 2200, 2250, false, -50)]
    public void ValidatePostContent_AccuratelyValidates_CharacterLimits(
        SocialPlatform platform,
        int expectedMax,
        int contentLen,
        bool expectedValid,
        int expectedRemaining)
    {
        // Arrange
        using var db = CreateInMemoryDb();
        var service = new SocialPublishingEngineService(db);
        var testContent = new string('A', contentLen);

        // Act
        var result = service.ValidatePostContent(platform, testContent);

        // Assert
        result.Platform.Should().Be(platform);
        result.MaxLimit.Should().Be(expectedMax);
        result.ActualLength.Should().Be(contentLen);
        result.IsValid.Should().Be(expectedValid);
        result.RemainingCharacters.Should().Be(expectedRemaining);
    }

    [Fact]
    public async Task RewriteForPlatformsAsync_TruncatesXDraft_WhenMasterDraftExceedsLimit()
    {
        // Arrange
        using var db = CreateInMemoryDb();
        var service = new SocialPublishingEngineService(db);
        var tenantId = Guid.NewGuid();
        var longDraft = new string('X', 400); // 400 chars, well above 280

        // Act
        var results = await service.RewriteForPlatformsAsync(tenantId, longDraft, new List<SocialPlatform> { SocialPlatform.X });

        // Assert
        var xPost = results.Single();
        xPost.CharacterCount.Should().BeLessThanOrEqualTo(280);
        xPost.IsWithinLimit.Should().BeTrue();
        xPost.FormattedContent.Should().EndWith("...");
        xPost.ValidationWarning.Should().BeNull();
    }

    [Fact]
    public async Task AuditConnectedAccountsTokenHealthAsync_DetectsExpiringWithin7Days_AndFlagsRefreshTokenRequired()
    {
        // Arrange
        using var db = CreateInMemoryDb();
        var service = new SocialPublishingEngineService(db);
        var tenantId = Guid.NewGuid();

        // Account 1: Expiring in 3 days (within 7 days) -> Flagged
        var accExpiringSoon = SocialAccount.Create(
            tenantId,
            SocialPlatform.X,
            "@growth_x",
            "x_999",
            "token_1",
            isActive: true,
            tokenExpiresAtUtc: DateTime.UtcNow.AddDays(3));

        // Account 2: Expired 1 day ago -> Flagged
        var accExpired = SocialAccount.Create(
            tenantId,
            SocialPlatform.LinkedIn,
            "SGrowing LinkedIn",
            "li_999",
            "token_2",
            isActive: true,
            tokenExpiresAtUtc: DateTime.UtcNow.AddDays(-1));

        // Account 3: Expires in 30 days -> Healthy
        var accHealthy = SocialAccount.Create(
            tenantId,
            SocialPlatform.Instagram,
            "@sgrowing_ig",
            "ig_999",
            "token_3",
            isActive: true,
            tokenExpiresAtUtc: DateTime.UtcNow.AddDays(30));

        // Account 4: No expiration date -> Healthy
        var accNoExpiry = SocialAccount.Create(
            tenantId,
            SocialPlatform.TikTok,
            "@sgrowing_tt",
            "tt_999",
            "token_4",
            isActive: true,
            tokenExpiresAtUtc: null);

        db.SocialAccounts.AddRange(accExpiringSoon, accExpired, accHealthy, accNoExpiry);
        await db.SaveChangesAsync();

        // Act
        var auditReport = await service.AuditConnectedAccountsTokenHealthAsync(tenantId);

        // Assert Report DTO
        auditReport.TenantId.Should().Be(tenantId);
        auditReport.TotalAccountsAudited.Should().Be(4);
        auditReport.ExpiringOrRequiredRefreshCount.Should().Be(2);
        auditReport.HealthyAccountsCount.Should().Be(2);

        var reportExpiring = auditReport.Accounts.First(a => a.AccountId == accExpiringSoon.Id);
        reportExpiring.RefreshTokenRequired.Should().BeTrue();
        reportExpiring.IsExpired.Should().BeFalse();
        reportExpiring.StatusSummary.Should().Be("ExpiringSoon");
        reportExpiring.DaysUntilExpiration.Should().BeInRange(2, 4);

        var reportExpired = auditReport.Accounts.First(a => a.AccountId == accExpired.Id);
        reportExpired.RefreshTokenRequired.Should().BeTrue();
        reportExpired.IsExpired.Should().BeTrue();
        reportExpired.StatusSummary.Should().Be("Expired");

        var reportHealthy = auditReport.Accounts.First(a => a.AccountId == accHealthy.Id);
        reportHealthy.RefreshTokenRequired.Should().BeFalse();
        reportHealthy.StatusSummary.Should().Be("Healthy");

        // Assert Database Persistence
        var persistedSoon = await db.SocialAccounts.FindAsync(accExpiringSoon.Id);
        persistedSoon!.RefreshTokenRequired.Should().BeTrue();

        var persistedExpired = await db.SocialAccounts.FindAsync(accExpired.Id);
        persistedExpired!.RefreshTokenRequired.Should().BeTrue();

        var persistedHealthy = await db.SocialAccounts.FindAsync(accHealthy.Id);
        persistedHealthy!.RefreshTokenRequired.Should().BeFalse();
    }

    [Fact]
    public async Task DurableSocialPublishingDaemon_AuditTokenExpirationHealthAsync_FlagsAccounts_AndQueuesOutboxAlert()
    {
        // Arrange
        using var db = CreateInMemoryDb();
        var tenantId = Guid.NewGuid();

        var expiringAccount = SocialAccount.Create(
            tenantId,
            SocialPlatform.TikTok,
            "@sgrowing_tiktok",
            "tt_123",
            "token_tok",
            isActive: true,
            tokenExpiresAtUtc: DateTime.UtcNow.AddDays(2));

        var healthyAccount = SocialAccount.Create(
            tenantId,
            SocialPlatform.LinkedIn,
            "SGrowing Corp",
            "li_123",
            "token_li",
            isActive: true,
            tokenExpiresAtUtc: DateTime.UtcNow.AddDays(25));

        db.SocialAccounts.AddRange(expiringAccount, healthyAccount);
        await db.SaveChangesAsync();

        // Act
        var flaggedCount = await DurableSocialPublishingDaemon.AuditTokenExpirationHealthAsync(db);

        // Assert
        flaggedCount.Should().Be(1);

        var reloadedExpiring = await db.SocialAccounts.FindAsync(expiringAccount.Id);
        reloadedExpiring!.RefreshTokenRequired.Should().BeTrue();

        var reloadedHealthy = await db.SocialAccounts.FindAsync(healthyAccount.Id);
        reloadedHealthy!.RefreshTokenRequired.Should().BeFalse();

        // Check Outbox Notification
        var outboxMessages = await db.OutboxMessages.ToListAsync();
        outboxMessages.Should().ContainSingle();
        var msg = outboxMessages.Single();
        msg.EventType.Should().Be("SocialAccount.TokenExpirationImminent");
        msg.TenantId.Should().Be(tenantId);
        msg.Payload.Should().Contain("@sgrowing_tiktok");
        msg.Payload.Should().Contain("TikTok");
    }

    [Fact]
    public async Task GenerateViralHooksAsync_ReturnsHookSuggestionsWithEstimatedVirality()
    {
        // Arrange
        using var db = CreateInMemoryDb();
        var service = new SocialPublishingEngineService(db);
        var tenantId = Guid.NewGuid();

        // Act
        var hooks = await service.GenerateViralHooksAsync(tenantId, "Autonomous AI CRM");

        // Assert
        hooks.Should().NotBeNullOrEmpty();
        hooks.Should().HaveCount(4);
        hooks.Should().Contain(h => h.Angle == "Contrarian");
        hooks.Should().Contain(h => h.Angle == "Statistical");
        hooks.Should().Contain(h => h.Angle == "Question");
        hooks.Should().Contain(h => h.Angle == "Story");
        hooks.Should().OnlyContain(h => h.EstimatedViralityScore >= 80.0);
    }
}
