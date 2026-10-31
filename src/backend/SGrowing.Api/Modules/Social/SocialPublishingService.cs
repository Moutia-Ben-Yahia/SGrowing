using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace SGrowing.Api.Modules.Social;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PublishingStatus
{
    Draft,
    Scheduled,
    Publishing,
    Published,
    Failed,
    Cancelled
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TokenHealthStatus
{
    Active,
    ExpiringSoon,
    Expired
}

public record SocialMediaAttachment(
    string Url,
    string AspectRatio = "1:1",
    string MediaType = "image", // "image", "video", "pdf"
    string? ThumbnailUrl = null,
    string? AltText = null
);

public record PlatformDispatchResult(
    string Platform,
    bool Success,
    string? ExternalPostId,
    long LatencyMs,
    string? Message,
    DateTime DispatchedAtUtc
);

public record PlatformValidationResult(
    string Platform,
    bool IsValid,
    List<string> Errors,
    List<string> Warnings,
    int CharacterCount,
    int MaxCharacters
);

public record ValidationReport(
    bool IsValid,
    List<string> Errors,
    List<string> Warnings,
    Dictionary<string, PlatformValidationResult> PlatformResults
);

public class UnifiedSocialPost
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; } = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public Dictionary<string, string> PlatformOverrides { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<SocialMediaAttachment> MediaAttachments { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    public List<string> TargetPlatforms { get; set; } = new();
    public DateTime? ScheduledAtUtc { get; set; }
    public DateTime? PublishedAtUtc { get; set; }
    public PublishingStatus Status { get; set; } = PublishingStatus.Draft;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public List<PlatformDispatchResult> DispatchResults { get; set; } = new();
    public string? FailureReason { get; set; }
}

public class ConnectedSocialAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; } = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public string Platform { get; set; } = string.Empty; // "X", "Instagram", "LinkedIn", "TikTok"
    public string AccountType { get; set; } = string.Empty; // "X Account", "Meta Page", "LinkedIn Org", "TikTok Creator"
    public string AccountName { get; set; } = string.Empty;
    public string Handle { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime TokenExpiresAtUtc { get; set; }
    public DateTime LastRefreshedAtUtc { get; set; } = DateTime.UtcNow;
    public bool AutoRefreshEnabled { get; set; } = true;
    public int FollowersCount { get; set; } = 0;

    public TokenHealthStatus TokenStatus => GetTokenStatus(DateTime.UtcNow);

    public TokenHealthStatus GetTokenStatus(DateTime asOfUtc)
    {
        if (TokenExpiresAtUtc <= asOfUtc) return TokenHealthStatus.Expired;
        if (TokenExpiresAtUtc <= asOfUtc.AddHours(72)) return TokenHealthStatus.ExpiringSoon;
        return TokenHealthStatus.Active;
    }

    public double RemainingDays => Math.Max(0, Math.Round((TokenExpiresAtUtc - DateTime.UtcNow).TotalDays, 1));
}

public record ComposePostRequest(
    string Title,
    string Content,
    List<string> TargetPlatforms,
    Dictionary<string, string>? PlatformOverrides = null,
    List<SocialMediaAttachment>? MediaAttachments = null,
    List<string>? Tags = null,
    DateTime? ScheduledAtUtc = null,
    Guid? TenantId = null
);

public record SchedulePostRequest(
    Guid PostId,
    DateTime ScheduledAtUtc
);

public interface ISocialPublishingService
{
    Task<UnifiedSocialPost> ComposePostAsync(ComposePostRequest request, CancellationToken ct = default);
    Task<UnifiedSocialPost?> GetPostByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<UnifiedSocialPost>> GetAllPostsAsync(Guid? tenantId = null, CancellationToken ct = default);
    
    ValidationReport ValidatePost(UnifiedSocialPost post);
    ValidationReport ValidateRequest(ComposePostRequest request);
    
    Task<UnifiedSocialPost> SchedulePostAsync(Guid id, DateTime scheduledAtUtc, CancellationToken ct = default);
    Task<UnifiedSocialPost> CancelScheduledPostAsync(Guid id, string? reason = null, CancellationToken ct = default);
    Task<UnifiedSocialPost> PublishNowAsync(Guid id, CancellationToken ct = default);
    Task<int> ProcessDueScheduledPostsAsync(DateTime? asOfUtc = null, CancellationToken ct = default);
    
    Task<List<UnifiedSocialPost>> GetCalendarPostsAsync(
        Guid? tenantId = null,
        int? year = null,
        int? month = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        PublishingStatus? status = null,
        string? platform = null,
        CancellationToken ct = default);
        
    Task<List<ConnectedSocialAccount>> GetConnectedAccountsAsync(Guid? tenantId = null, CancellationToken ct = default);
    Task<ConnectedSocialAccount?> GetAccountByIdAsync(Guid accountId, CancellationToken ct = default);
    Task<ConnectedSocialAccount> RefreshTokenAsync(Guid accountId, CancellationToken ct = default);
    Task<int> SweepAndAutoRefreshTokenHealthAsync(DateTime? asOfUtc = null, CancellationToken ct = default);
    
    void ResetToDefaultSeed();
}

public class SocialPublishingService : ISocialPublishingService
{
    private readonly ConcurrentDictionary<Guid, UnifiedSocialPost> _posts = new();
    private readonly ConcurrentDictionary<Guid, ConnectedSocialAccount> _accounts = new();
    private readonly object _stateLock = new();

    private static readonly Regex UrlRegex = new(@"https?://[^\s]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex HashtagRegex = new(@"#\w+", RegexOptions.Compiled);

    public SocialPublishingService()
    {
        SeedDefaults();
    }

    public void ResetToDefaultSeed()
    {
        lock (_stateLock)
        {
            _posts.Clear();
            _accounts.Clear();
            SeedDefaults();
        }
    }

    private void SeedDefaults()
    {
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var now = DateTime.UtcNow;

        // 1. Seed Connected Social Accounts
        var accountX = new ConnectedSocialAccount
        {
            Id = Guid.Parse("a1111111-1111-1111-1111-111111111111"),
            TenantId = tenantId,
            Platform = "X",
            AccountType = "X Account",
            AccountName = "SGrowing HQ",
            Handle = "@SGrowingHQ",
            AvatarUrl = "https://images.unsplash.com/photo-1618005182384-a83a8bd57fbe?w=150",
            AccessToken = "x_oauth2_live_token_77a9",
            RefreshToken = "x_refresh_token_99b2",
            TokenExpiresAtUtc = now.AddDays(45),
            FollowersCount = 28400
        };

        var accountIg = new ConnectedSocialAccount
        {
            Id = Guid.Parse("a2222222-2222-2222-2222-222222222222"),
            TenantId = tenantId,
            Platform = "Instagram",
            AccountType = "Meta Page",
            AccountName = "SGrowing Official",
            Handle = "@sgrowing_official",
            AvatarUrl = "https://images.unsplash.com/photo-1534528741775-53994a69daeb?w=150",
            AccessToken = "ig_graph_token_33c1",
            RefreshToken = "ig_refresh_token_44d2",
            TokenExpiresAtUtc = now.AddHours(36), // ExpiringSoon (< 72h)
            FollowersCount = 64200
        };

        var accountLi = new ConnectedSocialAccount
        {
            Id = Guid.Parse("a3333333-3333-3333-3333-333333333333"),
            TenantId = tenantId,
            Platform = "LinkedIn",
            AccountType = "LinkedIn Org",
            AccountName = "SGrowing Technologies",
            Handle = "sgrowing-technologies",
            AvatarUrl = "https://images.unsplash.com/photo-1572021335469-31706a17aaef?w=150",
            AccessToken = "li_token_prod_55e3",
            RefreshToken = "li_refresh_token_66f4",
            TokenExpiresAtUtc = now.AddDays(90),
            FollowersCount = 15300
        };

        var accountTt = new ConnectedSocialAccount
        {
            Id = Guid.Parse("a4444444-4444-4444-4444-444444444444"),
            TenantId = tenantId,
            Platform = "TikTok",
            AccountType = "TikTok Creator",
            AccountName = "SGrowing Creators",
            Handle = "@sgrowing_creators",
            AvatarUrl = "https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?w=150",
            AccessToken = "tt_open_api_tok_77g5",
            RefreshToken = "tt_refresh_token_88h6",
            TokenExpiresAtUtc = now.AddDays(30),
            FollowersCount = 112000
        };

        _accounts[accountX.Id] = accountX;
        _accounts[accountIg.Id] = accountIg;
        _accounts[accountLi.Id] = accountLi;
        _accounts[accountTt.Id] = accountTt;

        // 2. Seed Calendar Posts
        var post1 = new UnifiedSocialPost
        {
            Id = Guid.Parse("b1111111-1111-1111-1111-111111111111"),
            TenantId = tenantId,
            Title = "Product 3.0 Launch Announcement",
            Content = "We are thrilled to unveil SGrowing 3.0! Complete omnichannel growth, AI agent workflows, and predictive customer cadence. Learn more: https://sgrowing.io/launch",
            PlatformOverrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["X"] = "SGrowing 3.0 is live! Omnichannel growth, autonomous AI agents, and real-time cadence dispatch. Read the announcement: https://sgrowing.io/launch #SaaS #Growth",
                ["LinkedIn"] = "Today marks a monumental milestone for the entire SGrowing team and our global community of modern growth leaders.\n\nWe are officially rolling out SGrowing 3.0 — our unified omnichannel growth platform powered by autonomous AI SDRs, dynamic CPQ, and resilient deliverability.\n\nFull case study & architecture breakdown in comments! https://sgrowing.io/launch"
            },
            MediaAttachments = new List<SocialMediaAttachment>
            {
                new("https://images.unsplash.com/photo-1460925895917-afdab827c52f?w=1200", "16:9", "image", AltText: "SGrowing 3.0 Dashboard Preview")
            },
            Tags = new List<string> { "SaaS", "Growth", "ProductLaunch", "Omnichannel" },
            TargetPlatforms = new List<string> { "X", "LinkedIn" },
            ScheduledAtUtc = now.AddDays(1).Date.AddHours(14), // Tomorrow at 14:00 UTC
            Status = PublishingStatus.Scheduled,
            CreatedAtUtc = now.AddDays(-2)
        };

        var post2 = new UnifiedSocialPost
        {
            Id = Guid.Parse("b2222222-2222-2222-2222-222222222222"),
            TenantId = tenantId,
            Title = "Behind the Scenes Reel: AI Engineering Culture",
            Content = "A day in the life of our high-velocity engineering squad building autonomous growth dispatchers in .NET 10 & Angular.",
            MediaAttachments = new List<SocialMediaAttachment>
            {
                new("https://assets.sgrowing.io/media/bts-squad-reel.mp4", "9:16", "video", AltText: "BTS Engineering Squad")
            },
            Tags = new List<string> { "TechLife", "Engineering", "BuildInPublic", "Coding" },
            TargetPlatforms = new List<string> { "Instagram", "TikTok" },
            ScheduledAtUtc = now.AddDays(3).Date.AddHours(17), // In 3 days at 17:00 UTC
            Status = PublishingStatus.Scheduled,
            CreatedAtUtc = now.AddDays(-1)
        };

        var post3 = new UnifiedSocialPost
        {
            Id = Guid.Parse("b3333333-3333-3333-3333-333333333333"),
            TenantId = tenantId,
            Title = "Weekly Customer Retention Metrics Infographic",
            Content = "B2B SaaS companies that adopted multi-touch cadence reduced pipeline slippage by 42%. Here are the 4 key metrics to measure.",
            MediaAttachments = new List<SocialMediaAttachment>
            {
                new("https://images.unsplash.com/photo-1551288049-bebda4e38f71?w=1200", "1:1", "image", AltText: "Metrics Chart")
            },
            Tags = new List<string> { "B2B", "Revenue", "CustomerSuccess" },
            TargetPlatforms = new List<string> { "LinkedIn", "Instagram", "X" },
            PublishedAtUtc = now.AddDays(-2),
            Status = PublishingStatus.Published,
            CreatedAtUtc = now.AddDays(-3),
            DispatchResults = new List<PlatformDispatchResult>
            {
                new("LinkedIn", true, "li_urn_share_99410", 42, "Dispatched successfully", now.AddDays(-2)),
                new("Instagram", true, "ig_media_id_88204", 65, "Dispatched successfully", now.AddDays(-2)),
                new("X", true, "x_tweet_id_1892837", 38, "Dispatched successfully", now.AddDays(-2))
            }
        };

        var post4 = new UnifiedSocialPost
        {
            Id = Guid.Parse("b4444444-4444-4444-4444-444444444444"),
            TenantId = tenantId,
            Title = "Draft: Q4 Customer Appreciation Showcase",
            Content = "Celebrating our top 100 enterprise growth milestones this quarter! Special thank you to our incredible users.",
            MediaAttachments = new List<SocialMediaAttachment>
            {
                new("https://images.unsplash.com/photo-1522071820081-009f0129c71c?w=1200", "1:1", "image")
            },
            Tags = new List<string> { "CustomerLove", "Milestones" },
            TargetPlatforms = new List<string> { "LinkedIn", "X" },
            Status = PublishingStatus.Draft,
            CreatedAtUtc = now.AddHours(-5)
        };

        _posts[post1.Id] = post1;
        _posts[post2.Id] = post2;
        _posts[post3.Id] = post3;
        _posts[post4.Id] = post4;
    }

    // -------------------------------------------------------------------------
    // Post Management & Composition
    // -------------------------------------------------------------------------

    public Task<UnifiedSocialPost> ComposePostAsync(ComposePostRequest request, CancellationToken ct = default)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (string.IsNullOrWhiteSpace(request.Content)) throw new ArgumentException("Content is required.", nameof(request.Content));
        if (request.TargetPlatforms == null || request.TargetPlatforms.Count == 0)
            throw new ArgumentException("At least one target platform is required.", nameof(request.TargetPlatforms));

        var post = new UnifiedSocialPost
        {
            Id = Guid.NewGuid(),
            TenantId = request.TenantId ?? Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Title = string.IsNullOrWhiteSpace(request.Title) ? "Untitled Post" : request.Title.Trim(),
            Content = request.Content.Trim(),
            PlatformOverrides = request.PlatformOverrides != null 
                ? new Dictionary<string, string>(request.PlatformOverrides, StringComparer.OrdinalIgnoreCase) 
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            MediaAttachments = request.MediaAttachments ?? new List<SocialMediaAttachment>(),
            Tags = request.Tags != null ? request.Tags.Select(t => t.Trim().TrimStart('#')).Where(t => !string.IsNullOrEmpty(t)).Distinct().ToList() : new List<string>(),
            TargetPlatforms = request.TargetPlatforms.Select(p => NormalizePlatformName(p)).Distinct().ToList(),
            ScheduledAtUtc = request.ScheduledAtUtc,
            Status = request.ScheduledAtUtc.HasValue && request.ScheduledAtUtc.Value > DateTime.UtcNow 
                ? PublishingStatus.Scheduled 
                : PublishingStatus.Draft,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _posts[post.Id] = post;
        return Task.FromResult(post);
    }

    public Task<UnifiedSocialPost?> GetPostByIdAsync(Guid id, CancellationToken ct = default)
    {
        _posts.TryGetValue(id, out var post);
        return Task.FromResult(post);
    }

    public Task<List<UnifiedSocialPost>> GetAllPostsAsync(Guid? tenantId = null, CancellationToken ct = default)
    {
        var query = _posts.Values.AsEnumerable();
        if (tenantId.HasValue && tenantId.Value != Guid.Empty)
        {
            query = query.Where(p => p.TenantId == tenantId.Value);
        }

        return Task.FromResult(query.OrderByDescending(p => p.CreatedAtUtc).ToList());
    }

    // -------------------------------------------------------------------------
    // Platform Validation Rules Engine
    // -------------------------------------------------------------------------

    public ValidationReport ValidateRequest(ComposePostRequest request)
    {
        var dummyPost = new UnifiedSocialPost
        {
            TenantId = request.TenantId ?? Guid.Empty,
            Title = request.Title ?? string.Empty,
            Content = request.Content ?? string.Empty,
            PlatformOverrides = request.PlatformOverrides != null 
                ? new Dictionary<string, string>(request.PlatformOverrides, StringComparer.OrdinalIgnoreCase) 
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            MediaAttachments = request.MediaAttachments ?? new List<SocialMediaAttachment>(),
            Tags = request.Tags ?? new List<string>(),
            TargetPlatforms = request.TargetPlatforms != null 
                ? request.TargetPlatforms.Select(NormalizePlatformName).ToList() 
                : new List<string>(),
            ScheduledAtUtc = request.ScheduledAtUtc
        };

        return ValidatePost(dummyPost);
    }

    public ValidationReport ValidatePost(UnifiedSocialPost post)
    {
        var allErrors = new List<string>();
        var allWarnings = new List<string>();
        var platformResults = new Dictionary<string, PlatformValidationResult>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(post.Content) && (post.PlatformOverrides == null || post.PlatformOverrides.Count == 0))
        {
            allErrors.Add("Base content or platform override cannot be empty.");
        }

        if (post.TargetPlatforms == null || post.TargetPlatforms.Count == 0)
        {
            allErrors.Add("At least one target platform must be selected.");
            return new ValidationReport(false, allErrors, allWarnings, platformResults);
        }

        foreach (var rawPlatform in post.TargetPlatforms)
        {
            var platform = NormalizePlatformName(rawPlatform);
            var result = ValidateSinglePlatform(platform, post);
            platformResults[platform] = result;

            if (!result.IsValid)
            {
                allErrors.AddRange(result.Errors);
            }
            allWarnings.AddRange(result.Warnings);
        }

        bool overallValid = allErrors.Count == 0;
        return new ValidationReport(overallValid, allErrors, allWarnings, platformResults);
    }

    private PlatformValidationResult ValidateSinglePlatform(string platform, UnifiedSocialPost post)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        var content = ResolveContentForPlatform(post, platform);

        int charCount = 0;
        int maxChars = 0;

        switch (platform.ToLowerInvariant())
        {
            case "x":
            case "twitter":
                maxChars = 280;
                charCount = CalculateTwitterLength(content);
                if (charCount > 280)
                {
                    errors.Add($"X/Twitter content cannot exceed 280 characters (current: {charCount}).");
                }
                if (post.MediaAttachments.Count > 4)
                {
                    errors.Add($"X/Twitter allows a maximum of 4 media attachments (current: {post.MediaAttachments.Count}).");
                }
                break;

            case "instagram":
            case "instagram business":
                maxChars = 2200;
                charCount = content.Length;
                if (charCount > 2200)
                {
                    errors.Add($"Instagram Business caption cannot exceed 2,200 characters (current: {charCount}).");
                }

                // Instagram requires at least 1 image or video
                if (post.MediaAttachments == null || post.MediaAttachments.Count == 0)
                {
                    errors.Add("Instagram Business requires at least 1 image or video attachment.");
                }
                else
                {
                    // Check supported aspect ratios: 1:1, 4:5, 9:16
                    var validAspectRatios = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "1:1", "4:5", "9:16" };
                    foreach (var media in post.MediaAttachments)
                    {
                        if (!string.IsNullOrWhiteSpace(media.AspectRatio) && !validAspectRatios.Contains(media.AspectRatio.Trim()))
                        {
                            errors.Add($"Instagram does not support aspect ratio '{media.AspectRatio}'. Supported aspect ratios: 1:1, 4:5, 9:16.");
                        }
                    }
                }

                // Check max 30 hashtags
                int hashtagCount = CountHashtags(content, post.Tags);
                if (hashtagCount > 30)
                {
                    errors.Add($"Instagram Business posts cannot exceed 30 hashtags (current: {hashtagCount}).");
                }
                break;

            case "linkedin":
                maxChars = 3000;
                charCount = content.Length;
                if (charCount > 3000)
                {
                    errors.Add($"LinkedIn content cannot exceed 3,000 characters (current: {charCount}).");
                }

                // LinkedIn supports text, images, or PDF carousels
                if (post.MediaAttachments != null && post.MediaAttachments.Count > 0)
                {
                    var supportedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "image", "video", "pdf", "carousel" };
                    foreach (var media in post.MediaAttachments)
                    {
                        var type = (media.MediaType ?? "").ToLowerInvariant();
                        if (!string.IsNullOrEmpty(type) && !supportedTypes.Contains(type))
                        {
                            errors.Add($"LinkedIn does not support media type '{media.MediaType}'. Supported formats: text, images, video, PDF carousels.");
                        }
                    }
                }
                break;

            case "tiktok":
                maxChars = 2200;
                charCount = content.Length;
                if (charCount > 2200)
                {
                    errors.Add($"TikTok caption cannot exceed 2,200 characters (current: {charCount}).");
                }

                // TikTok requires video attachment
                bool hasVideo = post.MediaAttachments != null && post.MediaAttachments.Any(m => 
                    string.Equals(m.MediaType, "video", StringComparison.OrdinalIgnoreCase) ||
                    m.Url.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) ||
                    m.Url.EndsWith(".mov", StringComparison.OrdinalIgnoreCase) ||
                    m.Url.EndsWith(".webm", StringComparison.OrdinalIgnoreCase));

                if (!hasVideo)
                {
                    errors.Add("TikTok requires at least one video attachment.");
                }
                break;

            default:
                maxChars = 5000;
                charCount = content.Length;
                warnings.Add($"Platform '{platform}' is using general validation rules.");
                break;
        }

        return new PlatformValidationResult(
            Platform: platform,
            IsValid: errors.Count == 0,
            Errors: errors,
            Warnings: warnings,
            CharacterCount: charCount,
            MaxCharacters: maxChars
        );
    }

    public static string ResolveContentForPlatform(UnifiedSocialPost post, string platform)
    {
        var normalized = NormalizePlatformName(platform);
        if (post.PlatformOverrides != null)
        {
            foreach (var kvp in post.PlatformOverrides)
            {
                if (string.Equals(NormalizePlatformName(kvp.Key), normalized, StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(kvp.Value))
                {
                    return kvp.Value;
                }
            }
        }

        return post.Content ?? string.Empty;
    }

    public static int CalculateTwitterLength(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        // Standard Twitter URL counting: Any URL counts as 23 characters (t.co length)
        var transformed = UrlRegex.Replace(text, new string('x', 23));
        return transformed.Length;
    }

    private static int CountHashtags(string content, List<string>? tags)
    {
        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrEmpty(content))
        {
            var matches = HashtagRegex.Matches(content);
            foreach (Match match in matches)
            {
                found.Add(match.Value.TrimStart('#').ToLowerInvariant());
            }
        }

        if (tags != null)
        {
            foreach (var tag in tags)
            {
                var clean = tag.Trim().TrimStart('#').ToLowerInvariant();
                if (!string.IsNullOrEmpty(clean))
                {
                    found.Add(clean);
                }
            }
        }

        return found.Count;
    }

    private static string NormalizePlatformName(string platform)
    {
        if (string.IsNullOrWhiteSpace(platform)) return "General";
        var p = platform.Trim();
        if (p.Equals("Twitter", StringComparison.OrdinalIgnoreCase) || p.Equals("X", StringComparison.OrdinalIgnoreCase)) return "X";
        if (p.Equals("Instagram", StringComparison.OrdinalIgnoreCase) || p.Equals("Instagram Business", StringComparison.OrdinalIgnoreCase)) return "Instagram";
        if (p.Equals("LinkedIn", StringComparison.OrdinalIgnoreCase) || p.Equals("LinkedIn Org", StringComparison.OrdinalIgnoreCase)) return "LinkedIn";
        if (p.Equals("TikTok", StringComparison.OrdinalIgnoreCase) || p.Equals("TikTok Creator", StringComparison.OrdinalIgnoreCase)) return "TikTok";
        return p;
    }

    // -------------------------------------------------------------------------
    // Publishing Queue & State Machine
    // -------------------------------------------------------------------------

    public Task<UnifiedSocialPost> SchedulePostAsync(Guid id, DateTime scheduledAtUtc, CancellationToken ct = default)
    {
        lock (_stateLock)
        {
            if (!_posts.TryGetValue(id, out var post))
            {
                throw new KeyNotFoundException($"Social post with ID '{id}' was not found.");
            }

            // State Machine Transition Check
            if (post.Status == PublishingStatus.Published)
            {
                throw new InvalidOperationException("Cannot schedule an already published post.");
            }
            if (post.Status == PublishingStatus.Publishing)
            {
                throw new InvalidOperationException("Cannot schedule a post that is currently publishing.");
            }

            // Validation prior to scheduling
            var validation = ValidatePost(post);
            if (!validation.IsValid)
            {
                throw new InvalidOperationException($"Cannot schedule invalid post: {string.Join("; ", validation.Errors)}");
            }

            post.ScheduledAtUtc = scheduledAtUtc;
            post.Status = PublishingStatus.Scheduled;
            post.UpdatedAtUtc = DateTime.UtcNow;

            return Task.FromResult(post);
        }
    }

    public Task<UnifiedSocialPost> CancelScheduledPostAsync(Guid id, string? reason = null, CancellationToken ct = default)
    {
        lock (_stateLock)
        {
            if (!_posts.TryGetValue(id, out var post))
            {
                throw new KeyNotFoundException($"Social post with ID '{id}' was not found.");
            }

            if (post.Status == PublishingStatus.Published)
            {
                throw new InvalidOperationException("Cannot cancel an already published post.");
            }
            if (post.Status == PublishingStatus.Publishing)
            {
                throw new InvalidOperationException("Cannot cancel a post currently in the publishing queue.");
            }

            post.Status = PublishingStatus.Cancelled;
            post.FailureReason = reason ?? "Post scheduling was cancelled by user.";
            post.UpdatedAtUtc = DateTime.UtcNow;

            return Task.FromResult(post);
        }
    }

    public async Task<UnifiedSocialPost> PublishNowAsync(Guid id, CancellationToken ct = default)
    {
        UnifiedSocialPost post;

        lock (_stateLock)
        {
            if (!_posts.TryGetValue(id, out post!))
            {
                throw new KeyNotFoundException($"Social post with ID '{id}' was not found.");
            }

            if (post.Status == PublishingStatus.Published)
            {
                throw new InvalidOperationException("This post has already been published.");
            }
            if (post.Status == PublishingStatus.Publishing)
            {
                throw new InvalidOperationException("This post is already in progress of publishing.");
            }

            // State transition to Publishing
            post.Status = PublishingStatus.Publishing;
            post.UpdatedAtUtc = DateTime.UtcNow;
        }

        // Validate post across platforms
        var validation = ValidatePost(post);
        if (!validation.IsValid)
        {
            lock (_stateLock)
            {
                post.Status = PublishingStatus.Failed;
                post.FailureReason = $"Validation failed: {string.Join("; ", validation.Errors)}";
                post.UpdatedAtUtc = DateTime.UtcNow;
            }
            return post;
        }

        // Execute simulated platform dispatch with latency tracking
        var results = new List<PlatformDispatchResult>();
        bool allSuccess = true;

        foreach (var platform in post.TargetPlatforms)
        {
            var sw = Stopwatch.StartNew();
            // Simulate platform API dispatch network trip (30 - 70ms)
            await Task.Delay(Random.Shared.Next(30, 70), ct);
            sw.Stop();

            var externalId = $"{platform.ToLowerInvariant()}_{Guid.NewGuid():N}";
            results.Add(new PlatformDispatchResult(
                Platform: platform,
                Success: true,
                ExternalPostId: externalId,
                LatencyMs: sw.ElapsedMilliseconds,
                Message: "Dispatched successfully via official platform API.",
                DispatchedAtUtc: DateTime.UtcNow
            ));
        }

        lock (_stateLock)
        {
            post.DispatchResults = results;
            post.Status = allSuccess ? PublishingStatus.Published : PublishingStatus.Failed;
            if (allSuccess)
            {
                post.PublishedAtUtc = DateTime.UtcNow;
            }
            post.UpdatedAtUtc = DateTime.UtcNow;
        }

        return post;
    }

    public async Task<int> ProcessDueScheduledPostsAsync(DateTime? asOfUtc = null, CancellationToken ct = default)
    {
        var cutoff = asOfUtc ?? DateTime.UtcNow;
        List<Guid> duePostIds;

        lock (_stateLock)
        {
            duePostIds = _posts.Values
                .Where(p => p.Status == PublishingStatus.Scheduled &&
                            p.ScheduledAtUtc.HasValue &&
                            p.ScheduledAtUtc.Value <= cutoff)
                .OrderBy(p => p.ScheduledAtUtc)
                .Select(p => p.Id)
                .ToList();
        }

        int publishedCount = 0;
        foreach (var postId in duePostIds)
        {
            try
            {
                var result = await PublishNowAsync(postId, ct);
                if (result.Status == PublishingStatus.Published)
                {
                    publishedCount++;
                }
            }
            catch
            {
                // Continue queue processing on error
            }
        }

        return publishedCount;
    }

    // -------------------------------------------------------------------------
    // Calendar Querying
    // -------------------------------------------------------------------------

    public Task<List<UnifiedSocialPost>> GetCalendarPostsAsync(
        Guid? tenantId = null,
        int? year = null,
        int? month = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        PublishingStatus? status = null,
        string? platform = null,
        CancellationToken ct = default)
    {
        var query = _posts.Values.AsEnumerable();

        if (tenantId.HasValue && tenantId.Value != Guid.Empty)
        {
            query = query.Where(p => p.TenantId == tenantId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(p => p.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(platform))
        {
            var normPlatform = NormalizePlatformName(platform);
            query = query.Where(p => p.TargetPlatforms.Any(tp => string.Equals(NormalizePlatformName(tp), normPlatform, StringComparison.OrdinalIgnoreCase)));
        }

        // Date range filtering
        if (startDate.HasValue && endDate.HasValue)
        {
            query = query.Where(p =>
            {
                var postDate = p.ScheduledAtUtc ?? p.PublishedAtUtc ?? p.CreatedAtUtc;
                return postDate >= startDate.Value && postDate <= endDate.Value;
            });
        }
        else if (year.HasValue && month.HasValue)
        {
            query = query.Where(p =>
            {
                var postDate = p.ScheduledAtUtc ?? p.PublishedAtUtc ?? p.CreatedAtUtc;
                return postDate.Year == year.Value && postDate.Month == month.Value;
            });
        }
        else if (year.HasValue)
        {
            query = query.Where(p =>
            {
                var postDate = p.ScheduledAtUtc ?? p.PublishedAtUtc ?? p.CreatedAtUtc;
                return postDate.Year == year.Value;
            });
        }

        var results = query
            .OrderBy(p => p.ScheduledAtUtc ?? p.PublishedAtUtc ?? p.CreatedAtUtc)
            .ToList();

        return Task.FromResult(results);
    }

    // -------------------------------------------------------------------------
    // Proactive OAuth 2.0 Token Health & Refresh
    // -------------------------------------------------------------------------

    public Task<List<ConnectedSocialAccount>> GetConnectedAccountsAsync(Guid? tenantId = null, CancellationToken ct = default)
    {
        var query = _accounts.Values.AsEnumerable();
        if (tenantId.HasValue && tenantId.Value != Guid.Empty)
        {
            query = query.Where(a => a.TenantId == tenantId.Value);
        }

        return Task.FromResult(query.OrderBy(a => a.Platform).ToList());
    }

    public Task<ConnectedSocialAccount?> GetAccountByIdAsync(Guid accountId, CancellationToken ct = default)
    {
        _accounts.TryGetValue(accountId, out var account);
        return Task.FromResult(account);
    }

    public Task<ConnectedSocialAccount> RefreshTokenAsync(Guid accountId, CancellationToken ct = default)
    {
        lock (_stateLock)
        {
            if (!_accounts.TryGetValue(accountId, out var account))
            {
                throw new KeyNotFoundException($"Social account with ID '{accountId}' was not found.");
            }

            // Perform token exchange & extend lifetime by 60 days
            account.AccessToken = $"tok_live_{account.Platform.ToLowerInvariant()}_{Guid.NewGuid():N}";
            account.RefreshToken = $"ref_tok_{account.Platform.ToLowerInvariant()}_{Guid.NewGuid():N}";
            account.TokenExpiresAtUtc = DateTime.UtcNow.AddDays(60);
            account.LastRefreshedAtUtc = DateTime.UtcNow;

            return Task.FromResult(account);
        }
    }

    public async Task<int> SweepAndAutoRefreshTokenHealthAsync(DateTime? asOfUtc = null, CancellationToken ct = default)
    {
        var now = asOfUtc ?? DateTime.UtcNow;
        List<Guid> expiringAccountIds;

        lock (_stateLock)
        {
            expiringAccountIds = _accounts.Values
                .Where(a => a.AutoRefreshEnabled &&
                           (a.GetTokenStatus(now) == TokenHealthStatus.ExpiringSoon ||
                            a.GetTokenStatus(now) == TokenHealthStatus.Expired))
                .Select(a => a.Id)
                .ToList();
        }

        int refreshedCount = 0;
        foreach (var id in expiringAccountIds)
        {
            await RefreshTokenAsync(id, ct);
            refreshedCount++;
        }

        return refreshedCount;
    }
}
