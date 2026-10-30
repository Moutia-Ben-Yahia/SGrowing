using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using SGrowing.Api.Modules.Feedback;
using Xunit;

namespace SGrowing.Tests.Feedback;

public class FeedbackBoardServiceTests
{
    private static FeedbackBoardService CreateService() => new(null);

    #region 1. Impact Score Formula Tests

    [Fact]
    public void CalculateImpactScore_WithZeroUpvotes_ShouldReturnZero()
    {
        // Act
        var score = FeedbackBoardService.CalculateImpactScore(0, 50000m);

        // Assert
        score.Should().Be(0m);
    }

    [Fact]
    public void CalculateImpactScore_WithZeroArr_ShouldEqualUpvotesTimesLog10Of10()
    {
        // Arrange
        // AccountARR = 0 => log10(0 + 10) = log10(10) = 1.0
        // Score = 7 * 1.0 = 7.00m
        int upvotes = 7;
        decimal arr = 0m;

        // Act
        var score = FeedbackBoardService.CalculateImpactScore(upvotes, arr);

        // Assert
        score.Should().Be(7.00m);
    }

    [Fact]
    public void CalculateImpactScore_WithVaryingArr_ShouldScaleLogarithmically()
    {
        // Arrange & Act
        // 10 upvotes, ARR = 90 => log10(100) = 2.0 => 20.00
        var scoreTier1 = FeedbackBoardService.CalculateImpactScore(10, 90m);

        // 10 upvotes, ARR = 990 => log10(1000) = 3.0 => 30.00
        var scoreTier2 = FeedbackBoardService.CalculateImpactScore(10, 990m);

        // 10 upvotes, ARR = 99990 => log10(100000) = 5.0 => 50.00
        var scoreTier3 = FeedbackBoardService.CalculateImpactScore(10, 99990m);

        // Assert
        scoreTier1.Should().Be(20.00m);
        scoreTier2.Should().Be(30.00m);
        scoreTier3.Should().Be(50.00m);
        scoreTier3.Should().BeGreaterThan(scoreTier2);
        scoreTier2.Should().BeGreaterThan(scoreTier1);
    }

    [Fact]
    public void CalculateImpactScore_WithNegativeArr_ShouldClampToZero()
    {
        // Arrange
        int upvotes = 15;
        decimal negativeArr = -12000m;

        // Act
        var score = FeedbackBoardService.CalculateImpactScore(upvotes, negativeArr);

        // Assert: clamped to 0 => log10(10) = 1.0 => 15.00m
        score.Should().Be(15.00m);
    }

    #endregion

    #region 2. Post Creation & Validation Tests

    [Fact]
    public async Task CreatePostAsync_WithValidRequest_ShouldInitializeCorrectlyWithAuthorVote()
    {
        // Arrange
        var service = CreateService();
        var tenantId = Guid.NewGuid();
        var request = new CreateBoardPostRequest(
            TenantId: tenantId,
            Title: "ZATCA QR Code Microservice Support",
            Description: "Allow microservice containers to directly sign ZATCA Phase 2 tax invoices.",
            Category: FeedbackCategories.Integration,
            AuthorEmail: "cto@gcc-merchant.sa",
            AuthorName: "Bader Al-Otaibi",
            AccountId: "ACC-TEST-001",
            AccountARR: 99990m,
            EstimatedQuarter: "Q4 2026"
        );

        // Act
        var post = await service.CreatePostAsync(request);

        // Assert
        post.Should().NotBeNull();
        post.Id.Should().NotBeEmpty();
        post.TenantId.Should().Be(tenantId);
        post.Title.Should().Be("ZATCA QR Code Microservice Support");
        post.Category.Should().Be(FeedbackCategories.Integration);
        post.Status.Should().Be(FeedbackStatuses.Submitted);
        post.UpvoteCount.Should().Be(1); // Author auto-voted
        post.ImpactScore.Should().Be(5.00m); // 1 * log10(99990 + 10) = 5.00
        post.EstimatedQuarter.Should().Be("Q4 2026");
        post.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CreatePostAsync_WithoutAuthorEmail_ShouldInitializeWithZeroVotes()
    {
        // Arrange
        var service = CreateService();
        var tenantId = Guid.NewGuid();
        var request = new CreateBoardPostRequest(
            TenantId: tenantId,
            Title: "Anonymous Suggestion",
            Description: "General layout feedback.",
            Category: FeedbackCategories.UxImprovement,
            AuthorEmail: null,
            AccountARR: 50000m
        );

        // Act
        var post = await service.CreatePostAsync(request);

        // Assert
        post.UpvoteCount.Should().Be(0);
        post.ImpactScore.Should().Be(0m);
    }

    [Fact]
    public async Task CreatePostAsync_WithEmptyTitle_ShouldThrowArgumentException()
    {
        // Arrange
        var service = CreateService();
        var request = new CreateBoardPostRequest(
            TenantId: Guid.NewGuid(),
            Title: "   ",
            Description: "Description only"
        );

        // Act
        var act = async () => await service.CreatePostAsync(request);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Title cannot be empty*");
    }

    #endregion

    #region 3. Upvoting, Toggling & Anti-Gaming Tests

    [Fact]
    public async Task ToggleVoteAsync_NewVoter_ShouldIncrementUpvotesAndRecalculateImpact()
    {
        // Arrange
        var service = CreateService();
        var tenantId = Guid.NewGuid();
        var post = await service.CreatePostAsync(new CreateBoardPostRequest(
            tenantId,
            "Real-Time Inventory Webhook",
            "Notify ERP on stock change",
            FeedbackCategories.Integration,
            null,
            null,
            "ACC-100",
            90m // log10(100) = 2.0
        ));

        post.UpvoteCount.Should().Be(0);

        // Act: Voter casts an upvote
        var voteResult = await service.ToggleVoteAsync(tenantId, post.Id, "user1@domain.com", "192.168.10.1");

        // Assert
        voteResult.IsUpvoted.Should().BeTrue();
        voteResult.UpvoteCount.Should().Be(1);
        voteResult.ImpactScore.Should().Be(2.00m); // 1 * 2.0
    }

    [Fact]
    public async Task ToggleVoteAsync_ExistingVoter_ShouldRemoveVoteAndDecrementUpvotes()
    {
        // Arrange
        var service = CreateService();
        var tenantId = Guid.NewGuid();
        var post = await service.CreatePostAsync(new CreateBoardPostRequest(
            tenantId,
            "Apple Pay Single Click Checkout",
            "Speed up mobile checkout",
            FeedbackCategories.FeatureRequest,
            null,
            null,
            "ACC-200",
            990m // log10(1000) = 3.0
        ));

        // First vote
        await service.ToggleVoteAsync(tenantId, post.Id, "shopper@store.com", "10.0.0.5");

        // Act: Second vote from same email => Toggle OFF
        var unvoteResult = await service.ToggleVoteAsync(tenantId, post.Id, "shopper@store.com", "10.0.0.5");

        // Assert
        unvoteResult.IsUpvoted.Should().BeFalse();
        unvoteResult.UpvoteCount.Should().Be(0);
        unvoteResult.ImpactScore.Should().Be(0m);
    }

    [Fact]
    public async Task ToggleVoteAsync_SameIpDifferentEmail_ShouldThrowInvalidOperationExceptionDueToAntiGaming()
    {
        // Arrange
        var service = CreateService();
        var tenantId = Guid.NewGuid();
        var post = await service.CreatePostAsync(new CreateBoardPostRequest(
            tenantId,
            "Contested High-Priority Feature",
            "Anti-gaming ballot stuffing test",
            FeedbackCategories.FeatureRequest,
            null,
            null,
            "ACC-TEST",
            10000m
        ));

        const string clientIp = "198.51.100.42";

        // First legit voter from this IP
        await service.ToggleVoteAsync(tenantId, post.Id, "first.voter@corp.com", clientIp);

        // Act: Second voter attempts to vote from the SAME network IP using a different email
        var duplicateGamingAttempt = async () =>
            await service.ToggleVoteAsync(tenantId, post.Id, "second.fake.voter@corp.com", clientIp);

        // Assert: Anti-gaming should trigger and prevent ballot stuffing
        await duplicateGamingAttempt.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Duplicate vote prevented by anti-gaming engine*");
    }

    [Fact]
    public async Task ToggleVoteAsync_DifferentIpAndEmail_ShouldAllowMultipleUpvotes()
    {
        // Arrange
        var service = CreateService();
        var tenantId = Guid.NewGuid();
        var post = await service.CreatePostAsync(new CreateBoardPostRequest(
            tenantId,
            "Multi-Courier Fleet Dispatcher",
            "Auto balance shipments across 4 couriers",
            FeedbackCategories.FeatureRequest,
            null,
            null,
            "ACC-FLEET",
            990m // log10(1000) = 3.0
        ));

        // Act
        var res1 = await service.ToggleVoteAsync(tenantId, post.Id, "user1@riyadh.sa", "192.0.2.1");
        var res2 = await service.ToggleVoteAsync(tenantId, post.Id, "user2@jeddah.sa", "192.0.2.2");
        var res3 = await service.ToggleVoteAsync(tenantId, post.Id, "user3@dammam.sa", "192.0.2.3");

        // Assert
        res3.UpvoteCount.Should().Be(3);
        res3.ImpactScore.Should().Be(9.00m); // 3 * 3.0
    }

    [Fact]
    public async Task ToggleVoteAsync_WithEmptyEmail_ShouldThrowArgumentException()
    {
        // Arrange
        var service = CreateService();
        var tenantId = Guid.NewGuid();
        var post = await service.CreatePostAsync(new CreateBoardPostRequest(tenantId, "Test Post", "Description"));

        // Act
        var act = async () => await service.ToggleVoteAsync(tenantId, post.Id, "   ");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Voter email is required*");
    }

    [Fact]
    public async Task ToggleVoteAsync_NonExistentPost_ShouldThrowKeyNotFoundException()
    {
        // Arrange
        var service = CreateService();

        // Act
        var act = async () => await service.ToggleVoteAsync(Guid.NewGuid(), Guid.NewGuid(), "test@domain.com");

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    #endregion

    #region 4. Status Machine & Roadmap Transition Tests

    [Fact]
    public async Task UpdatePostStatusAsync_SubmittedToPlanned_ShouldUpdateStatusAndQuarter()
    {
        // Arrange
        var service = CreateService();
        var tenantId = Guid.NewGuid();
        var post = await service.CreatePostAsync(new CreateBoardPostRequest(tenantId, "Automated SMS OTP", "Description"));

        // Act
        var updated = await service.UpdatePostStatusAsync(tenantId, post.Id, new UpdateBoardPostStatusRequest(
            TenantId: tenantId,
            NewStatus: FeedbackStatuses.Planned,
            ResponseNotes: "Approved by Product Council.",
            EstimatedQuarter: "Q1 2027"
        ));

        // Assert
        updated.Status.Should().Be(FeedbackStatuses.Planned);
        updated.EstimatedQuarter.Should().Be("Q1 2027");
        updated.ResponseNotes.Should().Be("Approved by Product Council.");
        updated.CompletedAt.Should().BeNull();
    }

    [Fact]
    public async Task UpdatePostStatusAsync_PlannedToInProgress_ShouldTransitionSuccessfully()
    {
        // Arrange
        var service = CreateService();
        var tenantId = Guid.NewGuid();
        var post = await service.CreatePostAsync(new CreateBoardPostRequest(tenantId, "Feature", "Description"));
        await service.UpdatePostStatusAsync(tenantId, post.Id, new UpdateBoardPostStatusRequest(tenantId, FeedbackStatuses.Planned));

        // Act
        var inProgress = await service.UpdatePostStatusAsync(tenantId, post.Id, new UpdateBoardPostStatusRequest(
            TenantId: tenantId,
            NewStatus: FeedbackStatuses.InProgress,
            ResponseNotes: "Assigned to Sprint 45"
        ));

        // Assert
        inProgress.Status.Should().Be(FeedbackStatuses.InProgress);
        inProgress.CompletedAt.Should().BeNull();
    }

    [Fact]
    public async Task UpdatePostStatusAsync_InProgressToCompleted_ShouldSetCompletedAtTimestamp()
    {
        // Arrange
        var service = CreateService();
        var tenantId = Guid.NewGuid();
        var post = await service.CreatePostAsync(new CreateBoardPostRequest(tenantId, "Feature", "Description"));
        await service.UpdatePostStatusAsync(tenantId, post.Id, new UpdateBoardPostStatusRequest(tenantId, FeedbackStatuses.Planned));
        await service.UpdatePostStatusAsync(tenantId, post.Id, new UpdateBoardPostStatusRequest(tenantId, FeedbackStatuses.InProgress));

        // Act
        var completed = await service.UpdatePostStatusAsync(tenantId, post.Id, new UpdateBoardPostStatusRequest(
            TenantId: tenantId,
            NewStatus: FeedbackStatuses.Completed,
            ResponseNotes: "Shipped in release v3.5.0!"
        ));

        // Assert
        completed.Status.Should().Be(FeedbackStatuses.Completed);
        completed.CompletedAt.Should().NotBeNull();
        completed.CompletedAt.Value.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task UpdatePostStatusAsync_CompletedToInProgress_ShouldClearCompletedAtTimestamp()
    {
        // Arrange
        var service = CreateService();
        var tenantId = Guid.NewGuid();
        var post = await service.CreatePostAsync(new CreateBoardPostRequest(tenantId, "Feature", "Description"));
        await service.UpdatePostStatusAsync(tenantId, post.Id, new UpdateBoardPostStatusRequest(tenantId, FeedbackStatuses.Planned));
        await service.UpdatePostStatusAsync(tenantId, post.Id, new UpdateBoardPostStatusRequest(tenantId, FeedbackStatuses.InProgress));
        await service.UpdatePostStatusAsync(tenantId, post.Id, new UpdateBoardPostStatusRequest(tenantId, FeedbackStatuses.Completed));

        // Act: Re-opening completed feature
        var reopened = await service.UpdatePostStatusAsync(tenantId, post.Id, new UpdateBoardPostStatusRequest(
            TenantId: tenantId,
            NewStatus: FeedbackStatuses.InProgress,
            ResponseNotes: "Re-opened for additional edge-case bugfix"
        ));

        // Assert
        reopened.Status.Should().Be(FeedbackStatuses.InProgress);
        reopened.CompletedAt.Should().BeNull();
    }

    [Fact]
    public async Task UpdatePostStatusAsync_InvalidTransition_ShouldThrowInvalidOperationException()
    {
        // Arrange: Submitted cannot jump straight to Completed without planning or progress
        var service = CreateService();
        var tenantId = Guid.NewGuid();
        var post = await service.CreatePostAsync(new CreateBoardPostRequest(tenantId, "Fresh Idea", "Description"));

        // Act
        var act = async () => await service.UpdatePostStatusAsync(tenantId, post.Id, new UpdateBoardPostStatusRequest(
            tenantId,
            FeedbackStatuses.Completed
        ));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Invalid status transition*");
    }

    [Fact]
    public async Task UpdatePostStatusAsync_InvalidStatusName_ShouldThrowArgumentException()
    {
        // Arrange
        var service = CreateService();
        var tenantId = Guid.NewGuid();
        var post = await service.CreatePostAsync(new CreateBoardPostRequest(tenantId, "Idea", "Description"));

        // Act
        var act = async () => await service.UpdatePostStatusAsync(tenantId, post.Id, new UpdateBoardPostStatusRequest(
            tenantId,
            "NonExistentStatusXYZ"
        ));

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*is not a valid feedback status*");
    }

    #endregion

    #region 5. Querying, Filtering & Sorting Tests

    [Fact]
    public async Task GetPostsAsync_WithCategoryFilter_ShouldReturnMatchingCategoryOnly()
    {
        // Arrange: Uses pre-seeded GCC requests
        var service = CreateService();
        var tenantId = Guid.NewGuid();

        // Act
        var integrations = await service.GetPostsAsync(tenantId, category: FeedbackCategories.Integration);

        // Assert
        integrations.Should().NotBeEmpty();
        integrations.Should().OnlyContain(p => p.Category == FeedbackCategories.Integration);
    }

    [Fact]
    public async Task GetPostsAsync_WithStatusFilter_ShouldReturnMatchingStatusOnly()
    {
        // Arrange
        var service = CreateService();
        var tenantId = Guid.NewGuid();

        // Act
        var plannedPosts = await service.GetPostsAsync(tenantId, status: FeedbackStatuses.Planned);

        // Assert
        plannedPosts.Should().NotBeEmpty();
        plannedPosts.Should().OnlyContain(p => p.Status == FeedbackStatuses.Planned);
    }

    [Fact]
    public async Task GetPostsAsync_WithSearchTerm_ShouldFilterByTitleDescriptionOrQuarter()
    {
        // Arrange
        var service = CreateService();
        var tenantId = Guid.NewGuid();

        // Act
        var results = await service.GetPostsAsync(tenantId, search: "WhatsApp");

        // Assert
        results.Should().NotBeEmpty();
        results.Should().Contain(p => p.Title.Contains("WhatsApp"));
    }

    [Fact]
    public async Task GetPostsAsync_WithSortingModes_ShouldSortByImpactTopAndNewest()
    {
        // Arrange
        var service = CreateService();
        var tenantId = Guid.NewGuid();

        // Act
        var sortedByImpact = await service.GetPostsAsync(tenantId, sortBy: "impact");
        var sortedByTop = await service.GetPostsAsync(tenantId, sortBy: "top");
        var sortedByNewest = await service.GetPostsAsync(tenantId, sortBy: "newest");

        // Assert: Impact sorting
        for (int i = 0; i < sortedByImpact.Count - 1; i++)
        {
            sortedByImpact[i].ImpactScore.Should().BeGreaterThanOrEqualTo(sortedByImpact[i + 1].ImpactScore);
        }

        // Top sorting
        for (int i = 0; i < sortedByTop.Count - 1; i++)
        {
            sortedByTop[i].UpvoteCount.Should().BeGreaterThanOrEqualTo(sortedByTop[i + 1].UpvoteCount);
        }

        // Newest sorting
        for (int i = 0; i < sortedByNewest.Count - 1; i++)
        {
            sortedByNewest[i].CreatedAt.Should().BeOnOrAfter(sortedByNewest[i + 1].CreatedAt);
        }
    }

    #endregion

    #region 6. Commenting & Badge Tests

    [Fact]
    public async Task AddCommentAsync_CommunityMember_ShouldRecordCommentWithoutOfficialBadge()
    {
        // Arrange
        var service = CreateService();
        var tenantId = Guid.NewGuid();
        var post = await service.CreatePostAsync(new CreateBoardPostRequest(tenantId, "Feedback Post", "Desc"));

        // Act
        var comment = await service.AddCommentAsync(tenantId, post.Id, new AddFeedbackCommentRequest(
            TenantId: tenantId,
            AuthorName: "Saud Al-Farhan",
            AuthorEmail: "saud@customer.com",
            Content: "We would use this heavily in our Riyadh warehouse!",
            IsOfficialTeamResponse: false
        ));

        // Assert
        comment.Should().NotBeNull();
        comment.AuthorName.Should().Be("Saud Al-Farhan");
        comment.IsOfficialTeamResponse.Should().BeFalse();
        comment.Content.Should().Be("We would use this heavily in our Riyadh warehouse!");
    }

    [Fact]
    public async Task AddCommentAsync_OfficialTeam_ShouldRecordCommentWithOfficialBadge()
    {
        // Arrange
        var service = CreateService();
        var tenantId = Guid.NewGuid();
        var post = await service.CreatePostAsync(new CreateBoardPostRequest(tenantId, "Feature Post", "Desc"));

        // Act
        var comment = await service.AddCommentAsync(tenantId, post.Id, new AddFeedbackCommentRequest(
            TenantId: tenantId,
            AuthorName: "SGrowing Core Team",
            AuthorEmail: "team@sgrowing.io",
            Content: "Our team has reviewed this and scheduled it for Q1 2027 release.",
            IsOfficialTeamResponse: true
        ));

        // Assert
        comment.IsOfficialTeamResponse.Should().BeTrue();
    }

    [Fact]
    public async Task AddCommentAsync_EmptyContent_ShouldThrowArgumentException()
    {
        // Arrange
        var service = CreateService();
        var tenantId = Guid.NewGuid();
        var post = await service.CreatePostAsync(new CreateBoardPostRequest(tenantId, "Post", "Desc"));

        // Act
        var act = async () => await service.AddCommentAsync(tenantId, post.Id, new AddFeedbackCommentRequest(
            tenantId,
            "User",
            "user@test.com",
            "    "
        ));

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Comment content cannot be empty*");
    }

    #endregion

    #region 7. Roadmap Grouping & Summary KPIs Tests

    [Fact]
    public async Task GetRoadmapAsync_ShouldGroupPostsIntoPlannedInProgressAndCompleted()
    {
        // Arrange
        var service = CreateService();
        var tenantId = Guid.NewGuid();

        // Act
        var roadmap = await service.GetRoadmapAsync(tenantId);

        // Assert
        roadmap.Planned.Should().NotBeEmpty();
        roadmap.Planned.Should().OnlyContain(p => p.Status == FeedbackStatuses.Planned);

        roadmap.InProgress.Should().NotBeEmpty();
        roadmap.InProgress.Should().OnlyContain(p => p.Status == FeedbackStatuses.InProgress);

        roadmap.Completed.Should().NotBeEmpty();
        roadmap.Completed.Should().OnlyContain(p => p.Status == FeedbackStatuses.Completed);
    }

    [Fact]
    public async Task GetSummaryAsync_ShouldCalculateAggregatedKpiMetricsAccurately()
    {
        // Arrange
        var service = CreateService();
        var tenantId = Guid.NewGuid();

        // Act
        var summary = await service.GetSummaryAsync(tenantId);

        // Assert
        summary.TotalIdeas.Should().BeGreaterThanOrEqualTo(8); // 8 pre-seeded
        summary.TotalVotes.Should().BeGreaterThan(0);
        summary.CompletedVelocity.Should().BeGreaterThanOrEqualTo(2);
        summary.TotalArrDemand.Should().BeGreaterThan(500000m);
        summary.AverageImpactScore.Should().BeGreaterThan(0m);
    }

    #endregion
}
