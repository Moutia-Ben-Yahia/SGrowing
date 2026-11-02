using FluentAssertions;
using SGrowing.Api.Modules.Marketing.SocialPublishing;
using Xunit;

namespace SGrowing.Tests.Marketing;

public class SocialPublishingTests
{
    private readonly ISocialPublishingEngine _engine = new SocialPublishingEngine();

    [Fact]
    public void SchedulePost_CreatesPostWithPendingApprovalStatus()
    {
        // Arrange
        var tenantId = "tenant-enterprise-10";
        var scheduledAt = DateTime.UtcNow.AddDays(2);
        var request = new SchedulePostRequest(
            TenantId: tenantId,
            Platforms: new List<SocialPlatform> { SocialPlatform.LinkedIn, SocialPlatform.X },
            ContentText: "Announcing our breakthrough AI-Native Social Publishing Engine! #Growth #SaaS",
            MediaAssetUrls: new List<string> { "https://cdn.sgrowing.io/marketing/hero.png" },
            ScheduledAtUtc: scheduledAt
        );

        // Act
        var post = _engine.SchedulePost(request);

        // Assert
        post.Should().NotBeNull();
        post.Id.Should().NotBeEmpty();
        post.TenantId.Should().Be(tenantId);
        post.Platforms.Should().Contain(new[] { SocialPlatform.LinkedIn, SocialPlatform.X });
        post.ContentText.Should().Be(request.ContentText);
        post.MediaAssetUrls.Should().ContainSingle().Which.Should().Be("https://cdn.sgrowing.io/marketing/hero.png");
        post.ScheduledAtUtc.Should().Be(scheduledAt);
        post.PublishedAtUtc.Should().BeNull();
        post.Status.Should().Be(SocialPostStatus.PendingApproval);
        post.ApproverId.Should().BeNull();
        post.CreatedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        post.Metrics.Should().ContainKey("Impressions").WhoseValue.Should().Be(0);
        post.Metrics.Should().ContainKey("Clicks").WhoseValue.Should().Be(0);
        post.Metrics.Should().ContainKey("Likes").WhoseValue.Should().Be(0);
        post.Metrics.Should().ContainKey("Shares").WhoseValue.Should().Be(0);

        var retrieved = _engine.GetPost(tenantId, post.Id);
        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be(post.Id);
    }

    [Fact]
    public void ApprovePost_WhenApproved_TransitionsToApprovedStatus()
    {
        // Arrange
        var tenantId = "tenant-approval-test";
        var post = _engine.SchedulePost(new SchedulePostRequest(
            TenantId: tenantId,
            Platforms: new List<SocialPlatform> { SocialPlatform.Facebook },
            ContentText: "Quarterly product showcase webinar.",
            MediaAssetUrls: new List<string>(),
            ScheduledAtUtc: DateTime.UtcNow.AddHours(24)
        ));

        var approveRequest = new ApprovePostRequest(
            PostId: post.Id,
            TenantId: tenantId,
            ApproverId: "user_growth_lead_42",
            IsApproved: true
        );

        // Act
        var approved = _engine.ApprovePost(approveRequest);

        // Assert
        approved.Status.Should().Be(SocialPostStatus.Approved);
        approved.ApproverId.Should().Be("user_growth_lead_42");

        var stored = _engine.GetPost(tenantId, post.Id);
        stored!.Status.Should().Be(SocialPostStatus.Approved);
        stored.ApproverId.Should().Be("user_growth_lead_42");
    }

    [Fact]
    public void ApprovePost_WhenRejected_TransitionsToDraftStatus()
    {
        // Arrange
        var tenantId = "tenant-reject-test";
        var post = _engine.SchedulePost(new SchedulePostRequest(
            TenantId: tenantId,
            Platforms: new List<SocialPlatform> { SocialPlatform.X },
            ContentText: "Draft tweet needing copy refinement.",
            MediaAssetUrls: new List<string>(),
            ScheduledAtUtc: DateTime.UtcNow.AddHours(12)
        ));

        var rejectRequest = new ApprovePostRequest(
            PostId: post.Id,
            TenantId: tenantId,
            ApproverId: "approver_editor_99",
            IsApproved: false
        );

        // Act
        var rejected = _engine.ApprovePost(rejectRequest);

        // Assert
        rejected.Status.Should().Be(SocialPostStatus.Draft);
        rejected.ApproverId.Should().BeNull();

        var stored = _engine.GetPost(tenantId, post.Id);
        stored!.Status.Should().Be(SocialPostStatus.Draft);
    }

    [Fact]
    public void DirectPublishing_WhenApproved_PublishesImmediatelyAndSetsPublishedAtUtc()
    {
        // Arrange
        var tenantId = "tenant-direct-publish";
        var post = _engine.SchedulePost(new SchedulePostRequest(
            TenantId: tenantId,
            Platforms: new List<SocialPlatform> { SocialPlatform.LinkedIn },
            ContentText: "Exciting company milestone achieved today!",
            MediaAssetUrls: new List<string>(),
            ScheduledAtUtc: DateTime.UtcNow.AddDays(7)
        ));

        // State machine requires approval before publishing
        _engine.ApprovePost(new ApprovePostRequest(
            PostId: post.Id,
            TenantId: tenantId,
            ApproverId: "director_marketing",
            IsApproved: true
        ));

        var publishRequest = new PublishNowRequest(post.Id, tenantId);

        // Act
        var published = _engine.PublishNow(publishRequest);

        // Assert
        published.Status.Should().Be(SocialPostStatus.Published);
        published.PublishedAtUtc.Should().NotBeNull();
        published.PublishedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        // Re-publishing an already published post must throw InvalidOperationException
        var republishAct = () => _engine.PublishNow(publishRequest);
        republishAct.Should().Throw<InvalidOperationException>()
            .WithMessage("*already published*");
    }

    [Fact]
    public void PublishNow_WhenNotApproved_ThrowsInvalidOperationException()
    {
        // Arrange
        var tenantId = "tenant-unapproved";
        var post = _engine.SchedulePost(new SchedulePostRequest(
            TenantId: tenantId,
            Platforms: new List<SocialPlatform> { SocialPlatform.X },
            ContentText: "Unapproved post that should not go live directly.",
            MediaAssetUrls: new List<string>(),
            ScheduledAtUtc: DateTime.UtcNow.AddDays(3)
        ));

        var publishRequest = new PublishNowRequest(post.Id, tenantId);

        // Act & Assert
        var act = () => _engine.PublishNow(publishRequest);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*must be Approved*");
    }

    [Fact]
    public void ValidationGuardrails_EnforcesContentAndPlatformRules()
    {
        var tenantId = "tenant-guardrails";

        // 1. Content text cannot be empty
        var emptyContentAct = () => _engine.SchedulePost(new SchedulePostRequest(
            TenantId: tenantId,
            Platforms: new List<SocialPlatform> { SocialPlatform.X },
            ContentText: "   ",
            MediaAssetUrls: new List<string>(),
            ScheduledAtUtc: DateTime.UtcNow.AddHours(1)
        ));
        emptyContentAct.Should().Throw<ArgumentException>()
            .WithMessage("*Content text cannot be empty*");

        // 2. Platforms list cannot be empty
        var emptyPlatformsAct = () => _engine.SchedulePost(new SchedulePostRequest(
            TenantId: tenantId,
            Platforms: new List<SocialPlatform>(),
            ContentText: "Valid text",
            MediaAssetUrls: new List<string>(),
            ScheduledAtUtc: DateTime.UtcNow.AddHours(1)
        ));
        emptyPlatformsAct.Should().Throw<ArgumentException>()
            .WithMessage("*At least one social platform must be selected*");

        // 3. LinkedIn max 3000 chars guardrail
        var longLinkedInText = new string('L', 3001);
        var longLinkedInAct = () => _engine.SchedulePost(new SchedulePostRequest(
            TenantId: tenantId,
            Platforms: new List<SocialPlatform> { SocialPlatform.LinkedIn },
            ContentText: longLinkedInText,
            MediaAssetUrls: new List<string>(),
            ScheduledAtUtc: DateTime.UtcNow.AddHours(1)
        ));
        longLinkedInAct.Should().Throw<ArgumentException>()
            .WithMessage("*3000 characters*");

        // Valid LinkedIn post with exactly 3000 chars succeeds
        var validLinkedInText = new string('L', 3000);
        var validLinkedInPost = _engine.SchedulePost(new SchedulePostRequest(
            TenantId: tenantId,
            Platforms: new List<SocialPlatform> { SocialPlatform.LinkedIn },
            ContentText: validLinkedInText,
            MediaAssetUrls: new List<string>(),
            ScheduledAtUtc: DateTime.UtcNow.AddHours(1)
        ));
        validLinkedInPost.Should().NotBeNull();
    }

    [Fact]
    public void ValidationGuardrails_EnforcesInstagramMediaAndAspectRatioChecks()
    {
        var tenantId = "tenant-instagram";

        // 1. Instagram requires at least 1 media asset
        var noMediaAct = () => _engine.SchedulePost(new SchedulePostRequest(
            TenantId: tenantId,
            Platforms: new List<SocialPlatform> { SocialPlatform.Instagram },
            ContentText: "Post without media",
            MediaAssetUrls: new List<string>(),
            ScheduledAtUtc: DateTime.UtcNow.AddHours(2)
        ));
        noMediaAct.Should().Throw<ArgumentException>()
            .WithMessage("*Instagram posts require at least one media asset*");

        // 2. Instagram rejects unsupported aspect ratio (e.g. 3:1 or 2:1)
        var invalidAspectAct = () => _engine.SchedulePost(new SchedulePostRequest(
            TenantId: tenantId,
            Platforms: new List<SocialPlatform> { SocialPlatform.Instagram },
            ContentText: "Post with bad aspect ratio",
            MediaAssetUrls: new List<string> { "https://cdn.sgrowing.io/banner.jpg?aspect=3:1" },
            ScheduledAtUtc: DateTime.UtcNow.AddHours(2)
        ));
        invalidAspectAct.Should().Throw<ArgumentException>()
            .WithMessage("*Instagram does not support aspect ratio '3:1'*");

        // 3. Instagram accepts supported aspect ratios: 1:1, 4:5, 9:16, 16:9
        var supportedRatios = new[] { "1:1", "4:5", "9:16", "16:9" };
        foreach (var ratio in supportedRatios)
        {
            var validPost = _engine.SchedulePost(new SchedulePostRequest(
                TenantId: tenantId,
                Platforms: new List<SocialPlatform> { SocialPlatform.Instagram },
                ContentText: $"Post with valid aspect ratio {ratio}",
                MediaAssetUrls: new List<string> { $"https://cdn.sgrowing.io/pic.jpg?aspect={ratio}" },
                ScheduledAtUtc: DateTime.UtcNow.AddHours(2)
            ));
            validPost.Should().NotBeNull();
        }
    }

    [Fact]
    public void GetCalendar_FiltersPostsByTenantAndDateRange()
    {
        // Arrange
        var tenantA = "tenant-calendar-alpha";
        var tenantB = "tenant-calendar-beta";
        var baseTime = DateTime.UtcNow;

        // Post 1: Tenant A, within target range (Day +2)
        var postInRange = _engine.SchedulePost(new SchedulePostRequest(
            TenantId: tenantA,
            Platforms: new List<SocialPlatform> { SocialPlatform.LinkedIn },
            ContentText: "In range post",
            MediaAssetUrls: new List<string>(),
            ScheduledAtUtc: baseTime.AddDays(2)
        ));

        // Post 2: Tenant A, outside target range (Day +15)
        _engine.SchedulePost(new SchedulePostRequest(
            TenantId: tenantA,
            Platforms: new List<SocialPlatform> { SocialPlatform.X },
            ContentText: "Out of range post",
            MediaAssetUrls: new List<string>(),
            ScheduledAtUtc: baseTime.AddDays(15)
        ));

        // Post 3: Tenant B, within target range (different tenant)
        _engine.SchedulePost(new SchedulePostRequest(
            TenantId: tenantB,
            Platforms: new List<SocialPlatform> { SocialPlatform.Facebook },
            ContentText: "Tenant B post",
            MediaAssetUrls: new List<string>(),
            ScheduledAtUtc: baseTime.AddDays(2)
        ));

        // Act
        var calendar = _engine.GetCalendar(tenantA, baseTime.AddDays(1), baseTime.AddDays(5));

        // Assert
        calendar.Should().ContainSingle();
        calendar[0].Id.Should().Be(postInRange.Id);
        calendar[0].TenantId.Should().Be(tenantA);
        calendar[0].ContentText.Should().Be("In range post");
    }

    [Fact]
    public void IngestAnalytics_AggregatesMetricsAcrossMultiplePlatforms()
    {
        // Arrange
        var tenantId = "tenant-analytics-engine";
        var post = _engine.SchedulePost(new SchedulePostRequest(
            TenantId: tenantId,
            Platforms: new List<SocialPlatform> { SocialPlatform.LinkedIn, SocialPlatform.Instagram, SocialPlatform.X },
            ContentText: "Omnichannel launch event announcement",
            MediaAssetUrls: new List<string> { "https://cdn.sgrowing.io/launch.jpg?aspect=1:1" },
            ScheduledAtUtc: DateTime.UtcNow.AddHours(1)
        ));

        _engine.ApprovePost(new ApprovePostRequest(post.Id, tenantId, "growth_lead", true));
        _engine.PublishNow(new PublishNowRequest(post.Id, tenantId));

        // Act 1: Ingest Instagram payload
        var igPayload = new SocialAnalyticsWebhookPayload(
            PostId: post.Id,
            TenantId: tenantId,
            Platform: SocialPlatform.Instagram,
            Impressions: 1200,
            Clicks: 85,
            Likes: 210,
            Shares: 18
        );
        var postAfterIg = _engine.IngestAnalytics(igPayload);

        // Assert 1
        postAfterIg.Metrics["Impressions"].Should().Be(1200);
        postAfterIg.Metrics["Clicks"].Should().Be(85);
        postAfterIg.Metrics["Likes"].Should().Be(210);
        postAfterIg.Metrics["Shares"].Should().Be(18);
        postAfterIg.Metrics["Instagram_Impressions"].Should().Be(1200);

        // Act 2: Ingest LinkedIn payload (aggregates)
        var liPayload = new SocialAnalyticsWebhookPayload(
            PostId: post.Id,
            TenantId: tenantId,
            Platform: SocialPlatform.LinkedIn,
            Impressions: 3400,
            Clicks: 140,
            Likes: 95,
            Shares: 32
        );
        var postAfterLi = _engine.IngestAnalytics(liPayload);

        // Assert 2: Summed totals
        postAfterLi.Metrics["Impressions"].Should().Be(1200 + 3400);
        postAfterLi.Metrics["Clicks"].Should().Be(85 + 140);
        postAfterLi.Metrics["Likes"].Should().Be(210 + 95);
        postAfterLi.Metrics["Shares"].Should().Be(18 + 32);
        postAfterLi.Metrics["LinkedIn_Impressions"].Should().Be(3400);
        postAfterLi.Metrics["Instagram_Impressions"].Should().Be(1200);
    }

    [Fact]
    public void IngestAnalytics_WithNonExistentPost_ThrowsKeyNotFoundException()
    {
        // Arrange
        var missingPayload = new SocialAnalyticsWebhookPayload(
            PostId: Guid.NewGuid(),
            TenantId: "tenant-missing",
            Platform: SocialPlatform.TikTok,
            Impressions: 50,
            Clicks: 2,
            Likes: 1,
            Shares: 0
        );

        // Act & Assert
        var act = () => _engine.IngestAnalytics(missingPayload);
        act.Should().Throw<KeyNotFoundException>()
            .WithMessage("*was not found*");
    }
}
