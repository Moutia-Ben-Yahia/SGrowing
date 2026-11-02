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
}
