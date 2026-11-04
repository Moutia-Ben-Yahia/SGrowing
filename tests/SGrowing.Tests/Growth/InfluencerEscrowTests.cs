using SGrowing.Api.Modules.Growth.Influencers;
using Xunit;

namespace SGrowing.Tests.Growth;

public sealed class InfluencerEscrowTests
{
    private readonly InfluencerEscrowEngine _engine = new();

    [Fact]
    public async Task GetCreators_ReturnsSeededVerifiedCreators()
    {
        var creators = await _engine.GetCreatorsAsync();

        Assert.NotNull(creators);
        Assert.NotEmpty(creators);
        Assert.Contains(creators, c => c.CreatorId == "creator_sara_tech");
        Assert.True(creators.All(c => c.AuthenticityScore >= 90.0));
    }

    [Fact]
    public async Task CreateContract_InitializesFundedEscrow()
    {
        var request = new CreateEscrowContractRequest(
            CreatorId: "creator_sara_tech",
            CampaignName: "Enterprise AI Launch",
            TotalEscrowAmount: 6000m,
            Milestones: new[]
            {
                new EscrowMilestone("ms_01", "Post 1", 3000m, "Reel + Link", 50000, 0, false, false),
                new EscrowMilestone("ms_02", "Post 2", 3000m, "Story Series", 30000, 0, false, false)
            }
        );

        var contract = await _engine.CreateContractAsync(request);

        Assert.NotNull(contract);
        Assert.Equal("Funded", contract.Status);
        Assert.Equal(6000m, contract.RemainingAmount);
        Assert.Equal(0m, contract.ReleasedAmount);
    }

    [Fact]
    public async Task VerifyAndReleaseMilestone_UpdatesReleasedAmountAndStatus()
    {
        var contracts = await _engine.GetContractsAsync();
        var contract = contracts.First(c => c.ContractId == "escrow_contract_01");

        var request = new ReleaseMilestoneRequest(
            ContractId: contract.ContractId,
            MilestoneId: "ms_02",
            ActualViewsAchieved: 82000,
            VerificationEvidenceUrl: "https://tiktok.com/@sarah.builds.saas/video/991283"
        );

        var updated = await _engine.VerifyAndReleaseMilestoneAsync(request);

        Assert.NotNull(updated);
        Assert.Equal(5000m, updated.ReleasedAmount);
        Assert.Equal(0m, updated.RemainingAmount);
        Assert.Equal("Completed", updated.Status);
        Assert.NotNull(updated.CompletedAtUtc);
    }

    [Fact]
    public async Task CreateContract_ThrowsKeyNotFoundException_WhenCreatorDoesNotExist()
    {
        var request = new CreateEscrowContractRequest(
            CreatorId: "creator_ghost_nonexistent",
            CampaignName: "Ghost Campaign",
            TotalEscrowAmount: 4000m,
            Milestones: new[]
            {
                new EscrowMilestone("ms_01", "Post 1", 4000m, "Video", 10000, 0, false, false)
            }
        );

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => _engine.CreateContractAsync(request));
        Assert.Contains("creator_ghost_nonexistent", ex.Message);
    }

    [Fact]
    public async Task VerifyAndReleaseMilestone_ThrowsKeyNotFoundException_WhenContractNotFound()
    {
        var request = new ReleaseMilestoneRequest(
            ContractId: "escrow_unknown_contract_999",
            MilestoneId: "ms_01",
            ActualViewsAchieved: 10000,
            VerificationEvidenceUrl: "https://example.com/proof"
        );

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => _engine.VerifyAndReleaseMilestoneAsync(request));
        Assert.Contains("escrow_unknown_contract_999", ex.Message);
    }

    [Fact]
    public async Task VerifyAndReleaseMilestone_ThrowsInvalidOperationException_WhenMilestoneAlreadyReleased()
    {
        // In seeded contract1, ms_01 has IsReleased = true
        var request = new ReleaseMilestoneRequest(
            ContractId: "escrow_contract_01",
            MilestoneId: "ms_01",
            ActualViewsAchieved: 70000,
            VerificationEvidenceUrl: "https://example.com/duplicate"
        );

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _engine.VerifyAndReleaseMilestoneAsync(request));
        Assert.Contains("ms_01", ex.Message);
        Assert.Contains("already been released", ex.Message);
    }

    [Fact]
    public async Task GetCampaignSummary_ReturnsAggregatedMetricsAndRoas()
    {
        var summary = await _engine.GetCampaignSummaryAsync();

        Assert.NotNull(summary);
        Assert.True(summary.TotalActiveContracts >= 1);
        Assert.True(summary.TotalEscrowSecured >= 5000m);
        Assert.True(summary.TotalReleasedPayouts >= 2000m);
        Assert.True(summary.AttributedGmv > 0m);
        Assert.Equal(4.8, summary.InfluencerRoas);
    }

    [Fact]
    public async Task CreateContract_AppendsContractToActiveContractsList()
    {
        var request = new CreateEscrowContractRequest(
            CreatorId: "creator_sara_tech",
            CampaignName: "Enterprise Expansion",
            TotalEscrowAmount: 8000m,
            Milestones: new[]
            {
                new EscrowMilestone("ms_exp_01", "Webinar", 8000m, "Live Webinar", 25000, 0, false, false)
            }
        );

        var created = await _engine.CreateContractAsync(request);
        var contracts = await _engine.GetContractsAsync();

        Assert.Contains(contracts, c => c.ContractId == created.ContractId);
    }
}
