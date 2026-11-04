using Microsoft.EntityFrameworkCore;
using Moq;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Feedback;
using SGrowing.Core.Entities;
using SGrowing.Core.Outbox;
using Xunit;

namespace SGrowing.Tests.Feedback;

public class FeatureFeedbackRoadmapTests
{
    private readonly SGrowingDbContext _db;
    private readonly Mock<IOutbox> _outboxMock;
    private readonly FeatureFeedbackService _service;

    public FeatureFeedbackRoadmapTests()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new SGrowingDbContext(options);
        _outboxMock = new Mock<IOutbox>();
        _service = new FeatureFeedbackService(_db, _outboxMock.Object);
    }

    [Fact]
    public async Task CreatePost_InitializesOpenStatusAndAuthorVote()
    {
        var tenantId = Guid.NewGuid();

        var post = await _service.CreatePostAsync(
            tenantId,
            "Native HubSpot Bi-Directional Sync",
            "We need two-way sync for enterprise accounts.",
            category: "Integrations",
            authorEmail: "product@client.com",
            authorName: "John Doe"
        );

        Assert.NotNull(post);
        Assert.Equal("Native HubSpot Bi-Directional Sync", post.Title);
        Assert.Equal(FeedbackStatus.Open, post.Status);
        Assert.Equal(1, post.UpvoteCount);
        Assert.Equal("Integrations", post.Category);
    }

    [Fact]
    public async Task ToggleVote_IdempotentlyAddsAndRemovesVote()
    {
        var tenantId = Guid.NewGuid();
        var post = await _service.CreatePostAsync(
            tenantId,
            "Dark Mode Dashboard Theme",
            "Support dark mode for late night operations.",
            category: "General",
            authorEmail: "author@corp.com"
        );

        Assert.Equal(1, post.UpvoteCount);

        // 1. Another voter votes -> increments to 2
        var voter2 = "designer@company.com";
        var res1 = await _service.ToggleVoteAsync(tenantId, post.Id, voter2);
        Assert.True(res1.IsUpvoted);
        Assert.Equal(2, res1.TotalUpvotes);

        // 2. Same voter votes again -> toggles off and decrements to 1
        var res2 = await _service.ToggleVoteAsync(tenantId, post.Id, voter2);
        Assert.False(res2.IsUpvoted);
        Assert.Equal(1, res2.TotalUpvotes);
    }

    [Fact]
    public async Task GetRoadmapBoard_CategorizesByLifecycleStatus()
    {
        var tenantId = Guid.NewGuid();

        var p1 = await _service.CreatePostAsync(tenantId, "Open Idea 1", "Desc", category: "General", authorEmail: "u1@test.com");
        var p2 = await _service.CreatePostAsync(tenantId, "Planned Idea 2", "Desc", category: "General", authorEmail: "u2@test.com");
        var p3 = await _service.CreatePostAsync(tenantId, "In Progress Idea 3", "Desc", category: "General", authorEmail: "u3@test.com");
        var p4 = await _service.CreatePostAsync(tenantId, "Completed Feature 4", "Desc", category: "General", authorEmail: "u4@test.com");

        await _service.UpdatePostStatusAsync(tenantId, p2.Id, FeedbackStatus.Planned);
        await _service.UpdatePostStatusAsync(tenantId, p3.Id, FeedbackStatus.InProgress);
        await _service.UpdatePostStatusAsync(tenantId, p4.Id, FeedbackStatus.Completed);

        var board = await _service.GetRoadmapBoardAsync(tenantId);

        Assert.NotNull(board);
        Assert.Single(board.OpenIdeas);
        Assert.Single(board.PlannedFeatures);
        Assert.Single(board.InProgressFeatures);
        Assert.Single(board.CompletedFeatures);

        Assert.Equal("Open Idea 1", board.OpenIdeas[0].Title);
        Assert.Equal("Planned Idea 2", board.PlannedFeatures[0].Title);
        Assert.Equal("In Progress Idea 3", board.InProgressFeatures[0].Title);
        Assert.Equal("Completed Feature 4", board.CompletedFeatures[0].Title);
    }

    [Fact]
    public async Task ToggleVote_ThrowsKeyNotFoundException_WhenPostDoesNotExist()
    {
        var tenantId = Guid.NewGuid();
        var unknownPostId = Guid.NewGuid();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.ToggleVoteAsync(tenantId, unknownPostId, "voter@domain.com"));
    }

    [Fact]
    public async Task UpdatePostStatus_ThrowsKeyNotFoundException_WhenPostDoesNotExist()
    {
        var tenantId = Guid.NewGuid();
        var unknownPostId = Guid.NewGuid();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.UpdatePostStatusAsync(tenantId, unknownPostId, FeedbackStatus.Completed));
    }

    [Fact]
    public async Task GetPostsAsync_FiltersByCategoryAndStatus_WithDescendingUpvoteSort()
    {
        var tenantId = Guid.NewGuid();

        var p1 = await _service.CreatePostAsync(tenantId, "Billing Feature A", "Desc", category: "Billing", authorEmail: "a@a.com");
        var p2 = await _service.CreatePostAsync(tenantId, "Billing Feature B", "Desc", category: "Billing", authorEmail: "b@b.com");
        var p3 = await _service.CreatePostAsync(tenantId, "Security Feature C", "Desc", category: "Security", authorEmail: "c@c.com");

        // Give p2 two more upvotes (total 3)
        await _service.ToggleVoteAsync(tenantId, p2.Id, "extra1@corp.com");
        await _service.ToggleVoteAsync(tenantId, p2.Id, "extra2@corp.com");

        var billingPosts = await _service.GetPostsAsync(tenantId, category: "billing");

        Assert.Equal(2, billingPosts.Count);
        Assert.Equal(p2.Id, billingPosts[0].Id); // Higher upvotes first
        Assert.Equal(3, billingPosts[0].UpvoteCount);
        Assert.Equal(p1.Id, billingPosts[1].Id);
        Assert.Equal(1, billingPosts[1].UpvoteCount);
    }

    [Fact]
    public async Task GetRoadmapBoard_IncludesUnderReviewStatusInOpenIdeas()
    {
        var tenantId = Guid.NewGuid();

        var post = await _service.CreatePostAsync(tenantId, "Under Review Feature", "Desc", category: "Core", authorEmail: "review@corp.com");
        await _service.UpdatePostStatusAsync(tenantId, post.Id, FeedbackStatus.UnderReview);

        var board = await _service.GetRoadmapBoardAsync(tenantId);

        Assert.Single(board.OpenIdeas);
        Assert.Equal("Under Review Feature", board.OpenIdeas[0].Title);
        Assert.Equal(FeedbackStatus.UnderReview, board.OpenIdeas[0].Status);
    }

    [Fact]
    public async Task GetRoadmapBoard_WhenPostIsClosed_ExcludesFromAllActiveColumns()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var post = await _service.CreatePostAsync(tenantId, "Deprecated Feature", "Will not do", category: "Core", authorEmail: "product@corp.com");
        
        // Act - Mark closed
        await _service.UpdatePostStatusAsync(tenantId, post.Id, FeedbackStatus.Closed);
        var board = await _service.GetRoadmapBoardAsync(tenantId);

        // Assert - Closed posts must not appear in any active board lane
        Assert.Empty(board.OpenIdeas);
        Assert.Empty(board.PlannedFeatures);
        Assert.Empty(board.InProgressFeatures);
        Assert.Empty(board.CompletedFeatures);

        _outboxMock.Verify(o => o.EnqueueAsync(
            tenantId,
            "Feedback.StatusChanged",
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
