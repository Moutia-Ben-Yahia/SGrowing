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
}
