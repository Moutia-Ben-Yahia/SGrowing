using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SGrowing.Api.Data;

namespace SGrowing.Api.Modules.Feedback;

#region Models & DTOs

public static class FeedbackCategories
{
    public const string FeatureRequest = "FeatureRequest";
    public const string Integration = "Integration";
    public const string BugReport = "BugReport";
    public const string UxImprovement = "UxImprovement";

    public static readonly string[] All = [FeatureRequest, Integration, BugReport, UxImprovement];
}

public static class FeedbackStatuses
{
    public const string Submitted = "Submitted";
    public const string UnderReview = "UnderReview";
    public const string Planned = "Planned";
    public const string InProgress = "InProgress";
    public const string Completed = "Completed";
    public const string Declined = "Declined";

    public static readonly string[] All = [Submitted, UnderReview, Planned, InProgress, Completed, Declined];
}

public class FeedbackPost
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = FeedbackCategories.FeatureRequest;
    public string Status { get; set; } = FeedbackStatuses.Submitted;
    public string AuthorEmail { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public string AccountId { get; set; } = string.Empty;
    public decimal AccountARR { get; set; }
    public int UpvoteCount { get; set; }
    public decimal ImpactScore { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string EstimatedQuarter { get; set; } = string.Empty;
    public string? ResponseNotes { get; set; }
}

public class FeedbackVote
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid PostId { get; set; }
    public string VoterEmail { get; set; } = string.Empty;
    public string VoterIp { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}

public class FeedbackComment
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid PostId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string AuthorEmail { get; set; } = string.Empty;
    public bool IsOfficialTeamResponse { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}

public record FeedbackPostDetailDto(
    FeedbackPost Post,
    List<FeedbackComment> Comments,
    int TotalVotes,
    bool HasVoted
);

public record CreateBoardPostRequest(
    Guid TenantId,
    string Title,
    string Description,
    string? Category = null,
    string? AuthorEmail = null,
    string? AuthorName = null,
    string? AccountId = null,
    decimal AccountARR = 0m,
    string? EstimatedQuarter = null
);

public record ToggleBoardVoteRequest(
    Guid TenantId,
    string VoterEmail,
    string? VoterIp = null
);

public record VoteToggleResult(
    Guid PostId,
    bool IsUpvoted,
    int UpvoteCount,
    decimal ImpactScore,
    string Message
);

public record AddFeedbackCommentRequest(
    Guid TenantId,
    string AuthorName,
    string AuthorEmail,
    string Content,
    bool IsOfficialTeamResponse = false
);

public record UpdateBoardPostStatusRequest(
    Guid TenantId,
    string NewStatus,
    string? ResponseNotes = null,
    string? EstimatedQuarter = null
);

public record RoadmapDto(
    List<FeedbackPost> Planned,
    List<FeedbackPost> InProgress,
    List<FeedbackPost> Completed
);

public record FeedbackSummaryDto(
    int TotalIdeas,
    int TotalVotes,
    int CompletedVelocity,
    decimal TotalArrDemand,
    int UnderReviewCount,
    int InProgressCount,
    decimal AverageImpactScore
);

#endregion

#region Service Interface

public interface IFeedbackBoardService
{
    Task<List<FeedbackPost>> GetPostsAsync(
        Guid tenantId,
        string? category = null,
        string? status = null,
        string? search = null,
        string? sortBy = null,
        CancellationToken ct = default);

    Task<FeedbackPostDetailDto> GetPostByIdAsync(
        Guid tenantId,
        Guid postId,
        string? voterEmail = null,
        CancellationToken ct = default);

    Task<FeedbackPost> CreatePostAsync(
        CreateBoardPostRequest request,
        CancellationToken ct = default);

    Task<VoteToggleResult> ToggleVoteAsync(
        Guid tenantId,
        Guid postId,
        string voterEmail,
        string? voterIp = null,
        CancellationToken ct = default);

    Task<FeedbackComment> AddCommentAsync(
        Guid tenantId,
        Guid postId,
        AddFeedbackCommentRequest request,
        CancellationToken ct = default);

    Task<FeedbackPost> UpdatePostStatusAsync(
        Guid tenantId,
        Guid postId,
        UpdateBoardPostStatusRequest request,
        CancellationToken ct = default);

    Task<RoadmapDto> GetRoadmapAsync(
        Guid tenantId,
        CancellationToken ct = default);

    Task<FeedbackSummaryDto> GetSummaryAsync(
        Guid tenantId,
        CancellationToken ct = default);
}

#endregion

#region Implementation

public class FeedbackBoardService : IFeedbackBoardService
{
    private readonly SGrowingDbContext? _db;

    // In-memory tenant stores with thread-safe concurrency
    private static readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, FeedbackPost>> _tenantPosts = new();
    private static readonly ConcurrentDictionary<Guid, List<FeedbackVote>> _postVotes = new();
    private static readonly ConcurrentDictionary<Guid, List<FeedbackComment>> _postComments = new();
    private static readonly ConcurrentDictionary<Guid, bool> _seededTenants = new();
    private static readonly object _syncRoot = new();

    private static readonly HashSet<string> ValidStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        FeedbackStatuses.Submitted,
        FeedbackStatuses.UnderReview,
        FeedbackStatuses.Planned,
        FeedbackStatuses.InProgress,
        FeedbackStatuses.Completed,
        FeedbackStatuses.Declined
    };

    private static readonly Dictionary<string, HashSet<string>> AllowedTransitions = new(StringComparer.OrdinalIgnoreCase)
    {
        [FeedbackStatuses.Submitted] = new(StringComparer.OrdinalIgnoreCase)
        {
            FeedbackStatuses.UnderReview,
            FeedbackStatuses.Planned,
            FeedbackStatuses.Declined
        },
        [FeedbackStatuses.UnderReview] = new(StringComparer.OrdinalIgnoreCase)
        {
            FeedbackStatuses.Planned,
            FeedbackStatuses.InProgress,
            FeedbackStatuses.Declined,
            FeedbackStatuses.Submitted
        },
        [FeedbackStatuses.Planned] = new(StringComparer.OrdinalIgnoreCase)
        {
            FeedbackStatuses.InProgress,
            FeedbackStatuses.UnderReview,
            FeedbackStatuses.Declined
        },
        [FeedbackStatuses.InProgress] = new(StringComparer.OrdinalIgnoreCase)
        {
            FeedbackStatuses.Completed,
            FeedbackStatuses.Planned,
            FeedbackStatuses.Declined,
            FeedbackStatuses.UnderReview
        },
        [FeedbackStatuses.Completed] = new(StringComparer.OrdinalIgnoreCase)
        {
            FeedbackStatuses.InProgress,
            FeedbackStatuses.UnderReview
        },
        [FeedbackStatuses.Declined] = new(StringComparer.OrdinalIgnoreCase)
        {
            FeedbackStatuses.UnderReview,
            FeedbackStatuses.Submitted,
            FeedbackStatuses.Planned
        }
    };

    public FeedbackBoardService(SGrowingDbContext? db = null)
    {
        _db = db;
    }

    /// <summary>
    /// Impact Score Formula:
    /// ImpactScore = Upvotes * log10(AccountARR + 10.0m)
    /// </summary>
    public static decimal CalculateImpactScore(int upvotes, decimal accountArr)
    {
        if (upvotes <= 0) return 0m;
        var safeArr = Math.Max(0m, accountArr);
        var logFactor = Math.Log10((double)(safeArr + 10.0m));
        var rawScore = (double)upvotes * logFactor;
        return Math.Round((decimal)rawScore, 2);
    }

    public Task<List<FeedbackPost>> GetPostsAsync(
        Guid tenantId,
        string? category = null,
        string? status = null,
        string? search = null,
        string? sortBy = null,
        CancellationToken ct = default)
    {
        EnsureSeeded(tenantId);
        var postsMap = _tenantPosts.GetOrAdd(tenantId, _ => new ConcurrentDictionary<Guid, FeedbackPost>());
        var query = postsMap.Values.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(p => string.Equals(p.Category, category.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(p => string.Equals(p.Status, status.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(p =>
                p.Title.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                p.Description.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                p.AuthorName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                p.EstimatedQuarter.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        var sort = sortBy?.Trim().ToLowerInvariant() ?? "impact";
        query = sort switch
        {
            "top" => query.OrderByDescending(p => p.UpvoteCount).ThenByDescending(p => p.ImpactScore),
            "newest" => query.OrderByDescending(p => p.CreatedAt),
            "trending" => query.OrderByDescending(p =>
            {
                var daysOld = (decimal)Math.Max(0, (DateTimeOffset.UtcNow - p.CreatedAt).TotalDays);
                return (p.UpvoteCount * 2m + p.ImpactScore * 0.5m) / (1m + daysOld * 0.15m);
            }),
            _ => query.OrderByDescending(p => p.ImpactScore).ThenByDescending(p => p.UpvoteCount)
        };

        return Task.FromResult(query.ToList());
    }

    public Task<FeedbackPostDetailDto> GetPostByIdAsync(
        Guid tenantId,
        Guid postId,
        string? voterEmail = null,
        CancellationToken ct = default)
    {
        EnsureSeeded(tenantId);
        var postsMap = _tenantPosts.GetOrAdd(tenantId, _ => new ConcurrentDictionary<Guid, FeedbackPost>());
        if (!postsMap.TryGetValue(postId, out var post))
        {
            throw new KeyNotFoundException($"Feedback post '{postId}' was not found for tenant '{tenantId}'.");
        }

        var comments = _postComments.GetOrAdd(postId, _ => new List<FeedbackComment>());
        List<FeedbackComment> commentCopy;
        lock (comments)
        {
            commentCopy = comments.OrderBy(c => c.CreatedAt).ToList();
        }

        var votes = _postVotes.GetOrAdd(postId, _ => new List<FeedbackVote>());
        bool hasVoted = false;
        if (!string.IsNullOrWhiteSpace(voterEmail))
        {
            var cleanEmail = voterEmail.Trim().ToLowerInvariant();
            lock (votes)
            {
                hasVoted = votes.Any(v => v.VoterEmail.Equals(cleanEmail, StringComparison.OrdinalIgnoreCase));
            }
        }

        var dto = new FeedbackPostDetailDto(post, commentCopy, post.UpvoteCount, hasVoted);
        return Task.FromResult(dto);
    }

    public Task<FeedbackPost> CreatePostAsync(
        CreateBoardPostRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            throw new ArgumentException("Title cannot be empty.", nameof(request.Title));
        }

        EnsureSeeded(request.TenantId);
        var postsMap = _tenantPosts.GetOrAdd(request.TenantId, _ => new ConcurrentDictionary<Guid, FeedbackPost>());

        var now = DateTimeOffset.UtcNow;
        var category = NormalizeCategory(request.Category);
        var initialVotes = 0;
        var authorEmail = request.AuthorEmail?.Trim() ?? string.Empty;
        var authorName = request.AuthorName?.Trim() ?? string.Empty;

        var postId = Guid.NewGuid();
        var post = new FeedbackPost
        {
            Id = postId,
            TenantId = request.TenantId,
            Title = request.Title.Trim(),
            Description = request.Description?.Trim() ?? string.Empty,
            Category = category,
            Status = FeedbackStatuses.Submitted,
            AuthorEmail = authorEmail,
            AuthorName = string.IsNullOrWhiteSpace(authorName) ? (authorEmail.Split('@').FirstOrDefault() ?? "Community User") : authorName,
            AccountId = request.AccountId?.Trim() ?? string.Empty,
            AccountARR = Math.Max(0m, request.AccountARR),
            UpvoteCount = 0,
            ImpactScore = 0m,
            CreatedAt = now,
            UpdatedAt = now,
            CompletedAt = null,
            EstimatedQuarter = request.EstimatedQuarter?.Trim() ?? string.Empty,
            ResponseNotes = null
        };

        // If author provided email, automatically record their initial vote
        if (!string.IsNullOrWhiteSpace(authorEmail))
        {
            initialVotes = 1;
            post.UpvoteCount = initialVotes;
            var votes = _postVotes.GetOrAdd(postId, _ => new List<FeedbackVote>());
            lock (votes)
            {
                votes.Add(new FeedbackVote
                {
                    Id = Guid.NewGuid(),
                    TenantId = request.TenantId,
                    PostId = postId,
                    VoterEmail = authorEmail.ToLowerInvariant(),
                    VoterIp = "author-initial",
                    CreatedAt = now
                });
            }
        }

        post.ImpactScore = CalculateImpactScore(post.UpvoteCount, post.AccountARR);
        postsMap[postId] = post;

        return Task.FromResult(post);
    }

    public Task<VoteToggleResult> ToggleVoteAsync(
        Guid tenantId,
        Guid postId,
        string voterEmail,
        string? voterIp = null,
        CancellationToken ct = default)
    {
        EnsureSeeded(tenantId);
        var postsMap = _tenantPosts.GetOrAdd(tenantId, _ => new ConcurrentDictionary<Guid, FeedbackPost>());
        if (!postsMap.TryGetValue(postId, out var post))
        {
            throw new KeyNotFoundException($"Feedback post '{postId}' was not found.");
        }

        if (string.IsNullOrWhiteSpace(voterEmail))
        {
            throw new ArgumentException("Voter email is required to vote.", nameof(voterEmail));
        }

        var cleanEmail = voterEmail.Trim().ToLowerInvariant();
        var cleanIp = voterIp?.Trim() ?? string.Empty;
        var votes = _postVotes.GetOrAdd(postId, _ => new List<FeedbackVote>());

        lock (votes)
        {
            // 1. Check if user with this email has already voted -> Toggle OFF
            var existingVoteByEmail = votes.FirstOrDefault(v => v.VoterEmail.Equals(cleanEmail, StringComparison.OrdinalIgnoreCase));
            if (existingVoteByEmail != null)
            {
                votes.Remove(existingVoteByEmail);
                post.UpvoteCount = Math.Max(0, post.UpvoteCount - 1);
                post.ImpactScore = CalculateImpactScore(post.UpvoteCount, post.AccountARR);
                post.UpdatedAt = DateTimeOffset.UtcNow;

                return Task.FromResult(new VoteToggleResult(
                    postId,
                    false,
                    post.UpvoteCount,
                    post.ImpactScore,
                    "Vote removed successfully."
                ));
            }

            // 2. Anti-gaming check: If non-local IP already has a vote on this post with a different email, reject
            if (!string.IsNullOrWhiteSpace(cleanIp) && !IsLocalIp(cleanIp))
            {
                var existingVoteByIp = votes.FirstOrDefault(v => v.VoterIp.Equals(cleanIp, StringComparison.OrdinalIgnoreCase));
                if (existingVoteByIp != null)
                {
                    throw new InvalidOperationException(
                        $"Duplicate vote prevented by anti-gaming engine: a vote has already been submitted from this network address ({cleanIp}).");
                }
            }

            // 3. New vote -> Toggle ON
            votes.Add(new FeedbackVote
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                PostId = postId,
                VoterEmail = cleanEmail,
                VoterIp = cleanIp,
                CreatedAt = DateTimeOffset.UtcNow
            });

            post.UpvoteCount++;
            post.ImpactScore = CalculateImpactScore(post.UpvoteCount, post.AccountARR);
            post.UpdatedAt = DateTimeOffset.UtcNow;

            return Task.FromResult(new VoteToggleResult(
                postId,
                true,
                post.UpvoteCount,
                post.ImpactScore,
                "Vote registered successfully."
            ));
        }
    }

    public Task<FeedbackComment> AddCommentAsync(
        Guid tenantId,
        Guid postId,
        AddFeedbackCommentRequest request,
        CancellationToken ct = default)
    {
        EnsureSeeded(tenantId);
        var postsMap = _tenantPosts.GetOrAdd(tenantId, _ => new ConcurrentDictionary<Guid, FeedbackPost>());
        if (!postsMap.TryGetValue(postId, out var post))
        {
            throw new KeyNotFoundException($"Feedback post '{postId}' was not found.");
        }

        if (string.IsNullOrWhiteSpace(request.Content))
        {
            throw new ArgumentException("Comment content cannot be empty.", nameof(request.Content));
        }

        var comment = new FeedbackComment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PostId = postId,
            AuthorName = string.IsNullOrWhiteSpace(request.AuthorName) ? "Team Contributor" : request.AuthorName.Trim(),
            AuthorEmail = request.AuthorEmail?.Trim() ?? string.Empty,
            IsOfficialTeamResponse = request.IsOfficialTeamResponse,
            Content = request.Content.Trim(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        var comments = _postComments.GetOrAdd(postId, _ => new List<FeedbackComment>());
        lock (comments)
        {
            comments.Add(comment);
        }

        post.UpdatedAt = DateTimeOffset.UtcNow;
        return Task.FromResult(comment);
    }

    public Task<FeedbackPost> UpdatePostStatusAsync(
        Guid tenantId,
        Guid postId,
        UpdateBoardPostStatusRequest request,
        CancellationToken ct = default)
    {
        EnsureSeeded(tenantId);
        var postsMap = _tenantPosts.GetOrAdd(tenantId, _ => new ConcurrentDictionary<Guid, FeedbackPost>());
        if (!postsMap.TryGetValue(postId, out var post))
        {
            throw new KeyNotFoundException($"Feedback post '{postId}' was not found.");
        }

        var newStatus = request.NewStatus?.Trim() ?? string.Empty;
        var canonicalStatus = ValidStatuses.FirstOrDefault(s => s.Equals(newStatus, StringComparison.OrdinalIgnoreCase));
        if (canonicalStatus == null)
        {
            throw new ArgumentException($"'{newStatus}' is not a valid feedback status. Allowed: {string.Join(", ", ValidStatuses)}", nameof(request.NewStatus));
        }

        var currentStatus = post.Status;

        // If status changed, validate state machine transition
        if (!currentStatus.Equals(canonicalStatus, StringComparison.OrdinalIgnoreCase))
        {
            if (AllowedTransitions.TryGetValue(currentStatus, out var allowedNext) && !allowedNext.Contains(canonicalStatus))
            {
                throw new InvalidOperationException($"Invalid status transition from '{currentStatus}' to '{canonicalStatus}'.");
            }

            post.Status = canonicalStatus;

            if (canonicalStatus.Equals(FeedbackStatuses.Completed, StringComparison.OrdinalIgnoreCase))
            {
                post.CompletedAt = DateTimeOffset.UtcNow;
            }
            else if (currentStatus.Equals(FeedbackStatuses.Completed, StringComparison.OrdinalIgnoreCase))
            {
                // Re-opened from Completed
                post.CompletedAt = null;
            }
        }

        if (request.ResponseNotes != null)
        {
            post.ResponseNotes = request.ResponseNotes.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.EstimatedQuarter))
        {
            post.EstimatedQuarter = request.EstimatedQuarter.Trim();
        }

        post.UpdatedAt = DateTimeOffset.UtcNow;
        return Task.FromResult(post);
    }

    public Task<RoadmapDto> GetRoadmapAsync(Guid tenantId, CancellationToken ct = default)
    {
        EnsureSeeded(tenantId);
        var postsMap = _tenantPosts.GetOrAdd(tenantId, _ => new ConcurrentDictionary<Guid, FeedbackPost>());
        var allPosts = postsMap.Values.ToList();

        var planned = allPosts
            .Where(p => p.Status.Equals(FeedbackStatuses.Planned, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(p => p.ImpactScore)
            .ThenByDescending(p => p.UpvoteCount)
            .ToList();

        var inProgress = allPosts
            .Where(p => p.Status.Equals(FeedbackStatuses.InProgress, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(p => p.ImpactScore)
            .ThenByDescending(p => p.UpvoteCount)
            .ToList();

        var completed = allPosts
            .Where(p => p.Status.Equals(FeedbackStatuses.Completed, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(p => p.CompletedAt ?? p.UpdatedAt)
            .ToList();

        return Task.FromResult(new RoadmapDto(planned, inProgress, completed));
    }

    public Task<FeedbackSummaryDto> GetSummaryAsync(Guid tenantId, CancellationToken ct = default)
    {
        EnsureSeeded(tenantId);
        var postsMap = _tenantPosts.GetOrAdd(tenantId, _ => new ConcurrentDictionary<Guid, FeedbackPost>());
        var allPosts = postsMap.Values.ToList();

        var totalIdeas = allPosts.Count;
        var totalVotes = allPosts.Sum(p => p.UpvoteCount);
        var completedVelocity = allPosts.Count(p => p.Status.Equals(FeedbackStatuses.Completed, StringComparison.OrdinalIgnoreCase));
        var totalArrDemand = allPosts
            .Where(p => !p.Status.Equals(FeedbackStatuses.Declined, StringComparison.OrdinalIgnoreCase))
            .Sum(p => p.AccountARR);
        var underReviewCount = allPosts.Count(p => p.Status.Equals(FeedbackStatuses.UnderReview, StringComparison.OrdinalIgnoreCase));
        var inProgressCount = allPosts.Count(p => p.Status.Equals(FeedbackStatuses.InProgress, StringComparison.OrdinalIgnoreCase));
        var averageImpactScore = totalIdeas > 0
            ? Math.Round(allPosts.Average(p => p.ImpactScore), 2)
            : 0m;

        return Task.FromResult(new FeedbackSummaryDto(
            totalIdeas,
            totalVotes,
            completedVelocity,
            totalArrDemand,
            underReviewCount,
            inProgressCount,
            averageImpactScore
        ));
    }

    #endregion

    #region Helpers & Seeding

    private static string NormalizeCategory(string? category)
    {
        if (string.IsNullOrWhiteSpace(category)) return FeedbackCategories.FeatureRequest;
        var match = FeedbackCategories.All.FirstOrDefault(c => c.Equals(category.Trim(), StringComparison.OrdinalIgnoreCase));
        return match ?? FeedbackCategories.FeatureRequest;
    }

    private static bool IsLocalIp(string ip)
    {
        return ip is "127.0.0.1" or "::1" or "localhost" or "author-initial" or "test-local";
    }

    private static void EnsureSeeded(Guid tenantId)
    {
        if (_seededTenants.ContainsKey(tenantId)) return;

        lock (_syncRoot)
        {
            if (_seededTenants.ContainsKey(tenantId)) return;

            var postsMap = _tenantPosts.GetOrAdd(tenantId, _ => new ConcurrentDictionary<Guid, FeedbackPost>());

            var now = DateTimeOffset.UtcNow;
            var seeds = new List<(FeedbackPost Post, List<FeedbackComment> Comments)>
            {
                (
                    new FeedbackPost
                    {
                        Id = Guid.Parse("f1111111-1111-1111-1111-111111111111"),
                        TenantId = tenantId,
                        Title = "Direct WhatsApp Business Catalog Sync",
                        Description = "Enable 2-way real-time product catalog, inventory level, and price synchronization directly with Meta WhatsApp Business Cloud API for high-converting social commerce.",
                        Category = FeedbackCategories.Integration,
                        Status = FeedbackStatuses.InProgress,
                        AuthorEmail = "tariq.almansoor@riyadhretail.com",
                        AuthorName = "Tariq Al-Mansoor",
                        AccountId = "ACC-SA-8821",
                        AccountARR = 142000.0m,
                        UpvoteCount = 38,
                        EstimatedQuarter = "Q4 2026",
                        ResponseNotes = "Sprint 42 active development. Meta Cloud API webhooks and rate limiters tested. Target rollout mid-November.",
                        CreatedAt = now.AddDays(-18),
                        UpdatedAt = now.AddHours(-2),
                        CompletedAt = null
                    },
                    new List<FeedbackComment>
                    {
                        new()
                        {
                            Id = Guid.NewGuid(),
                            TenantId = tenantId,
                            PostId = Guid.Parse("f1111111-1111-1111-1111-111111111111"),
                            AuthorName = "Product Team",
                            AuthorEmail = "product@sgrowing.io",
                            IsOfficialTeamResponse = true,
                            Content = "WhatsApp Cloud API webhooks are now in staging! Beta invites going out to top accounts next week.",
                            CreatedAt = now.AddDays(-5)
                        }
                    }
                ),
                (
                    new FeedbackPost
                    {
                        Id = Guid.Parse("f2222222-2222-2222-2222-222222222222"),
                        TenantId = tenantId,
                        Title = "ZATCA E-Invoicing Phase 2 QR Integration",
                        Description = "Native cryptographic stamp signing, CSID renewal automation, and embedded Phase 2 compliant B2B/B2C QR code generation on all checkout and invoice PDFs.",
                        Category = FeedbackCategories.FeatureRequest,
                        Status = FeedbackStatuses.Completed,
                        AuthorEmail = "layla.finance@jeddahlogistics.sa",
                        AuthorName = "Layla Al-Harbi",
                        AccountId = "ACC-SA-1044",
                        AccountARR = 285000.0m,
                        UpvoteCount = 64,
                        EstimatedQuarter = "Q3 2026",
                        ResponseNotes = "Successfully certified and deployed to production. Integrated with Fatoora portal.",
                        CreatedAt = now.AddDays(-45),
                        UpdatedAt = now.AddDays(-10),
                        CompletedAt = now.AddDays(-10)
                    },
                    new List<FeedbackComment>
                    {
                        new()
                        {
                            Id = Guid.NewGuid(),
                            TenantId = tenantId,
                            PostId = Guid.Parse("f2222222-2222-2222-2222-222222222222"),
                            AuthorName = "Compliance Lead",
                            AuthorEmail = "compliance@sgrowing.io",
                            IsOfficialTeamResponse = true,
                            Content = "Shipped in Horizon 24. Live for all GCC tenants under Finance > Tax & Invoicing.",
                            CreatedAt = now.AddDays(-10)
                        }
                    }
                ),
                (
                    new FeedbackPost
                    {
                        Id = Guid.Parse("f3333333-3333-3333-3333-333333333333"),
                        TenantId = tenantId,
                        Title = "Zapier & Make.com Webhook Connectors",
                        Description = "Certified app directory connectors on Zapier and Make (Integromat) for bidirectional trigger dispatching on order status, payment receipt, and lead qualification.",
                        Category = FeedbackCategories.Integration,
                        Status = FeedbackStatuses.Planned,
                        AuthorEmail = "khalid@gulfgrowth.io",
                        AuthorName = "Khalid Bin Rashid",
                        AccountId = "ACC-AE-4091",
                        AccountARR = 68000.0m,
                        UpvoteCount = 27,
                        EstimatedQuarter = "Q1 2027",
                        ResponseNotes = "Architecture review passed. OpenAPI schema generation and partner developer accounts configured.",
                        CreatedAt = now.AddDays(-12),
                        UpdatedAt = now.AddDays(-1),
                        CompletedAt = null
                    },
                    new List<FeedbackComment>()
                ),
                (
                    new FeedbackPost
                    {
                        Id = Guid.Parse("f4444444-4444-4444-4444-444444444444"),
                        TenantId = tenantId,
                        Title = "Real-Time Courier Remittance Settlement Radar",
                        Description = "Automated cashflow reconciliation tracker tracking pending cash-on-delivery (COD) disbursements across SMSA, Aramex, and J&T Express with SLA dispute warnings.",
                        Category = FeedbackCategories.FeatureRequest,
                        Status = FeedbackStatuses.InProgress,
                        AuthorEmail = "omair.operations@sharqiah-direct.com",
                        AuthorName = "Omair Farooq",
                        AccountId = "ACC-SA-7120",
                        AccountARR = 195000.0m,
                        UpvoteCount = 49,
                        EstimatedQuarter = "Q4 2026",
                        ResponseNotes = "Remittance webhook parsing implemented. Beta testing with 5 high-volume COD merchants.",
                        CreatedAt = now.AddDays(-22),
                        UpdatedAt = now.AddHours(-6),
                        CompletedAt = null
                    },
                    new List<FeedbackComment>
                    {
                        new()
                        {
                            Id = Guid.NewGuid(),
                            TenantId = tenantId,
                            PostId = Guid.Parse("f4444444-4444-4444-4444-444444444444"),
                            AuthorName = "Logistics Team",
                            AuthorEmail = "logistics@sgrowing.io",
                            IsOfficialTeamResponse = true,
                            Content = "Courier float reconciliation algorithms are working with Aramex & SMSA sandbox feeds.",
                            CreatedAt = now.AddDays(-3)
                        }
                    }
                ),
                (
                    new FeedbackPost
                    {
                        Id = Guid.Parse("f5555555-5555-5555-5555-555555555555"),
                        TenantId = tenantId,
                        Title = "Multi-Currency Dynamic Forex Checkout",
                        Description = "Allow cross-border shoppers across GCC (SAR, AED, KWD, BHD, QAR, OMR) to browse and pay in local currencies with automatic daily central bank FX pegging.",
                        Category = FeedbackCategories.FeatureRequest,
                        Status = FeedbackStatuses.Planned,
                        AuthorEmail = "sara.k@dubaimarketplace.ae",
                        AuthorName = "Sara Kazim",
                        AccountId = "ACC-AE-3190",
                        AccountARR = 110000.0m,
                        UpvoteCount = 31,
                        EstimatedQuarter = "Q1 2027",
                        ResponseNotes = "Scheduled for Horizon 32 after payment gateway multi-acquirer routing upgrades.",
                        CreatedAt = now.AddDays(-15),
                        UpdatedAt = now.AddDays(-2),
                        CompletedAt = null
                    },
                    new List<FeedbackComment>()
                ),
                (
                    new FeedbackPost
                    {
                        Id = Guid.Parse("f6666666-6666-6666-6666-666666666666"),
                        TenantId = tenantId,
                        Title = "Automated Abandoned Cart Voice Dialer",
                        Description = "Trigger proactive AI phone dialer calls within 15 minutes of checkout drop-off offering instant discount incentives and addressing delivery hesitation.",
                        Category = FeedbackCategories.UxImprovement,
                        Status = FeedbackStatuses.Planned,
                        AuthorEmail = "marwan@turboquick.sa",
                        AuthorName = "Marwan Al-Sayed",
                        AccountId = "ACC-SA-5542",
                        AccountARR = 54000.0m,
                        UpvoteCount = 19,
                        EstimatedQuarter = "Q1 2027",
                        ResponseNotes = "Queued for research. Telephony compliance and do-not-call filtering required.",
                        CreatedAt = now.AddDays(-8),
                        UpdatedAt = now.AddDays(-1),
                        CompletedAt = null
                    },
                    new List<FeedbackComment>()
                ),
                (
                    new FeedbackPost
                    {
                        Id = Guid.Parse("f7777777-7777-7777-7777-777777777777"),
                        TenantId = tenantId,
                        Title = "Granular Role-Based Access Control (RBAC) & Audit Log Export",
                        Description = "Custom permission sets for warehouse dispatchers, financial auditors, and customer success reps with cryptographically verifiable tamper-evident export.",
                        Category = FeedbackCategories.FeatureRequest,
                        Status = FeedbackStatuses.UnderReview,
                        AuthorEmail = "nasser.ciso@qatarretail.qa",
                        AuthorName = "Nasser Al-Thani",
                        AccountId = "ACC-QA-9011",
                        AccountARR = 340000.0m,
                        UpvoteCount = 22,
                        EstimatedQuarter = "Q2 2027",
                        ResponseNotes = "Under enterprise security review with compliance board.",
                        CreatedAt = now.AddDays(-6),
                        UpdatedAt = now.AddHours(-12),
                        CompletedAt = null
                    },
                    new List<FeedbackComment>()
                ),
                (
                    new FeedbackPost
                    {
                        Id = Guid.Parse("f8888888-8888-8888-8888-888888888888"),
                        TenantId = tenantId,
                        Title = "Dark Mode Contrast Fix in Mobile Safari Navigation",
                        Description = "Fix sticky header backdrop blur and contrast degradation on iOS Safari when scrolling long catalog listings in dark mode.",
                        Category = FeedbackCategories.BugReport,
                        Status = FeedbackStatuses.Completed,
                        AuthorEmail = "hassan.dev@beirutboutique.com",
                        AuthorName = "Hassan Daouk",
                        AccountId = "ACC-LB-2201",
                        AccountARR = 28000.0m,
                        UpvoteCount = 12,
                        EstimatedQuarter = "Q3 2026",
                        ResponseNotes = "Resolved in release v3.4.1 via Tailwind backdrop-blur-md CSS update.",
                        CreatedAt = now.AddDays(-30),
                        UpdatedAt = now.AddDays(-20),
                        CompletedAt = now.AddDays(-20)
                    },
                    new List<FeedbackComment>()
                )
            };

            foreach (var (post, commentsList) in seeds)
            {
                post.ImpactScore = CalculateImpactScore(post.UpvoteCount, post.AccountARR);
                postsMap[post.Id] = post;

                // Seed comments
                if (commentsList.Count > 0)
                {
                    var comments = _postComments.GetOrAdd(post.Id, _ => new List<FeedbackComment>());
                    lock (comments)
                    {
                        comments.AddRange(commentsList);
                    }
                }

                // Seed sample votes
                var votes = _postVotes.GetOrAdd(post.Id, _ => new List<FeedbackVote>());
                lock (votes)
                {
                    votes.Add(new FeedbackVote
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        PostId = post.Id,
                        VoterEmail = post.AuthorEmail.ToLowerInvariant(),
                        VoterIp = "author-initial",
                        CreatedAt = post.CreatedAt
                    });
                }
            }

            _seededTenants[tenantId] = true;
        }
    }

    #endregion
}
