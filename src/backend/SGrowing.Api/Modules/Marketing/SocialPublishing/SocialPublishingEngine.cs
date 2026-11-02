using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace SGrowing.Api.Modules.Marketing.SocialPublishing;

public interface ISocialPublishingEngine
{
    SocialPost SchedulePost(SchedulePostRequest request);
    SocialPost ApprovePost(ApprovePostRequest request);
    SocialPost PublishNow(PublishNowRequest request);
    List<SocialPost> GetCalendar(string tenantId, DateTime startUtc, DateTime endUtc);
    SocialPost IngestAnalytics(SocialAnalyticsWebhookPayload payload);
    SocialPost? GetPost(string tenantId, Guid postId);
}

public class SocialPublishingEngine : ISocialPublishingEngine
{
    private static readonly HashSet<string> InstagramAllowedAspectRatios = new(StringComparer.OrdinalIgnoreCase)
    {
        "1:1", "4:5", "9:16", "16:9"
    };

    private readonly ConcurrentDictionary<(string TenantId, Guid PostId), SocialPost> _posts = new();

    public SocialPost SchedulePost(SchedulePostRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.TenantId))
        {
            throw new ArgumentException("TenantId is required.", nameof(request.TenantId));
        }

        if (request.Platforms == null || request.Platforms.Count == 0)
        {
            throw new ArgumentException("At least one social platform must be selected.", nameof(request.Platforms));
        }

        if (string.IsNullOrWhiteSpace(request.ContentText))
        {
            throw new ArgumentException("Content text cannot be empty.", nameof(request.ContentText));
        }

        // LinkedIn validation guardrail: max 3000 chars
        if (request.Platforms.Contains(SocialPlatform.LinkedIn) && request.ContentText.Length > 3000)
        {
            throw new ArgumentException("LinkedIn post content exceeds maximum limit of 3000 characters.", nameof(request.ContentText));
        }

        // Instagram validation guardrail: requires at least 1 media asset
        if (request.Platforms.Contains(SocialPlatform.Instagram))
        {
            if (request.MediaAssetUrls == null || request.MediaAssetUrls.Count == 0)
            {
                throw new ArgumentException("Instagram posts require at least one media asset.", nameof(request.MediaAssetUrls));
            }

            ValidateInstagramAspectRatios(request.MediaAssetUrls);
        }

        var post = new SocialPost(
            Id: Guid.NewGuid(),
            TenantId: request.TenantId,
            Platforms: request.Platforms.Distinct().ToList(),
            ContentText: request.ContentText,
            MediaAssetUrls: request.MediaAssetUrls != null ? new List<string>(request.MediaAssetUrls) : new List<string>(),
            ScheduledAtUtc: request.ScheduledAtUtc,
            PublishedAtUtc: null,
            Status: SocialPostStatus.PendingApproval,
            Metrics: new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["Impressions"] = 0,
                ["Clicks"] = 0,
                ["Likes"] = 0,
                ["Shares"] = 0
            },
            ApproverId: null,
            CreatedAtUtc: DateTime.UtcNow
        );

        _posts[(post.TenantId, post.Id)] = post;
        return post;
    }

    public SocialPost ApprovePost(ApprovePostRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.TenantId))
        {
            throw new ArgumentException("TenantId is required.", nameof(request.TenantId));
        }

        if (request.PostId == Guid.Empty)
        {
            throw new ArgumentException("PostId is required.", nameof(request.PostId));
        }

        if (string.IsNullOrWhiteSpace(request.ApproverId))
        {
            throw new ArgumentException("ApproverId is required.", nameof(request.ApproverId));
        }

        if (!_posts.TryGetValue((request.TenantId, request.PostId), out var post))
        {
            throw new KeyNotFoundException($"Post {request.PostId} was not found for tenant {request.TenantId}.");
        }

        if (post.Status == SocialPostStatus.Published)
        {
            throw new InvalidOperationException("Cannot change approval status for an already published post.");
        }

        if (post.Status == SocialPostStatus.Failed)
        {
            throw new InvalidOperationException("Cannot approve a post in Failed status.");
        }

        // State machine transition: PendingApproval -> Approved or Draft (if rejected)
        var newStatus = request.IsApproved ? SocialPostStatus.Approved : SocialPostStatus.Draft;
        var updated = post with
        {
            Status = newStatus,
            ApproverId = request.IsApproved ? request.ApproverId : null
        };

        _posts[(request.TenantId, request.PostId)] = updated;
        return updated;
    }

    public SocialPost PublishNow(PublishNowRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.TenantId))
        {
            throw new ArgumentException("TenantId is required.", nameof(request.TenantId));
        }

        if (request.PostId == Guid.Empty)
        {
            throw new ArgumentException("PostId is required.", nameof(request.PostId));
        }

        if (!_posts.TryGetValue((request.TenantId, request.PostId), out var post))
        {
            throw new KeyNotFoundException($"Post {request.PostId} was not found for tenant {request.TenantId}.");
        }

        if (post.Status == SocialPostStatus.Published)
        {
            throw new InvalidOperationException("Post is already published.");
        }

        if (post.Status == SocialPostStatus.Failed)
        {
            throw new InvalidOperationException("Cannot publish a post in Failed status.");
        }

        // Must be in Approved status (or Scheduled once approved) to be published
        if (post.Status != SocialPostStatus.Approved && post.Status != SocialPostStatus.Scheduled)
        {
            throw new InvalidOperationException($"Cannot publish post in status '{post.Status}'. Post must be Approved prior to publishing.");
        }

        var published = post with
        {
            Status = SocialPostStatus.Published,
            PublishedAtUtc = DateTime.UtcNow
        };

        _posts[(request.TenantId, request.PostId)] = published;
        return published;
    }

    public List<SocialPost> GetCalendar(string tenantId, DateTime startUtc, DateTime endUtc)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            throw new ArgumentException("TenantId is required.", nameof(tenantId));
        }

        return _posts.Values
            .Where(p => string.Equals(p.TenantId, tenantId, StringComparison.OrdinalIgnoreCase))
            .Where(p => (p.ScheduledAtUtc.HasValue && p.ScheduledAtUtc.Value >= startUtc && p.ScheduledAtUtc.Value <= endUtc)
                     || (p.PublishedAtUtc.HasValue && p.PublishedAtUtc.Value >= startUtc && p.PublishedAtUtc.Value <= endUtc))
            .OrderBy(p => p.ScheduledAtUtc ?? p.PublishedAtUtc ?? p.CreatedAtUtc)
            .ToList();
    }

    public SocialPost IngestAnalytics(SocialAnalyticsWebhookPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        if (string.IsNullOrWhiteSpace(payload.TenantId))
        {
            throw new ArgumentException("TenantId is required.", nameof(payload.TenantId));
        }

        if (payload.PostId == Guid.Empty)
        {
            throw new ArgumentException("PostId is required.", nameof(payload.PostId));
        }

        if (!_posts.TryGetValue((payload.TenantId, payload.PostId), out var post))
        {
            throw new KeyNotFoundException($"Post {payload.PostId} was not found for tenant {payload.TenantId}.");
        }

        var aggregatedMetrics = new Dictionary<string, int>(post.Metrics, StringComparer.OrdinalIgnoreCase)
        {
            ["Impressions"] = post.Metrics.GetValueOrDefault("Impressions", 0) + payload.Impressions,
            ["Clicks"] = post.Metrics.GetValueOrDefault("Clicks", 0) + payload.Clicks,
            ["Likes"] = post.Metrics.GetValueOrDefault("Likes", 0) + payload.Likes,
            ["Shares"] = post.Metrics.GetValueOrDefault("Shares", 0) + payload.Shares,
            [$"{payload.Platform}_Impressions"] = post.Metrics.GetValueOrDefault($"{payload.Platform}_Impressions", 0) + payload.Impressions,
            [$"{payload.Platform}_Clicks"] = post.Metrics.GetValueOrDefault($"{payload.Platform}_Clicks", 0) + payload.Clicks,
            [$"{payload.Platform}_Likes"] = post.Metrics.GetValueOrDefault($"{payload.Platform}_Likes", 0) + payload.Likes,
            [$"{payload.Platform}_Shares"] = post.Metrics.GetValueOrDefault($"{payload.Platform}_Shares", 0) + payload.Shares
        };

        var updated = post with { Metrics = aggregatedMetrics };
        _posts[(payload.TenantId, payload.PostId)] = updated;
        return updated;
    }

    public SocialPost? GetPost(string tenantId, Guid postId)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            throw new ArgumentException("TenantId is required.", nameof(tenantId));
        }

        if (postId == Guid.Empty)
        {
            throw new ArgumentException("PostId is required.", nameof(postId));
        }

        return _posts.TryGetValue((tenantId, postId), out var post) ? post : null;
    }

    private static void ValidateInstagramAspectRatios(IEnumerable<string> mediaUrls)
    {
        foreach (var url in mediaUrls)
        {
            var ratio = ExtractAspectRatio(url);
            if (!string.IsNullOrEmpty(ratio) && !InstagramAllowedAspectRatios.Contains(ratio))
            {
                throw new ArgumentException(
                    $"Instagram does not support aspect ratio '{ratio}'. Allowed aspect ratios are 1:1, 4:5, 9:16, 16:9.",
                    nameof(SchedulePostRequest.MediaAssetUrls));
            }
        }
    }

    private static string? ExtractAspectRatio(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        // Matches ?aspect=2:1 or ?aspect_ratio=3:1 or ?ratio=custom
        var queryMatch = Regex.Match(
            url,
            @"[?&](?:aspect|aspect_ratio|ratio)=([^&#]+)",
            RegexOptions.IgnoreCase);

        if (queryMatch.Success)
        {
            return Uri.UnescapeDataString(queryMatch.Groups[1].Value.Trim());
        }

        // Matches path tokens like /aspect/2:1 or /aspect-2:1
        var pathMatch = Regex.Match(
            url,
            @"(?:aspect|ratio)[/_-]([0-9]+:[0-9]+)",
            RegexOptions.IgnoreCase);

        if (pathMatch.Success)
        {
            return pathMatch.Groups[1].Value.Trim();
        }

        return null;
    }
}
