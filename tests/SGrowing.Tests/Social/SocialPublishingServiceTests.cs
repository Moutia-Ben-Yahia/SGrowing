using SGrowing.Api.Modules.Social;
using Xunit;

namespace SGrowing.Tests.Social;

public class SocialPublishingServiceTests
{
    private readonly SocialPublishingService _service;

    public SocialPublishingServiceTests()
    {
        _service = new SocialPublishingService();
        _service.ResetToDefaultSeed();
    }

    // =========================================================================
    // 1. Post Composition & Platform Overrides
    // =========================================================================

    [Fact]
    public async Task ComposePostAsync_WithValidRequest_ReturnsDraftPostWithCorrectProperties()
    {
        var request = new ComposePostRequest(
            Title: "Exciting New Update",
            Content: "We launched an amazing new feature today!",
            TargetPlatforms: new List<string> { "X", "LinkedIn" },
            Tags: new List<string> { "Launch", "SaaS" }
        );

        var post = await _service.ComposePostAsync(request);

        Assert.NotNull(post);
        Assert.NotEqual(Guid.Empty, post.Id);
        Assert.Equal("Exciting New Update", post.Title);
        Assert.Equal("We launched an amazing new feature today!", post.Content);
        Assert.Equal(PublishingStatus.Draft, post.Status);
        Assert.Contains("X", post.TargetPlatforms);
        Assert.Contains("LinkedIn", post.TargetPlatforms);
        Assert.Contains("Launch", post.Tags);
    }

    [Fact]
    public async Task ComposePostAsync_WithScheduledDateInFuture_InitializesAsScheduled()
    {
        var futureDate = DateTime.UtcNow.AddDays(5);
        var request = new ComposePostRequest(
            Title: "Future Scheduled Post",
            Content: "Scheduled for next week launch event.",
            TargetPlatforms: new List<string> { "LinkedIn" },
            ScheduledAtUtc: futureDate
        );

        var post = await _service.ComposePostAsync(request);

        Assert.Equal(PublishingStatus.Scheduled, post.Status);
        Assert.Equal(futureDate, post.ScheduledAtUtc);
    }

    [Fact]
    public async Task ComposePostAsync_WithPlatformOverrides_StoresOverridesCorrectly()
    {
        var overrides = new Dictionary<string, string>
        {
            ["X"] = "Short punchy tweet #SaaS",
            ["LinkedIn"] = "Extensive professional overview with full strategic insights."
        };

        var request = new ComposePostRequest(
            Title: "Multi-Platform Post",
            Content: "Base generic content.",
            TargetPlatforms: new List<string> { "X", "LinkedIn" },
            PlatformOverrides: overrides
        );

        var post = await _service.ComposePostAsync(request);

        Assert.Equal(2, post.PlatformOverrides.Count);
        Assert.Equal("Short punchy tweet #SaaS", post.PlatformOverrides["X"]);
        Assert.Equal("Extensive professional overview with full strategic insights.", post.PlatformOverrides["LinkedIn"]);
    }

    [Fact]
    public async Task ComposePostAsync_ThrowsWhenContentIsEmpty()
    {
        var request = new ComposePostRequest(
            Title: "No Content",
            Content: "   ",
            TargetPlatforms: new List<string> { "X" }
        );

        await Assert.ThrowsAsync<ArgumentException>(() => _service.ComposePostAsync(request));
    }

    [Fact]
    public async Task ComposePostAsync_ThrowsWhenTargetPlatformsEmpty()
    {
        var request = new ComposePostRequest(
            Title: "No Platforms",
            Content: "Some content here",
            TargetPlatforms: new List<string>()
        );

        await Assert.ThrowsAsync<ArgumentException>(() => _service.ComposePostAsync(request));
    }

    [Fact]
    public void ResolveContentForPlatform_UsesPlatformOverrideWhenPresent()
    {
        var post = new UnifiedSocialPost
        {
            Content = "Base content",
            PlatformOverrides = new Dictionary<string, string>
            {
                ["X"] = "Custom X tweet"
            }
        };

        var content = SocialPublishingService.ResolveContentForPlatform(post, "X");

        Assert.Equal("Custom X tweet", content);
    }

    [Fact]
    public void ResolveContentForPlatform_FallsBackToBaseContentWhenOverrideMissing()
    {
        var post = new UnifiedSocialPost
        {
            Content = "Base content for all networks",
            PlatformOverrides = new Dictionary<string, string>
            {
                ["LinkedIn"] = "LinkedIn special copy"
            }
        };

        var content = SocialPublishingService.ResolveContentForPlatform(post, "X");

        Assert.Equal("Base content for all networks", content);
    }

    // =========================================================================
    // 2. Platform Validation: X / Twitter Rules
    // =========================================================================

    [Fact]
    public void Validation_X_PassesWhenWithin280Characters()
    {
        var post = new UnifiedSocialPost
        {
            Content = "Short tweet well within 280 characters limit.",
            TargetPlatforms = new List<string> { "X" }
        };

        var report = _service.ValidatePost(post);

        Assert.True(report.IsValid);
        Assert.True(report.PlatformResults["X"].IsValid);
        Assert.Empty(report.PlatformResults["X"].Errors);
    }

    [Fact]
    public void Validation_X_FailsWhenExceeding280Characters()
    {
        var longText = new string('A', 281);
        var post = new UnifiedSocialPost
        {
            Content = longText,
            TargetPlatforms = new List<string> { "X" }
        };

        var report = _service.ValidatePost(post);

        Assert.False(report.IsValid);
        Assert.False(report.PlatformResults["X"].IsValid);
        Assert.Contains(report.PlatformResults["X"].Errors, e => e.Contains("cannot exceed 280 characters"));
    }

    [Fact]
    public void Validation_X_CalculatesTwitterUrlAs23Characters()
    {
        // 100 character long URL + 258 characters = 358 raw chars.
        // But with Twitter standard 23 chars for URL: 23 + 257 = 280 chars => Valid!
        var longUrl = "https://example.com/very/long/deep/link/with/lots/of/parameters?query=enterprise&campaign=q4&ref=sgrowing";
        var prefix = new string('x', 257); // 257 + 23 = 280 chars
        var post = new UnifiedSocialPost
        {
            Content = $"{prefix} {longUrl}",
            TargetPlatforms = new List<string> { "X" }
        };

        // Note: prefix (257) + space (1) + url (23) = 281, which is > 280
        var calculatedLength = SocialPublishingService.CalculateTwitterLength(post.Content);
        Assert.Equal(257 + 1 + 23, calculatedLength);

        // Within limit test: 256 + 1 + 23 = 280
        var validPrefix = new string('x', 256);
        post.Content = $"{validPrefix} {longUrl}";
        var report = _service.ValidatePost(post);

        Assert.True(report.IsValid);
        Assert.Equal(280, report.PlatformResults["X"].CharacterCount);
    }

    [Fact]
    public void Validation_X_EnforcesMaximum4MediaAttachments()
    {
        var post = new UnifiedSocialPost
        {
            Content = "Tweet with 5 images",
            TargetPlatforms = new List<string> { "X" },
            MediaAttachments = new List<SocialMediaAttachment>
            {
                new("https://img.io/1.jpg"),
                new("https://img.io/2.jpg"),
                new("https://img.io/3.jpg"),
                new("https://img.io/4.jpg"),
                new("https://img.io/5.jpg")
            }
        };

        var report = _service.ValidatePost(post);

        Assert.False(report.IsValid);
        Assert.Contains(report.PlatformResults["X"].Errors, e => e.Contains("maximum of 4 media attachments"));
    }

    [Fact]
    public void Validation_X_AllowsUpTo4MediaAttachments()
    {
        var post = new UnifiedSocialPost
        {
            Content = "Tweet with exactly 4 images",
            TargetPlatforms = new List<string> { "X" },
            MediaAttachments = new List<SocialMediaAttachment>
            {
                new("https://img.io/1.jpg"),
                new("https://img.io/2.jpg"),
                new("https://img.io/3.jpg"),
                new("https://img.io/4.jpg")
            }
        };

        var report = _service.ValidatePost(post);

        Assert.True(report.IsValid);
    }

    // =========================================================================
    // 3. Platform Validation: Instagram Business Rules
    // =========================================================================

    [Fact]
    public void Validation_Instagram_RequiresAtLeastOneImageOrVideo()
    {
        var post = new UnifiedSocialPost
        {
            Content = "Instagram post without any image or video",
            TargetPlatforms = new List<string> { "Instagram" },
            MediaAttachments = new List<SocialMediaAttachment>()
        };

        var report = _service.ValidatePost(post);

        Assert.False(report.IsValid);
        Assert.Contains(report.PlatformResults["Instagram"].Errors, e => e.Contains("requires at least 1 image or video"));
    }

    [Fact]
    public void Validation_Instagram_PassesWithValidImageAttachment()
    {
        var post = new UnifiedSocialPost
        {
            Content = "Instagram caption with great visuals #grow",
            TargetPlatforms = new List<string> { "Instagram" },
            MediaAttachments = new List<SocialMediaAttachment>
            {
                new("https://img.io/photo.jpg", AspectRatio: "1:1", MediaType: "image")
            }
        };

        var report = _service.ValidatePost(post);

        Assert.True(report.IsValid);
        Assert.True(report.PlatformResults["Instagram"].IsValid);
    }

    [Theory]
    [InlineData("1:1")]
    [InlineData("4:5")]
    [InlineData("9:16")]
    public void Validation_Instagram_ValidatesSupportedAspectRatios_1to1_4to5_9to16(string aspectRatio)
    {
        var post = new UnifiedSocialPost
        {
            Content = "Instagram photo test",
            TargetPlatforms = new List<string> { "Instagram" },
            MediaAttachments = new List<SocialMediaAttachment>
            {
                new("https://img.io/test.jpg", AspectRatio: aspectRatio, MediaType: "image")
            }
        };

        var report = _service.ValidatePost(post);

        Assert.True(report.IsValid);
    }

    [Fact]
    public void Validation_Instagram_FailsWithUnsupportedAspectRatio()
    {
        var post = new UnifiedSocialPost
        {
            Content = "Instagram wide banner test",
            TargetPlatforms = new List<string> { "Instagram" },
            MediaAttachments = new List<SocialMediaAttachment>
            {
                new("https://img.io/banner.jpg", AspectRatio: "16:9", MediaType: "image")
            }
        };

        var report = _service.ValidatePost(post);

        Assert.False(report.IsValid);
        Assert.Contains(report.PlatformResults["Instagram"].Errors, e => e.Contains("Supported aspect ratios: 1:1, 4:5, 9:16"));
    }

    [Fact]
    public void Validation_Instagram_EnforcesMax30Hashtags_PassesWhenUnderLimit()
    {
        var tags = Enumerable.Range(1, 28).Select(i => $"tag{i}").ToList();
        var post = new UnifiedSocialPost
        {
            Content = "Post with 28 tags",
            TargetPlatforms = new List<string> { "Instagram" },
            Tags = tags,
            MediaAttachments = new List<SocialMediaAttachment> { new("https://img.io/pic.jpg", "1:1") }
        };

        var report = _service.ValidatePost(post);

        Assert.True(report.IsValid);
    }

    [Fact]
    public void Validation_Instagram_FailsWhenExceeding30Hashtags()
    {
        var tags = Enumerable.Range(1, 32).Select(i => $"tag{i}").ToList();
        var post = new UnifiedSocialPost
        {
            Content = "Post with 32 tags",
            TargetPlatforms = new List<string> { "Instagram" },
            Tags = tags,
            MediaAttachments = new List<SocialMediaAttachment> { new("https://img.io/pic.jpg", "1:1") }
        };

        var report = _service.ValidatePost(post);

        Assert.False(report.IsValid);
        Assert.Contains(report.PlatformResults["Instagram"].Errors, e => e.Contains("cannot exceed 30 hashtags"));
    }

    [Fact]
    public void Validation_Instagram_EnforcesMax2200Characters()
    {
        var longCaption = new string('I', 2201);
        var post = new UnifiedSocialPost
        {
            Content = longCaption,
            TargetPlatforms = new List<string> { "Instagram" },
            MediaAttachments = new List<SocialMediaAttachment> { new("https://img.io/pic.jpg", "1:1") }
        };

        var report = _service.ValidatePost(post);

        Assert.False(report.IsValid);
        Assert.Contains(report.PlatformResults["Instagram"].Errors, e => e.Contains("caption cannot exceed 2,200 characters"));
    }

    // =========================================================================
    // 4. Platform Validation: LinkedIn Rules
    // =========================================================================

    [Fact]
    public void Validation_LinkedIn_PassesWhenWithin3000Characters()
    {
        var content = new string('L', 2999);
        var post = new UnifiedSocialPost
        {
            Content = content,
            TargetPlatforms = new List<string> { "LinkedIn" }
        };

        var report = _service.ValidatePost(post);

        Assert.True(report.IsValid);
        Assert.Equal(2999, report.PlatformResults["LinkedIn"].CharacterCount);
    }

    [Fact]
    public void Validation_LinkedIn_FailsWhenExceeding3000Characters()
    {
        var content = new string('L', 3001);
        var post = new UnifiedSocialPost
        {
            Content = content,
            TargetPlatforms = new List<string> { "LinkedIn" }
        };

        var report = _service.ValidatePost(post);

        Assert.False(report.IsValid);
        Assert.Contains(report.PlatformResults["LinkedIn"].Errors, e => e.Contains("cannot exceed 3,000 characters"));
    }

    [Fact]
    public void Validation_LinkedIn_SupportsImagesVideoAndPdfCarousels()
    {
        var post = new UnifiedSocialPost
        {
            Content = "LinkedIn Presentation deck",
            TargetPlatforms = new List<string> { "LinkedIn" },
            MediaAttachments = new List<SocialMediaAttachment>
            {
                new("https://files.io/deck.pdf", AspectRatio: "1:1", MediaType: "pdf")
            }
        };

        var report = _service.ValidatePost(post);

        Assert.True(report.IsValid);
    }

    // =========================================================================
    // 5. Platform Validation: TikTok Rules
    // =========================================================================

    [Fact]
    public void Validation_TikTok_RequiresVideoAttachment()
    {
        var post = new UnifiedSocialPost
        {
            Content = "TikTok post with only a static photo",
            TargetPlatforms = new List<string> { "TikTok" },
            MediaAttachments = new List<SocialMediaAttachment>
            {
                new("https://img.io/photo.jpg", "9:16", "image")
            }
        };

        var report = _service.ValidatePost(post);

        Assert.False(report.IsValid);
        Assert.Contains(report.PlatformResults["TikTok"].Errors, e => e.Contains("requires at least one video attachment"));
    }

    [Fact]
    public void Validation_TikTok_PassesWithMp4VideoAttachment()
    {
        var post = new UnifiedSocialPost
        {
            Content = "Viral clip! #tech",
            TargetPlatforms = new List<string> { "TikTok" },
            MediaAttachments = new List<SocialMediaAttachment>
            {
                new("https://video.io/clip.mp4", "9:16", "video")
            }
        };

        var report = _service.ValidatePost(post);

        Assert.True(report.IsValid);
    }

    [Fact]
    public void Validation_TikTok_FailsWhenCaptionExceeds2200Characters()
    {
        var post = new UnifiedSocialPost
        {
            Content = new string('T', 2205),
            TargetPlatforms = new List<string> { "TikTok" },
            MediaAttachments = new List<SocialMediaAttachment>
            {
                new("https://video.io/clip.mp4", "9:16", "video")
            }
        };

        var report = _service.ValidatePost(post);

        Assert.False(report.IsValid);
        Assert.Contains(report.PlatformResults["TikTok"].Errors, e => e.Contains("caption cannot exceed 2,200 characters"));
    }

    // =========================================================================
    // 6. Calendar Filtering & Retrieval
    // =========================================================================

    [Fact]
    public async Task Calendar_FiltersByMonthAndYearCorrectly()
    {
        var now = DateTime.UtcNow;
        var posts = await _service.GetCalendarPostsAsync(year: now.Year, month: now.Month);

        Assert.NotEmpty(posts);
        foreach (var post in posts)
        {
            var date = post.ScheduledAtUtc ?? post.PublishedAtUtc ?? post.CreatedAtUtc;
            Assert.Equal(now.Year, date.Year);
            Assert.Equal(now.Month, date.Month);
        }
    }

    [Fact]
    public async Task Calendar_FiltersByDateRangeStartAndEnd()
    {
        var start = DateTime.UtcNow.Date;
        var end = DateTime.UtcNow.Date.AddDays(2).AddHours(23).AddMinutes(59);

        var posts = await _service.GetCalendarPostsAsync(startDate: start, endDate: end);

        Assert.NotNull(posts);
        foreach (var post in posts)
        {
            var date = post.ScheduledAtUtc ?? post.PublishedAtUtc ?? post.CreatedAtUtc;
            Assert.True(date >= start && date <= end);
        }
    }

    [Fact]
    public async Task Calendar_FiltersByPlatformCorrectly()
    {
        var posts = await _service.GetCalendarPostsAsync(platform: "Instagram");

        Assert.NotEmpty(posts);
        Assert.All(posts, p => Assert.Contains("Instagram", p.TargetPlatforms));
    }

    [Fact]
    public async Task Calendar_FiltersByPublishingStatus()
    {
        var scheduledPosts = await _service.GetCalendarPostsAsync(status: PublishingStatus.Scheduled);

        Assert.NotEmpty(scheduledPosts);
        Assert.All(scheduledPosts, p => Assert.Equal(PublishingStatus.Scheduled, p.Status));
    }

    // =========================================================================
    // 7. State Machine Transitions
    // =========================================================================

    [Fact]
    public async Task StateMachine_TransitionsFromDraftToScheduled()
    {
        var draft = await _service.ComposePostAsync(new ComposePostRequest(
            Title: "Draft to Schedule",
            Content: "Ready to go live next week.",
            TargetPlatforms: new List<string> { "LinkedIn" }
        ));

        Assert.Equal(PublishingStatus.Draft, draft.Status);

        var targetTime = DateTime.UtcNow.AddDays(7);
        var scheduled = await _service.SchedulePostAsync(draft.Id, targetTime);

        Assert.Equal(PublishingStatus.Scheduled, scheduled.Status);
        Assert.Equal(targetTime, scheduled.ScheduledAtUtc);
    }

    [Fact]
    public async Task StateMachine_PreventsSchedulingInvalidPost()
    {
        var invalidDraft = new UnifiedSocialPost
        {
            Title = "Invalid Draft",
            Content = new string('A', 300), // Exceeds X's 280 chars
            TargetPlatforms = new List<string> { "X" },
            Status = PublishingStatus.Draft
        };

        // Inject directly into service collection for test
        var prop = typeof(SocialPublishingService).GetField("_posts", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var dict = (System.Collections.Concurrent.ConcurrentDictionary<Guid, UnifiedSocialPost>)prop.GetValue(_service)!;
        dict[invalidDraft.Id] = invalidDraft;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.SchedulePostAsync(invalidDraft.Id, DateTime.UtcNow.AddDays(1)));
    }

    [Fact]
    public async Task StateMachine_PreventsSchedulingAlreadyPublishedPost()
    {
        var publishedPost = (await _service.GetCalendarPostsAsync(status: PublishingStatus.Published)).First();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.SchedulePostAsync(publishedPost.Id, DateTime.UtcNow.AddDays(1)));
    }

    [Fact]
    public async Task StateMachine_AllowsCancellingScheduledPost()
    {
        var scheduledPost = (await _service.GetCalendarPostsAsync(status: PublishingStatus.Scheduled)).First();

        var cancelled = await _service.CancelScheduledPostAsync(scheduledPost.Id, "Shifted marketing strategy");

        Assert.Equal(PublishingStatus.Cancelled, cancelled.Status);
        Assert.Equal("Shifted marketing strategy", cancelled.FailureReason);
    }

    [Fact]
    public async Task StateMachine_PreventsCancellingPublishedPost()
    {
        var publishedPost = (await _service.GetCalendarPostsAsync(status: PublishingStatus.Published)).First();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CancelScheduledPostAsync(publishedPost.Id));
    }

    // =========================================================================
    // 8. Immediate Publish & Simulated Dispatch
    // =========================================================================

    [Fact]
    public async Task PublishNowAsync_SuccessfullyDispatchesToAllTargetPlatforms()
    {
        var post = await _service.ComposePostAsync(new ComposePostRequest(
            Title: "Immediate Flash Sale",
            Content: "Flash 50% off starts right now! https://sgrowing.io/deal",
            TargetPlatforms: new List<string> { "X", "LinkedIn" }
        ));

        var published = await _service.PublishNowAsync(post.Id);

        Assert.Equal(PublishingStatus.Published, published.Status);
        Assert.NotNull(published.PublishedAtUtc);
        Assert.Equal(2, published.DispatchResults.Count);
        Assert.All(published.DispatchResults, r => Assert.True(r.Success));
    }

    [Fact]
    public async Task PublishNowAsync_RecordsNetworkLatencyAndExternalPostId()
    {
        var post = await _service.ComposePostAsync(new ComposePostRequest(
            Title: "Latency Test Post",
            Content: "Testing network dispatch latency recording.",
            TargetPlatforms: new List<string> { "X" }
        ));

        var published = await _service.PublishNowAsync(post.Id);
        var result = published.DispatchResults.Single(r => r.Platform == "X");

        Assert.True(result.LatencyMs > 0, "Network latency must be tracked and greater than 0ms.");
        Assert.StartsWith("x_", result.ExternalPostId);
        Assert.Contains("Dispatched successfully", result.Message);
    }

    [Fact]
    public async Task PublishNowAsync_FailsPostWhenValidationRulesAreViolated()
    {
        var invalidPost = new UnifiedSocialPost
        {
            Title = "TikTok Missing Video",
            Content = "TikTok text only post",
            TargetPlatforms = new List<string> { "TikTok" },
            Status = PublishingStatus.Draft
        };

        var prop = typeof(SocialPublishingService).GetField("_posts", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var dict = (System.Collections.Concurrent.ConcurrentDictionary<Guid, UnifiedSocialPost>)prop.GetValue(_service)!;
        dict[invalidPost.Id] = invalidPost;

        var result = await _service.PublishNowAsync(invalidPost.Id);

        Assert.Equal(PublishingStatus.Failed, result.Status);
        Assert.NotNull(result.FailureReason);
        Assert.Contains("requires at least one video attachment", result.FailureReason);
    }

    [Fact]
    public async Task PublishNowAsync_ThrowsWhenPostNotFound()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.PublishNowAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task PublishNowAsync_ThrowsWhenPostAlreadyPublished()
    {
        var post = (await _service.GetCalendarPostsAsync(status: PublishingStatus.Published)).First();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.PublishNowAsync(post.Id));
    }

    [Fact]
    public async Task ProcessDueScheduledPostsAsync_PublishesDuePostsAutomatically()
    {
        // Compose a post scheduled 1 minute ago (due)
        var pastTime = DateTime.UtcNow.AddMinutes(-5);
        var post = await _service.ComposePostAsync(new ComposePostRequest(
            Title: "Due Post",
            Content: "This post was due 5 minutes ago and should auto-publish.",
            TargetPlatforms: new List<string> { "LinkedIn" },
            ScheduledAtUtc: pastTime
        ));
        // Force status to Scheduled
        post.Status = PublishingStatus.Scheduled;

        var count = await _service.ProcessDueScheduledPostsAsync(DateTime.UtcNow);

        Assert.True(count >= 1);
        var updated = await _service.GetPostByIdAsync(post.Id);
        Assert.Equal(PublishingStatus.Published, updated!.Status);
    }

    // =========================================================================
    // 9. OAuth 2.0 Token Health & Proactive Refresh
    // =========================================================================

    [Fact]
    public void OAuth_TokenStatusClassification_ClassifiesActiveWhenMoreThan72Hours()
    {
        var account = new ConnectedSocialAccount
        {
            TokenExpiresAtUtc = DateTime.UtcNow.AddDays(14)
        };

        Assert.Equal(TokenHealthStatus.Active, account.TokenStatus);
    }

    [Fact]
    public void OAuth_TokenStatusClassification_ClassifiesExpiringSoonWithin72Hours()
    {
        var account = new ConnectedSocialAccount
        {
            TokenExpiresAtUtc = DateTime.UtcNow.AddHours(48)
        };

        Assert.Equal(TokenHealthStatus.ExpiringSoon, account.TokenStatus);
    }

    [Fact]
    public void OAuth_TokenStatusClassification_ClassifiesExpiredWhenPastExpiration()
    {
        var account = new ConnectedSocialAccount
        {
            TokenExpiresAtUtc = DateTime.UtcNow.AddMinutes(-10)
        };

        Assert.Equal(TokenHealthStatus.Expired, account.TokenStatus);
    }

    [Fact]
    public async Task OAuth_RefreshTokenAsync_ExtendsValidityBy60DaysAndIssuesNewTokens()
    {
        var accounts = await _service.GetConnectedAccountsAsync();
        var igAccount = accounts.First(a => a.Platform == "Instagram");
        var oldToken = igAccount.AccessToken;

        var refreshed = await _service.RefreshTokenAsync(igAccount.Id);

        Assert.NotEqual(oldToken, refreshed.AccessToken);
        Assert.True(refreshed.TokenExpiresAtUtc > DateTime.UtcNow.AddDays(50));
        Assert.Equal(TokenHealthStatus.Active, refreshed.TokenStatus);
    }

    [Fact]
    public async Task OAuth_RefreshTokenAsync_ThrowsWhenAccountNotFound()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.RefreshTokenAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task OAuth_SweepAndAutoRefreshTokenHealthAsync_AutoRefreshesExpiringAccounts()
    {
        // Seed default has Instagram account expiring in 36 hours (ExpiringSoon)
        var refreshedCount = await _service.SweepAndAutoRefreshTokenHealthAsync();

        Assert.True(refreshedCount >= 1);

        var accounts = await _service.GetConnectedAccountsAsync();
        var igAccount = accounts.First(a => a.Platform == "Instagram");
        Assert.Equal(TokenHealthStatus.Active, igAccount.TokenStatus);
    }

    // =========================================================================
    // 10. Concurrency & Multithreaded Scheduling Safety
    // =========================================================================

    [Fact]
    public async Task Concurrency_MultithreadedConcurrentScheduling_IsThreadSafeAndConsistent()
    {
        var tasks = Enumerable.Range(1, 40).Select(async i =>
        {
            var post = await _service.ComposePostAsync(new ComposePostRequest(
                Title: $"Concurrent Post {i}",
                Content: $"Body content for concurrent task {i}",
                TargetPlatforms: new List<string> { "LinkedIn" },
                ScheduledAtUtc: DateTime.UtcNow.AddHours(i)
            ));
            return post.Id;
        });

        var createdIds = await Task.WhenAll(tasks);

        Assert.Equal(40, createdIds.Distinct().Count());
        var allPosts = await _service.GetAllPostsAsync();
        Assert.True(allPosts.Count >= 40);
    }

    [Fact]
    public async Task Concurrency_ConcurrentPublishAttempts_OnlyDispatchesOnce()
    {
        var post = await _service.ComposePostAsync(new ComposePostRequest(
            Title: "Race Condition Publish Test",
            Content: "Only one execution thread should succeed.",
            TargetPlatforms: new List<string> { "X" }
        ));

        int successCount = 0;
        int failureCount = 0;

        var tasks = Enumerable.Range(1, 10).Select(async _ =>
        {
            try
            {
                var published = await _service.PublishNowAsync(post.Id);
                if (published.Status == PublishingStatus.Published)
                {
                    Interlocked.Increment(ref successCount);
                }
            }
            catch (InvalidOperationException)
            {
                Interlocked.Increment(ref failureCount);
            }
        });

        await Task.WhenAll(tasks);

        Assert.Equal(1, successCount);
        Assert.Equal(9, failureCount);
    }
}
