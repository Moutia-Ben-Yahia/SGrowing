using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Feedback.Community;
using Xunit;

namespace SGrowing.Tests.Feedback;

public sealed class CommunityFeedbackServiceTests
{
    private readonly SGrowingDbContext _db;
    private readonly CommunityFeedbackService _service;

    public CommunityFeedbackServiceTests()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new SGrowingDbContext(options);
        _service = new CommunityFeedbackService(_db);
    }

    [Fact]
    public async Task SubmitPostAsync_NewPost_CreatesPostAndInitialAuthorVote()
    {
        var tenantId = Guid.NewGuid();

        var post = await _service.SubmitPostAsync(
            tenantId,
            "Native Stripe Terminal Invoicing Integration",
            "Allow retail merchants to swipe physical POS cards and sync invoice state in real-time.",
            "merchant@atlas-retail.dz");

        post.Should().NotBeNull();
        post.TenantId.Should().Be(tenantId);
        post.Title.Should().Be("Native Stripe Terminal Invoicing Integration");
        post.AuthorEmail.Should().Be("merchant@atlas-retail.dz");
        post.RoadmapStatus.Should().Be("UnderReview");
        post.CanonicalPostId.Should().BeNull();

        var votes = await _db.FeedbackPostVotes.Where(v => v.PostId == post.Id).ToListAsync();
        votes.Should().HaveCount(1);
        votes[0].VoterEmail.Should().Be("merchant@atlas-retail.dz");
    }

    [Fact]
    public async Task SubmitPostAsync_DuplicateTitle_SetsCanonicalPostId()
    {
        var tenantId = Guid.NewGuid();

        var original = await _service.SubmitPostAsync(
            tenantId,
            "WhatsApp Voice Notes AI Transcriber",
            "Transcribe inbound Arabic voice notes automatically",
            "founder@carthagelog.tn");

        var duplicate = await _service.SubmitPostAsync(
            tenantId,
            "whatsapp voice notes ai transcriber", // exact same title in different casing
            "Alternative proposal for voice notes",
            "sre@maghrebretail.dz");

        duplicate.CanonicalPostId.Should().Be(original.Id);
    }

    [Fact]
    public async Task VotePostAsync_FirstTimeVote_IncrementsUpvotesAndReturnsTrue()
    {
        var tenantId = Guid.NewGuid();
        var post = await _service.SubmitPostAsync(
            tenantId,
            "Dark Mode Dashboard Theme",
            "Provide high-contrast dark mode for night shift operators",
            "dev@sgrowing.io");

        var voted = await _service.VotePostAsync(tenantId, post.Id, "designer@agency.com");

        voted.Should().BeTrue();
        var refreshed = await _db.CommunityFeedbackPosts.FirstAsync(p => p.Id == post.Id);
        refreshed.UpvoteCount.Should().Be(2); // 1 initial author vote + 1 new vote
    }

    [Fact]
    public async Task VotePostAsync_DuplicateVote_ReturnsFalseWithoutIncrementing()
    {
        var tenantId = Guid.NewGuid();
        var post = await _service.SubmitPostAsync(
            tenantId,
            "Export Financials to QuickBooks Online",
            "Bi-directional chart of accounts sync",
            "cfo@enterprise.corp");

        // Author tries to vote again
        var repeatVote = await _service.VotePostAsync(tenantId, post.Id, "cfo@enterprise.corp");
        repeatVote.Should().BeFalse();

        // Second voter votes twice
        var v1 = await _service.VotePostAsync(tenantId, post.Id, "auditor@deloitte.com");
        v1.Should().BeTrue();
        var v2 = await _service.VotePostAsync(tenantId, post.Id, "auditor@deloitte.com");
        v2.Should().BeFalse();
    }

    [Fact]
    public async Task VotePostAsync_NonExistentPost_ReturnsFalse()
    {
        var tenantId = Guid.NewGuid();
        var result = await _service.VotePostAsync(tenantId, Guid.NewGuid(), "voter@random.org");
        result.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateRoadmapStatusAsync_ExistingPost_UpdatesStatusAndOfficialResponse()
    {
        var tenantId = Guid.NewGuid();
        var post = await _service.SubmitPostAsync(
            tenantId,
            "Algérie Poste BaridiMob Integration",
            "Support local payment gateway",
            "merchant@oran.dz");

        var updated = await _service.UpdateRoadmapStatusAsync(
            tenantId,
            post.Id,
            "Planned",
            "Scheduled for Q1 delivery by Payments engineering squad.");

        updated.RoadmapStatus.Should().Be("Planned");
        updated.OfficialResponse.Should().Be("Scheduled for Q1 delivery by Payments engineering squad.");
    }

    [Fact]
    public async Task UpdateRoadmapStatusAsync_MissingPost_ThrowsKeyNotFoundException()
    {
        var tenantId = Guid.NewGuid();
        var act = async () => await _service.UpdateRoadmapStatusAsync(tenantId, Guid.NewGuid(), "Planned");

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*Feedback post*not found*");
    }

    [Fact]
    public async Task GetPublicRoadmapAsync_CategorizesIntoPlannedInProgressCompleted()
    {
        var tenantId = Guid.NewGuid();

        var p1 = await _service.SubmitPostAsync(tenantId, "Feature 1", "Desc 1", "u1@test.com");
        var p2 = await _service.SubmitPostAsync(tenantId, "Feature 2", "Desc 2", "u2@test.com");
        var p3 = await _service.SubmitPostAsync(tenantId, "Feature 3", "Desc 3", "u3@test.com");

        await _service.UpdateRoadmapStatusAsync(tenantId, p1.Id, "Planned");
        await _service.UpdateRoadmapStatusAsync(tenantId, p2.Id, "InProgress");
        await _service.UpdateRoadmapStatusAsync(tenantId, p3.Id, "Completed");

        var roadmap = await _service.GetPublicRoadmapAsync(tenantId);

        roadmap.Should().NotBeNull();
        roadmap.TenantId.Should().Be(tenantId);
        roadmap.Planned.Should().ContainSingle(p => p.Id == p1.Id);
        roadmap.InProgress.Should().ContainSingle(p => p.Id == p2.Id);
        roadmap.Completed.Should().ContainSingle(p => p.Id == p3.Id);
    }

    [Fact]
    public async Task SearchPostsAsync_MatchesTitleOrDescriptionMarkdown()
    {
        var tenantId = Guid.NewGuid();

        await _service.SubmitPostAsync(tenantId, "Multi-Carrier Waybill Printing", "Zebra thermal printer support via Bluetooth", "ops@shipping.dz");
        await _service.SubmitPostAsync(tenantId, "Shopify Inventory Sync", "Stock deduction webhook handler", "dev@shopify.com");

        var results = await _service.SearchPostsAsync(tenantId, "bluetooth");

        results.Should().ContainSingle();
        results[0].Title.Should().Be("Multi-Carrier Waybill Printing");
    }
}
